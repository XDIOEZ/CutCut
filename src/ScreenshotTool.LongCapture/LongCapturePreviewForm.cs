using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenshotTool.LongCapture;

/// <summary>
/// Displays a non-activating, topmost preview of the image assembled by a long-capture session.
/// </summary>
/// <remarks>
/// Construct and show this form on the WinForms UI thread by calling <see cref="ShowPreview"/>.
/// After it has been shown, <see cref="UpdatePreviewAsync"/> and
/// <see cref="ClosePreviewAsync"/> may be called from any thread. The caller retains ownership of
/// the composed bitmap and must keep it immutable until <see cref="UpdatePreviewAsync"/> completes.
/// The form owns and disposes only its bounded thumbnail copy.
/// </remarks>
internal sealed class LongCapturePreviewForm : Form
{
    private const int PreferredWidth = 292;
    private const int MinimumWidth = 176;
    private const int MinimumHeight = 280;
    private const int MaximumHeight = 760;
    private const int PlacementGap = 12;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly int _uiThreadId;
    private readonly int _maximumThumbnailWidth;
    private readonly int _maximumThumbnailHeight;
    private readonly Rectangle _selectionScreenBounds;
    private readonly Label _statusLabel;
    private readonly LongCapturePreviewCanvas _previewCanvas;
    private Control? _dragCaptureOwner;
    private Point _dragPointerOffset;
    private long _nextUpdateSequence;
    private long _appliedUpdateSequence;

