namespace ScreenshotTool.LongCapture;

internal static class LongCapturePreviewLayout
{
    private const int Margin = 10;

    public static RectangleF GetImageBounds(
        Size clientSize,
        Size imageSize,
        double zoom,
        PointF panOffset)
    {
        var availableWidth = Math.Max(1, clientSize.Width - Margin * 2);
        var availableHeight = Math.Max(1, clientSize.Height - Margin * 2);
        var fitScale = Math.Min(
            availableWidth / (double)imageSize.Width,
            availableHeight / (double)imageSize.Height);
        var width = Math.Max(1F, (float)(imageSize.Width * fitScale * zoom));
        var height = Math.Max(1F, (float)(imageSize.Height * fitScale * zoom));
        return new RectangleF(
            (clientSize.Width - width) / 2F + panOffset.X,
            (clientSize.Height - height) / 2F + panOffset.Y,
            width,
            height);
    }

    public static PointF ZoomAt(
        Size clientSize,
        Size imageSize,
        double previousZoom,
        double nextZoom,
        PointF previousPanOffset,
        Point cursor)
    {
        var previousBounds = GetImageBounds(
            clientSize,
            imageSize,
            previousZoom,
            previousPanOffset);
        var imageX = (cursor.X - previousBounds.Left) / previousBounds.Width;
        var imageY = (cursor.Y - previousBounds.Top) / previousBounds.Height;
        var nextBaseBounds = GetImageBounds(
            clientSize,
            imageSize,
            nextZoom,
            PointF.Empty);
        var nextPan = new PointF(
            cursor.X - nextBaseBounds.Left - imageX * nextBaseBounds.Width,
            cursor.Y - nextBaseBounds.Top - imageY * nextBaseBounds.Height);
        return ClampPan(clientSize, imageSize, nextZoom, nextPan);
    }

    public static PointF ClampPan(
        Size clientSize,
        Size imageSize,
        double zoom,
        PointF panOffset)
    {
        var baseBounds = GetImageBounds(clientSize, imageSize, zoom, PointF.Empty);
        return new PointF(
            ClampAxis(
                panOffset.X,
                baseBounds.Left,
                baseBounds.Width,
                clientSize.Width),
            ClampAxis(
                panOffset.Y,
                baseBounds.Top,
                baseBounds.Height,
                clientSize.Height));
    }

    public static bool CanPan(Size clientSize, Size imageSize, double zoom)
        => zoom > 1D;

    private static float ClampAxis(
        float offset,
        float baseStart,
        float contentLength,
        int clientLength)
    {
        if (contentLength <= clientLength)
        {
            var containedMinimum = -baseStart;
            var containedMaximum = clientLength - contentLength - baseStart;
            return Math.Clamp(offset, containedMinimum, containedMaximum);
        }

        var minimum = clientLength - contentLength - baseStart;
        var maximum = -baseStart;
        return Math.Clamp(offset, minimum, maximum);
    }
}
