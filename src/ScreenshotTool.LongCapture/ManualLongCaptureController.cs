using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ScreenshotTool.LongCapture;

internal sealed record ManualLongCaptureCaptureResult(
    Bitmap? Image,
    bool Cancelled,
    bool SafetyStopped,
    ManualLongCaptureStopReason StopReason,
    int AcceptedFrameCount,
    FrameMatchDecision? LastMatchDecision,
    string Diagnostic);

internal enum ManualLongCaptureWorkKind
{
    BurstStarted,
    FrameCaptured,
    BurstQuiet,
    FrameBacklog,
    CaptureFailed
}

internal sealed record ManualLongCaptureWorkItem(
    ManualLongCaptureWorkKind Kind,
    long WheelVersion,
    Bitmap? Frame = null,
    Exception? Error = null);

/// <summary>
/// Composes the input observer, intermediate-frame pump, stable-frame sampler, bidirectional stitch
/// state and two auxiliary windows for one user-driven long-capture session.
/// </summary>
internal sealed class ManualLongCaptureController(
    Rectangle selectionScreenBounds,
    ILongCaptureFrameSource frameSource,
    LongCaptureOptions? options = null)
{
    private readonly LongCaptureOptions _options = options ?? new LongCaptureOptions();

    public async Task<ManualLongCaptureCaptureResult> CaptureAsync(
        CancellationToken cancellationToken)
    {
        ValidateOptions();
        var commands = new ConcurrentQueue<ManualLongCaptureInput>();
        var workItems = new ConcurrentQueue<ManualLongCaptureWorkItem>();
        using var workAvailable = new SemaphoreSlim(0);
        using var session = new ManualLongCaptureSession(_options);
        var frameWindow = new LongCaptureSelectionFrameForm(selectionScreenBounds);
        var previewWindow = new LongCapturePreviewForm(selectionScreenBounds, _options);
        var safeFrameSource = new PreviewSafeFrameSource(frameSource, previewWindow);
        var sampler = new StableFrameSampler(_options);
        var queuedFrameBudget = new PendingFrameBudget(
            _options.MaximumQueuedIntermediateFrames,
            _options.MaximumQueuedIntermediatePixels);
        ManualLongCaptureFramePump? framePump = null;
        LongCaptureInputMonitor? inputMonitor = null;
        EventHandler<ManualLongCaptureInput>? inputHandler = null;
        EventHandler? finishHandler = null;
        EventHandler? cancelHandler = null;
        var acceptingWork = 1;
        var acceptingWheel = 0;
        var backlogSignalled = 0;
        ManualLongCaptureCaptureResult? result = null;

        void SignalWorkAvailable()
        {
            try
            {
                workAvailable.Release();
            }
            catch (ObjectDisposedException)
            {
                // Cleanup won a race with a late native callback.
            }
        }

        void QueueCommand(ManualLongCaptureInput input)
        {
            if (Volatile.Read(ref acceptingWork) == 0)
            {
                return;
            }

            commands.Enqueue(input);
            SignalWorkAvailable();
        }

        void QueueFrame(Bitmap frame, long wheelVersion)
        {
            if (Volatile.Read(ref acceptingWork) == 0)
            {
                frame.Dispose();
                return;
            }
            if (!queuedFrameBudget.TryReserve(frame))
            {
                frame.Dispose();
                if (Interlocked.Exchange(ref backlogSignalled, 1) == 0)
                {
                    Interlocked.Exchange(ref acceptingWheel, 0);
                    framePump?.Stop();
                    QueueWork(new ManualLongCaptureWorkItem(
                        ManualLongCaptureWorkKind.FrameBacklog,
                        framePump?.WheelVersion ?? wheelVersion));
                }
                return;
            }

            workItems.Enqueue(new ManualLongCaptureWorkItem(
                ManualLongCaptureWorkKind.FrameCaptured,
                wheelVersion,
                frame));
            SignalWorkAvailable();
        }

        void QueueWork(ManualLongCaptureWorkItem work)
        {
            if (Volatile.Read(ref acceptingWork) == 0)
            {
                work.Frame?.Dispose();
                return;
            }

            workItems.Enqueue(work);
            SignalWorkAvailable();
        }

        async Task PauseUntilUserDecisionAsync()
        {
            Interlocked.Exchange(ref acceptingWheel, 0);
            framePump?.Stop();
            await previewWindow.UpdateStatusAsync(
                GetPausedStatus(session.StopReason),
                cancellationToken);
        }

        try
        {
            framePump = new ManualLongCaptureFramePump(
                safeFrameSource,
                _options,
                QueueFrame,
                wheelVersion => QueueWork(new ManualLongCaptureWorkItem(
                    ManualLongCaptureWorkKind.BurstStarted,
                    wheelVersion)),
                wheelVersion => QueueWork(new ManualLongCaptureWorkItem(
                    ManualLongCaptureWorkKind.BurstQuiet,
                    wheelVersion)),
                error => QueueWork(new ManualLongCaptureWorkItem(
                    ManualLongCaptureWorkKind.CaptureFailed,
                    framePump?.WheelVersion ?? 0,
                    Error: error)));
            inputMonitor = new LongCaptureInputMonitor(selectionScreenBounds);

            inputHandler = (_, input) =>
            {
                if (input.Kind == ManualLongCaptureInputKind.Wheel)
                {
                    if (Volatile.Read(ref acceptingWheel) != 0)
                    {
                        framePump.NotifyWheel();
                    }
                    return;
                }

                Interlocked.Exchange(ref acceptingWheel, 0);
                framePump.Stop();
                QueueCommand(input);
            };
            finishHandler = (_, _) =>
            {
                Interlocked.Exchange(ref acceptingWheel, 0);
                framePump.Stop();
                QueueCommand(new ManualLongCaptureInput(
                    ManualLongCaptureInputKind.Finish));
            };
            cancelHandler = (_, _) =>
            {
                Interlocked.Exchange(ref acceptingWheel, 0);
                framePump.Stop();
                QueueCommand(new ManualLongCaptureInput(
                    ManualLongCaptureInputKind.Cancel));
            };
            inputMonitor.InputReceived += inputHandler;
            previewWindow.FinishRequested += finishHandler;
            previewWindow.CancelRequested += cancelHandler;

            frameWindow.ShowFrame();
            previewWindow.ShowPreview();
            await previewWindow.UpdateStatusAsync(
                "长截图预览 · 正在准备捕获区域…",
                cancellationToken);
            await Task.Delay(_options.InitialTargetSettleMilliseconds, cancellationToken);
            commands.TryDequeue(out var earlyCommand);
            if (earlyCommand?.Kind == ManualLongCaptureInputKind.Cancel)
            {
                result = CreateCancelledResult(session);
                return result;
            }

            var firstFrame = safeFrameSource.CaptureFrame();
            var first = await Task.Run(
                () => session.Start(firstFrame),
                CancellationToken.None);
            var commandAfterStart = earlyCommand?.Kind ??
                                    ConsumePendingCommand(commands);
            if (commandAfterStart is not null)
            {
                result = await ExecuteCommandAsync(
                    commandAfterStart.Value,
                    session,
                    sampler,
                    safeFrameSource,
                    commands,
                    workItems,
                    queuedFrameBudget,
                    cancellationToken);
                return result;
            }
            Interlocked.Exchange(ref acceptingWheel, 1);
            await RefreshPreviewAsync(
                previewWindow,
                session,
                first.AcceptedFrameCount,
                cancellationToken);

            await previewWindow.UpdateStatusAsync(
                "长截图预览 · 请在蓝框内向上或向下滚动",
                cancellationToken);

            var previewDirty = false;
            var lastPreviewAt = Environment.TickCount64;
            while (result is null)
            {
                var next = await ReadNextAsync(
                    commands,
                    workItems,
                    workAvailable,
                    cancellationToken);
                if (next.Command is not null)
                {
                    result = await ExecuteCommandAsync(
                        next.Command.Kind,
                        session,
                        sampler,
                        safeFrameSource,
                        commands,
                        workItems,
                        queuedFrameBudget,
                        cancellationToken);
                    continue;
                }

                var work = next.Work!;
                if (ShouldWaitForUserCompletion(session.State))
                {
                    DiscardWorkItem(work, queuedFrameBudget);
                    continue;
                }
                switch (work.Kind)
                {
                    case ManualLongCaptureWorkKind.BurstStarted:
                        await previewWindow.UpdateStatusAsync(
                            "长截图预览 · 正在捕捉滚动中的画面…",
                            cancellationToken);
                        break;

                    case ManualLongCaptureWorkKind.FrameCaptured:
                        {
                            queuedFrameBudget.Release(work.Frame!);
                            var submission = await SubmitFrameAsync(
                                session,
                                work.Frame!,
                                stopOnRejected: false);
                            var pendingCommand = ConsumePendingCommand(commands);
                            if (pendingCommand is not null)
                            {
                                framePump.Stop();
                                result = await ExecuteCommandAsync(
                                    pendingCommand.Value,
                                    session,
                                    sampler,
                                    safeFrameSource,
                                    commands,
                                    workItems,
                                    queuedFrameBudget,
                                    cancellationToken);
                                break;
                            }
                            previewDirty |= submission.PreviewChanged;
                            if (ShouldWaitForUserCompletion(session.State))
                            {
                                if (previewDirty)
                                {
                                    await RefreshPreviewAsync(
                                        previewWindow,
                                        session,
                                        submission.AcceptedFrameCount,
                                        cancellationToken);
                                    previewDirty = false;
                                    lastPreviewAt = Environment.TickCount64;
                                }
                                await PauseUntilUserDecisionAsync();
                                break;
                            }

                            if (previewDirty &&
                                Environment.TickCount64 - lastPreviewAt >=
                                _options.PreviewRefreshIntervalMilliseconds)
                            {
                                await RefreshPreviewAsync(
                                    previewWindow,
                                    session,
                                    submission.AcceptedFrameCount,
                                    cancellationToken);
                                previewDirty = false;
                                lastPreviewAt = Environment.TickCount64;

                                pendingCommand = ConsumePendingCommand(commands);
                                if (pendingCommand is not null)
                                {
                                    framePump.Stop();
                                    result = await ExecuteCommandAsync(
                                        pendingCommand.Value,
                                        session,
                                        sampler,
                                        safeFrameSource,
                                        commands,
                                        workItems,
                                        queuedFrameBudget,
                                        cancellationToken);
                                }
                            }
                            break;
                        }

                    case ManualLongCaptureWorkKind.BurstQuiet:
                        {
                            if (work.WheelVersion != framePump.WheelVersion)
                            {
                                break;
                            }

                            await previewWindow.UpdateStatusAsync(
                                "长截图预览 · 滚动已停止，正在校验拼接…",
                                cancellationToken);
                            StableFrameSampleResult sample;
                            while (true)
                            {
                                sample = await sampler.CaptureAsync(
                                    safeFrameSource,
                                    () => !commands.IsEmpty ||
                                          work.WheelVersion != framePump.WheelVersion,
                                    cancellationToken);
                                if (sample.Status != StableFrameSampleStatus.TimedOut)
                                {
                                    break;
                                }

                                await previewWindow.UpdateStatusAsync(
                                    "长截图预览 · 画面仍在变化，正在自动等待稳定…",
                                    cancellationToken);
                                await Task.Delay(
                                    _options.StabilizeIntervalMilliseconds,
                                    cancellationToken);
                                if (!commands.IsEmpty ||
                                    work.WheelVersion != framePump.WheelVersion)
                                {
                                    break;
                                }
                            }
                            if (sample.Status == StableFrameSampleStatus.Interrupted)
                            {
                                break;
                            }
                            if (sample.Status == StableFrameSampleStatus.TimedOut ||
                                sample.Frame is null)
                            {
                                await previewWindow.UpdateStatusAsync(
                                    "长截图预览 · 画面仍在变化，停止后会自动重试",
                                    cancellationToken);
                                break;
                            }

                            var submission = await SubmitFrameAsync(
                                session,
                                sample.Frame,
                                stopOnRejected: true);
                            var pendingCommand = ConsumePendingCommand(commands);
                            if (pendingCommand is not null)
                            {
                                framePump.Stop();
                                result = await ExecuteCommandAsync(
                                    pendingCommand.Value,
                                    session,
                                    sampler,
                                    safeFrameSource,
                                    commands,
                                    workItems,
                                    queuedFrameBudget,
                                    cancellationToken);
                                break;
                            }
                            previewDirty |= submission.PreviewChanged;
                            if (previewDirty)
                            {
                                await RefreshPreviewAsync(
                                    previewWindow,
                                    session,
                                    submission.AcceptedFrameCount,
                                    cancellationToken);
                                previewDirty = false;
                                lastPreviewAt = Environment.TickCount64;

                                pendingCommand = ConsumePendingCommand(commands);
                                if (pendingCommand is not null)
                                {
                                    framePump.Stop();
                                    result = await ExecuteCommandAsync(
                                        pendingCommand.Value,
                                        session,
                                        sampler,
                                        safeFrameSource,
                                        commands,
                                        workItems,
                                        queuedFrameBudget,
                                        cancellationToken);
                                    break;
                                }
                            }

                            if (ShouldWaitForUserCompletion(session.State))
                            {
                                await PauseUntilUserDecisionAsync();
                                break;
                            }

                            await previewWindow.UpdateStatusAsync(
                                GetSubmissionStatus(submission),
                                cancellationToken);
                            break;
                        }

                    case ManualLongCaptureWorkKind.FrameBacklog:
                        session.StopForFrameBacklog();
                        await PauseUntilUserDecisionAsync();
                        break;

                    case ManualLongCaptureWorkKind.CaptureFailed:
                        if (ConsumePendingCommand(commands) ==
                            ManualLongCaptureInputKind.Cancel)
                        {
                            result = CreateCancelledResult(session);
                            break;
                        }
                        throw new InvalidOperationException(
                            "捕捉滚动中的屏幕画面失败。",
                            work.Error);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref acceptingWheel, 0);
            Interlocked.Exchange(ref acceptingWork, 0);

            if (inputMonitor is not null && inputHandler is not null)
            {
                inputMonitor.InputReceived -= inputHandler;
            }
            if (finishHandler is not null)
            {
                previewWindow.FinishRequested -= finishHandler;
            }
            if (cancelHandler is not null)
            {
                previewWindow.CancelRequested -= cancelHandler;
            }

            TryCleanup(() => inputMonitor?.Dispose());
            TryCleanup(() => framePump?.Dispose());
            while (workItems.TryDequeue(out var pendingWork))
            {
                if (pendingWork.Kind == ManualLongCaptureWorkKind.FrameCaptured)
                {
                    queuedFrameBudget.Release(pendingWork.Frame!);
                }
                pendingWork.Frame?.Dispose();
            }

            try
            {
                await previewWindow.ClosePreviewAsync();
            }
            catch
            {
                // Window cleanup must not discard an already completed capture result.
            }
            TryCleanup(previewWindow.Dispose);
            TryCleanup(() =>
            {
                if (!frameWindow.IsDisposed)
                {
                    frameWindow.Close();
                }
            });
            TryCleanup(frameWindow.Dispose);
        }

        return result ?? throw new InvalidOperationException(
            "手动长截图结束时没有生成结果。");
    }

    private static Task<ManualLongCaptureCaptureResult> ExecuteCommandAsync(
        ManualLongCaptureInputKind command,
        ManualLongCaptureSession session,
        StableFrameSampler sampler,
        ILongCaptureFrameSource frameSource,
        ConcurrentQueue<ManualLongCaptureInput> commands,
        ConcurrentQueue<ManualLongCaptureWorkItem> workItems,
        PendingFrameBudget queuedFrameBudget,
        CancellationToken cancellationToken) =>
        command == ManualLongCaptureInputKind.Cancel
            ? Task.FromResult(CreateCancelledResult(session))
            : CompleteAfterFinishAsync(
                session,
                sampler,
                frameSource,
                commands,
                workItems,
                queuedFrameBudget,
                cancellationToken);

    private static async Task<ManualLongCaptureCaptureResult> CompleteAfterFinishAsync(
        ManualLongCaptureSession session,
        StableFrameSampler sampler,
        ILongCaptureFrameSource frameSource,
        ConcurrentQueue<ManualLongCaptureInput> commands,
        ConcurrentQueue<ManualLongCaptureWorkItem> workItems,
        PendingFrameBudget queuedFrameBudget,
        CancellationToken cancellationToken)
    {
        if (ShouldWaitForUserCompletion(session.State))
        {
            DiscardPendingWorkItems(workItems, queuedFrameBudget);
            return await CompleteSessionAsync(
                session,
                safetyStopped: false,
                lastMatchDecision: null,
                $"用户完成了已暂停的手动长截图。{session.Diagnostic}",
                commands);
        }

        ManualLongCaptureSubmission? lastSubmission = null;
        while (workItems.TryDequeue(out var work))
        {
            if (work.Kind == ManualLongCaptureWorkKind.CaptureFailed)
            {
                throw new InvalidOperationException(
                    "捕捉滚动中的屏幕画面失败。",
                    work.Error);
            }
            if (work.Kind == ManualLongCaptureWorkKind.FrameBacklog)
            {
                session.StopForFrameBacklog();
                return await CompleteSessionAsync(
                    session,
                    safetyStopped: false,
                    lastSubmission?.MatchDecision,
                    session.Diagnostic,
                    commands);
            }
            if (work.Kind != ManualLongCaptureWorkKind.FrameCaptured)
            {
                continue;
            }

            queuedFrameBudget.Release(work.Frame!);
            lastSubmission = await SubmitFrameAsync(
                session,
                work.Frame!,
                stopOnRejected: false);
            if (ConsumePendingCommand(commands) == ManualLongCaptureInputKind.Cancel)
            {
                return CreateCancelledResult(session);
            }
            if (ShouldWaitForUserCompletion(session.State))
            {
                return await CompleteSessionAsync(
                    session,
                    safetyStopped: false,
                    lastSubmission.MatchDecision,
                    lastSubmission.Diagnostic,
                    commands);
            }
        }

        while (true)
        {
            var sample = await sampler.CaptureAsync(
                frameSource,
                () => !commands.IsEmpty,
                cancellationToken);
            if (sample.Status != StableFrameSampleStatus.Interrupted)
            {
                if (sample.Status == StableFrameSampleStatus.Stable &&
                    sample.Frame is not null)
                {
                    lastSubmission = await SubmitFrameAsync(
                        session,
                        sample.Frame,
                        stopOnRejected: true);
                    if (ConsumePendingCommand(commands) ==
                        ManualLongCaptureInputKind.Cancel)
                    {
                        return CreateCancelledResult(session);
                    }
                    if (ShouldWaitForUserCompletion(session.State))
                    {
                        return await CompleteSessionAsync(
                            session,
                            safetyStopped: false,
                            lastSubmission.MatchDecision,
                            lastSubmission.Diagnostic,
                            commands);
                    }
                }

                var diagnostic = sample.Status == StableFrameSampleStatus.TimedOut
                    ? "用户完成了手动长截图；最终画面未稳定，已保留此前验证通过的内容。"
                    : "用户完成了手动长截图。";
                return await CompleteSessionAsync(
                    session,
                    safetyStopped: false,
                    lastSubmission?.MatchDecision,
                    diagnostic,
                    commands);
            }

            if (ConsumePendingCommand(commands) == ManualLongCaptureInputKind.Cancel)
            {
                return CreateCancelledResult(session);
            }
        }
    }

    internal static ManualLongCaptureInputKind? ConsumePendingCommand(
        ConcurrentQueue<ManualLongCaptureInput> commands)
    {
        var cancelRequested = false;
        var finishRequested = false;
        while (commands.TryDequeue(out var command))
        {
            cancelRequested |= command.Kind == ManualLongCaptureInputKind.Cancel;
            finishRequested |= command.Kind == ManualLongCaptureInputKind.Finish;
        }
        return cancelRequested
            ? ManualLongCaptureInputKind.Cancel
            : finishRequested
                ? ManualLongCaptureInputKind.Finish
                : null;
    }

    internal static bool ShouldWaitForUserCompletion(
        ManualLongCaptureSessionState state) =>
        state == ManualLongCaptureSessionState.SafetyStopped;

    private static void DiscardPendingWorkItems(
        ConcurrentQueue<ManualLongCaptureWorkItem> workItems,
        PendingFrameBudget queuedFrameBudget)
    {
        while (workItems.TryDequeue(out var work))
        {
            DiscardWorkItem(work, queuedFrameBudget);
        }
    }

    private static void DiscardWorkItem(
        ManualLongCaptureWorkItem work,
        PendingFrameBudget queuedFrameBudget)
    {
        if (work.Kind == ManualLongCaptureWorkKind.FrameCaptured && work.Frame is not null)
        {
            queuedFrameBudget.Release(work.Frame);
        }
        work.Frame?.Dispose();
    }

    private static async Task<ManualLongCaptureSubmission> SubmitFrameAsync(
        ManualLongCaptureSession session,
        Bitmap frame,
        bool stopOnRejected)
    {
        try
        {
            return await Task.Run(
                () => session.SubmitStableFrame(frame, stopOnRejected),
                CancellationToken.None);
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    private static ManualLongCaptureCaptureResult CreateCancelledResult(
        ManualLongCaptureSession session)
    {
        session.Cancel();
        return new ManualLongCaptureCaptureResult(
            null,
            true,
            false,
            ManualLongCaptureStopReason.None,
            session.AcceptedFrameCount,
            null,
            session.Diagnostic);
    }

    private static async Task<ManualLongCaptureCaptureResult> CompleteSessionAsync(
        ManualLongCaptureSession session,
        bool safetyStopped,
        FrameMatchDecision? lastMatchDecision,
        string diagnostic,
        ConcurrentQueue<ManualLongCaptureInput> commands)
    {
        if (ConsumePendingCommand(commands) == ManualLongCaptureInputKind.Cancel)
        {
            return CreateCancelledResult(session);
        }

        var stopReason = session.StopReason;
        var acceptedFrameCount = session.AcceptedFrameCount;
        var image = await Task.Run(
            session.Complete,
            CancellationToken.None);
        if (ConsumePendingCommand(commands) == ManualLongCaptureInputKind.Cancel)
        {
            image.Dispose();
            return new ManualLongCaptureCaptureResult(
                null,
                true,
                false,
                ManualLongCaptureStopReason.None,
                acceptedFrameCount,
                null,
                "用户在生成长截图结果时取消了捕获。");
        }
        return new ManualLongCaptureCaptureResult(
            image,
            false,
            safetyStopped,
            stopReason,
            acceptedFrameCount,
            lastMatchDecision,
            diagnostic);
    }

    private static async Task<(
        ManualLongCaptureInput? Command,
        ManualLongCaptureWorkItem? Work)> ReadNextAsync(
        ConcurrentQueue<ManualLongCaptureInput> commands,
        ConcurrentQueue<ManualLongCaptureWorkItem> workItems,
        SemaphoreSlim workAvailable,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await workAvailable.WaitAsync(cancellationToken);
            if (commands.TryDequeue(out var command))
            {
                return (command, null);
            }
            if (workItems.TryDequeue(out var work))
            {
                return (null, work);
            }
        }
    }

    private static async Task RefreshPreviewAsync(
        LongCapturePreviewForm previewWindow,
        ManualLongCaptureSession session,
        int acceptedFrameCount,
        CancellationToken cancellationToken)
    {
        var previewData = await Task.Run(
            () =>
            {
                var preview = session.CreatePreviewImage();
                try
                {
                    return (Image: preview, SourceSize: session.EstimatedSize);
                }
                catch
                {
                    preview.Dispose();
                    throw;
                }
            },
            CancellationToken.None);
        using var preview = previewData.Image;
        await previewWindow.UpdatePreviewAsync(
            preview,
            acceptedFrameCount,
            previewData.SourceSize,
            cancellationToken);
    }

    private static string GetSubmissionStatus(ManualLongCaptureSubmission submission)
    {
        if (submission.PreviewChanged)
        {
            return $"长截图预览 · 已向{GetDirectionText(submission.Direction)}扩展，可继续滚动";
        }
        if (submission.Decision == LongCaptureAppendDecision.NoMotion)
        {
            return "长截图预览 · 本次画面没有新增内容，可继续滚动";
        }
        if (submission.Decision == LongCaptureAppendDecision.Rejected)
        {
            return "长截图预览 · 本次画面未通过校验，可小幅反向滚动后重试";
        }
        return $"长截图预览 · 已回到捕获过的位置，可继续{GetDirectionText(submission.Direction)}滚动";
    }

    private static string GetPausedStatus(ManualLongCaptureStopReason reason) => reason switch
    {
        ManualLongCaptureStopReason.MatchRejected =>
            "长截图已暂停 · 接缝无法确认，请点击完成或取消",
        ManualLongCaptureStopReason.SizeLimit =>
            "长截图已暂停 · 已达到尺寸上限，请点击完成或取消",
        ManualLongCaptureStopReason.FrameLimit =>
            "长截图已暂停 · 已达到帧数上限，请点击完成或取消",
        ManualLongCaptureStopReason.FrameBacklog =>
            "长截图已暂停 · 滚动过快，请点击完成或取消",
        _ => "长截图已暂停 · 请点击完成或取消"
    };

    private static string GetDirectionText(VerticalScrollDirection direction) =>
        direction == VerticalScrollDirection.Up ? "上" : "下";

    private void ValidateOptions()
    {
        if (_options.MaximumQueuedIntermediateFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.MaximumQueuedIntermediateFrames));
        }
        if (_options.MaximumQueuedIntermediatePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.MaximumQueuedIntermediatePixels));
        }
        if (_options.PreviewRefreshIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.PreviewRefreshIntervalMilliseconds));
        }
        if (_options.ConsecutiveStableRejectionLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.ConsecutiveStableRejectionLimit));
        }
    }

    private static void TryCleanup(Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch
        {
            // Cleanup failures must not mask the capture result or its original exception.
        }
    }

    private sealed class PendingFrameBudget(int maximumFrames, long maximumPixels)
    {
        private readonly object _gate = new();
        private int _count;
        private long _pixels;

        public bool TryReserve(Bitmap frame)
        {
            var framePixels = checked((long)frame.Width * frame.Height);
            lock (_gate)
            {
                if (_count >= maximumFrames ||
                    (_count > 0 && framePixels > maximumPixels - _pixels))
                {
                    return false;
                }

                _count++;
                _pixels += framePixels;
                return true;
            }
        }

        public void Release(Bitmap frame)
        {
            var framePixels = checked((long)frame.Width * frame.Height);
            lock (_gate)
            {
                _count--;
                _pixels -= framePixels;
                if (_count >= 0 && _pixels >= 0)
                {
                    return;
                }

                _count = 0;
                _pixels = 0;
                throw new InvalidOperationException("手动长截图中间帧计数失去平衡。");
            }
        }
    }

    private sealed class PreviewSafeFrameSource(
        ILongCaptureFrameSource inner,
        LongCapturePreviewForm previewWindow) : ILongCaptureFrameSource
    {
        public Bitmap CaptureFrame()
        {
            var hidePreview = previewWindow.OverlapsSelection && previewWindow.Visible;
            if (hidePreview)
            {
                previewWindow.Hide();
                DwmFlush();
            }

            try
            {
                return inner.CaptureFrame();
            }
            finally
            {
                if (hidePreview && !previewWindow.IsDisposed)
                {
                    previewWindow.ShowPreview();
                    DwmFlush();
                }
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();
    }
}
