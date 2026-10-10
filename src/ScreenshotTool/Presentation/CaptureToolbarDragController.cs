namespace ScreenshotTool.Presentation;

internal sealed class CaptureToolbarDragController : IDisposable
{
    private readonly Control _surface;
    private Control _target;
    private Control? _dragSource;
    private Point _pointerStart;
    private Point _targetStart;
    private Point? _manualLocation;

    public CaptureToolbarDragController(Control surface)
    {
        _surface = surface;
        _target = surface;
        Attach(surface);
        foreach (Control child in surface.Controls)
        {
            Attach(child);
        }
        surface.ControlAdded += HandleControlAdded;
        surface.ControlRemoved += HandleControlRemoved;
    }

    public bool IsDragging => _dragSource is not null;

    public void SetTarget(Control target)
    {
        EndDrag();
        _target = target;
        _manualLocation = null;
    }

    public void Position(Point suggestedLocation)
    {
        _target.Location = _manualLocation is { } manual
            ? ConstrainLocation(manual)
            : suggestedLocation;
        if (_manualLocation.HasValue)
        {
            _manualLocation = _target.Location;
        }
    }

    public static Point ClampLocation(Point location, Size size, Rectangle bounds) => new(
        Math.Clamp(location.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - size.Width)),
        Math.Clamp(location.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - size.Height)));

    public void Dispose()
    {
        EndDrag();
        _surface.ControlAdded -= HandleControlAdded;
        _surface.ControlRemoved -= HandleControlRemoved;
        Detach(_surface);
        foreach (Control child in _surface.Controls)
        {
            Detach(child);
        }
    }

    private void HandleControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is { } control)
        {
            Attach(control);
        }
    }

    private void HandleControlRemoved(object? sender, ControlEventArgs e)
    {
        if (e.Control is { } control)
        {
            if (ReferenceEquals(control, _dragSource))
            {
                EndDrag();
            }
            Detach(control);
        }
    }

    private void Attach(Control control)
    {
        // Buttons and command progress keep their own input semantics.
        if (control != _surface && control is not Label && control is not Panel)
        {
            return;
        }
        control.Cursor = Cursors.SizeAll;
        control.MouseDown += HandleMouseDown;
        control.MouseMove += HandleMouseMove;
        control.MouseUp += HandleMouseUp;
        control.MouseCaptureChanged += HandleMouseCaptureChanged;
        control.VisibleChanged += HandleVisibleChanged;
    }

    private void Detach(Control control)
    {
        control.MouseDown -= HandleMouseDown;
        control.MouseMove -= HandleMouseMove;
        control.MouseUp -= HandleMouseUp;
        control.MouseCaptureChanged -= HandleMouseCaptureChanged;
        control.VisibleChanged -= HandleVisibleChanged;
    }

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || sender is not Control source)
        {
            return;
        }
        _pointerStart = source.PointToScreen(e.Location);
        _targetStart = _target.Location;
        _dragSource = source;
        source.Capture = true;
    }

    private void HandleMouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragSource is not { } source)
        {
            return;
        }
        var pointer = source.PointToScreen(e.Location);
        var delta = new Size(pointer.X - _pointerStart.X, pointer.Y - _pointerStart.Y);
        if (delta.IsEmpty)
        {
            return;
        }
        _manualLocation = ConstrainLocation(_targetStart + delta);
        _target.Location = _manualLocation.Value;
    }

    private void HandleMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            EndDrag();
        }
    }

    private void HandleMouseCaptureChanged(object? sender, EventArgs e)
    {
        if (_dragSource is { Capture: false })
        {
            EndDrag();
        }
    }

    private void HandleVisibleChanged(object? sender, EventArgs e)
    {
        if (sender is Control { Visible: false })
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        var source = _dragSource;
        _dragSource = null;
        if (source is { IsDisposed: false, Capture: true })
        {
            source.Capture = false;
        }
    }

    private Point ConstrainLocation(Point location)
    {
        var parent = _target.Parent;
        var screenLocation = parent is null ? location : parent.PointToScreen(location);
        var bounds = Screen.FromRectangle(new Rectangle(screenLocation, _target.Size)).WorkingArea;
        if (parent is not null)
        {
            bounds.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        }
        var clamped = ClampLocation(screenLocation, _target.Size, bounds);
        return parent is null ? clamped : parent.PointToClient(clamped);
    }
}
