namespace ScreenshotTool.Presentation;

internal static class TextEditorInteraction
{
    public static bool ShouldBeginMove(MouseButtons button, bool altPressed) =>
        button == MouseButtons.Left && altPressed;

    public static Rectangle Move(Rectangle original, Point pointerOffset, Rectangle limits)
    {
        if (original.IsEmpty || limits.IsEmpty)
        {
            return original;
        }

        var width = Math.Min(original.Width, limits.Width);
        var height = Math.Min(original.Height, limits.Height);
        var x = Math.Clamp(
            original.X + pointerOffset.X,
            limits.Left,
            Math.Max(limits.Left, limits.Right - width));
        var y = Math.Clamp(
            original.Y + pointerOffset.Y,
            limits.Top,
            Math.Max(limits.Top, limits.Bottom - height));
        return new Rectangle(x, y, width, height);
    }
}
