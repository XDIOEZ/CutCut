using ScreenshotTool.PinnedImage.DragDrop;

namespace ScreenshotTool.PinnedImage;

// Accepts external images without adding controls over the pinned image.
internal sealed class PinnedImageDragController : IDisposable
{
    private readonly Form _owner;
    private readonly Action<Bitmap, Rectangle>? _showImage;
    private bool _disposed;

    public PinnedImageDragController(Form owner, Action<Bitmap, Rectangle>? showImage)
    {
        _owner = owner;
        _showImage = showImage;
        if (showImage is not null)
        {
            owner.AllowDrop = true;
            owner.DragEnter += HandleDragOver;
            owner.DragOver += HandleDragOver;
            owner.DragDrop += HandleDrop;
        }
    }

    private void HandleDragOver(object? sender, DragEventArgs e) =>
        e.Effect = WindowsArtifactDragDrop.GetDropEffect(e, includeVideo: false);

    private async void HandleDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var paths = WindowsArtifactDragDrop.GetFiles(e.Data, includeVideo: false);
            var point = new Point(e.X, e.Y);
            if (paths.Length > 0)
            {
                foreach (var path in paths)
                {
                    using var image = await Task.Run(() => WindowsArtifactDragDrop.LoadImage(path));
                    if (_disposed)
                    {
                        return;
                    }
                    _showImage?.Invoke(image, new Rectangle(point, image.Size));
                    point.Offset(24, 24);
                }
            }
            else if (e.Data?.GetData(DataFormats.Bitmap) is Image bitmap)
            {
                using var image = (Bitmap)bitmap.Clone();
                _showImage?.Invoke(image, new Rectangle(point, image.Size));
            }
        }
        catch (Exception exception)
        {
            if (!_disposed && !_owner.IsDisposed)
            {
                MessageBox.Show(_owner, $"无法拖入图片：{exception.Message}", "拖入失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _owner.DragEnter -= HandleDragOver;
        _owner.DragOver -= HandleDragOver;
        _owner.DragDrop -= HandleDrop;
    }
}
