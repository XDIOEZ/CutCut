namespace ScreenshotTool.Presentation;

internal static class GroupMoveLayout
{
    public static Point ClampOffset(Rectangle groupBounds, Point desiredOffset, Rectangle limits)
    {
        if (groupBounds.IsEmpty || limits.IsEmpty)
        {
            return Point.Empty;
        }

        var horizontal = ClampAxis(
            desiredOffset.X,
            limits.Left - groupBounds.Left,
            limits.Right - groupBounds.Right);
        var vertical = ClampAxis(
            desiredOffset.Y,
            limits.Top - groupBounds.Top,
            limits.Bottom - groupBounds.Bottom);
        return new Point(horizontal, vertical);
    }

    private static int ClampAxis(int value, int minimum, int maximum) =>
        minimum <= maximum ? Math.Clamp(value, minimum, maximum) : 0;
}
