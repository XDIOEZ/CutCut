namespace ScreenshotTool.Abstractions;

internal interface IClipboardService
{
    void SetImage(Image image);

    // Writes an image asynchronously when the concrete clipboard service supports it.
    Task SetImageAsync(Image image, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetImage(image);
        return Task.CompletedTask;
    }

    Bitmap? GetImage();

    string? GetText();

    void SetText(string text);
}
