namespace ScreenshotTool.Presentation;

internal static class SelectionMover
{
    public static Rectangle Move(Rectangle original, Point pointerOffset, Rectangle limits)
    {
        if (original.IsEmpty || limits.IsEmpty)
        {
            return original;
        }

        var maximumX = Math.Max(limits.Left, limits.Right - original.Width);
        var maximumY = Math.Max(limits.Top, limits.Bottom - original.Height);
        var x = Math.Clamp(original.X + pointerOffset.X, limits.Left, maximumX);
        var y = Math.Clamp(original.Y + pointerOffset.Y, limits.Top, maximumY);
        return new Rectangle(x, y, original.Width, original.Height);
    }
}
