namespace ScreenshotTool.Presentation;

internal static class LongCaptureEditorFrameLayout
{
    public static Rectangle GetSizeBadgeBounds(
        Rectangle frame,
        Size textSize,
        Rectangle clientBounds)
    {
        var width = textSize.Width + 10;
        var height = textSize.Height + 4;
        var x = Math.Clamp(
            frame.Left,
            clientBounds.Left + 4,
            Math.Max(clientBounds.Left + 4, clientBounds.Right - width - 4));
        var y = frame.Top - height - 6;
        if (y < clientBounds.Top + 4)
        {
            y = frame.Top + 6;
        }

        return new Rectangle(x, y, width, height);
    }

    public static bool IsMoveHandle(
        Rectangle frame,
        Rectangle sizeBadge,
        Point pointer,
        int borderTolerance)
    {
        var badgeHitBounds = sizeBadge;
        badgeHitBounds.Inflate(
            Math.Max(4, borderTolerance),
            Math.Max(3, borderTolerance / 2));
        if (badgeHitBounds.Contains(pointer))
        {
            return true;
        }

        var outer = frame;
        outer.Inflate(borderTolerance, borderTolerance);
        if (!outer.Contains(pointer))
        {
            return false;
        }

        var inner = frame;
        inner.Inflate(-borderTolerance, -borderTolerance);
        return inner.IsEmpty || !inner.Contains(pointer);
    }
}
