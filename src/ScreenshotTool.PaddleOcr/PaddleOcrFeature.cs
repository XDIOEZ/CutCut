using ScreenshotTool.Contracts;

namespace ScreenshotTool.PaddleOcr;

internal sealed class PaddleOcrFeature :
    CaptureFeatureBase,
    ICaptureToolbarCommandProvider,
    ICaptureToolbarCommandProgressProvider
{
    private readonly IPaddleOcrRecognizer _recognizer;
    private readonly IReadOnlyList<CaptureToolbarCommand> _commands;
    private readonly string _commandId;
    private readonly string _resultTitle;
    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _activeRecognitionCancellation;
    private bool _disposed;

    public PaddleOcrFeature(
        string featureId,
        int order,
        string commandId,
        string commandText,
        string commandToolTip,
        string resultTitle,
        IPaddleOcrRecognizer recognizer)
    {
        Id = featureId;
        Order = order;
        _commandId = commandId;
        _resultTitle = resultTitle;
        _recognizer = recognizer;
        _commands = Array.AsReadOnly(
        [
            new CaptureToolbarCommand(
                commandId,
                commandText,
                commandToolTip,
                Math.Max(68, commandText.Length * 12))
        ]);
    }

    public override string Id { get; }

    public override int Order { get; }

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() => _commands;

    public bool UsesIndeterminateProgress(string commandId) =>
        string.Equals(commandId, _commandId, StringComparison.Ordinal);

    public async Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, _commandId, StringComparison.Ordinal))
        {
            throw new ArgumentException("未知的 PP-OCR 命令。", nameof(commandId));
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
        _recognizer.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async Task RecognizeSelectionAsync(CancellationToken cancellationToken)
    {
        if (Host is not ICaptureTextResultHost resultHost ||
            Host is not ICaptureArtifactHost artifactHost)
        {
            ShowMessage(
                "当前轻截版本不支持 OCR 结果窗口，请更新基础程序后重试。",
                "PP-OCR 不可用",
                MessageBoxIcon.Warning);
            return;
        }

        if (!Host.HasSelection || Host.Selection.IsEmpty)
        {
            ShowMessage(
                "请先框选需要识别文字的区域。",
                _resultTitle,
                MessageBoxIcon.Information);
            return;
        }

        using var image = Host.CopyDesktopSelection();
        var text = await _recognizer.RecognizeAsync(image, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowMessage(
                "当前选区中没有识别到文字。可以缩小选区或提高文字清晰度后重试。",
                "没有识别到文字",
                MessageBoxIcon.Information);
            return;
        }

        resultHost.ShowTextResult(_resultTitle, text);
        artifactHost.CompleteCaptureSession();
    }

    private static void ShowMessage(string message, string title, MessageBoxIcon icon) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
}
