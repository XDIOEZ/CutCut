namespace ScreenshotTool.PinnedImage;

[Flags]
internal enum PinnedImageResizeEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8
}

internal static class PinnedImageWindowLayout
{
    public static readonly Size MinimumSize = new(32, 24);

    public static Rectangle CreateInitialBounds(
        Size imageSize,
        Rectangle suggestedBounds,
        Rectangle workingArea)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || workingArea.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var maximumWidth = Math.Max(
            1,
            Math.Min(
                workingArea.Width,
                suggestedBounds.Width > 0 ? suggestedBounds.Width : imageSize.Width));
        var maximumHeight = Math.Max(
            1,
            Math.Min(
                workingArea.Height,
                suggestedBounds.Height > 0 ? suggestedBounds.Height : imageSize.Height));
        var scale = Math.Min(
            1D,
            Math.Min(
                maximumWidth / (double)imageSize.Width,
                maximumHeight / (double)imageSize.Height));
        var width = Math.Clamp(
            (int)Math.Round(imageSize.Width * scale),
            Math.Min(MinimumSize.Width, workingArea.Width),
            workingArea.Width);
        var height = Math.Clamp(
            (int)Math.Round(imageSize.Height * scale),
            Math.Min(MinimumSize.Height, workingArea.Height),
            workingArea.Height);
        var requestedLocation = suggestedBounds.IsEmpty
            ? new Point(
                workingArea.Left + (workingArea.Width - width) / 2,
                workingArea.Top + (workingArea.Height - height) / 2)
            : suggestedBounds.Location;
        var x = Math.Clamp(
            requestedLocation.X,
            workingArea.Left,
            Math.Max(workingArea.Left, workingArea.Right - width));
        var y = Math.Clamp(
            requestedLocation.Y,
            workingArea.Top,
            Math.Max(workingArea.Top, workingArea.Bottom - height));
        return new Rectangle(x, y, width, height);
    }

    public static PinnedImageResizeEdges HitTestEdges(
        Size clientSize,
        Point point,
        int gripSize)
    {
        if (clientSize.Width <= 0 ||
            clientSize.Height <= 0 ||
            gripSize <= 0 ||
            point.X < 0 ||
            point.Y < 0 ||
            point.X >= clientSize.Width ||
            point.Y >= clientSize.Height)
        {
            return PinnedImageResizeEdges.None;
        }

        var edges = PinnedImageResizeEdges.None;
        if (point.X < gripSize)
        {
            edges |= PinnedImageResizeEdges.Left;
        }
        else if (point.X >= clientSize.Width - gripSize)
        {
            edges |= PinnedImageResizeEdges.Right;
        }
        if (point.Y < gripSize)
        {
            edges |= PinnedImageResizeEdges.Top;
        }
        else if (point.Y >= clientSize.Height - gripSize)
        {
            edges |= PinnedImageResizeEdges.Bottom;
        }
        return edges;
    }

    public static Rectangle Move(Rectangle original, Point pointerOffset) =>
        new(
            original.X + pointerOffset.X,
            original.Y + pointerOffset.Y,
            original.Width,
            original.Height);

    public static Rectangle Resize(
        Rectangle original,
        PinnedImageResizeEdges edges,
        Point pointerOffset,
        bool preserveAspectRatio)
    {
        if (original.IsEmpty || edges == PinnedImageResizeEdges.None)
        {
            return original;
        }

        var unconstrained = ResizeIndependent(original, edges, pointerOffset);
        if (!preserveAspectRatio)
        {
            return unconstrained;
        }

        var horizontal = (edges & (PinnedImageResizeEdges.Left | PinnedImageResizeEdges.Right)) != 0;
        var vertical = (edges & (PinnedImageResizeEdges.Top | PinnedImageResizeEdges.Bottom)) != 0;
        var ratio = original.Width / (double)original.Height;
        if (horizontal && !vertical)
        {
            var width = unconstrained.Width;
            var height = Math.Max(
                MinimumSize.Height,
                (int)Math.Round(width / ratio));
            if (height == MinimumSize.Height)
            {
                width = Math.Max(MinimumSize.Width, (int)Math.Round(height * ratio));
            }
            var x = edges.HasFlag(PinnedImageResizeEdges.Left)
                ? original.Right - width
                : original.Left;
            var y = original.Top + (original.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }
        if (vertical && !horizontal)
        {
            var height = unconstrained.Height;
            var width = Math.Max(
                MinimumSize.Width,
                (int)Math.Round(height * ratio));
            if (width == MinimumSize.Width)
            {
                height = Math.Max(MinimumSize.Height, (int)Math.Round(width / ratio));
            }
            var x = original.Left + (original.Width - width) / 2;
            var y = edges.HasFlag(PinnedImageResizeEdges.Top)
                ? original.Bottom - height
                : original.Top;
            return new Rectangle(x, y, width, height);
        }

        var widthScale = unconstrained.Width / (double)original.Width;
        var heightScale = unconstrained.Height / (double)original.Height;
        var scale = Math.Abs(widthScale - 1D) >= Math.Abs(heightScale - 1D)
            ? widthScale
            : heightScale;
        var minimumScale = Math.Max(
            MinimumSize.Width / (double)original.Width,
            MinimumSize.Height / (double)original.Height);
        scale = Math.Max(scale, minimumScale);
        var scaledWidth = Math.Max(
            MinimumSize.Width,
            (int)Math.Round(original.Width * scale));
        var scaledHeight = Math.Max(
            MinimumSize.Height,
            (int)Math.Round(original.Height * scale));
        var scaledX = edges.HasFlag(PinnedImageResizeEdges.Left)
            ? original.Right - scaledWidth
            : original.Left;
        var scaledY = edges.HasFlag(PinnedImageResizeEdges.Top)
            ? original.Bottom - scaledHeight
            : original.Top;
        return new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);
    }

    public static Cursor GetCursor(PinnedImageResizeEdges edges) => edges switch
    {
        PinnedImageResizeEdges.Left or PinnedImageResizeEdges.Right => Cursors.SizeWE,
        PinnedImageResizeEdges.Top or PinnedImageResizeEdges.Bottom => Cursors.SizeNS,
        PinnedImageResizeEdges.Left | PinnedImageResizeEdges.Top or
            PinnedImageResizeEdges.Right | PinnedImageResizeEdges.Bottom => Cursors.SizeNWSE,
        PinnedImageResizeEdges.Right | PinnedImageResizeEdges.Top or
            PinnedImageResizeEdges.Left | PinnedImageResizeEdges.Bottom => Cursors.SizeNESW,
        _ => Cursors.SizeAll
    };

    private static Rectangle ResizeIndependent(
        Rectangle original,
        PinnedImageResizeEdges edges,
        Point pointerOffset)
    {
        var left = edges.HasFlag(PinnedImageResizeEdges.Left)
            ? original.Left + pointerOffset.X
            : original.Left;
        var right = edges.HasFlag(PinnedImageResizeEdges.Right)
            ? original.Right + pointerOffset.X
            : original.Right;
        var top = edges.HasFlag(PinnedImageResizeEdges.Top)
            ? original.Top + pointerOffset.Y
            : original.Top;
        var bottom = edges.HasFlag(PinnedImageResizeEdges.Bottom)
            ? original.Bottom + pointerOffset.Y
            : original.Bottom;

        if (right - left < MinimumSize.Width)
        {
            if (edges.HasFlag(PinnedImageResizeEdges.Left))
            {
                left = right - MinimumSize.Width;
            }
            else
            {
                right = left + MinimumSize.Width;
            }
        }
        if (bottom - top < MinimumSize.Height)
        {
            if (edges.HasFlag(PinnedImageResizeEdges.Top))
            {
                top = bottom - MinimumSize.Height;
            }
            else
            {
                bottom = top + MinimumSize.Height;
            }
        }
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
