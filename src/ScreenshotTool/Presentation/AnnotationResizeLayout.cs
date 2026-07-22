using ScreenshotTool.Editing;

namespace ScreenshotTool.Presentation;

internal static class AnnotationResizeLayout
{
    private const int MinimumSideLength = 8;

    public static Rectangle Resize(
        Rectangle original,
        StickerHitTarget corner,
        Point pointer,
        Rectangle selection)
    {
        if (!StickerLayout.IsResizeHandle(corner) || original.IsEmpty || selection.IsEmpty)
        {
            return original;
        }

        var anchor = GetOppositeCorner(original, corner);
        return corner switch
        {
            StickerHitTarget.TopLeft => Rectangle.FromLTRB(
                ClampLeading(pointer.X, selection.Left, anchor.X),
                ClampLeading(pointer.Y, selection.Top, anchor.Y),
                anchor.X,
                anchor.Y),
            StickerHitTarget.TopRight => Rectangle.FromLTRB(
                anchor.X,
                ClampLeading(pointer.Y, selection.Top, anchor.Y),
                ClampTrailing(pointer.X, anchor.X, selection.Right),
                anchor.Y),
            StickerHitTarget.Top => Rectangle.FromLTRB(
                original.Left,
                ClampLeading(pointer.Y, selection.Top, original.Bottom),
                original.Right,
                original.Bottom),
            StickerHitTarget.Right => Rectangle.FromLTRB(
                original.Left,
                original.Top,
                ClampTrailing(pointer.X, original.Left, selection.Right),
                original.Bottom),
            StickerHitTarget.Bottom => Rectangle.FromLTRB(
                original.Left,
                original.Top,
                original.Right,
                ClampTrailing(pointer.Y, original.Top, selection.Bottom)),
            StickerHitTarget.Left => Rectangle.FromLTRB(
                ClampLeading(pointer.X, selection.Left, original.Right),
                original.Top,
                original.Right,
                original.Bottom),
            StickerHitTarget.BottomLeft => Rectangle.FromLTRB(
                ClampLeading(pointer.X, selection.Left, anchor.X),
                anchor.Y,
                anchor.X,
                ClampTrailing(pointer.Y, anchor.Y, selection.Bottom)),
            StickerHitTarget.BottomRight => Rectangle.FromLTRB(
                anchor.X,
                anchor.Y,
                ClampTrailing(pointer.X, anchor.X, selection.Right),
                ClampTrailing(pointer.Y, anchor.Y, selection.Bottom)),
            _ => original
        };
    }

    private static int ClampLeading(int value, int limit, int anchor)
    {
        var available = Math.Max(1, anchor - limit);
        var minimum = Math.Min(MinimumSideLength, available);
        return Math.Clamp(value, limit, anchor - minimum);
    }

    private static int ClampTrailing(int value, int anchor, int limit)
    {
        var available = Math.Max(1, limit - anchor);
        var minimum = Math.Min(MinimumSideLength, available);
        return Math.Clamp(value, anchor + minimum, limit);
    }

    private static Point GetOppositeCorner(Rectangle bounds, StickerHitTarget corner) => corner switch
    {
        StickerHitTarget.TopLeft => new Point(bounds.Right, bounds.Bottom),
        StickerHitTarget.TopRight => new Point(bounds.Left, bounds.Bottom),
        StickerHitTarget.BottomLeft => new Point(bounds.Right, bounds.Top),
        StickerHitTarget.BottomRight => new Point(bounds.Left, bounds.Top),
        _ => bounds.Location
    };

}
