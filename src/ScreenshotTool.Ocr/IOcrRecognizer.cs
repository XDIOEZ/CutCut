namespace ScreenshotTool.Ocr;

internal interface IOcrRecognizer
{
    Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken);
}
