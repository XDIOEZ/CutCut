using System.Drawing.Drawing2D;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure;

internal sealed class ScreenCaptureService : IScreenCaptureService
{
    public DesktopSnapshot CaptureDesktop()
    {
        var bounds = SystemInformation.VirtualScreen;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("没有检测到可截图的显示器。 ");
        }

        var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return new DesktopSnapshot(bitmap, bounds);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
