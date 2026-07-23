namespace ScreenshotTool.Editing;

internal static class AnnotationHandleLayout
{
    public static IReadOnlyList<(StickerHitTarget Target, Rectangle Bounds)> GetHandles(
        MovableAnnotation annotation,
        int handleSize)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (annotation is not ArrowAnnotation arrow)
        {
            return StickerLayout.GetHandles(annotation.Bounds, handleSize);
        }

        return
        [
            CreateEndpointHandle(arrow.Bounds, arrow.Start, handleSize),
            CreateEndpointHandle(arrow.Bounds, arrow.End, handleSize)
        ];
    }

    public static StickerHitTarget HitTest(
        MovableAnnotation annotation,
        Point point,
        int handleSize,
        int hitTolerance)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (!annotation.SupportsResize)
        {
            return annotation.HitTest(point, hitTolerance)
                ? StickerHitTarget.Move
                : StickerHitTarget.None;
        }

        var unrotatedPoint = annotation.ToUnrotatedPoint(point);
        foreach (var (target, handle) in GetHandles(annotation, handleSize))
        {
            if (handle.Contains(unrotatedPoint))
            {
                return target;
            }
        }

        return annotation.Bounds.Contains(unrotatedPoint)
            ? StickerHitTarget.Move
            : StickerHitTarget.None;
    }

    private static (StickerHitTarget Target, Rectangle Bounds) CreateEndpointHandle(
        Rectangle bounds,
        Point endpoint,
        int handleSize)
    {
        var half = handleSize / 2;
        return (
            ResolveCorner(bounds, endpoint),
            new Rectangle(endpoint.X - half, endpoint.Y - half, handleSize, handleSize));
    }

    private static StickerHitTarget ResolveCorner(Rectangle bounds, Point endpoint)
    {
        var right = Math.Max(bounds.Left, bounds.Right - 1);
        var bottom = Math.Max(bounds.Top, bounds.Bottom - 1);
        var leftSide = Math.Abs(endpoint.X - bounds.Left) <= Math.Abs(endpoint.X - right);
        var topSide = Math.Abs(endpoint.Y - bounds.Top) <= Math.Abs(endpoint.Y - bottom);
        return (leftSide, topSide) switch
        {
            (true, true) => StickerHitTarget.TopLeft,
            (false, true) => StickerHitTarget.TopRight,
            (true, false) => StickerHitTarget.BottomLeft,
            _ => StickerHitTarget.BottomRight
        };
    }
}
