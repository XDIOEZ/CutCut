namespace ScreenshotTool.Presentation;

// Moves capture UI away without hiding it, changing editor state, or ending its session.
internal sealed class CaptureWindowParkingLease : IDisposable
{
    private readonly List<(Form Window, Rectangle Bounds)> _windows = [];
    private readonly Action _restored;
    private bool _disposed;

    public CaptureWindowParkingLease(IEnumerable<Form> windows, Action restored)
    {
        _restored = restored;
        var parkingBounds = CaptureOverlayForm.GetLiveCaptureParkingBounds(
            SystemInformation.VirtualScreen);
        try
        {
            foreach (var window in windows.Where(window => !window.IsDisposed && window.Visible))
            {
                _windows.Add((window, window.Bounds));
                window.Location = parkingBounds.Location;
                if (!Rectangle.Intersect(window.Bounds, SystemInformation.VirtualScreen).IsEmpty)
                {
                    throw new InvalidOperationException("无法临时移开截图界面，已取消本次采集。");
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        List<Exception>? failures = null;
        foreach (var (window, bounds) in _windows.AsEnumerable().Reverse())
        {
            try
            {
                if (!window.IsDisposed)
                {
                    window.Bounds = bounds;
                }
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }
        try
        {
            _restored();
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }
        if (failures is not null)
        {
            throw new AggregateException("恢复截图界面失败。", failures);
        }
    }
}
