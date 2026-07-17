namespace ScreenshotTool.Presentation;

internal static class CaptureEditorViewportLayout
{
    public const double MinimumZoom = 0.03D;
    public const double MaximumZoom = 8D;

    public static double ClampZoom(double zoom) =>
        Math.Clamp(zoom, MinimumZoom, MaximumZoom);

    public static double CalculateFitZoom(Size viewportSize, Size imageSize)
    {
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0 ||
            imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return 1D;
        }

        var availableWidth = Math.Max(
            1,
            viewportSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
        var availableHeight = Math.Max(
            1,
            viewportSize.Height - SystemInformation.HorizontalScrollBarHeight - 8);
        return ClampZoom(Math.Min(1D, Math.Min(
            availableWidth / (double)imageSize.Width,
            availableHeight / (double)imageSize.Height)));
    }

    public static double CalculateWidthFitZoom(Size viewportSize, Size imageSize)
    {
        if (viewportSize.Width <= 0 || imageSize.Width <= 0)
        {
            return 1D;
        }

        return ClampZoom(Math.Min(1D, viewportSize.Width / (double)imageSize.Width));
    }

    public static Size CalculateCanvasSize(Size imageSize, double zoom) => new(
        Math.Max(1, (int)Math.Round(imageSize.Width * ClampZoom(zoom))),
        Math.Max(1, (int)Math.Round(imageSize.Height * ClampZoom(zoom))));

    public static Point CalculateZoomScroll(
        double previousZoom,
        double nextZoom,
        Point currentScroll,
        Size viewportSize,
        Point? viewportAnchor = null,
        PointF? anchoredImagePoint = null)
    {
        previousZoom = ClampZoom(previousZoom);
        nextZoom = ClampZoom(nextZoom);
        var clientAnchor = viewportAnchor ??
                           new Point(viewportSize.Width / 2, viewportSize.Height / 2);
        var imageAnchor = anchoredImagePoint ?? new PointF(
            (currentScroll.X + clientAnchor.X) / (float)previousZoom,
            (currentScroll.Y + clientAnchor.Y) / (float)previousZoom);
        return new Point(
            Math.Max(0, (int)Math.Round(imageAnchor.X * nextZoom - clientAnchor.X)),
            Math.Max(0, (int)Math.Round(imageAnchor.Y * nextZoom - clientAnchor.Y)));
    }

    public static Point CalculatePanScroll(
        Point originScroll,
        Point pointerOrigin,
        Point pointerCurrent) => new(
        Math.Max(0, originScroll.X - (pointerCurrent.X - pointerOrigin.X)),
        Math.Max(0, originScroll.Y - (pointerCurrent.Y - pointerOrigin.Y)));
}
