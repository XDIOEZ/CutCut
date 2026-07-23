namespace ScreenshotTool.PaddleOcr;

internal interface IPaddleOcrRecognizer : IDisposable
{
    Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken);
}
