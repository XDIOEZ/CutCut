namespace ScreenshotTool.Abstractions;

internal interface IClipboardService
{
    void SetImage(Image image);

    // Writes an image with optional application data for editable paste.
    void SetImageWithData(Image image, string format, string data) => SetImage(image);

    // Reads optional application data without interpreting its business meaning.
    string? GetData(string format) => null;

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
