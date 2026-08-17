using System.Drawing.Drawing2D;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureBackgroundLayer : IDisposable
{
    private const int ShadeAlpha = 118;
    private bool _disposed;

    public CaptureBackgroundLayer(Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentException("截图背景尺寸无效。", nameof(source));
        }

        Source = source;
        Dimmed = CreateDimmedImage(source);
    }

    public Bitmap Source { get; }

    public Bitmap Dimmed { get; }

    public void Replace(Rectangle targetBounds, Bitmap replacement)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(replacement);
        var sourceBounds = new Rectangle(Point.Empty, Source.Size);
        if (targetBounds.IsEmpty || Rectangle.Intersect(sourceBounds, targetBounds) != targetBounds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetBounds),
                targetBounds,
                "刷新区域必须完整位于截图背景内。");
        }
        if (replacement.Size != targetBounds.Size)
        {
            throw new ArgumentException(
                "刷新截图尺寸必须与原截图框一致。",
                nameof(replacement));
        }

        DrawSourceCopy(Source, replacement, targetBounds.Location);
        DrawSourceCopy(Dimmed, replacement, targetBounds.Location);
        using var graphics = Graphics.FromImage(Dimmed);
        graphics.CompositingMode = CompositingMode.SourceOver;
        using var shade = new SolidBrush(Color.FromArgb(ShadeAlpha, 0, 0, 0));
        graphics.FillRectangle(shade, targetBounds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Dimmed.Dispose();
    }

    private static Bitmap CreateDimmedImage(Bitmap source)
    {
        var dimmed = new Bitmap(
            source.Width,
            source.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            DrawSourceCopy(dimmed, source, Point.Empty);
            using var graphics = Graphics.FromImage(dimmed);
            graphics.CompositingMode = CompositingMode.SourceOver;
            using var shade = new SolidBrush(Color.FromArgb(ShadeAlpha, 0, 0, 0));
            graphics.FillRectangle(shade, new Rectangle(Point.Empty, source.Size));
            return dimmed;
        }
        catch
        {
            dimmed.Dispose();
            throw;
        }
    }

    private static void DrawSourceCopy(Bitmap target, Bitmap source, Point location)
    {
        using var graphics = Graphics.FromImage(target);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(source, location);
    }
}
