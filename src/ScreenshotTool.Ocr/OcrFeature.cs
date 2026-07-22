using ScreenshotTool.Contracts;

namespace ScreenshotTool.Ocr;

internal sealed class OcrFeature : CaptureFeatureBase, ICaptureToolbarCommandProvider
{
    internal const string CommandId = "screenshot-tool.ocr.recognize";
    private static readonly IReadOnlyList<CaptureToolbarCommand> Commands =
        Array.AsReadOnly(
        [
            new CaptureToolbarCommand(
                CommandId,
                "OCR",
                "识别当前选区中的文字，并在侧边小窗口中打开可编辑结果",
                54)
        ]);

    private readonly IOcrRecognizer _recognizer;
    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _activeRecognitionCancellation;
    private bool _disposed;

    public OcrFeature(IOcrRecognizer recognizer)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        _recognizer = recognizer;
    }

    public override string Id => "screenshot-tool.ocr.feature";

    public override int Order => 550;

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() => Commands;

    public async Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, CommandId, StringComparison.Ordinal))
        {
            throw new ArgumentException("未知的 OCR 命令。", nameof(commandId));
        }

        CancellationTokenSource activeCancellation;
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeRecognitionCancellation is not null)
            {
                return;
            }

            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            _activeRecognitionCancellation = activeCancellation;
        }

        try
        {
            await RecognizeSelectionAsync(activeCancellation.Token);
        }
        finally
        {
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_activeRecognitionCancellation, activeCancellation))
                {
                    _activeRecognitionCancellation = null;
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
            activeCancellation = _activeRecognitionCancellation;
        }

        _lifetimeCancellation.Cancel();
        activeCancellation?.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task RecognizeSelectionAsync(CancellationToken cancellationToken)
    {
        if (Host is not ICaptureTextResultHost resultHost ||
            Host is not ICaptureArtifactHost artifactHost)
        {
            ShowMessage(
                "当前轻截版本不支持 OCR 结果窗口，请更新基础程序后重试。",
                "OCR 不可用",
                MessageBoxIcon.Warning);
            return;
        }

        if (!Host.HasSelection || Host.Selection.IsEmpty)
        {
            ShowMessage(
                "请先框选需要识别文字的区域。",
                "OCR 文字识别",
                MessageBoxIcon.Information);
            return;
        }

        using var image = Host.CopyDesktopSelection();
        var text = await _recognizer.RecognizeAsync(image, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
        {
            ShowMessage(
                "当前选区中没有识别到文字。可以缩小选区、提高文字清晰度后重试。",
                "没有识别到文字",
                MessageBoxIcon.Information);
            return;
        }

        resultHost.ShowTextResult("OCR 识别结果", text);
        artifactHost.CompleteCaptureSession();
    }

    private static void ShowMessage(string message, string title, MessageBoxIcon icon) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
}
