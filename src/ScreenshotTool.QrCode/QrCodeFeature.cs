using ScreenshotTool.Contracts;

namespace ScreenshotTool.QrCode;

internal sealed class QrCodeFeature : CaptureFeatureBase, ICaptureToolbarCommandProvider
{
    internal const string CommandId = "screenshot-tool.qr-code.scan";
    private static readonly IReadOnlyList<CaptureToolbarCommand> Commands =
        Array.AsReadOnly(
        [
            new CaptureToolbarCommand(
                CommandId,
                "二维码",
                "扫描当前选区中的二维码，并在侧边小窗口中打开结果",
                66)
        ]);

    private readonly IQrCodeScanner _scanner;
    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _activeScanCancellation;
    private bool _disposed;

    public QrCodeFeature(IQrCodeScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        _scanner = scanner;
    }

    public override string Id => "screenshot-tool.qr-code.feature";

    public override int Order => 560;

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() => Commands;

    public async Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, CommandId, StringComparison.Ordinal))
        {
            throw new ArgumentException("未知的二维码扫描命令。", nameof(commandId));
        }

        CancellationTokenSource activeCancellation;
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeScanCancellation is not null)
            {
                return;
            }

            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            _activeScanCancellation = activeCancellation;
        }

        try
        {
            await ScanSelectionAsync(activeCancellation.Token);
        }
        finally
        {
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_activeScanCancellation, activeCancellation))
                {
                    _activeScanCancellation = null;
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
            activeCancellation = _activeScanCancellation;
        }

        _lifetimeCancellation.Cancel();
        activeCancellation?.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private async Task ScanSelectionAsync(CancellationToken cancellationToken)
    {
        if (Host is not ICaptureTextResultHost resultHost ||
            Host is not ICaptureArtifactHost artifactHost)
        {
            ShowMessage(
                "当前轻截版本不支持二维码结果窗口，请更新基础程序后重试。",
                "二维码扫描不可用",
                MessageBoxIcon.Warning);
            return;
        }

        if (!Host.HasSelection || Host.Selection.IsEmpty)
        {
            ShowMessage(
                "请先框选需要扫描二维码的区域。",
                "二维码扫描",
                MessageBoxIcon.Information);
            return;
        }

        using var image = Host.CopyDesktopSelection();
        var results = await _scanner.ScanAsync(image, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (results.Count == 0)
        {
            ShowMessage(
                "当前选区中没有识别到二维码。可以缩小选区、确保二维码完整清晰后重试。",
                "没有识别到二维码",
                MessageBoxIcon.Information);
            return;
        }

        var text = string.Join(Environment.NewLine + Environment.NewLine, results);
        resultHost.ShowTextResult("二维码扫描结果", text);
        artifactHost.CompleteCaptureSession();
    }

    private static void ShowMessage(string message, string title, MessageBoxIcon icon) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
}
