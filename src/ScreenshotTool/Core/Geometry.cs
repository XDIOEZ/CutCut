namespace ScreenshotTool.Core;

internal static class Geometry
{
    public static Rectangle Normalize(Point first, Point second)
    {
        var x = Math.Min(first.X, second.X);
        var y = Math.Min(first.Y, second.Y);
        return new Rectangle(x, y, Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));
    }

    public static Point Clamp(Point point, Rectangle bounds) => new(
        Math.Clamp(point.X, bounds.Left, bounds.Right - 1),
        Math.Clamp(point.Y, bounds.Top, bounds.Bottom - 1));
}
