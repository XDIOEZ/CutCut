using System.Drawing.Imaging;

namespace ScreenshotTool.LongCapture;

internal interface ILongCaptureFrameSource
{
    Bitmap CaptureFrame();
}

internal interface ILongCaptureScrollDriver : IDisposable
{
    bool IsUserCancellationRequested { get; }

    ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
        CancellationToken cancellationToken);

    ValueTask<ScrollInputResult> ScrollDownAsync(
        ScrollInputMode mode,
        CancellationToken cancellationToken);
}

internal enum ScrollInputMode
{
    SystemInput,
    TargetedWindowMessage
}

internal sealed record ScrollTargetPreparationResult(
    bool Succeeded,
    ScrollInputMode PreferredInputMode,
    bool ForegroundConfirmed,
    string Diagnostic);

internal sealed record ScrollInputResult(
    bool Succeeded,
    ScrollInputMode Mode,
    string Diagnostic);

internal enum LongCaptureStopReason
{
    EndReached,
    NoScrollableMotion,
    ScrollTargetUnavailable,
    UserCancelled,
    MatchRejected,
    UnstableContent,
    SizeLimit,
    FrameLimit,
    ScrollFailed
}

internal sealed record LongCaptureEngineResult(
    Bitmap Image,
    LongCaptureStopReason StopReason,
    bool IsComplete,
    int AcceptedFrameCount,
    FrameMatchDecision? LastMatchDecision,
    string Diagnostic);

internal sealed class LongCaptureEngine(LongCaptureOptions? options = null)
{
    private readonly LongCaptureOptions _options = options ?? new LongCaptureOptions();

    public async Task<LongCaptureEngineResult> CaptureAsync(
        ILongCaptureFrameSource frameSource,
        ILongCaptureScrollDriver scrollDriver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frameSource);
        ArgumentNullException.ThrowIfNull(scrollDriver);

        using var session = new LongCaptureStitchSession(_options);
        var targetPreparation = await scrollDriver.PrepareTargetAsync(cancellationToken);
        await Task.Delay(_options.InitialTargetSettleMilliseconds, cancellationToken);
        session.AddFrame(frameSource.CaptureFrame());
        if (!targetPreparation.Succeeded)
        {
            return new LongCaptureEngineResult(
                session.BuildResult(),
                LongCaptureStopReason.ScrollTargetUnavailable,
                false,
                session.AcceptedFrameCount,
                null,
                targetPreparation.Diagnostic);
        }

        var noMotionCount = 0;
        var scrollMode = targetPreparation.PreferredInputMode;
        var lastScrollDiagnostic = targetPreparation.Diagnostic;
        FrameMatchDecision? lastMatchDecision = null;
        var stopReason = LongCaptureStopReason.FrameLimit;
        var complete = false;
        var diagnostic = "长截图达到最大帧数。";

        for (var attempt = 1; attempt < _options.MaximumFrames; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scrollDriver.IsUserCancellationRequested)
            {
                stopReason = LongCaptureStopReason.UserCancelled;
                diagnostic = "用户取消了长截图。";
                break;
            }

            var scrollInput = await scrollDriver.ScrollDownAsync(
                scrollMode,
                cancellationToken);
            lastScrollDiagnostic = scrollInput.Diagnostic;
            if (!scrollInput.Succeeded)
            {
                stopReason = LongCaptureStopReason.ScrollFailed;
                diagnostic = scrollInput.Diagnostic;
                break;
            }

            var stableSample = await new StableFrameSampler(_options).CaptureAsync(
                frameSource,
                () => scrollDriver.IsUserCancellationRequested,
                cancellationToken);
            using var stableFrame = stableSample.Frame;
            if (stableSample.Status != StableFrameSampleStatus.Stable || stableFrame is null)
            {
                if (scrollDriver.IsUserCancellationRequested)
                {
                    stopReason = LongCaptureStopReason.UserCancelled;
                    diagnostic = "用户取消了长截图。";
                }
                else
                {
                    stopReason = LongCaptureStopReason.UnstableContent;
                    diagnostic = "滚动后的画面持续变化，无法取得可验证的稳定帧。";
                }
                break;
            }

            var append = session.AddFrame(Clone(stableFrame));
            lastMatchDecision = append.Match?.Decision;
            switch (append.Decision)
            {
                case LongCaptureAppendDecision.Accepted:
                    noMotionCount = 0;
                    scrollMode = targetPreparation.PreferredInputMode;
                    break;
                case LongCaptureAppendDecision.NoMotion:
                    noMotionCount++;
                    scrollMode = scrollInput.Mode == ScrollInputMode.SystemInput
                        ? ScrollInputMode.TargetedWindowMessage
                        : ScrollInputMode.SystemInput;
                    if (noMotionCount >= _options.ConsecutiveNoMotionLimit)
                    {
                        if (session.AcceptedFrameCount < 2)
                        {
                            stopReason = LongCaptureStopReason.NoScrollableMotion;
                            diagnostic =
                                "系统滚轮和定向滚轮都已尝试，但选区画面没有发生变化。" +
                                $" 最后一次输入：{lastScrollDiagnostic}";
                        }
                        else
                        {
                            stopReason = LongCaptureStopReason.EndReached;
                            complete = true;
                            diagnostic = "已在连续两次滚动尝试后确认页面没有继续位移，判定到达底部。";
                        }
                    }
                    break;
                case LongCaptureAppendDecision.LimitReached:
                    stopReason = LongCaptureStopReason.SizeLimit;
                    diagnostic = append.Diagnostic;
                    break;
                case LongCaptureAppendDecision.Rejected:
                    if (_options.SafetyChecksEnabled)
                    {
                        stopReason = LongCaptureStopReason.MatchRejected;
                        diagnostic = $"{append.Diagnostic} 滚动输入：{lastScrollDiagnostic}";
                    }
                    else
                    {
                        diagnostic = $"{append.Diagnostic} 宽松模式已跳过本帧并继续。";
                    }
                    break;
            }

            if (complete ||
                stopReason == LongCaptureStopReason.NoScrollableMotion ||
                append.Decision is
                LongCaptureAppendDecision.LimitReached ||
                (_options.SafetyChecksEnabled &&
                 append.Decision == LongCaptureAppendDecision.Rejected))
            {
                break;
            }
        }

        return new LongCaptureEngineResult(
            session.BuildResult(),
            stopReason,
            complete,
            session.AcceptedFrameCount,
            lastMatchDecision,
            diagnostic);
    }

    private static Bitmap Clone(Bitmap source) =>
        source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);
}
