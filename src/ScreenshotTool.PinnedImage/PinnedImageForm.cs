using System.Drawing.Drawing2D;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.PinnedImage;

internal interface IPinnedImageWindow : IDisposable
{
    event EventHandler? WindowClosed;

    void Show();

    void Close();
}

internal interface IPinnedImageWindowFactory
{
    IPinnedImageWindow Create(
        Bitmap image,
        Rectangle suggestedBounds,
        IModuleImageHost imageHost);
}

internal sealed class PinnedImageWindowFactory : IPinnedImageWindowFactory
{
    public IPinnedImageWindow Create(
        Bitmap image,
        Rectangle suggestedBounds,
        IModuleImageHost imageHost) =>
        new PinnedImageForm(image, suggestedBounds, imageHost);
}

internal sealed class PinnedImageForm : Form, IPinnedImageWindow
{
    private readonly Bitmap _image;
    private readonly IModuleImageHost _imageHost;
    private readonly ContextMenuStrip _menu;
    private readonly PinnedImageTextController _textController;
    private PinnedImageResizeEdges _resizeEdges;
    private Point _pointerOrigin;
    private Rectangle _boundsOrigin;
    private bool _dragging;
    private bool _resourcesDisposed;

    // Creates the floating image window and composes optional text interaction.
    public PinnedImageForm(
        Bitmap image,
        Rectangle suggestedBounds,
        IModuleImageHost imageHost)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageHost);
        _image = image;
        _imageHost = imageHost;

        Text = "轻截贴图";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = PinnedImageWindowLayout.MinimumSize;
        BackColor = Color.Black;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        var workingArea = Screen.FromRectangle(suggestedBounds).WorkingArea;
        Bounds = PinnedImageWindowLayout.CreateInitialBounds(
            image.Size,
            suggestedBounds,
            workingArea);

        _menu = new ContextMenuStrip();
        var delete = _menu.Items.Add("删除", null, (_, _) => Close());
        delete.Name = "DeletePinnedImageMenuItem";
        var copy = _menu.Items.Add("复制", null, (_, _) => ExecuteImageAction(
            () => _imageHost.CopyImage(_image),
            "复制贴图失败"));
        copy.Name = "CopyPinnedImageMenuItem";
        var save = _menu.Items.Add("保存", null, (_, _) => ExecuteImageAction(
            () => _imageHost.SaveImage(_image),
            "保存贴图失败"));
        save.Name = "SavePinnedImageMenuItem";
        var edit = _menu.Items.Add("编辑", null, (_, _) => EditImage());
        edit.Name = "EditPinnedImageMenuItem";
        ContextMenuStrip = _menu;
        _textController = new PinnedImageTextController(this, _image, _imageHost, _menu);
    }

    public event EventHandler? WindowClosed;

    internal Bitmap SourceImage => _image;

    // Draws the source image followed by transient text selection feedback.
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(
            _image,
            ClientRectangle,
            new Rectangle(Point.Empty, _image.Size),
            GraphicsUnit.Pixel);
        _textController.Draw(e.Graphics);
    }

    // Routes text gestures before starting image movement or resizing.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (_textController.HandleMouseDown(e, ModifierKeys))
        {
            return;
        }
        if (e.Button != MouseButtons.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        _resizeEdges = PinnedImageWindowLayout.HitTestEdges(
            ClientSize,
            e.Location,
            GetGripSize());
        _pointerOrigin = Cursor.Position;
        _boundsOrigin = Bounds;
        _dragging = true;
        Capture = true;
        Cursor = PinnedImageWindowLayout.GetCursor(_resizeEdges);
    }

    // Updates the active gesture and shows the cursor for the current mode.
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_textController.HandleMouseMove(e))
        {
            return;
        }
        if (_dragging)
        {
            var pointer = Cursor.Position;
            var offset = new Point(
                pointer.X - _pointerOrigin.X,
                pointer.Y - _pointerOrigin.Y);
            Bounds = _resizeEdges == PinnedImageResizeEdges.None
                ? PinnedImageWindowLayout.Move(_boundsOrigin, offset)
                : PinnedImageWindowLayout.Resize(
                    _boundsOrigin,
                    _resizeEdges,
                    offset,
                    preserveAspectRatio: !IsShiftPressed());
            Cursor = PinnedImageWindowLayout.GetCursor(_resizeEdges);
            return;
        }

        if (_textController.IsTextMode && (ModifierKeys & Keys.Alt) == 0)
        {
            Cursor = Cursors.IBeam;
            base.OnMouseMove(e);
            return;
        }

        var edges = PinnedImageWindowLayout.HitTestEdges(
            ClientSize,
            e.Location,
            GetGripSize());
        Cursor = PinnedImageWindowLayout.GetCursor(edges);
        base.OnMouseMove(e);
    }

    // Completes the active text or image gesture and releases pointer capture.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_textController.HandleMouseUp(e))
        {
            return;
        }
        if (e.Button == MouseButtons.Left && _dragging)
        {
            _dragging = false;
            Capture = false;
            _resizeEdges = PinnedImageResizeEdges.None;
            var edges = PinnedImageWindowLayout.HitTestEdges(
                ClientSize,
                e.Location,
                GetGripSize());
            Cursor = PinnedImageWindowLayout.GetCursor(edges);
            return;
        }
        base.OnMouseUp(e);
    }

    // Clears gesture state when another window acquires pointer capture.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
        {
            _textController.ReleaseCapture();
            _dragging = false;
            _resizeEdges = PinnedImageResizeEdges.None;
        }
        base.OnMouseCaptureChanged(e);
    }

    // Cancels text work before notifying the module that its pin has closed.
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _textController.Dispose();
        base.OnFormClosed(e);
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    // Releases optional text interaction before the menu and source image.
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _textController.Dispose();
            _menu.Dispose();
            _image.Dispose();
        }
        base.Dispose(disposing);
    }

    // Gives text mode standard copy/select-all shortcuts before the window processes other keys.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        _textController.HandleKey(keyData) || base.ProcessCmdKey(ref msg, keyData);

    private int GetGripSize() => Math.Max(5, DeviceDpi * 6 / 96);

    private void EditImage()
    {
        Hide();
        try
        {
            _imageHost.EditImage(_image);
            Close();
        }
        catch (Exception exception)
        {
            Show();
            MessageBox.Show(
                this,
                exception.Message,
                "编辑贴图失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ExecuteImageAction(Action action, string title)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static bool IsShiftPressed() =>
        (ModifierKeys & Keys.Shift) == Keys.Shift;
}
