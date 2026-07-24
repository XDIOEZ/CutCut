namespace ScreenshotTool.Presentation;

internal static class ExistingImageEditLayout
{
    public static Rectangle CalculateSelection(
        Rectangle overlayScreenBounds,
        Rectangle clientBounds,
        Rectangle targetScreenBounds,
        Size imageSize)
    {
        if (clientBounds.Width <= 0 || clientBounds.Height <= 0 ||
            imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return Rectangle.Empty;
        }

        var targetClientBounds = new Rectangle(
            clientBounds.Left + targetScreenBounds.Left - overlayScreenBounds.Left,
            clientBounds.Top + targetScreenBounds.Top - overlayScreenBounds.Top,
            targetScreenBounds.Width,
            targetScreenBounds.Height);
        var viewportBounds = Rectangle.Intersect(clientBounds, targetClientBounds);
        if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
        {
            viewportBounds = clientBounds;
        }

        var horizontalMargin = Math.Clamp(viewportBounds.Width / 12, 24, 80);
        var verticalMargin = Math.Clamp(viewportBounds.Height / 10, 32, 96);
        var availableSize = new Size(
            Math.Max(1, viewportBounds.Width - horizontalMargin * 2),
            Math.Max(1, viewportBounds.Height - verticalMargin * 2));
        var zoom = CaptureEditorViewportLayout.CalculateFitZoom(availableSize, imageSize);
        var displaySize = CaptureEditorViewportLayout.CalculateCanvasSize(imageSize, zoom);
        return new Rectangle(
            viewportBounds.Left + (viewportBounds.Width - displaySize.Width) / 2,
            viewportBounds.Top + (viewportBounds.Height - displaySize.Height) / 2,
            displaySize.Width,
            displaySize.Height);
    }
}
