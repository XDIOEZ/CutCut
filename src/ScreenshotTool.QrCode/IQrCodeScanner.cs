namespace ScreenshotTool.QrCode;

internal interface IQrCodeScanner
{
    Task<IReadOnlyList<string>> ScanAsync(Bitmap image, CancellationToken cancellationToken);
}
