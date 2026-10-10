using System.Runtime.InteropServices;
using ScreenshotTool.Core;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureSessionCoordinator : IDisposable
{
    private readonly Form _mainWindow;
    private readonly Func<int> _getMaximumSessions;
    private readonly Func<bool> _shouldHideMainWindow;
    private readonly Func<bool> _isExiting;
    private readonly List<CaptureOverlayForm> _sessions = [];
    private CaptureOverlayForm? _activeSession;
    private bool _preparing;
    private bool _mainWindowHidden;
    private bool _captureProtectionApplied;
    private double _originalOpacity;
    private FormWindowState _originalWindowState;
    private bool _disposed;

    public CaptureSessionCoordinator(
        Form mainWindow,
        Func<int> getMaximumSessions,
        Func<bool> shouldHideMainWindow,
        Func<bool> isExiting)
    {
        _mainWindow = mainWindow;
        _getMaximumSessions = getMaximumSessions;
        _shouldHideMainWindow = shouldHideMainWindow;
        _isExiting = isExiting;
    }

    public int Count => _sessions.Count;

    public int MaximumSessions => CaptureSessionLimitPolicy.Normalize(_getMaximumSessions());

    public IDisposable? TryBeginPreparation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_preparing || Count >= MaximumSessions)
        {
            return null;
        }

        _preparing = true;
        try
        {
            if (Count == 0)
            {
                HideMainWindowForCapture();
            }
            UpdateBackgroundInput();
            return new ActionLease(FinishPreparation);
        }
        catch
        {
            FinishPreparation();
            throw;
        }
    }

    public void Register(CaptureOverlayForm overlay)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sessions.Contains(overlay))
        {
            return;
        }

        _sessions.Add(overlay);
        _activeSession = overlay;
        overlay.ParkOtherCaptureSessionsForSnapshot = () => ParkForSnapshot(overlay);
        overlay.IsPointerOverOtherCaptureSession = point => _sessions.Any(other =>
            !ReferenceEquals(other, overlay) &&
            !other.IsDisposed && other.ContainsInteractiveScreenPoint(point));
        overlay.SessionActivated += HandleSessionActivated;
        overlay.FormClosed += HandleSessionClosed;
        overlay.Disposed += HandleSessionDisposed;
        UpdateBackgroundInput();
    }

    public IDisposable ParkForSnapshot(CaptureOverlayForm? exceptOverlay = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var leases = new List<IDisposable>();
        try
        {
            foreach (var overlay in _sessions.ToArray())
            {
                if (!ReferenceEquals(overlay, exceptOverlay) && !overlay.IsDisposed)
                {
                    leases.Add(overlay.ParkForSnapshot());
                }
            }
        }
        catch
        {
            ReleaseLeases(leases);
            throw;
        }

        return new ActionLease(() => ReleaseLeases(leases));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var overlay in _sessions.ToArray())
        {
            Unregister(overlay);
            overlay.Close();
            overlay.Dispose();
        }
        RestoreMainWindow();
    }

    private void HandleSessionActivated(object? sender, EventArgs e)
    {
        if (sender is not CaptureOverlayForm overlay || !_sessions.Remove(overlay))
        {
            return;
        }

        // Keep the most recently activated session last so closing it returns
        // background input to the previous independent screenshot or recorder.
        _sessions.Add(overlay);
        _activeSession = overlay;
        UpdateBackgroundInput();
    }

    private void HandleSessionClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is CaptureOverlayForm overlay)
        {
            Unregister(overlay);
        }
    }

    private void HandleSessionDisposed(object? sender, EventArgs e)
    {
        if (sender is CaptureOverlayForm overlay)
        {
            Unregister(overlay);
        }
    }

    private void Unregister(CaptureOverlayForm overlay)
    {
        overlay.SessionActivated -= HandleSessionActivated;
        overlay.FormClosed -= HandleSessionClosed;
        overlay.Disposed -= HandleSessionDisposed;
        overlay.ParkOtherCaptureSessionsForSnapshot = null;
        overlay.IsPointerOverOtherCaptureSession = null;
        if (!_sessions.Remove(overlay))
        {
            return;
        }

        if (ReferenceEquals(_activeSession, overlay))
        {
            _activeSession = _sessions.LastOrDefault();
        }
        if (!_disposed)
        {
            UpdateBackgroundInput();
            if (Count == 0 && !_preparing)
            {
                RestoreMainWindow();
            }
        }
    }

    private void FinishPreparation()
    {
        _preparing = false;
        if (_disposed)
        {
            return;
        }

        UpdateBackgroundInput();
        if (Count == 0)
        {
            RestoreMainWindow();
        }
    }

    private void UpdateBackgroundInput()
    {
        // Release the old global shortcut before the new owner tries to register it.
        foreach (var overlay in _sessions)
        {
            if (!overlay.IsDisposed &&
                (_preparing || !ReferenceEquals(overlay, _activeSession)))
            {
                overlay.SetBackgroundInputEnabled(false);
            }
        }
        if (!_preparing && _activeSession is { IsDisposed: false })
        {
            _activeSession.SetBackgroundInputEnabled(true);
        }
    }

    private void HideMainWindowForCapture()
    {
        if (_mainWindowHidden || !MainWindowCaptureVisibilityPolicy.ShouldHide(
                _shouldHideMainWindow(), _mainWindow.Visible, _mainWindow.WindowState))
        {
            return;
        }

        _originalOpacity = _mainWindow.Opacity;
        _originalWindowState = _mainWindow.WindowState;
        _mainWindowHidden = true;
        _captureProtectionApplied = WindowCaptureProtection.TryExclude(_mainWindow);
        DwmFlush();
        _mainWindow.Opacity = 0D;
        DwmFlush();
        _captureProtectionApplied =
            WindowCaptureProtection.TryExclude(_mainWindow) || _captureProtectionApplied;
        _mainWindow.Hide();
        DwmFlush();
    }

    private void RestoreMainWindow()
    {
        if (!_mainWindowHidden)
        {
            return;
        }

        _mainWindowHidden = false;
        if (!_mainWindow.IsDisposed)
        {
            _mainWindow.Opacity = _originalOpacity;
            if (!_isExiting())
            {
                _mainWindow.Show();
                _mainWindow.WindowState = _originalWindowState;
                _mainWindow.Activate();
            }
            if (_captureProtectionApplied)
            {
                WindowCaptureProtection.TryAllow(_mainWindow);
            }
        }
        _captureProtectionApplied = false;
    }

    private static void ReleaseLeases(List<IDisposable> leases)
    {
        List<Exception>? failures = null;
        for (var index = leases.Count - 1; index >= 0; index--)
        {
            try
            {
                leases[index].Dispose();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }
        if (failures is not null)
        {
            throw new AggregateException("恢复截图窗口失败。", failures);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    private sealed class ActionLease(Action release) : IDisposable
    {
        private Action? _release = release;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
