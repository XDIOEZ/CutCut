namespace ScreenshotTool.Presentation;

internal static class TextEditorCommitLayout
{
    public static float CalculateImageFontSize(float visualFontSize, double zoom)
    {
        if (visualFontSize <= 0F)
        {
            throw new ArgumentOutOfRangeException(nameof(visualFontSize));
        }

        return visualFontSize / (float)Math.Max(
            CaptureEditorViewportLayout.MinimumZoom,
            zoom);
    }
}