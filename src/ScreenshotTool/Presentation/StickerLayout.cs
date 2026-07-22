namespace ScreenshotTool.Editing;

internal enum StickerHitTarget
{
    None,
    Move,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

internal static class StickerLayout
{
    private const double MaximumAreaRatio = 2D / 3D;

    public static Rectangle CreateInitialBounds(Size imageSize, Rectangle selection, Point anchor)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || selection.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var imageArea = (double)imageSize.Width * imageSize.Height;
        var maximumArea = (double)selection.Width * selection.Height * MaximumAreaRatio;
        var scale = Math.Min(1D, Math.Sqrt(maximumArea / imageArea));
        scale = Math.Min(scale, selection.Width / (double)imageSize.Width);
        scale = Math.Min(scale, selection.Height / (double)imageSize.Height);

        var width = Math.Max(1, (int)Math.Floor(imageSize.Width * scale));
        var height = Math.Max(1, (int)Math.Floor(imageSize.Height * scale));
        return CenterAt(new Size(width, height), anchor, selection);
    }

    public static Rectangle Move(Rectangle original, Point pointerOffset, Rectangle selection)
    {
        var maximumX = Math.Max(selection.Left, selection.Right - original.Width);
        var maximumY = Math.Max(selection.Top, selection.Bottom - original.Height);
        var x = Math.Clamp(original.X + pointerOffset.X, selection.Left, maximumX);
        var y = Math.Clamp(original.Y + pointerOffset.Y, selection.Top, maximumY);
        return new Rectangle(x, y, original.Width, original.Height);
    }

    public static Rectangle Resize(
        Rectangle original,
        StickerHitTarget corner,
        Point pointer,
        Rectangle selection)
    {
        if (!IsCorner(corner) || original.IsEmpty || selection.IsEmpty)
        {
            return original;
        }

        var anchor = GetOppositeCorner(original, corner);
        var desiredWidth = Math.Abs(pointer.X - anchor.X);
        var desiredHeight = Math.Abs(pointer.Y - anchor.Y);
        var desiredScale = Math.Max(
            desiredWidth / (double)original.Width,
            desiredHeight / (double)original.Height);

        var availableWidth = corner is StickerHitTarget.TopLeft or StickerHitTarget.BottomLeft
            ? anchor.X - selection.Left
            : selection.Right - anchor.X;
        var availableHeight = corner is StickerHitTarget.TopLeft or StickerHitTarget.TopRight
            ? anchor.Y - selection.Top
            : selection.Bottom - anchor.Y;
        var maximumArea = (double)selection.Width * selection.Height * MaximumAreaRatio;
        var maximumScale = Math.Min(
            Math.Sqrt(maximumArea / ((double)original.Width * original.Height)),
            Math.Min(
                availableWidth / (double)original.Width,
                availableHeight / (double)original.Height));
        maximumScale = Math.Max(1D / Math.Max(original.Width, original.Height), maximumScale);

        var minimumScale = Math.Min(1D, 24D / Math.Max(original.Width, original.Height));
        minimumScale = Math.Min(minimumScale, maximumScale);
        var scale = Math.Clamp(desiredScale, minimumScale, maximumScale);
        var width = Math.Max(1, (int)Math.Floor(original.Width * scale));
        var height = Math.Max(1, (int)Math.Floor(original.Height * scale));

        return corner switch
        {
            StickerHitTarget.TopLeft => Rectangle.FromLTRB(anchor.X - width, anchor.Y - height, anchor.X, anchor.Y),
            StickerHitTarget.TopRight => Rectangle.FromLTRB(anchor.X, anchor.Y - height, anchor.X + width, anchor.Y),
            StickerHitTarget.BottomLeft => Rectangle.FromLTRB(anchor.X - width, anchor.Y, anchor.X, anchor.Y + height),
            StickerHitTarget.BottomRight => Rectangle.FromLTRB(anchor.X, anchor.Y, anchor.X + width, anchor.Y + height),
            _ => original
        };
    }

