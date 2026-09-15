namespace ScreenshotTool.Contracts;

// Points follow reading direction: top-left, top-right, bottom-right, bottom-left in source pixels.
public sealed record ImageTextRegion(
    string Text,
    int LineIndex,
    PointF TopLeft,
    PointF TopRight,
    PointF BottomRight,
    PointF BottomLeft,
    string? LeadingText = null);

public sealed record ImageTextRecognitionResult(IReadOnlyList<ImageTextRegion> Regions);

public sealed record ImageTextRecognizerInfo(string Id, string DisplayName);

public interface IImageTextRecognitionProvider
{
    // Recognizes source pixels without opening UI or taking ownership of the image.
    Task<ImageTextRecognitionResult> RecognizeImageTextAsync(Bitmap image, CancellationToken cancellationToken);
}

public interface IImageTextRecognitionService
{
    // Returns currently enabled providers without retaining module instances.
    IReadOnlyList<ImageTextRecognizerInfo> GetTextRecognizers();

    // Holds the selected module alive until recognition has finished, including cancellation.
    Task<ImageTextRecognitionResult> RecognizeImageTextAsync(
        string providerId, Bitmap image, CancellationToken cancellationToken);
}

public interface IModuleImageTextHost : IImageTextRecognitionService
{
    // Copies selected text through the host clipboard service.
    void CopyText(string text);
}