    public LongCapturePreviewForm(
        Rectangle selectionScreenBounds,
        LongCaptureOptions? options = null)
    {
        if (selectionScreenBounds.Width <= 0 || selectionScreenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionScreenBounds),
                "The long-capture selection must have a positive size.");
        }

        var effectiveOptions = options ?? new LongCaptureOptions();
        if (effectiveOptions.MaximumPreviewWidth <= 0 ||
            effectiveOptions.MaximumPreviewHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The long-capture preview size must be positive.");
        }

        _uiThreadId = Environment.CurrentManagedThreadId;
        _maximumThumbnailWidth = effectiveOptions.MaximumPreviewWidth;
        _maximumThumbnailHeight = effectiveOptions.MaximumPreviewHeight;
        _selectionScreenBounds = selectionScreenBounds;

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(48, 48, 52);
        ControlBox = true;
        DoubleBuffered = true;
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(MinimumWidth, MinimumHeight);
        Padding = new Padding(1);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "长截图预览";
        TopMost = true;

        var header = new Panel
        {
            Name = "PreviewDragHeader",
            BackColor = Color.FromArgb(37, 37, 41),
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(12, 0, 10, 0)
        };
        _statusLabel = new Label
        {
            Name = "PreviewDragStatus",
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(238, 238, 242),
            Text = "长截图预览 · 等待滚动",
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(_statusLabel);
        AttachDragHandle(header);
        AttachDragHandle(_statusLabel);
        header.DoubleClick += (_, _) => ToggleMaximized();
        _statusLabel.DoubleClick += (_, _) => ToggleMaximized();

        var footer = new Panel
        {
            BackColor = Color.FromArgb(37, 37, 41),
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Padding(8, 4, 6, 4)
        };
        var hintLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(190, 190, 198),
            Text = "画面内滚轮缩放  ·  蓝框内滚轮采集  ·  完成后编辑",
            TextAlign = ContentAlignment.MiddleLeft
        };
        var cancelButton = CreateFooterButton("取消", 48);
        cancelButton.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        var finishButton = CreateFooterButton("完成并编辑", 82);
        finishButton.Click += (_, _) => FinishRequested?.Invoke(this, EventArgs.Empty);
        footer.Controls.Add(hintLabel);
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(finishButton);

        _previewCanvas = new LongCapturePreviewCanvas
        {
            BackColor = Color.FromArgb(22, 22, 25),
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        Controls.Add(_previewCanvas);
        Controls.Add(footer);
        Controls.Add(header);
        Bounds = CalculatePlacement(selectionScreenBounds);
    }

    /// <summary>
    /// Raised on the form UI thread when the user clicks the preview's Finish button.
    /// </summary>
    public event EventHandler? FinishRequested;

    /// <summary>
    /// Raised on the form UI thread when the user clicks the preview's Cancel button.
    /// Keyboard Escape handling remains the responsibility of the long-capture input controller.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Gets whether the last-resort preview placement intersects the capture selection. When true,
    /// the capture loop should temporarily hide the preview while copying pixels from the screen.
    /// </summary>
    public bool OverlapsSelection => Bounds.IntersectsWith(_selectionScreenBounds);

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    /// <summary>
    /// Shows the preview without activating it. This method must be called on the thread that
    /// created the form.
    /// </summary>
    public void ShowPreview()
    {
        VerifyUiThread();
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(LongCapturePreviewForm));
        }

        if (!Visible)
        {
            Show();
        }

        SetWindowPos(
            Handle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    /// <summary>
    /// Replaces the displayed preview with a bounded thumbnail of <paramref name="composedImage"/>.
    /// This method may be called from any thread after <see cref="ShowPreview"/>. The source image
    /// remains owned by the caller and must not be mutated or disposed until the returned task
    /// completes.
    /// </summary>
    public ValueTask UpdatePreviewAsync(
        Bitmap composedImage,
        int acceptedFrameCount,
        CancellationToken cancellationToken = default) =>
        UpdatePreviewAsync(
            composedImage,
            acceptedFrameCount,
            composedImage.Size,
            cancellationToken);

    public async ValueTask UpdatePreviewAsync(
        Bitmap composedImage,
        int acceptedFrameCount,
        Size sourceSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(composedImage);
        if (acceptedFrameCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedFrameCount));
        }
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSize));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (IsDisposed || Disposing)
        {
            throw new ObjectDisposedException(nameof(LongCapturePreviewForm));
        }

        var sequence = Interlocked.Increment(ref _nextUpdateSequence);
        var thumbnail = await Task.Run(
            () => CreateThumbnail(composedImage, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var ownershipTransferred = false;
        try
        {
            ownershipTransferred = await InvokeOnUiThreadAsync(
                () =>
                {
                    if (sequence <= _appliedUpdateSequence)
                    {
                        thumbnail.Dispose();
                        return;
                    }

                    _appliedUpdateSequence = sequence;
                    _previewCanvas.SetImage(thumbnail);
                    _statusLabel.Text =
                        $"长截图预览 · {acceptedFrameCount} 段 · {sourceSize.Width} × {sourceSize.Height}";
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                thumbnail.Dispose();
            }
        }
    }

    /// <summary>
    /// Closes the form on its owning UI thread. It is safe to call this method from any thread.
    /// </summary>
    public async ValueTask ClosePreviewAsync()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        await InvokeOnUiThreadAsync(Close, CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask UpdateStatusAsync(
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (IsDisposed || Disposing)
        {
            return;
        }

        await InvokeOnUiThreadAsync(
            () => _statusLabel.Text = status,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Temporarily hides or restores the preview from any thread. Restoring never activates the
    /// window. This is intended for capture loops that must avoid recording an overlapping preview.
    /// </summary>
    public async ValueTask SetPreviewVisibleAsync(
        bool visible,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        await InvokeOnUiThreadAsync(
            () =>
            {
                if (visible)
                {
                    ShowPreview();
                }
                else
                {
                    Hide();
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borderPen = new Pen(Color.FromArgb(92, 92, 101));
        e.Graphics.DrawRectangle(
            borderPen,
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1));
    }

    internal static Point CalculateDraggedLocation(
        Point pointerScreenPosition,
        Point pointerOffset,
        Size windowSize,
        Rectangle workArea)
    {
        if (windowSize.Width <= 0 || windowSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        }
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea));
        }

        var maximumX = Math.Max(workArea.Left, workArea.Right - windowSize.Width);
        var maximumY = Math.Max(workArea.Top, workArea.Bottom - windowSize.Height);
        return new Point(
            Math.Clamp(pointerScreenPosition.X - pointerOffset.X, workArea.Left, maximumX),
            Math.Clamp(pointerScreenPosition.Y - pointerOffset.Y, workArea.Top, maximumY));
    }

    private static Rectangle CalculatePlacement(Rectangle selectionBounds)
    {
        var selectionCenter = new Point(
            selectionBounds.Left + selectionBounds.Width / 2,
            selectionBounds.Top + selectionBounds.Height / 2);
        var workArea = Screen.FromPoint(selectionCenter).WorkingArea;
        var height = Math.Min(
            workArea.Height,
            Math.Clamp(selectionBounds.Height, MinimumHeight, MaximumHeight));
        var rightSpace = workArea.Right - selectionBounds.Right - PlacementGap;
        var leftSpace = selectionBounds.Left - workArea.Left - PlacementGap;

        var placeOnRight = rightSpace >= PreferredWidth;
        var placeOnLeft = !placeOnRight && leftSpace >= MinimumWidth;
        if (!placeOnRight && !placeOnLeft && rightSpace >= MinimumWidth)
        {
            placeOnRight = true;
        }

        int width;
        int x;
        if (placeOnRight)
        {
            width = Math.Min(PreferredWidth, rightSpace);
            x = selectionBounds.Right + PlacementGap;
        }
        else if (placeOnLeft)
        {
            width = Math.Min(PreferredWidth, leftSpace);
            x = selectionBounds.Left - PlacementGap - width;
        }
        else
        {
            var alternative = FindNonOverlappingPlacement(
                selectionBounds,
                workArea,
                height);
            if (alternative is not null)
            {
                return alternative.Value;
            }

            // The selection occupies nearly the whole monitor. Keep the preview usable as a
            // separate topmost window, even though this last-resort placement overlaps its edge.
            width = Math.Min(PreferredWidth, workArea.Width);
            width = Math.Max(Math.Min(MinimumWidth, workArea.Width), width);
            x = workArea.Right - width;
        }

        var maximumTop = Math.Max(workArea.Top, workArea.Bottom - height);
        var y = Math.Clamp(selectionBounds.Top, workArea.Top, maximumTop);
        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        return new Rectangle(x, y, width, height);
    }

    private static Rectangle? FindNonOverlappingPlacement(
        Rectangle selectionBounds,
        Rectangle primaryWorkArea,
        int preferredHeight)
    {
        var width = Math.Min(PreferredWidth, primaryWorkArea.Width);
        var spaceAbove = selectionBounds.Top - PlacementGap - primaryWorkArea.Top;
        if (spaceAbove >= MinimumHeight)
        {
            var height = Math.Min(preferredHeight, spaceAbove);
            var x = Math.Clamp(
                selectionBounds.Left,
                primaryWorkArea.Left,
                Math.Max(primaryWorkArea.Left, primaryWorkArea.Right - width));
            return new Rectangle(x, selectionBounds.Top - PlacementGap - height, width, height);
        }

        var spaceBelow = primaryWorkArea.Bottom - selectionBounds.Bottom - PlacementGap;
        if (spaceBelow >= MinimumHeight)
        {
            var height = Math.Min(preferredHeight, spaceBelow);
            var x = Math.Clamp(
                selectionBounds.Left,
                primaryWorkArea.Left,
                Math.Max(primaryWorkArea.Left, primaryWorkArea.Right - width));
            return new Rectangle(x, selectionBounds.Bottom + PlacementGap, width, height);
        }

        var selectionCenter = new Point(
            selectionBounds.Left + selectionBounds.Width / 2,
            selectionBounds.Top + selectionBounds.Height / 2);
        foreach (var screen in Screen.AllScreens
                     .OrderBy(candidate => DistanceSquared(candidate.WorkingArea, selectionCenter)))
        {
            var area = screen.WorkingArea;
            var candidateWidth = Math.Min(PreferredWidth, area.Width);
            var candidateHeight = Math.Min(preferredHeight, area.Height);
            var candidate = new Rectangle(
                area.Left,
                Math.Clamp(
                    selectionBounds.Top,
                    area.Top,
                    Math.Max(area.Top, area.Bottom - candidateHeight)),
                candidateWidth,
                candidateHeight);
            if (!candidate.IntersectsWith(selectionBounds))
            {
                return candidate;
            }
        }

        return null;
    }

    private static long DistanceSquared(Rectangle area, Point point)
    {
        var closestX = Math.Clamp(point.X, area.Left, area.Right);
        var closestY = Math.Clamp(point.Y, area.Top, area.Bottom);
        var deltaX = (long)closestX - point.X;
        var deltaY = (long)closestY - point.Y;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static Button CreateFooterButton(string text, int width) => new()
    {
        AutoSize = false,
        BackColor = Color.FromArgb(56, 56, 62),
        Dock = DockStyle.Right,
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.FromArgb(232, 232, 236),
        Margin = Padding.Empty,
        TabStop = false,
        Text = text,
        Width = width
    };

    private void AttachDragHandle(Control control)
    {
        control.Cursor = Cursors.SizeAll;
        control.MouseDown += HandleDragMouseDown;
        control.MouseMove += HandleDragMouseMove;
        control.MouseUp += HandleDragMouseUp;
        control.MouseCaptureChanged += HandleDragMouseCaptureChanged;
    }

    private void HandleDragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || sender is not Control control)
        {
            return;
        }

        EndWindowDrag();
        var pointer = control.PointToScreen(e.Location);
        _dragPointerOffset = new Point(pointer.X - Left, pointer.Y - Top);
        _dragCaptureOwner = control;
        control.Capture = true;
    }

    private void HandleDragMouseMove(object? sender, MouseEventArgs e)
    {
        if (!ReferenceEquals(sender, _dragCaptureOwner) || sender is not Control control)
        {
            return;
        }
        if (e.Button != MouseButtons.Left)
        {
            EndWindowDrag();
            return;
        }

        var pointer = control.PointToScreen(e.Location);
        Location = CalculateDraggedLocation(
            pointer,
            _dragPointerOffset,
            Size,
            Screen.FromPoint(pointer).WorkingArea);
    }

    private void HandleDragMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ReferenceEquals(sender, _dragCaptureOwner))
        {
            EndWindowDrag();
        }
    }

    private void HandleDragMouseCaptureChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _dragCaptureOwner) &&
            sender is Control { Capture: false })
        {
            _dragCaptureOwner = null;
        }
    }

    private void EndWindowDrag()
    {
        var owner = _dragCaptureOwner;
        _dragCaptureOwner = null;
        if (owner is not null)
        {
            owner.Capture = false;
        }
    }

    private void ToggleMaximized() => WindowState = WindowState == FormWindowState.Maximized
        ? FormWindowState.Normal
        : FormWindowState.Maximized;

    private Bitmap CreateThumbnail(Bitmap source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scale = Math.Min(
            1d,
            Math.Min(
                (double)_maximumThumbnailWidth / source.Width,
                (double)_maximumThumbnailHeight / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var thumbnail = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(thumbnail);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(Point.Empty, thumbnail.Size),
                new Rectangle(Point.Empty, source.Size),
                GraphicsUnit.Pixel);
            cancellationToken.ThrowIfCancellationRequested();
            return thumbnail;
        }
        catch
        {
            thumbnail.Dispose();
            throw;
        }
    }

    private Task<bool> InvokeOnUiThreadAsync(
        Action action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsDisposed || Disposing)
        {
            return Task.FromResult(false);
        }

        if (!InvokeRequired)
        {
            action();
            return Task.FromResult(true);
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsDisposed || Disposing)
                    {
                        completion.TrySetResult(false);
                        return;
                    }

                    action();
                    completion.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }));
        }
        catch (InvalidOperationException) when (IsDisposed || Disposing)
        {
            completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private void VerifyUiThread()
    {
        if (_uiThreadId != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException(
                "LongCapturePreviewForm must be shown on the UI thread that created it.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    private sealed class LongCapturePreviewCanvas : Control
    {
        private Bitmap? _image;
        private double _zoom = 1D;
        private PointF _panOffset;
        private Point? _panStart;
        private PointF _panOrigin;

        public LongCapturePreviewCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Cursor = Cursors.Hand;
        }

        public void SetImage(Bitmap image)
        {
            ArgumentNullException.ThrowIfNull(image);
            var previous = _image;
            _image = image;
            previous?.Dispose();
            _panOffset = previous is null
                ? PointF.Empty
                : LongCapturePreviewLayout.ClampPan(
                    ClientSize,
                    image.Size,
                    _zoom,
                    _panOffset);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_image is null)
            {
                return;
            }

            var previousZoom = _zoom;
            var nextZoom = Math.Clamp(
                previousZoom * (e.Delta > 0 ? 1.2D : 1D / 1.2D),
                1D,
                8D);
            if (Math.Abs(previousZoom - nextZoom) < 0.001D)
            {
                return;
            }

            _panOffset = LongCapturePreviewLayout.ZoomAt(
                ClientSize,
                _image.Size,
                previousZoom,
                nextZoom,
                _panOffset,
                e.Location);
            _zoom = nextZoom;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left ||
                _image is null ||
                !LongCapturePreviewLayout.CanPan(ClientSize, _image.Size, _zoom) ||
                !LongCapturePreviewLayout.GetImageBounds(
                    ClientSize,
                    _image.Size,
                    _zoom,
                    _panOffset).Contains(e.Location))
            {
                return;
            }

            _panStart = e.Location;
            _panOrigin = _panOffset;
            Capture = true;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_panStart is not { } start || e.Button != MouseButtons.Left)
            {
                return;
            }

            var requested = new PointF(
                _panOrigin.X + e.X - start.X,
                _panOrigin.Y + e.Y - start.Y);
            if (_image is not null)
            {
                _panOffset = LongCapturePreviewLayout.ClampPan(
                    ClientSize,
                    _image.Size,
                    _zoom,
                    requested);
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _panStart = null;
                Capture = false;
                Cursor = Cursors.Hand;
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture)
            {
                _panStart = null;
                Cursor = Cursors.Hand;
            }
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (_image is not null)
            {
                _panOffset = LongCapturePreviewLayout.ClampPan(
                    ClientSize,
                    _image.Size,
                    _zoom,
                    _panOffset);
            }
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_image is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                DrawWaitingText(e.Graphics);
                return;
            }

            var bounds = Rectangle.Round(LongCapturePreviewLayout.GetImageBounds(
                ClientSize,
                _image.Size,
                _zoom,
                _panOffset));

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(_image, bounds);
            using var borderPen = new Pen(Color.FromArgb(120, 120, 128));
            e.Graphics.DrawRectangle(
                borderPen,
                bounds.X,
                bounds.Y,
                Math.Max(0, bounds.Width - 1),
                Math.Max(0, bounds.Height - 1));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _image?.Dispose();
                _image = null;
            }

            base.Dispose(disposing);
        }

        private void DrawWaitingText(Graphics graphics)
        {
            const string text = "向上或向下滚动后将在这里显示拼接预览";
            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                ClientRectangle,
                Color.FromArgb(142, 142, 150),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }
}
