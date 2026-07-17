using ScreenshotTool.Contracts;

namespace ScreenshotTool.LongCapture;

internal sealed class LongCaptureFeature :
    CaptureFeatureBase,
    ICaptureToolbarCommandProvider
{
    private const string CommandId = "screenshot-tool.long-capture.start";
    private static readonly IReadOnlyList<CaptureToolbarCommand> ToolbarCommands =
        Array.AsReadOnly(
        [
            new CaptureToolbarCommand(
                CommandId,
                "长截图",
                "使用鼠标滚轮向上或向下滚动，程序实时捕捉并拼接（点击完成并编辑，Esc 取消）",
                68)
        ]);

    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _activeCaptureCancellation;
    private bool _disposed;

    public override string Id => "screenshot-tool.long-capture.feature";

    public override int Order => 500;

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() =>
        ToolbarCommands;

    internal static LongCaptureOptions CreateOptions(ICaptureFeatureHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new LongCaptureOptions
        {
            SafetyChecksEnabled = host.GetBooleanPreference(
                CaptureFeaturePreferenceIds.LongCaptureSafetyChecks,
                defaultValue: false)
        };
    }

    public async Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, CommandId, StringComparison.Ordinal))
        {
            throw new ArgumentException("未知的长截图命令。", nameof(commandId));
        }

        CancellationTokenSource? activeCancellation;
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeCaptureCancellation is not null)
            {
                return;
            }

            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            _activeCaptureCancellation = activeCancellation;
        }

        try
        {
            await ExecuteCaptureAsync(activeCancellation.Token);
        }
        finally
        {
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_activeCaptureCancellation, activeCancellation))
                {
                    _activeCaptureCancellation = null;
                }
            }

            activeCancellation.Dispose();
        }
    }

    public override void Dispose()
    {
        CancellationTokenSource? activeCancellation;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            activeCancellation = _activeCaptureCancellation;
        }

        _lifetimeCancellation.Cancel();
        activeCancellation?.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task ExecuteCaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Host is not ILiveCaptureFeatureHost liveHost)
        {
            ShowUnsupportedHostMessage();
            return;
        }

        if (!liveHost.HasSelection || liveHost.SelectionScreenBounds.IsEmpty)
        {
            MessageBox.Show(
                "请先框选需要滚动截取的区域。",
                "长截图",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (liveHost.HasEdits)
        {
            MessageBox.Show(
                "当前截图已经包含编辑内容。为避免滚动拼接后元素位置失真，请先完成或取消当前截图，再开始长截图。",
                "无法开始长截图",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var selectionScreenBounds = liveHost.SelectionScreenBounds;
        var options = CreateOptions(liveHost);
        ManualLongCaptureCaptureResult? result = null;
        ScrollTargetPreparationResult? targetPreparation = null;
        Exception? failure = null;
        var overlayHidden = false;
        WindowsScrollDriver? scrollDriver = null;

        try
        {
            overlayHidden = true;
            liveHost.SetOverlayVisible(false);

            scrollDriver = new WindowsScrollDriver(
                selectionScreenBounds,
                moveCursor: true,
                activateTarget: true,
                restoreCursor: false);
            targetPreparation = await scrollDriver.PrepareTargetAsync(cancellationToken);
            if (targetPreparation.Succeeded)
            {
                var frameSource = new LiveCaptureFrameSource(liveHost);
                var controller = new ManualLongCaptureController(
                    selectionScreenBounds,
                    frameSource,
                    options);
                result = await controller.CaptureAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Session teardown cancels an active capture. No partial result is retained.
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            scrollDriver?.Dispose();
            if (overlayHidden)
            {
                try
                {
                    liveHost.SetOverlayVisible(true);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    // The owning overlay was disposed while its feature session was cancelled.
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            result?.Image?.Dispose();
            return;
        }

        if (failure is not null)
        {
            result?.Image?.Dispose();
            MessageBox.Show(
                $"长截图未完成：{failure.Message}",
                "长截图失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (targetPreparation is { Succeeded: false })
        {
            MessageBox.Show(
                "没有在选区中心找到可交互的目标窗口。请让选区中心落在需要滚动的正文、列表或网页内容上。" +
                $"\n\n诊断：{targetPreparation.Diagnostic}",
                "没有找到滚动目标",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (result is null)
        {
            return;
        }

        if (result.Cancelled)
        {
            return;
        }

        if (result.Image is null)
        {
            return;
        }

        if (options.SafetyChecksEnabled &&
            result.SafetyStopped &&
            result.AcceptedFrameCount < 2)
        {
            result.Image.Dispose();
            MessageBox.Show(
                $"滚动后的画面没有通过严格的双向重叠验证，因此没有猜测拼接位置。\n\n诊断：{result.Diagnostic}",
                "页面变化无法安全拼接",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (result.AcceptedFrameCount < 2)
        {
            result.Image.Dispose();
            MessageBox.Show(
                "还没有捕获到首帧之外的新内容。请在蓝色截图框内滚动，等右侧预览更新后再点击完成并编辑。",
                "长截图没有新增内容",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!result.SafetyStopped || !options.SafetyChecksEnabled)
        {
            ReplaceCaptureResult(liveHost, result.Image);
            return;
        }

        var keepVerifiedPart = AskWhetherToKeepVerifiedPart(result);
        if (!keepVerifiedPart)
        {
            result.Image.Dispose();
            return;
        }

        ReplaceCaptureResult(liveHost, result.Image);
    }

    private static bool AskWhetherToKeepVerifiedPart(
        ManualLongCaptureCaptureResult result)
    {
        var reason = result.StopReason switch
        {
            ManualLongCaptureStopReason.MatchRejected =>
                "下一次向上或向下滚动没有通过严格重叠验证。",
            ManualLongCaptureStopReason.SizeLimit =>
                "长截图已达到安全尺寸上限。",
            ManualLongCaptureStopReason.FrameLimit =>
                "长截图已达到最大可信帧数。",
            ManualLongCaptureStopReason.FrameBacklog =>
                "滚动速度超过了实时校验速度，为避免跨屏猜测接缝，捕获已停止。",
            _ => "长截图已安全停止。"
        };
        return MessageBox.Show(
            $"{reason}\n\n程序没有猜测接缝。是否保留前 {result.AcceptedFrameCount} 段已经验证的内容？" +
            $"\n\n诊断：{result.Diagnostic}",
            "长截图已安全停止",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    internal static LongCaptureUserMessage CreateInitialFailureMessage(
        LongCaptureEngineResult result)
    {
        var diagnostic = string.IsNullOrWhiteSpace(result.Diagnostic)
            ? string.Empty
            : $"\n\n诊断：{result.Diagnostic}";
        return result.StopReason switch
        {
            LongCaptureStopReason.ScrollTargetUnavailable => new LongCaptureUserMessage(
                "没有找到滚动目标",
                "框选范围里可以包含可滚动内容，但长截图仍需通过选区中心定位真正接收滚轮的窗口或控件。" +
                "请让选区中心落在可滚动正文、列表或网页内容上后重试。" + diagnostic,
                MessageBoxIcon.Warning),
            LongCaptureStopReason.NoScrollableMotion => new LongCaptureUserMessage(
                "滚轮未使选区发生变化",
                "选区里可以存在可滚动内容，但系统滚轮和定向滚轮都没有让当前画面发生变化。" +
                "请让选区中心落在真正接收滚轮的正文或列表上；如果目标程序以管理员身份运行，" +
                "也请用相同权限启动轻截。" + diagnostic,
                MessageBoxIcon.Warning),
            LongCaptureStopReason.ScrollFailed => new LongCaptureUserMessage(
                "滚动输入发送失败",
                "已找到选区，但无法把滚轮输入发送给选区中心的控件。" + diagnostic,
                MessageBoxIcon.Error),
            LongCaptureStopReason.MatchRejected => new LongCaptureUserMessage(
                "页面变化无法安全拼接",
                GetInitialMatchFailureText(result.LastMatchDecision) + diagnostic,
                MessageBoxIcon.Warning),
            LongCaptureStopReason.UnstableContent => new LongCaptureUserMessage(
                "滚动画面持续变化",
                "页面在滚动或动画过程中一直没有稳定下来，因此没有把过渡画面拼进结果。" + diagnostic,
                MessageBoxIcon.Warning),
            LongCaptureStopReason.SizeLimit => new LongCaptureUserMessage(
                "长截图尺寸受限",
                "第一段新增内容会超过长截图安全尺寸限制。" + diagnostic,
                MessageBoxIcon.Warning),
            LongCaptureStopReason.FrameLimit => new LongCaptureUserMessage(
                "长截图帧数受限",
                "在最大采集帧数内没有得到第二帧可信内容。" + diagnostic,
                MessageBoxIcon.Warning),
            _ => new LongCaptureUserMessage(
                "长截图未开始拼接",
                "没有得到第二帧可验证内容。" + diagnostic,
                MessageBoxIcon.Information)
        };
    }

    private static string GetInitialMatchFailureText(FrameMatchDecision? decision) =>
        decision switch
        {
            FrameMatchDecision.Ambiguous =>
                "页面已经发生变化，但重复内容产生了多个近似接缝，严格模式没有猜测拼接位置。",
            FrameMatchDecision.InsufficientTexture =>
                "页面已经发生变化，但当前画面纹理或重叠证据不足，无法可靠确定滚动距离。",
            FrameMatchDecision.UnsupportedFixedRegion =>
                "页面已经发生变化，但正文内检测到固定悬浮元素，继续拼接会产生重复内容。",
            FrameMatchDecision.InvalidDimensions =>
                "滚动前后的选区尺寸不一致，无法建立可靠的像素对应关系。",
            _ => "页面已经发生变化，但第一处重叠没有通过严格像素验证。"
        };

    private static bool AskWhetherToKeepVerifiedPart(LongCaptureEngineResult result)
    {
        var reason = result.StopReason switch
        {
            LongCaptureStopReason.MatchRejected =>
                "无法继续确认下一屏与已捕获内容的精确重叠。",
            LongCaptureStopReason.UnstableContent =>
                "滚动后的画面持续变化，无法取得可验证的稳定帧。",
            LongCaptureStopReason.SizeLimit =>
                "长截图已达到安全尺寸上限。",
            LongCaptureStopReason.FrameLimit =>
                "长截图已达到最大滚动帧数。",
            LongCaptureStopReason.ScrollFailed =>
                "无法继续向选区内的窗口发送滚动输入。",
            _ => "长截图提前停止。"
        };

        var answer = MessageBox.Show(
            $"{reason}\n\n为避免错误拼接，工具没有猜测缺失内容。是否保留前 {result.AcceptedFrameCount} 帧已经验证的部分？",
            "长截图已安全停止",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        return answer == DialogResult.Yes;
    }

    private static void ReplaceCaptureResult(
        ILiveCaptureFeatureHost liveHost,
        Bitmap image)
    {
        try
        {
            liveHost.ReplaceCaptureResult(image);
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private static void ShowUnsupportedHostMessage()
    {
        MessageBox.Show(
            "当前截图宿主不支持实时滚动采集，无法使用长截图模块。",
            "无法开始长截图",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}

internal sealed record LongCaptureUserMessage(
    string Title,
    string Text,
    MessageBoxIcon Icon);
