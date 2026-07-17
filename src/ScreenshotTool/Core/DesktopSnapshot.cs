namespace ScreenshotTool.Core;

internal sealed class DesktopSnapshot : IDisposable
{
    public DesktopSnapshot(Bitmap image, Rectangle bounds)
    {
        Image = image;
        Bounds = bounds;
    }

    public Bitmap Image { get; }

    public Rectangle Bounds { get; }

    public void Dispose() => Image.Dispose();
}