    public static StickerHitTarget HitTest(Rectangle bounds, Point point, int handleSize)
    {
        foreach (var (target, handle) in GetHandles(bounds, handleSize))
        {
            if (handle.Contains(point))
            {
                return target;
            }
        }

        return bounds.Contains(point) ? StickerHitTarget.Move : StickerHitTarget.None;
    }

    public static IReadOnlyList<(StickerHitTarget Target, Rectangle Bounds)> GetHandles(
        Rectangle bounds,
        int handleSize)
    {
        var half = handleSize / 2;
        return
        [
            (StickerHitTarget.TopLeft, new Rectangle(bounds.Left - half, bounds.Top - half, handleSize, handleSize)),
            (StickerHitTarget.Top, new Rectangle(bounds.Left + (bounds.Width - handleSize) / 2, bounds.Top - half, handleSize, handleSize)),
            (StickerHitTarget.TopRight, new Rectangle(bounds.Right - half, bounds.Top - half, handleSize, handleSize)),
            (StickerHitTarget.Right, new Rectangle(bounds.Right - half, bounds.Top + (bounds.Height - handleSize) / 2, handleSize, handleSize)),
            (StickerHitTarget.BottomRight, new Rectangle(bounds.Right - half, bounds.Bottom - half, handleSize, handleSize)),
            (StickerHitTarget.Bottom, new Rectangle(bounds.Left + (bounds.Width - handleSize) / 2, bounds.Bottom - half, handleSize, handleSize)),
            (StickerHitTarget.BottomLeft, new Rectangle(bounds.Left - half, bounds.Bottom - half, handleSize, handleSize)),
            (StickerHitTarget.Left, new Rectangle(bounds.Left - half, bounds.Top + (bounds.Height - handleSize) / 2, handleSize, handleSize))
        ];
    }

    public static Cursor GetCursor(StickerHitTarget target) => target switch
    {
        StickerHitTarget.Move => Cursors.SizeAll,
        StickerHitTarget.TopLeft or StickerHitTarget.BottomRight => Cursors.SizeNWSE,
        StickerHitTarget.TopRight or StickerHitTarget.BottomLeft => Cursors.SizeNESW,
        StickerHitTarget.Top or StickerHitTarget.Bottom => Cursors.SizeNS,
        StickerHitTarget.Left or StickerHitTarget.Right => Cursors.SizeWE,
        _ => Cursors.Default
    };

    public static bool IsCorner(StickerHitTarget target) => target is
        StickerHitTarget.TopLeft or
        StickerHitTarget.TopRight or
        StickerHitTarget.BottomLeft or
        StickerHitTarget.BottomRight;

    public static bool IsResizeHandle(StickerHitTarget target) =>
        target is not StickerHitTarget.None and not StickerHitTarget.Move;

    public static bool AdjustsHorizontalEdge(StickerHitTarget target) => target is
        StickerHitTarget.TopLeft or
        StickerHitTarget.TopRight or
        StickerHitTarget.Right or
        StickerHitTarget.BottomRight or
        StickerHitTarget.BottomLeft or
        StickerHitTarget.Left;

    public static bool AdjustsVerticalEdge(StickerHitTarget target) => target is
        StickerHitTarget.TopLeft or
        StickerHitTarget.Top or
        StickerHitTarget.TopRight or
        StickerHitTarget.BottomRight or
        StickerHitTarget.Bottom or
        StickerHitTarget.BottomLeft;

    private static Rectangle CenterAt(Size size, Point anchor, Rectangle selection)
    {
        var x = Math.Clamp(
            anchor.X - size.Width / 2,
            selection.Left,
            Math.Max(selection.Left, selection.Right - size.Width));
        var y = Math.Clamp(
            anchor.Y - size.Height / 2,
            selection.Top,
            Math.Max(selection.Top, selection.Bottom - size.Height));
        return new Rectangle(x, y, size.Width, size.Height);
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
