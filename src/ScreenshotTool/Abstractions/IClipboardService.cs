namespace ScreenshotTool.Abstractions;

internal interface IClipboardService
{
    void SetImage(Image image);

    Bitmap? GetImage();

    string? GetText();

    void SetText(string text);
}
