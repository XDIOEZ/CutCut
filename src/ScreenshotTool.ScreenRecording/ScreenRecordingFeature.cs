using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal sealed class ScreenRecordingFeature :
    CaptureFeatureBase,
    ICaptureToolbarCommandProvider
{
    private const string CommandId = "screenshot-tool.screen-recording.start";
    private static readonly IReadOnlyList<CaptureToolbarCommand> Commands = Array.AsReadOnly(
    [
        new CaptureToolbarCommand(
            CommandId,
            "录屏",
            "录制当前选区，可使用专属选择模式并添加矩形、箭头、画笔、文字和马赛克实时批注",
            58)
    ]);

    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _activeRecordingCancellation;
    private bool _disposed;

    private readonly string _helperDirectory;

    public ScreenRecordingFeature(string helperDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperDirectory);
        _helperDirectory = Path.GetFullPath(helperDirectory);
    }

    public override string Id => "screenshot-tool.screen-recording.feature";

    public override int Order => 600;

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() => Commands;

    public async Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, CommandId, StringComparison.Ordinal))
        {
            throw new ArgumentException("未知的录屏命令。", nameof(commandId));
        }

        CancellationTokenSource activeCancellation;
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeRecordingCancellation is not null)
            {
                return;
            }

            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            _activeRecordingCancellation = activeCancellation;
        }

        try
        {
            await RunRecordingAsync(activeCancellation.Token);
        }
        finally
        {
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_activeRecordingCancellation, activeCancellation))
                {
                    _activeRecordingCancellation = null;
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
            activeCancellation = _activeRecordingCancellation;
        }

        _lifetimeCancellation.Cancel();
        activeCancellation?.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task RunRecordingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Host is not ILiveCaptureFeatureHost liveHost ||
            Host is not ICaptureArtifactHost artifactHost ||
            Host is not ICaptureAnnotationHost annotationHost)
        {
            ShowMessage(
                "当前宿主版本不支持录屏所需的实时选区、核心批注与文件输出能力。",
                "无法开始录屏",
                MessageBoxIcon.Warning);
            return;
        }

        if (!liveHost.HasSelection || liveHost.SelectionScreenBounds.IsEmpty)
        {
            ShowMessage(
                "请先框选需要录制的屏幕区域。",
                "录屏",
                MessageBoxIcon.Information);
            return;
        }

        if (liveHost.HasEdits)
        {
            ShowMessage(
                "请在添加截图批注前开始录屏。录制开始后会切换到实时批注层，可使用专属“选择”模式并继续添加矩形、椭圆、箭头、画笔、文字和马赛克。",
                "请先开始录屏",
                MessageBoxIcon.Information);
            return;
        }

        if (!RecordingTarget.TryCreate(liveHost.SelectionScreenBounds, out var target) ||
            target is null)
        {
            ShowMessage(
                "录屏选区必须完整位于同一台显示器内，并且宽高至少为 2 像素。",
                "选区无法录制",
                MessageBoxIcon.Information);
            return;
        }

        Directory.CreateDirectory(artifactHost.OutputFolder);
        var outputPath = CreateOutputPath(artifactHost.OutputFolder, DateTime.Now);
        var recordingOptions = RecordingOptions.FromHost(Host);
        RecordingControlResult? result = null;
        Exception? failure = null;
        var overlayHidden = false;
        var completeCaptureSession = false;
        try
        {
            overlayHidden = true;
            liveHost.SetOverlayVisible(false);
            using var annotationSession = annotationHost is IConfigurableCaptureAnnotationHost configurableHost
                ? configurableHost.CreateAnnotationSession(
                    target.ScreenBounds,
                    new CaptureAnnotationSessionOptions(
                        recordingOptions.RegionIndicatorStyle,
                        recordingOptions.ShowMouseClickIndicator))
                : annotationHost.CreateAnnotationSession(target.ScreenBounds);
            result = await RecordingCoordinator.RunAsync(
                target,
                recordingOptions,
                annotationSession,
                _helperDirectory,
                outputPath,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryDelete(outputPath);
        }
        catch (Exception exception)
        {
            failure = exception;
            TryDelete(outputPath);
        }
        finally
        {
            completeCaptureSession = ShouldCompleteCaptureSession(
                result,
                failure,
                cancellationToken.IsCancellationRequested);
            if (overlayHidden && !completeCaptureSession)
            {
                try
                {
                    liveHost.SetOverlayVisible(true);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    // The screenshot session was closed while the recorder was stopping.
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (failure is not null)
        {
            ShowMessage(
                $"录屏未完成：{failure.Message}\n\n请确认系统已安装 Media Foundation 和 Visual C++ x64 运行库。",
                "录屏失败",
                MessageBoxIcon.Error);
            return;
        }

        if (completeCaptureSession && result is { FilePath: not null })
        {
            CompleteSavedRecording(artifactHost, result.FilePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result?.Error))
        {
            ShowMessage(
                $"录屏未完成：{result.Error}",
                "录屏失败",
                MessageBoxIcon.Error);
        }
    }

    internal static string CreateOutputPath(string outputFolder, DateTime timestamp)
    {
        var baseName = $"录屏_{timestamp:yyyyMMdd_HHmmss}";
        var path = Path.Combine(outputFolder, baseName + ".mp4");
        for (var suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(outputFolder, $"{baseName}_{suffix}.mp4");
        }
        return path;
    }

    internal static bool ShouldCompleteCaptureSession(
        RecordingControlResult? result,
        Exception? failure,
        bool cancellationRequested) =>
        !cancellationRequested && failure is null && result is { Saved: true, FilePath: not null };

    internal static void CompleteSavedRecording(ICaptureArtifactHost artifactHost, string path)
    {
        ArgumentNullException.ThrowIfNull(artifactHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            artifactHost.NotifyArtifactSaved(path);
        }
        finally
        {
            artifactHost.CompleteCaptureSession();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A failed native recorder may still be releasing the output handle.
        }
        catch (UnauthorizedAccessException)
        {
            // The original failure remains the actionable error.
        }
    }

    private static void ShowMessage(string message, string title, MessageBoxIcon icon) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
}
