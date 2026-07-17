using System.Diagnostics;
using System.Drawing.Drawing2D;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureOverlayForm : Form, ILiveCaptureFeatureHost
{
    private readonly DesktopSnapshot _snapshot;
    private readonly Bitmap _dimmedImage;
    private readonly IImageSaveService _imageSaveService;
    private readonly IClipboardService _clipboardService;
    private readonly IWindowLocator _windowLocator;
    private readonly CaptureFeatureSession _featureSession;
    private readonly ISelectionMoveAnnotationStrategy _selectionMoveAnnotationStrategy;
    private readonly ToolWidthController _toolWidthController;
    private readonly IReadOnlyDictionary<string, bool> _featurePreferences;
    private readonly SelectionRedrawGuard _selectionRedrawGuard = new();
    private readonly CaptureAnnotationEditor _annotationEditor;
    private AnnotationSelection _annotationSelection => _annotationEditor.Selection;
    private readonly string _outputFolder;
    private AnnotationDocument _document => _annotationEditor.Document;
    private readonly FlowLayoutPanel _toolbar;
    private readonly Dictionary<EditorTool, Button> _toolButtons = [];
    private readonly List<Button> _colorButtons = [];
    private readonly Button _widthButton;
    private readonly ToolTip _toolTip = new();
    private readonly Stopwatch _hoverLookupClock = Stopwatch.StartNew();

    private Rectangle _selection;
    private Rectangle _hoverWindowSelection;
    private Point _startPoint;
    private Point _currentPoint;
    private List<Point> _draftPoints = [];
    private EditorTool _tool;
    private Color _color = Color.FromArgb(239, 68, 68);
    private bool _hasSelection;
    private bool _isSelecting;
    private bool _isPendingWindowSelection;
    private SelectionResizeEdges _activeResizeEdges;
    private Rectangle _resizeOriginSelection;
    private bool _isMovingSelection;
    private bool _selectionMoveDidDrag;
    private Point _moveOriginPointer;
    private Rectangle _moveOriginSelection;
    private bool _isPendingAnnotationMarquee;
    private bool _isSelectingAnnotations;
    private Point _annotationMarqueeStart;
    private Rectangle _annotationMarqueeBounds;
    private StickerHitTarget _activeMovableTarget;
    private Point _movableDragOrigin;
    private Dictionary<MovableAnnotation, Rectangle> _movableOriginBounds = [];
    private Rectangle _movableGroupOriginBounds;
    private bool _isDrawing;
    private TransparentTextEditorControl? _textEditor;
    private Bitmap? _replacementCaptureImage;
    private double _replacementZoom = 1D;
    private Point _replacementScroll;
    private bool _isPanningReplacement;
    private Point _replacementPanPointerOrigin;
    private Point _replacementPanScrollOrigin;
    private bool _isMovingReplacementFrame;
    private Point _replacementFramePointerOrigin;
    private Rectangle _replacementFrameOrigin;
    private bool _featureCommandRunning;
    private bool _liveCaptureOverlayParked;
    private bool _finished;

    public CaptureOverlayForm(
        DesktopSnapshot snapshot,
        IImageSaveService imageSaveService,
        IClipboardService clipboardService,
        IWindowLocator windowLocator,
        ICaptureFeatureCatalog featureCatalog,
        ISelectionMoveAnnotationStrategy selectionMoveAnnotationStrategy,
        ToolWidthController toolWidthController,
        IReadOnlyDictionary<string, bool> featurePreferences,
        string outputFolder,
        DrawingToolCoefficients? drawingToolCoefficients = null)
    {
        _snapshot = snapshot;
        _dimmedImage = CreateDimmedImage(snapshot.Image);
        _imageSaveService = imageSaveService;
        _clipboardService = clipboardService;
        _windowLocator = windowLocator;
        _selectionMoveAnnotationStrategy = selectionMoveAnnotationStrategy;
        _toolWidthController = toolWidthController;
        _annotationEditor = new CaptureAnnotationEditor(drawingToolCoefficients);
        _featurePreferences = featurePreferences.ToDictionary(
            preference => preference.Key,
            preference => preference.Value,
            StringComparer.Ordinal);
        _outputFolder = outputFolder;

        Text = "轻截 - 选择截图区域";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = snapshot.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        Cursor = Cursors.Cross;
        BackColor = Color.Black;

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        _featureSession = new CaptureFeatureSession(featureCatalog, this);
        _toolbar = BuildToolbar();
        Controls.Add(_toolbar);
        _toolbar.Visible = false;
        _widthButton = (Button)_toolbar.Controls.Find("WidthButton", false)[0];

        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;
        MouseDown += HandleMouseDown;
        MouseMove += HandleMouseMove;
        MouseUp += HandleMouseUp;
        MouseWheel += HandleEditorMouseWheel;
        Deactivate += (_, _) => CancelTextEditor(commit: true);
    }

    public event EventHandler<string>? ScreenshotSaved;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
        UpdateHoverWindowSelection(PointToClient(Cursor.Position), force: true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // The dimmed desktop never changes, so redraw only the invalid region instead of
        // blending a full virtual-desktop-sized mask on every mouse move.
        DrawDesktopLayers(e.Graphics);

        var displaySelection = GetDisplaySelection();
        if (displaySelection.IsEmpty)
        {
            if (!_hasSelection && !_isSelecting)
            {
                DrawScreenHints(e.Graphics);
            }
            return;
        }

        if (_hasSelection)
        {
            var savedState = e.Graphics.Save();
            e.Graphics.SetClip(displaySelection, CombineMode.Intersect);
            if (_replacementCaptureImage is not null)
            {
                using var viewportBackground = new SolidBrush(Color.FromArgb(15, 23, 42));
                e.Graphics.FillRectangle(viewportBackground, displaySelection);
                e.Graphics.Transform = new Matrix(
                    (float)_replacementZoom,
                    0F,
                    0F,
                    (float)_replacementZoom,
                    displaySelection.X - _replacementScroll.X,
                    displaySelection.Y - _replacementScroll.Y);
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                e.Graphics.DrawImageUnscaled(_replacementCaptureImage, Point.Empty);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                _annotationEditor.Render(e.Graphics, _replacementCaptureImage);
                RenderDraft(e.Graphics);
                DrawMovableSelection(e.Graphics);
                DrawAnnotationMarquee(e.Graphics);
            }
            else
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                _document.Render(e.Graphics, _snapshot.Image);
                _featureSession.Render(e.Graphics, CaptureRenderTarget.Preview);
                RenderDraft(e.Graphics);
                DrawMovableSelection(e.Graphics);
                DrawAnnotationMarquee(e.Graphics);
            }
            e.Graphics.Restore(savedState);
        }

        using var border = new Pen(Color.FromArgb(56, 189, 248), 2F);
        e.Graphics.DrawRectangle(border, displaySelection.X, displaySelection.Y,
            Math.Max(1, displaySelection.Width - 1), Math.Max(1, displaySelection.Height - 1));
        DrawSizeBadge(e.Graphics, displaySelection);
    }

    private void DrawDesktopLayers(Graphics graphics)
    {
        using var clipRegion = graphics.Clip;
        using var identity = new Matrix();
        foreach (var scan in clipRegion.GetRegionScans(identity))
        {
            var paintArea = Rectangle.Intersect(Rectangle.Ceiling(scan), ClientRectangle);
            if (paintArea.Width <= 0 || paintArea.Height <= 0)
            {
                continue;
            }

            graphics.DrawImage(_dimmedImage, paintArea, paintArea, GraphicsUnit.Pixel);
            var selectedArea = Rectangle.Intersect(GetDisplaySelection(), paintArea);
            if (selectedArea.Width > 0 && selectedArea.Height > 0)
            {
                graphics.DrawImage(_snapshot.Image, selectedArea, selectedArea, GraphicsUnit.Pixel);
            }
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // OnPaint always covers the invalid region with the cached desktop image.
    }

    private static Bitmap CreateDimmedImage(Bitmap source)
    {
        var dimmed = new Bitmap(source.Width, source.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(dimmed);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(source, Point.Empty);
        graphics.CompositingMode = CompositingMode.SourceOver;
        using var shade = new SolidBrush(Color.FromArgb(118, 0, 0, 0));
        graphics.FillRectangle(shade, new Rectangle(Point.Empty, source.Size));
        return dimmed;
    }

    private Rectangle GetDisplaySelection() =>
        !_hasSelection && !_isSelecting ? _hoverWindowSelection : _selection;

    private Rectangle EditingBounds => _replacementCaptureImage is null
        ? _selection
        : new Rectangle(Point.Empty, _replacementCaptureImage.Size);

    private Bitmap EditingSource => _replacementCaptureImage ?? _snapshot.Image;

    private Point ToEditingPoint(Point clientPoint)
    {
        if (_replacementCaptureImage is null)
        {
            return clientPoint;
        }

        return new Point(
            Math.Clamp(
                (int)Math.Floor((clientPoint.X - _selection.X + _replacementScroll.X) / _replacementZoom),
                0,
                _replacementCaptureImage.Width - 1),
            Math.Clamp(
                (int)Math.Floor((clientPoint.Y - _selection.Y + _replacementScroll.Y) / _replacementZoom),
                0,
                _replacementCaptureImage.Height - 1));
    }

    private Point ToClientPoint(Point imagePoint) => _replacementCaptureImage is null
        ? imagePoint
        : new Point(
            _selection.X - _replacementScroll.X + (int)Math.Round(imagePoint.X * _replacementZoom),
            _selection.Y - _replacementScroll.Y + (int)Math.Round(imagePoint.Y * _replacementZoom));

    private Rectangle ToEditingBounds(Rectangle clientBounds)
    {
        if (_replacementCaptureImage is null)
        {
            return clientBounds;
        }

        var topLeft = ToEditingPoint(clientBounds.Location);
        var bottomRight = ToEditingPoint(new Point(clientBounds.Right, clientBounds.Bottom));
        return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    private FlowLayoutPanel BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(7, 6, 7, 6),
            Margin = Padding.Empty,
            BackColor = Color.FromArgb(26, 32, 44),
            Font = new Font("Microsoft YaHei UI", 8.5F)
        };

        foreach (var tool in CaptureEditorToolCatalog.Tools)
        {
            AddToolButton(toolbar, tool.Text, tool.Tool, tool.ToolTip);
        }
        toolbar.Controls.Add(CreateSeparator());

        var undo = CreateToolbarButton("撤销", 48);
        undo.Click += (_, _) =>
        {
            CancelTextEditor(commit: false);
            UndoLastAnnotation();
        };
        _toolTip.SetToolTip(undo, "撤销上一步（Ctrl+Z）");
        toolbar.Controls.Add(undo);

        var width = CreateToolbarButton($"粗细 {_toolWidthController.Current}", 68);
        width.Name = "WidthButton";
        width.Click += (_, _) => CycleWidth();
        width.MouseEnter += (_, _) => width.Focus();
        width.MouseLeave += (_, _) => Focus();
        width.MouseWheel += HandleWidthMouseWheel;
        _toolTip.SetToolTip(width,
            $"单击切换；悬停后滚轮调整（{_toolWidthController.Range.Minimum}–{_toolWidthController.Range.Maximum}）");
        toolbar.Controls.Add(width);

        foreach (var color in CaptureEditorToolCatalog.Palette)
        {
            var colorButton = new Button
            {
                Size = new Size(24, 28),
                Margin = new Padding(2, 1, 2, 1),
                BackColor = color,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            colorButton.FlatAppearance.BorderColor = color == _color ? Color.White : Color.FromArgb(71, 85, 105);
            colorButton.FlatAppearance.BorderSize = color == _color ? 2 : 1;
            colorButton.Click += (_, _) => SelectColor(color);
            _colorButtons.Add(colorButton);
            toolbar.Controls.Add(colorButton);
        }

        var featureCommands = _featureSession.GetToolbarCommands();
        if (featureCommands.Count > 0)
        {
            toolbar.Controls.Add(CreateSeparator());
            foreach (var featureCommand in featureCommands)
            {
                var command = featureCommand.Command;
                var button = CreateToolbarButton(command.Text, Math.Max(42, command.Width));
                button.Click += async (_, _) => await ExecuteFeatureCommandAsync(featureCommand, button);
                _toolTip.SetToolTip(button, command.ToolTip);
                toolbar.Controls.Add(button);
            }
        }

        toolbar.Controls.Add(CreateSeparator());
        var copy = CreateToolbarButton("复制", 48);
        copy.Click += (_, _) => CopySelectionAndClose();
        _toolTip.SetToolTip(copy, "复制到剪贴板并结束（Ctrl+C）");
        toolbar.Controls.Add(copy);

        var save = CreateToolbarButton("保存", 48, Color.FromArgb(37, 99, 235));
        save.Click += (_, _) => SaveSelectionAndClose();
        _toolTip.SetToolTip(save, "保存到挂接文件夹并复制（Ctrl+S）");
        toolbar.Controls.Add(save);

        var cancel = CreateToolbarButton("取消", 48);
        cancel.Click += (_, _) => Close();
        _toolTip.SetToolTip(cancel, "取消截图（Esc）");
        toolbar.Controls.Add(cancel);
        return toolbar;
    }

    private async Task ExecuteFeatureCommandAsync(
        CaptureFeatureCommand command,
        Button button)
    {
        if (_featureCommandRunning || _finished)
        {
            return;
        }

        _featureCommandRunning = true;
        button.Enabled = false;
        try
        {
            await _featureSession.ExecuteToolbarCommandAsync(command);
        }
        finally
        {
            _featureCommandRunning = false;
            if (!IsDisposed && !button.IsDisposed)
            {
                button.Enabled = true;
                Focus();
            }
        }
    }

    private void AddToolButton(FlowLayoutPanel toolbar, string text, EditorTool tool, string tip)
    {
        var button = CreateToolbarButton(text, tool == EditorTool.Mosaic ? 58 : 48);
        button.Click += (_, _) => SelectTool(EditorToolSelection.Toggle(_tool, tool));
        _toolTip.SetToolTip(button, tip);
        _toolButtons.Add(tool, button);
        toolbar.Controls.Add(button);
    }

    private static Button CreateToolbarButton(string text, int width, Color? backColor = null)
    {
        var color = backColor ?? Color.FromArgb(45, 55, 72);
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 30),
            Margin = new Padding(2, 0, 2, 0),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(71, 85, 105);
        return button;
    }

    private static Panel CreateSeparator() => new()
    {
        Size = new Size(1, 24),
        BackColor = Color.FromArgb(71, 85, 105),
        Margin = new Padding(5, 3, 5, 3)
    };

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        if (_replacementCaptureImage is not null &&
            e.Button == MouseButtons.Left &&
            IsReplacementFrameMoveHandle(e.Location))
        {
            CancelTextEditor(commit: true);
            _isMovingReplacementFrame = true;
            _replacementFramePointerOrigin = e.Location;
            _replacementFrameOrigin = _selection;
            _toolbar.Visible = false;
            Capture = true;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (_featureSession.HandleMouseDown(e))
        {
            return;
        }

        if (_replacementCaptureImage is not null && e.Button == MouseButtons.Right)
        {
            if (_selection.Contains(e.Location) && !_toolbar.Bounds.Contains(e.Location))
            {
                _isPanningReplacement = true;
                _replacementPanPointerOrigin = e.Location;
                _replacementPanScrollOrigin = _replacementScroll;
                Capture = true;
                Cursor = Cursors.Hand;
            }
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            if (_hasSelection && _selection.Contains(e.Location) && !_toolbar.Bounds.Contains(e.Location))
            {
                BeginSelectionMove(e.Location);
            }
            else if (_hasSelection)
            {
                SelectTool(EditorTool.None);
            }
            else
            {
                Close();
            }
            return;
        }

        if (e.Button != MouseButtons.Left || _toolbar.Bounds.Contains(e.Location))
        {
            return;
        }

        CancelTextEditor(commit: true);
        var editingPoint = ToEditingPoint(e.Location);
        if (CaptureSelectionRedrawPolicy.AllowsSelectionRedraw(
            _replacementCaptureImage is not null) &&
            _hasSelection &&
            _selectionRedrawGuard.IsRedrawRequested)
        {
            _selectionRedrawGuard.TryBeginRedraw(_document.Count > 0);
            BeginManualSelection(Geometry.Clamp(e.Location, ClientRectangle), _selection);
            return;
        }

        var selectionWasCleared = false;
        if (_hasSelection)
        {
            var multiSelect = IsControlPressed();
            if (multiSelect)
            {
                if (TryBeginMovableInteraction(editingPoint, multiSelect: true))
                {
                    return;
                }

                return;
            }

            if (_annotationSelection.Count == 1 && _annotationSelection.Primary is { } selected)
            {
                var movableTarget = HitTestMovable(selected, editingPoint);
                if (movableTarget is not StickerHitTarget.None and not StickerHitTarget.Move &&
                    TryBeginMovableInteraction(editingPoint, multiSelect: false))
                {
                    return;
                }
            }

            var resizeEdges = _replacementCaptureImage is null
                ? HitTestResizeEdges(e.Location)
                : SelectionResizeEdges.None;
            if (resizeEdges != SelectionResizeEdges.None)
            {
                BeginSelectionResize(resizeEdges);
                return;
            }

            var altPressed = IsAltPressed();
            if (!altPressed &&
                _tool == EditorTool.None &&
                (_document.Count > 0 || _replacementCaptureImage is not null) &&
                _selection.Contains(e.Location))
            {
                BeginAnnotationMarquee(editingPoint);
                return;
            }

            if (TemporaryAnnotationMoveMode.ShouldTryMove(_tool, altPressed) &&
                TryBeginMovableInteraction(editingPoint, multiSelect: false))
            {
                return;
            }
            if (altPressed)
            {
                UpdateIdleCursor(e.Location);
                return;
            }

            selectionWasCleared = ClearAnnotationSelection();
        }

        if (!_hasSelection)
        {
            UpdateHoverWindowSelection(e.Location, force: true);
            _startPoint = Geometry.Clamp(e.Location, ClientRectangle);
            _currentPoint = _startPoint;
            Capture = true;

            if (!_hoverWindowSelection.IsEmpty)
            {
                _isPendingWindowSelection = true;
            }
            else
            {
                BeginManualSelection(_startPoint, Rectangle.Empty);
            }
            return;
        }

        if (_tool == EditorTool.None)
        {
            if (_replacementCaptureImage is not null)
            {
                if (_selection.Contains(e.Location))
                {
                    BeginAnnotationMarquee(editingPoint);
                }
                return;
            }

            if (selectionWasCleared && !_selectionRedrawGuard.IsRedrawRequested)
            {
                UpdateIdleCursor(e.Location);
                return;
            }

            if (!_selectionRedrawGuard.TryBeginRedraw(_document.Count > 0))
            {
                ShowSelectionRedrawStartHint(e.Location);
                return;
            }

            BeginManualSelection(Geometry.Clamp(e.Location, ClientRectangle), _selection);
            return;
        }

        if (!_selection.Contains(e.Location))
        {
            return;
        }

        if (_tool == EditorTool.Text)
        {
            BeginTextEditor(e.Location);
            return;
        }

        _isDrawing = true;
        _startPoint = Geometry.Clamp(editingPoint, EditingBounds);
        _currentPoint = _startPoint;
        _draftPoints = [_startPoint];
        Capture = true;
    }

    private void HandleMouseMove(object? sender, MouseEventArgs e)
    {
        if (_isMovingReplacementFrame)
        {
            var previous = _selection;
            _selection = SelectionMover.Move(
                _replacementFrameOrigin,
                new Point(
                    e.X - _replacementFramePointerOrigin.X,
                    e.Y - _replacementFramePointerOrigin.Y),
                ClientRectangle);
            if (previous != _selection)
            {
                InvalidateReplacementFrameTransition(previous, _selection);
            }
            return;
        }

        if (_featureSession.HandleMouseMove(e))
        {
            return;
        }

        if (_isPanningReplacement)
        {
            _replacementScroll = ClampReplacementScroll(
                CaptureEditorViewportLayout.CalculatePanScroll(
                    _replacementPanScrollOrigin,
                    _replacementPanPointerOrigin,
                    e.Location));
            Invalidate(_selection, false);
            return;
        }

        var editingPoint = ToEditingPoint(e.Location);

        if (_isMovingSelection)
        {
            var pointerOffset = new Point(
                e.X - _moveOriginPointer.X,
                e.Y - _moveOriginPointer.Y);
            if (!_selectionMoveDidDrag && !IsManualDrag(_moveOriginPointer, e.Location))
            {
                return;
            }

            _selectionMoveDidDrag = true;
            _toolbar.Visible = false;
            var previousSelection = _selection;
            _selection = SelectionMover.Move(_moveOriginSelection, pointerOffset, ClientRectangle);
            var actualOffset = new Point(
                _selection.X - previousSelection.X,
                _selection.Y - previousSelection.Y);
            if (actualOffset.IsEmpty)
            {
                return;
            }

            var movedCategories = _selectionMoveAnnotationStrategy.MovedCategories;
            var previousVisualAreas = _document.GetVisualAreas(movedCategories)
                .Concat(GetSelectedAnnotationVisualAreas(movedCategories))
                .ToArray();
            _selectionMoveAnnotationStrategy.Apply(_document, actualOffset);
            InvalidateSelectionMoveTransition(
                previousSelection,
                _selection,
                previousVisualAreas,
                actualOffset);
            return;
        }

        if (_activeMovableTarget != StickerHitTarget.None && _annotationSelection.Primary is { } activeMovable)
        {
            var previousBounds = _annotationSelection.Bounds;
            var pointerOffset = new Point(
                editingPoint.X - _movableDragOrigin.X,
                editingPoint.Y - _movableDragOrigin.Y);
            if (_activeMovableTarget == StickerHitTarget.Move)
            {
                var actualOffset = GroupMoveLayout.ClampOffset(
                    _movableGroupOriginBounds,
                    pointerOffset,
                    EditingBounds);
                foreach (var (annotation, origin) in _movableOriginBounds)
                {
                    annotation.SetBounds(new Rectangle(
                        origin.X + actualOffset.X,
                        origin.Y + actualOffset.Y,
                        origin.Width,
                        origin.Height));
                }
            }
            else if (_movableOriginBounds.TryGetValue(activeMovable, out var origin))
            {
                var resizedBounds = activeMovable.PreserveAspectRatioWhenResizing
                    ? StickerLayout.Resize(
                        origin,
                        _activeMovableTarget,
                        editingPoint,
                        EditingBounds)
                    : AnnotationResizeLayout.Resize(
                        origin,
                        _activeMovableTarget,
                        editingPoint,
                        EditingBounds);
                activeMovable.SetBounds(resizedBounds);
            }
            InvalidateMovableTransition(previousBounds, _annotationSelection.Bounds);
            return;
        }

        if (_activeResizeEdges != SelectionResizeEdges.None)
        {
            var previousSelection = _selection;
            _selection = SelectionResizer.Resize(
                _resizeOriginSelection,
                _activeResizeEdges,
                Geometry.Clamp(e.Location, ClientRectangle),
                ClientRectangle,
                minimumSize: 12);
            InvalidateSelectionTransition(previousSelection, _selection);
            return;
        }

        if (_isPendingAnnotationMarquee)
        {
            var current = Geometry.Clamp(editingPoint, EditingBounds);
            if (IsManualDrag(_annotationMarqueeStart, current))
            {
                _isPendingAnnotationMarquee = false;
                _isSelectingAnnotations = true;
                ClearAnnotationSelection();
                var previous = _annotationMarqueeBounds;
                _annotationMarqueeBounds = Geometry.Normalize(_annotationMarqueeStart, current);
                InvalidateAnnotationMarqueeTransition(previous, _annotationMarqueeBounds);
            }
            return;
        }

        if (_isSelectingAnnotations)
        {
            var previous = _annotationMarqueeBounds;
            _annotationMarqueeBounds = Geometry.Normalize(
                _annotationMarqueeStart,
                Geometry.Clamp(editingPoint, EditingBounds));
            InvalidateAnnotationMarqueeTransition(previous, _annotationMarqueeBounds);
            return;
        }

        if (_isPendingWindowSelection)
        {
            _currentPoint = Geometry.Clamp(e.Location, ClientRectangle);
            if (IsManualDrag(_startPoint, _currentPoint))
            {
                var oldHoverSelection = _hoverWindowSelection;
                _isPendingWindowSelection = false;
                _hoverWindowSelection = Rectangle.Empty;
                _selection = Geometry.Normalize(_startPoint, _currentPoint);
                _isSelecting = true;
                ClearAnnotations();
                _toolbar.Visible = false;
                InvalidateSelectionTransition(oldHoverSelection, _selection);
            }
            else
            {
                UpdateHoverWindowSelection(e.Location, force: true);
            }
            return;
        }

        if (_isSelecting)
        {
            var oldSelection = _selection;
            _currentPoint = Geometry.Clamp(e.Location, ClientRectangle);
            _selection = Geometry.Normalize(_startPoint, _currentPoint);
            InvalidateSelectionTransition(oldSelection, _selection);
            return;
        }

        if (!_isDrawing)
        {
            if (!_hasSelection)
            {
                UpdateHoverWindowSelection(e.Location);
                Cursor = Cursors.Cross;
                return;
            }

            UpdateIdleCursor(e.Location);
            return;
        }

        var oldCurrentPoint = _currentPoint;
        _currentPoint = Geometry.Clamp(editingPoint, EditingBounds);
        if (_tool is EditorTool.Pen or EditorTool.Mosaic)
        {
            var last = _draftPoints[^1];
            if (Math.Abs(last.X - _currentPoint.X) + Math.Abs(last.Y - _currentPoint.Y) >= 2)
            {
                _draftPoints.Add(_currentPoint);
            }
        }
        InvalidateDraftTransition(oldCurrentPoint, _currentPoint);
    }

    private void HandleMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _isMovingReplacementFrame)
        {
            Capture = false;
            _isMovingReplacementFrame = false;
            PositionToolbar();
            _toolbar.Visible = true;
            UpdateIdleCursor(e.Location);
            return;
        }

        if (_featureSession.HandleMouseUp(e))
        {
            return;
        }

        if (e.Button == MouseButtons.Right && _isPanningReplacement)
        {
            Capture = false;
            _isPanningReplacement = false;
            UpdateIdleCursor(e.Location);
            return;
        }

        if (e.Button == MouseButtons.Right && _isMovingSelection)
        {
            Capture = false;
            _isMovingSelection = false;
            if (!_selectionMoveDidDrag)
            {
                SelectTool(EditorTool.None);
            }
            PositionToolbar();
            _toolbar.Visible = true;
            UpdateIdleCursor(e.Location);
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Capture = false;
        if (_activeMovableTarget != StickerHitTarget.None)
        {
            var currentBounds = _annotationSelection.Bounds;
            _activeMovableTarget = StickerHitTarget.None;
            if (_tool != EditorTool.None)
            {
                _annotationSelection.Clear();
            }
            PositionToolbar();
            _toolbar.Visible = true;
            UpdateIdleCursor(e.Location);
            if (!currentBounds.IsEmpty)
            {
                InvalidateMovableTransition(_movableGroupOriginBounds, currentBounds);
            }
            _movableOriginBounds.Clear();
            return;
        }

        if (_activeResizeEdges != SelectionResizeEdges.None)
        {
            _activeResizeEdges = SelectionResizeEdges.None;
            PositionToolbar();
            _toolbar.Visible = true;
            Cursor = SelectionResizer.GetCursor(HitTestResizeEdges(e.Location));
            InvalidateSelectionTransition(_resizeOriginSelection, _selection);
            return;
        }

        if (_isPendingAnnotationMarquee)
        {
            _isPendingAnnotationMarquee = false;
            _annotationMarqueeBounds = Rectangle.Empty;
            var previousSelectionBounds = _annotationSelection.Bounds;
            var movable = _document.FindTopMovableAt(
                _annotationMarqueeStart,
                GetMovableHitTolerance());
            if (movable is null)
            {
                ClearAnnotationSelection();
            }
            else if (!_annotationSelection.Contains(movable))
            {
                _annotationSelection.SelectOnly(movable);
                SelectTool(EditorTool.None);
                InvalidateMovableTransition(previousSelectionBounds, _annotationSelection.Bounds);
            }
            PositionToolbar();
            _toolbar.Visible = true;
            UpdateIdleCursor(e.Location);
            return;
        }

        if (_isSelectingAnnotations)
        {
            var marqueeBounds = _annotationMarqueeBounds;
            _isSelectingAnnotations = false;
            _annotationMarqueeBounds = Rectangle.Empty;
            _annotationSelection.SelectIntersecting(
                _document.GetMovableAnnotations(),
                marqueeBounds);
            InvalidateAnnotationMarqueeTransition(marqueeBounds, Rectangle.Empty);
            InvalidateMovableTransition(Rectangle.Empty, _annotationSelection.Bounds);
            PositionToolbar();
            _toolbar.Visible = true;
            UpdateIdleCursor(e.Location);
            return;
        }

        if (_isPendingWindowSelection)
        {
            _isPendingWindowSelection = false;
            if (!_hoverWindowSelection.IsEmpty)
            {
                CommitHoveredWindowSelection();
            }
            else
            {
                UpdateHoverWindowSelection(e.Location, force: true);
            }
            return;
        }

        if (_isSelecting)
        {
            var oldSelection = _selection;
            _isSelecting = false;
            _selection = Geometry.Normalize(_startPoint, Geometry.Clamp(e.Location, ClientRectangle));
            if (_selection.Width < 12 || _selection.Height < 12)
            {
                _selection = Rectangle.Empty;
                _hasSelection = false;
                InvalidateSelectionTransition(oldSelection, Rectangle.Empty);
                UpdateHoverWindowSelection(e.Location, force: true);
                return;
            }

            _hasSelection = true;
            SelectTool(EditorTool.None);
            PositionToolbar();
            _toolbar.Visible = true;
            InvalidateSelectionTransition(oldSelection, _selection);
            return;
        }

        if (!_isDrawing)
        {
            return;
        }

        _isDrawing = false;
        _currentPoint = Geometry.Clamp(ToEditingPoint(e.Location), EditingBounds);
        var annotation = BuildDraftAnnotation();
        if (annotation is not null)
        {
            _document.Add(annotation);
        }
        _draftPoints.Clear();
        InvalidateDraftTransition(_startPoint, _currentPoint);
    }

    private void BeginSelectionResize(SelectionResizeEdges edges)
    {
        _activeResizeEdges = edges;
        _resizeOriginSelection = _selection;
        _toolbar.Visible = false;
        Capture = true;
        Cursor = SelectionResizer.GetCursor(edges);
    }

    private void BeginSelectionMove(Point pointer)
    {
        CancelTextEditor(commit: true);
        _isMovingSelection = true;
        _selectionMoveDidDrag = false;
        _moveOriginPointer = pointer;
        _moveOriginSelection = _selection;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }

    private void BeginAnnotationMarquee(Point pointer)
    {
        _isPendingAnnotationMarquee = true;
        _isSelectingAnnotations = false;
        _annotationMarqueeStart = Geometry.Clamp(pointer, EditingBounds);
        _annotationMarqueeBounds = Rectangle.Empty;
        _toolbar.Visible = false;
        Capture = true;
        Cursor = Cursors.Cross;
    }

    private bool TryBeginMovableInteraction(Point point, bool multiSelect)
    {
        if (!EditingBounds.Contains(point))
        {
            return false;
        }

        var altPressed = IsAltPressed();
        var movable = !multiSelect && _annotationSelection.Count == 1
            ? _annotationSelection.Primary
            : null;
        var target = movable is null
            ? StickerHitTarget.None
            : HitTestMovable(movable, point);
        if (target == StickerHitTarget.None)
        {
            movable = _document.FindTopMovableAt(
                point,
                GetMovableHitTolerance());
            target = movable is null ? StickerHitTarget.None : StickerHitTarget.Move;
        }

        if (movable is null || target == StickerHitTarget.None)
        {
            return false;
        }

        var previousSelectionBounds = _annotationSelection.Bounds;
        if (multiSelect)
        {
            if (_annotationSelection.Contains(movable))
            {
                _annotationSelection.Remove(movable);
                SelectTool(EditorTool.None);
                InvalidateMovableTransition(previousSelectionBounds, _annotationSelection.Bounds);
                UpdateIdleCursor(point);
                return true;
            }

            _annotationSelection.Add(movable);
            target = StickerHitTarget.Move;
        }
        else if (!_annotationSelection.Contains(movable))
        {
            _annotationSelection.SelectOnly(movable);
            target = StickerHitTarget.Move;
        }
        else if (_annotationSelection.Count > 1)
        {
            target = StickerHitTarget.Move;
        }

        if (!TemporaryAnnotationMoveMode.ShouldPreserveTool(_tool, altPressed))
        {
            SelectTool(EditorTool.None);
        }
        if (target == StickerHitTarget.Move && _annotationSelection.RequiresAltToMove && !altPressed)
        {
            InvalidateMovableTransition(previousSelectionBounds, _annotationSelection.Bounds);
            if (!multiSelect)
            {
                var hintPoint = ToClientPoint(point);
                _toolTip.Show(
                    "移动编辑元素需要按住 Alt 并使用鼠标左键拖动。",
                    this,
                    hintPoint.X + 12,
                    hintPoint.Y + 18,
                    1800);
            }
            Cursor = multiSelect ? Cursors.Hand : Cursors.Default;
            return true;
        }

        _activeMovableTarget = target;
        _movableDragOrigin = point;
        _movableOriginBounds = _annotationSelection.Items.ToDictionary(
            annotation => annotation,
            annotation => annotation.Bounds);
        _movableGroupOriginBounds = _annotationSelection.Bounds;
        _toolbar.Visible = false;
        Capture = true;
        Cursor = StickerLayout.GetCursor(target);
        InvalidateMovableTransition(previousSelectionBounds, _annotationSelection.Bounds);
        return true;
    }

    private bool ClearAnnotationSelection()
    {
        if (_annotationSelection.Count == 0)
        {
            return false;
        }

        var bounds = _annotationSelection.Bounds;
        _annotationSelection.Clear();
        _activeMovableTarget = StickerHitTarget.None;
        _movableOriginBounds.Clear();
        InvalidateMovableTransition(bounds, Rectangle.Empty);
        return true;
    }

    private void ClearAnnotations()
    {
        _annotationSelection.Clear();
        _activeMovableTarget = StickerHitTarget.None;
        _movableOriginBounds.Clear();
        _document.Clear();
    }

    private void UndoLastAnnotation()
    {
        if (!_annotationEditor.Undo())
        {
            return;
        }

        _activeMovableTarget = StickerHitTarget.None;
        Invalidate(_selection);
    }

    private void DeleteSelectedAnnotations()
    {
        if (_annotationSelection.Count == 0)
        {
            return;
        }

        var deletion = _annotationEditor.DeleteSelected();
        _activeMovableTarget = StickerHitTarget.None;
        _movableOriginBounds.Clear();
        Capture = false;
        if (deletion.RemovedCount > 0)
        {
            var bounds = deletion.Bounds;
            bounds.Inflate(deletion.RenderMargin, deletion.RenderMargin);
            InvalidateMovableTransition(bounds, Rectangle.Empty);
        }
    }

    private void UpdateIdleCursor(Point point)
    {
        if (_replacementCaptureImage is not null && IsReplacementFrameMoveHandle(point))
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        var editingPoint = ToEditingPoint(point);
        var altPressed = IsAltPressed();
        var controlPressed = IsControlPressed();
        if (_annotationSelection.Count == 1 && _annotationSelection.Primary is { } selected)
        {
            var movableTarget = HitTestMovable(selected, editingPoint);
            if (movableTarget is not StickerHitTarget.None and not StickerHitTarget.Move)
            {
                Cursor = StickerLayout.GetCursor(movableTarget);
                return;
            }
        }

        var resizeEdges = _replacementCaptureImage is null
            ? HitTestResizeEdges(point)
            : SelectionResizeEdges.None;
        if (resizeEdges != SelectionResizeEdges.None)
        {
            Cursor = SelectionResizer.GetCursor(resizeEdges);
            return;
        }

        var hovered = _document.FindTopMovableAt(
            editingPoint,
            GetMovableHitTolerance());
        if (controlPressed && hovered is not null)
        {
            Cursor = Cursors.Hand;
            return;
        }

        if (hovered is not null && hovered.CanMove(altPressed))
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        Cursor = EditorIdleCursorPolicy.UsesDrawingCursor(
            _hasSelection,
            _selection.Contains(point),
            _tool)
            ? Cursors.Cross
            : Cursors.Default;
    }

    private int GetStickerHandleSize()
    {
        var size = Math.Max(9, DeviceDpi * 9 / 96);
        return _replacementCaptureImage is null
            ? size
            : Math.Max(6, (int)Math.Round(size / _replacementZoom));
    }

    private int GetMovableHitTolerance()
    {
        var tolerance = Math.Max(6, DeviceDpi * 6 / 96);
        return _replacementCaptureImage is null
            ? tolerance
            : Math.Max(4, (int)Math.Round(tolerance / _replacementZoom));
    }

    private static bool IsAltPressed() => (ModifierKeys & Keys.Alt) == Keys.Alt;

    private static bool IsAltKey(Keys keyCode) => keyCode is Keys.Menu or Keys.LMenu or Keys.RMenu;

    private static bool IsControlPressed() => (ModifierKeys & Keys.Control) == Keys.Control;

    private StickerHitTarget HitTestMovable(MovableAnnotation annotation, Point point) =>
        annotation.SupportsResize
            ? StickerLayout.HitTest(annotation.Bounds, point, GetStickerHandleSize())
            : annotation.HitTest(point, GetMovableHitTolerance())
                ? StickerHitTarget.Move
                : StickerHitTarget.None;

    private SelectionResizeEdges HitTestResizeEdges(Point point)
    {
        var tolerance = Math.Max(6, DeviceDpi * 6 / 96);
        return SelectionResizer.HitTest(_selection, point, tolerance);
    }

    private void BeginManualSelection(Point startPoint, Rectangle previousSelection)
    {
        ClearAnnotations();
        _isSelecting = true;
        _isPendingWindowSelection = false;
        _hasSelection = false;
        _toolbar.Visible = false;
        _startPoint = startPoint;
        _currentPoint = startPoint;
        _hoverWindowSelection = Rectangle.Empty;
        _selection = Rectangle.Empty;
        Capture = true;
        InvalidateSelectionTransition(previousSelection, Rectangle.Empty);
        InvalidateHintAreas();
    }

    private void CommitHoveredWindowSelection()
    {
        var selectedWindow = _hoverWindowSelection;
        _hoverWindowSelection = Rectangle.Empty;
        _selection = selectedWindow;
        _hasSelection = true;
        ClearAnnotations();
        SelectTool(EditorTool.None);
        PositionToolbar();
        _toolbar.Visible = true;
        InvalidateSelectionTransition(selectedWindow, _selection);
    }

    private void UpdateHoverWindowSelection(Point clientPoint, bool force = false)
    {
        if (_hasSelection || _isSelecting || _isDrawing)
        {
            return;
        }

        if (!force && _hoverLookupClock.ElapsedMilliseconds < 24)
        {
            return;
        }
        _hoverLookupClock.Restart();

        var screenPoint = new Point(
            clientPoint.X + _snapshot.Bounds.X,
            clientPoint.Y + _snapshot.Bounds.Y);
        var screenBounds = _windowLocator.FindWindowAt(screenPoint);
        var candidate = Rectangle.Empty;
        if (screenBounds is { } bounds)
        {
            candidate = new Rectangle(
                bounds.X - _snapshot.Bounds.X,
                bounds.Y - _snapshot.Bounds.Y,
                bounds.Width,
                bounds.Height);
            candidate.Intersect(ClientRectangle);
        }

        if (candidate == _hoverWindowSelection)
        {
            return;
        }

        var previous = _hoverWindowSelection;
        _hoverWindowSelection = candidate;
        InvalidateSelectionTransition(previous, candidate);
        if (previous.IsEmpty || candidate.IsEmpty)
        {
            InvalidateHintAreas();
        }
    }

    private static bool IsManualDrag(Point start, Point current)
    {
        var threshold = SystemInformation.DragSize;
        return Math.Abs(current.X - start.X) >= Math.Max(3, threshold.Width / 2) ||
               Math.Abs(current.Y - start.Y) >= Math.Max(3, threshold.Height / 2);
    }

    private void InvalidateSelectionTransition(Rectangle previous, Rectangle current)
    {
        using var dirty = new Region();
        dirty.MakeEmpty();
        if (!previous.IsEmpty)
        {
            dirty.Union(previous);
        }
        if (!current.IsEmpty)
        {
            dirty.Xor(current);
        }

        AddBorderToRegion(dirty, previous, 4);
        AddBorderToRegion(dirty, current, 4);
        AddSizeBadgeToRegion(dirty, previous);
        AddSizeBadgeToRegion(dirty, current);
        dirty.Intersect(ClientRectangle);
        Invalidate(dirty, false);
    }

    private void InvalidateReplacementFrameTransition(Rectangle previous, Rectangle current)
    {
        using var dirty = new Region();
        dirty.MakeEmpty();
        if (!previous.IsEmpty)
        {
            dirty.Union(previous);
        }
        if (!current.IsEmpty)
        {
            dirty.Union(current);
        }

        AddBorderToRegion(dirty, previous, 4);
        AddBorderToRegion(dirty, current, 4);
        AddSizeBadgeToRegion(dirty, previous);
        AddSizeBadgeToRegion(dirty, current);
        dirty.Intersect(ClientRectangle);
        Invalidate(dirty, false);
    }

    private void InvalidateSelectionMoveTransition(
        Rectangle previousSelection,
        Rectangle currentSelection,
        IEnumerable<Rectangle> previousVisualAreas,
        Point offset)
    {
        using var dirty = new Region();
        dirty.MakeEmpty();
        if (!previousSelection.IsEmpty)
        {
            dirty.Union(previousSelection);
        }
        if (!currentSelection.IsEmpty)
        {
            dirty.Xor(currentSelection);
        }

        foreach (var area in SelectionMoveInvalidation.GetMovedVisualAreas(
                     previousSelection,
                     currentSelection,
                     previousVisualAreas,
                     offset))
        {
            dirty.Union(area);
        }

        AddBorderToRegion(dirty, previousSelection, 4);
        AddBorderToRegion(dirty, currentSelection, 4);
        AddSizeBadgeToRegion(dirty, previousSelection);
        AddSizeBadgeToRegion(dirty, currentSelection);
        dirty.Intersect(ClientRectangle);
        Invalidate(dirty, false);
    }

    private IEnumerable<Rectangle> GetSelectedAnnotationVisualAreas(
        AnnotationCategory movedCategories)
    {
        foreach (var annotation in _annotationSelection.Items)
        {
            if ((annotation.Category & movedCategories) == AnnotationCategory.None)
            {
                continue;
            }

            var area = annotation.Bounds;
            var margin = Math.Max(GetStickerHandleSize() + 3, annotation.RenderMargin);
            area.Inflate(margin, margin);
            yield return area;
        }
    }

    private void InvalidateDraftTransition(Point previous, Point current)
    {
        if (_replacementCaptureImage is not null)
        {
            Invalidate(_selection, false);
            return;
        }

        Rectangle dirty;
        var margin = _toolWidthController.Current + 8;
        if (_tool is EditorTool.Rectangle or EditorTool.Ellipse or EditorTool.Arrow)
        {
            var oldBounds = Geometry.Normalize(_startPoint, previous);
            var newBounds = Geometry.Normalize(_startPoint, current);
            dirty = Rectangle.Union(oldBounds, newBounds);
            if (_tool == EditorTool.Arrow)
            {
                margin += _toolWidthController.Current * 3;
            }
        }
        else
        {
            dirty = Geometry.Normalize(previous, current);
            if (_tool == EditorTool.Mosaic)
            {
                margin += _toolWidthController.Current * 3 + 14;
            }
        }

        dirty.Inflate(margin, margin);
        dirty.Intersect(_selection);
        if (!dirty.IsEmpty)
        {
            Invalidate(dirty, false);
        }
    }

    private void InvalidateHintAreas()
    {
        foreach (var screen in Screen.AllScreens)
        {
            var localCenter = new Point(
                screen.Bounds.X - _snapshot.Bounds.X + screen.Bounds.Width / 2,
                screen.Bounds.Y - _snapshot.Bounds.Y + screen.Bounds.Height / 2);
            var hintBounds = new Rectangle(localCenter.X - 350, localCenter.Y - 55, 700, 110);
            hintBounds.Intersect(ClientRectangle);
            if (!hintBounds.IsEmpty)
            {
                Invalidate(hintBounds, false);
            }
        }
    }

    private static void AddBorderToRegion(Region region, Rectangle bounds, int thickness)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        region.Union(new Rectangle(bounds.Left - thickness, bounds.Top - thickness,
            bounds.Width + thickness * 2, thickness * 2));
        region.Union(new Rectangle(bounds.Left - thickness, bounds.Bottom - thickness,
            bounds.Width + thickness * 2, thickness * 2));
        region.Union(new Rectangle(bounds.Left - thickness, bounds.Top,
            thickness * 2, bounds.Height));
        region.Union(new Rectangle(bounds.Right - thickness, bounds.Top,
            thickness * 2, bounds.Height));
    }

    private static void AddSizeBadgeToRegion(Region region, Rectangle selection)
    {
        if (selection.IsEmpty)
        {
            return;
        }

        var y = selection.Top - 32;
        if (y < 4)
        {
            y = selection.Top + 4;
        }
        region.Union(new Rectangle(selection.Left - 2, y, 132, 32));
    }

    private Annotation? BuildDraftAnnotation()
        => _annotationEditor.BuildDraft(
            _tool,
            _startPoint,
            _currentPoint,
            _draftPoints,
            _color,
            _toolWidthController.Current);

    private void RenderDraft(Graphics graphics)
    {
        if (!_isDrawing)
        {
            return;
        }

        BuildDraftAnnotation()?.Render(graphics, EditingSource);
    }

    private void DrawMovableSelection(Graphics graphics)
    {
        if (_annotationSelection.Count == 0)
        {
            return;
        }

        _annotationEditor.DrawSelection(
            graphics,
            GetStickerHandleSize(),
            _replacementCaptureImage is null ? 1F : (float)_replacementZoom);
    }

    private void DrawAnnotationMarquee(Graphics graphics)
    {
        if (!_isSelectingAnnotations || _annotationMarqueeBounds.IsEmpty)
        {
            return;
        }

        using var fill = new SolidBrush(Color.FromArgb(42, 14, 165, 233));
        using var border = new Pen(Color.FromArgb(125, 211, 252), 1.4F)
        {
            DashStyle = DashStyle.Dash
        };
        graphics.FillRectangle(fill, _annotationMarqueeBounds);
        graphics.DrawRectangle(
            border,
            _annotationMarqueeBounds.X,
            _annotationMarqueeBounds.Y,
            Math.Max(1, _annotationMarqueeBounds.Width - 1),
            Math.Max(1, _annotationMarqueeBounds.Height - 1));
    }

    private void InvalidateAnnotationMarqueeTransition(Rectangle previous, Rectangle current)
    {
        if (_replacementCaptureImage is not null)
        {
            Invalidate(_selection, false);
            return;
        }

        var dirty = previous.IsEmpty
            ? current
            : current.IsEmpty
                ? previous
                : Rectangle.Union(previous, current);
        if (dirty.IsEmpty)
        {
            return;
        }

        dirty.Inflate(3, 3);
        dirty.Intersect(_selection);
        if (!dirty.IsEmpty)
        {
            Invalidate(dirty, false);
        }
    }

    private void InvalidateMovableTransition(Rectangle previous, Rectangle current)
    {
        if (_replacementCaptureImage is not null)
        {
            Invalidate(_selection, false);
            return;
        }

        var dirty = previous.IsEmpty
            ? current
            : current.IsEmpty
                ? previous
                : Rectangle.Union(previous, current);
        if (dirty.IsEmpty)
        {
            return;
        }

        var margin = Math.Max(
            GetStickerHandleSize() + 3,
            _annotationSelection.RenderMargin);
        dirty.Inflate(margin, margin);
        dirty.Intersect(ClientRectangle);
        if (!dirty.IsEmpty)
        {
            Invalidate(dirty, false);
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsAltKey(e.KeyCode))
        {
            UpdateIdleCursor(PointToClient(Cursor.Position));
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            HandleEscape();
            return;
        }

        if (e.Control && e.KeyCode == Keys.W)
        {
            e.SuppressKeyPress = true;
            if (!CaptureSelectionRedrawPolicy.AllowsSelectionRedraw(
                    _replacementCaptureImage is not null))
            {
                return;
            }
            CancelTextEditor(commit: true);
            StartSelectionRedraw();
            return;
        }

        if (_textEditor is null && e.Control && e.KeyCode == Keys.A)
        {
            e.SuppressKeyPress = true;
            CancelTextEditor(commit: true);
            HandleSelectAllShortcut();
            return;
        }

        if (_textEditor is not null)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (TextEditorEnterPolicy.Resolve(e.Control) == TextEditorEnterAction.InsertLineBreak)
                {
                    _textEditor.InsertText("\n");
                }
                else
                {
                    CancelTextEditor(commit: true);
                }
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                try
                {
                    var text = _clipboardService.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _textEditor.InsertText(text);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, $"粘贴文字失败：{exception.Message}", "粘贴失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return;
        }

        if (!e.Control && !e.Alt && !e.Shift)
        {
            var shortcutTool = EditorToolShortcut.Resolve(e.KeyCode);
            if (shortcutTool != EditorTool.None)
            {
                e.SuppressKeyPress = true;
                SelectTool(EditorToolSelection.Toggle(_tool, shortcutTool));
                UpdateIdleCursor(PointToClient(Cursor.Position));
                return;
            }
        }

        if (_featureSession.HandleKeyDown(e))
        {
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Delete && _annotationSelection.Count > 0)
        {
            e.SuppressKeyPress = true;
            DeleteSelectedAnnotations();
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            e.SuppressKeyPress = true;
            SaveSelectionAndClose();
        }
        else if (e.Control && e.KeyCode == Keys.C)
        {
            e.SuppressKeyPress = true;
            CopySelectionAndClose();
        }
        else if (e.Control && e.KeyCode == Keys.V && _hasSelection)
        {
            e.SuppressKeyPress = true;
            PasteClipboardContent();
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            e.SuppressKeyPress = true;
            UndoLastAnnotation();
        }
        else if (e.KeyCode == Keys.Enter && _hasSelection)
        {
            e.SuppressKeyPress = true;
            CopySelectionAndClose();
        }
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        if (IsAltKey(e.KeyCode))
        {
            UpdateIdleCursor(PointToClient(Cursor.Position));
        }
    }

    private void PasteClipboardContent()
    {
        try
        {
            var anchor = GetPasteAnchor();
            var image = _clipboardService.GetImage();
            if (image is not null)
            {
                PasteSticker(image, anchor);
                return;
            }

            var text = _clipboardService.GetText()?.TrimEnd('\r', '\n');
            if (!string.IsNullOrWhiteSpace(text))
            {
                PasteTextBox(text, anchor);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"粘贴剪贴板内容失败：{exception.Message}", "粘贴失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PasteSticker(Bitmap image, Point anchor)
    {
        var added = false;
        try
        {
            var bounds = StickerLayout.CreateInitialBounds(image.Size, EditingBounds, anchor);
            if (bounds.IsEmpty)
            {
                return;
            }

            ClearAnnotationSelection();
            var sticker = _annotationEditor.AddSticker(image, bounds);
            added = true;
            SelectTool(EditorTool.None);
            PositionToolbar();
            _toolbar.Visible = true;
            InvalidateMovableTransition(Rectangle.Empty, bounds);
            UpdateIdleCursor(PointToClient(Cursor.Position));
        }
        finally
        {
            if (!added)
            {
                image.Dispose();
            }
        }
    }

    private void PasteTextBox(string text, Point anchor)
    {
        var bounds = CreatePastedTextBounds(text, anchor);
        ClearAnnotationSelection();
        _annotationEditor.AddAndSelect(new PastedTextAnnotation(bounds, text));
        SelectTool(EditorTool.None);
        InvalidateMovableTransition(Rectangle.Empty, bounds);
        UpdateIdleCursor(PointToClient(Cursor.Position));
    }

    private Point GetPasteAnchor()
    {
        var mouse = PointToClient(Cursor.Position);
        return _replacementCaptureImage is not null
            ? _selection.Contains(mouse)
                ? ToEditingPoint(mouse)
                : ToEditingPoint(new Point(
                    _selection.Left + _selection.Width / 2,
                    _selection.Top + _selection.Height / 2))
            : _selection.Contains(mouse)
                ? mouse
                : new Point(
                    _selection.Left + _selection.Width / 2,
                    _selection.Top + _selection.Height / 2);
    }

    private Rectangle CreatePastedTextBounds(string text, Point anchor)
    {
        var editingBounds = EditingBounds;
        var maximumWidth = Math.Max(1, Math.Min(editingBounds.Width, editingBounds.Width * 2 / 3));
        var maximumHeight = Math.Max(1, Math.Min(editingBounds.Height, editingBounds.Height * 2 / 3));
        using var font = new Font(
            "Microsoft YaHei UI",
            PastedTextAnnotation.DefaultFontSize,
            FontStyle.Regular,
            GraphicsUnit.Pixel);
        var measured = TextRenderer.MeasureText(
            text,
            font,
            new Size(Math.Max(1, maximumWidth - 16), Math.Max(1, maximumHeight - 12)),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
        var minimumWidth = Math.Min(80, maximumWidth);
        var minimumHeight = Math.Min(36, maximumHeight);
        var width = Math.Clamp(measured.Width + 16, minimumWidth, maximumWidth);
        var height = Math.Clamp(measured.Height + 12, minimumHeight, maximumHeight);
        var x = Math.Clamp(
            anchor.X - width / 2,
            editingBounds.Left,
            Math.Max(editingBounds.Left, editingBounds.Right - width));
        var y = Math.Clamp(
            anchor.Y - height / 2,
            editingBounds.Top,
            Math.Max(editingBounds.Top, editingBounds.Bottom - height));
        return new Rectangle(x, y, width, height);
    }

    private void HandleSelectAllShortcut()
    {
        var editingElements = _document.GetMovableAnnotations();
        if (_replacementCaptureImage is not null)
        {
            _annotationSelection.SelectAll(editingElements);
            SelectTool(EditorTool.None);
            Invalidate(_selection, false);
            return;
        }
        var action = CaptureSelectAllPolicy.Resolve(
            editingElements.Count,
            _annotationSelection.IsExactSelection(editingElements));
        if (action == CaptureSelectAllAction.ExpandSelectionToFullScreen)
        {
            SelectAllScreen();
            return;
        }

        var previousBounds = _annotationSelection.Bounds;
        _isDrawing = false;
        _draftPoints.Clear();
        _activeMovableTarget = StickerHitTarget.None;
        _movableOriginBounds.Clear();
        Capture = false;
        _annotationSelection.SelectAll(editingElements);
        SelectTool(EditorTool.None);
        PositionToolbar();
        _toolbar.Visible = true;
        InvalidateMovableTransition(previousBounds, _annotationSelection.Bounds);
        UpdateIdleCursor(PointToClient(Cursor.Position));
    }

    private void SelectAllScreen()
    {
        CancelTextEditor(commit: true);
        _isSelecting = false;
        _isPendingWindowSelection = false;
        _isDrawing = false;
        _hasSelection = true;
        _hoverWindowSelection = Rectangle.Empty;
        _selection = ClientRectangle;
        SelectTool(EditorTool.None);
        PositionToolbar();
        _toolbar.Visible = true;
        Invalidate();
    }

    private void SelectTool(EditorTool tool)
    {
        CancelTextEditor(commit: true);
        if (tool != EditorTool.None)
        {
            ClearAnnotationSelection();
        }
        _tool = tool;
        foreach (var pair in _toolButtons)
        {
            pair.Value.BackColor = pair.Key == tool
                ? Color.FromArgb(37, 99, 235)
                : Color.FromArgb(45, 55, 72);
        }
        Cursor = tool == EditorTool.None ? Cursors.Default : Cursors.Cross;
    }

    private void SelectColor(Color color)
    {
        _color = color;
        foreach (var button in _colorButtons)
        {
            button.FlatAppearance.BorderColor = button.BackColor.ToArgb() == color.ToArgb()
                ? Color.White
                : Color.FromArgb(71, 85, 105);
            button.FlatAppearance.BorderSize = button.BackColor.ToArgb() == color.ToArgb() ? 2 : 1;
        }
    }

    private void CycleWidth()
    {
        if (_toolWidthController.CyclePreset())
        {
            UpdateWidthButton();
        }
    }

    private void HandleWidthMouseWheel(object? sender, MouseEventArgs e)
    {
        var pointer = _widthButton.PointToClient(Cursor.Position);
        if (!_widthButton.ClientRectangle.Contains(pointer))
        {
            return;
        }

        var steps = e.Delta / SystemInformation.MouseWheelScrollDelta;
        if (steps == 0)
        {
            steps = Math.Sign(e.Delta);
        }

        if (_toolWidthController.Adjust(steps))
        {
            UpdateWidthButton();
        }
    }

    private void HandleEditorMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_replacementCaptureImage is null ||
            !_selection.Contains(e.Location) ||
            _toolbar.Bounds.Contains(e.Location))
        {
            return;
        }

        var previousZoom = _replacementZoom;
        var nextZoom = CaptureEditorViewportLayout.ClampZoom(
            previousZoom * (e.Delta > 0 ? 1.2D : 1D / 1.2D));
        var viewportAnchor = new Point(
            e.X - _selection.X,
            e.Y - _selection.Y);
        _replacementScroll = CaptureEditorViewportLayout.CalculateZoomScroll(
            previousZoom,
            nextZoom,
            _replacementScroll,
            _selection.Size,
            viewportAnchor);
        _replacementZoom = nextZoom;
        _replacementScroll = ClampReplacementScroll(_replacementScroll);
        Invalidate(_selection, false);
    }

    private Point ClampReplacementScroll(Point scroll)
    {
        if (_replacementCaptureImage is null)
        {
            return Point.Empty;
        }

        var canvasSize = CaptureEditorViewportLayout.CalculateCanvasSize(
            _replacementCaptureImage.Size,
            _replacementZoom);
        return new Point(
            Math.Clamp(scroll.X, 0, Math.Max(0, canvasSize.Width - _selection.Width)),
            Math.Clamp(scroll.Y, 0, Math.Max(0, canvasSize.Height - _selection.Height)));
    }

    private void UpdateWidthButton()
    {
        _widthButton.Text = $"粗细 {_toolWidthController.Current}";
    }

    private void PositionToolbar()
    {
        _toolbar.PerformLayout();
        var preferred = _toolbar.GetPreferredSize(Size.Empty);
        _toolbar.Size = preferred;

        var x = Math.Clamp(_selection.Right - preferred.Width, 8, Math.Max(8, ClientSize.Width - preferred.Width - 8));
        var below = _selection.Bottom + 8;
        var y = below + preferred.Height <= ClientSize.Height
            ? below
            : _selection.Top - preferred.Height - 8;
        y = Math.Clamp(y, 8, Math.Max(8, ClientSize.Height - preferred.Height - 8));
        _toolbar.Location = new Point(x, y);
        _toolbar.BringToFront();
    }

    private void BeginTextEditor(Point location)
    {
        CancelTextEditor(commit: true);
        var minimumSize = new Size(
            Math.Min(120, _selection.Width),
            Math.Min(38, _selection.Height));
        _textEditor = new TransparentTextEditorControl(
            location,
            minimumSize,
            _color,
            _selection,
            _clipboardService);
        _textEditor.CommitRequested += (_, _) => CancelTextEditor(commit: true);
        Controls.Add(_textEditor);
        _textEditor.BringToFront();
        _textEditor.Focus();
    }

    private void StartSelectionRedraw()
    {
        if (!_hasSelection)
        {
            return;
        }

        var marqueeBounds = _annotationMarqueeBounds;
        _isPendingAnnotationMarquee = false;
        _isSelectingAnnotations = false;
        _annotationMarqueeBounds = Rectangle.Empty;
        Capture = false;
        InvalidateAnnotationMarqueeTransition(marqueeBounds, Rectangle.Empty);
        _selectionRedrawGuard.RequestRedraw();
        _isDrawing = false;
        _draftPoints.Clear();
        ClearAnnotationSelection();
        SelectTool(EditorTool.None);
        var pointer = Geometry.Clamp(PointToClient(Cursor.Position), ClientRectangle);
        _toolTip.Show(
            "已启动重新框选，请按住左键拖动选择新区域。",
            this,
            pointer.X + 12,
            pointer.Y + 18,
            2200);
        Cursor = Cursors.Cross;
    }

    private void ShowSelectionRedrawStartHint(Point pointer)
    {
        _toolTip.Show(
            "如需重新框选，请按 Ctrl+W 启动，再按住左键拖动。",
            this,
            pointer.X + 12,
            pointer.Y + 18,
            2200);
        Cursor = Cursors.Default;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape && (keyData & Keys.Modifiers) == Keys.None)
        {
            HandleEscape();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void HandleEscape()
    {
        var action = CaptureEscapePolicy.Resolve(
            isTextEditing: _textEditor is not null,
            hasElementSelection: _annotationSelection.Count > 0 &&
                                 !_isPendingAnnotationMarquee &&
                                 !_isSelectingAnnotations,
            hasDrawingTool: _isDrawing || _tool != EditorTool.None,
            isAdjustingSelection: _activeResizeEdges != SelectionResizeEdges.None ||
                                  _isMovingSelection ||
                                  _isMovingReplacementFrame ||
                                  _isPendingAnnotationMarquee ||
                                  _isSelectingAnnotations);

        switch (action)
        {
            case CaptureEscapeAction.CompleteTextEditing:
                CancelTextEditor(commit: true);
                ClearAnnotationSelection();
                SelectTool(EditorTool.None);
                RestoreToolbarAfterEditing();
                return;
            case CaptureEscapeAction.ClearElementSelection:
                _activeMovableTarget = StickerHitTarget.None;
                _activeResizeEdges = SelectionResizeEdges.None;
                _isMovingSelection = false;
                _isMovingReplacementFrame = false;
                _selectionMoveDidDrag = false;
                _movableOriginBounds.Clear();
                Capture = false;
                ClearAnnotationSelection();
                RestoreToolbarAfterEditing();
                return;
            case CaptureEscapeAction.CancelDrawingTool:
                _isDrawing = false;
                _activeResizeEdges = SelectionResizeEdges.None;
                _isMovingSelection = false;
                _isMovingReplacementFrame = false;
                _selectionMoveDidDrag = false;
                _draftPoints.Clear();
                Capture = false;
                SelectTool(EditorTool.None);
                Invalidate(_selection);
                RestoreToolbarAfterEditing();
                return;
            case CaptureEscapeAction.FinishSelectionAdjustment:
                var marqueeBounds = _annotationMarqueeBounds;
                _activeResizeEdges = SelectionResizeEdges.None;
                _isMovingSelection = false;
                _isMovingReplacementFrame = false;
                _selectionMoveDidDrag = false;
                _isPendingAnnotationMarquee = false;
                _isSelectingAnnotations = false;
                _annotationMarqueeBounds = Rectangle.Empty;
                Capture = false;
                InvalidateAnnotationMarqueeTransition(marqueeBounds, Rectangle.Empty);
                RestoreToolbarAfterEditing();
                return;
            case CaptureEscapeAction.CloseCapture:
            default:
                var featureEscape = new KeyEventArgs(Keys.Escape);
                if (_featureSession.HandleKeyDown(featureEscape))
                {
                    Invalidate(_selection);
                    return;
                }

                CancelCapture();
                return;
        }
    }

    private void RestoreToolbarAfterEditing()
    {
        if (_hasSelection)
        {
            PositionToolbar();
            _toolbar.Visible = true;
        }
        UpdateIdleCursor(PointToClient(Cursor.Position));
    }

    private void CancelCapture()
    {
        if (_finished || IsDisposed)
        {
            return;
        }

        _finished = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void CancelTextEditor(bool commit)
    {
        if (_textEditor is null)
        {
            return;
        }

        var editor = _textEditor;
        _textEditor = null;
        if (commit && !string.IsNullOrWhiteSpace(editor.Text))
        {
            ClearAnnotationSelection();
            var annotation = new TextAnnotation(
                ToEditingBounds(editor.TextContentBounds),
                editor.Text.TrimEnd(),
                editor.ForeColor,
                TextEditorCommitLayout.CalculateImageFontSize(
                    editor.TextFontSize,
                    _replacementCaptureImage is null ? 1D : _replacementZoom));
            _document.Add(annotation);
            _annotationSelection.SelectOnly(annotation);
        }
        Controls.Remove(editor);
        editor.Dispose();
        Focus();
        Invalidate(_selection);
    }

    private Bitmap RenderSelection()
    {
        if (!_hasSelection || _selection.Width <= 0 || _selection.Height <= 0)
        {
            throw new InvalidOperationException("请先拖动鼠标选择截图区域；没有编辑元素时也可按 Ctrl+A 选择全部画面。 ");
        }

        CancelTextEditor(commit: true);
        if (_replacementCaptureImage is not null)
        {
            return _annotationEditor.RenderResult(_replacementCaptureImage);
        }

        var result = new Bitmap(_selection.Width, _selection.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.DrawImage(_snapshot.Image,
                new Rectangle(Point.Empty, _selection.Size),
                _selection,
                GraphicsUnit.Pixel);
            graphics.TranslateTransform(-_selection.X, -_selection.Y);
            graphics.SetClip(_selection);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _document.Render(graphics, _snapshot.Image);
            _featureSession.Render(graphics, CaptureRenderTarget.Export);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private void SaveSelectionAndClose()
    {
        if (_finished)
        {
            return;
        }

        try
        {
            using var image = RenderSelection();
            var path = _imageSaveService.SavePng(image, _outputFolder);
            try
            {
                _clipboardService.SetImage(image);
            }
            catch (Exception clipboardException)
            {
                _finished = true;
                ScreenshotSaved?.Invoke(this, path);
                MessageBox.Show(this,
                    $"截图已保存到：\n{path}\n\n但复制到剪贴板失败：{clipboardException.Message}",
                    "已保存，复制失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _finished = true;
            ScreenshotSaved?.Invoke(this, path);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"保存截图失败：{exception.Message}", "保存失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopySelectionAndClose()
    {
        if (_finished)
        {
            return;
        }

        try
        {
            using var image = RenderSelection();
            _clipboardService.SetImage(image);
            _finished = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"复制截图失败：{exception.Message}", "复制失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DrawScreenHints(Graphics graphics)
    {
        const string primary = "移动鼠标智能选择窗口";
        const string secondary = "单击选择窗口   ·   拖动自由框选   ·   Ctrl+A 全选元素/全屏   ·   Esc 取消";
        using var primaryFont = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
        using var secondaryFont = new Font("Microsoft YaHei UI", 10F);
        using var textBrush = new SolidBrush(Color.White);
        using var hintBrush = new SolidBrush(Color.FromArgb(220, 226, 232, 240));

        var primarySize = graphics.MeasureString(primary, primaryFont);
        var secondarySize = graphics.MeasureString(secondary, secondaryFont);

        foreach (var screen in Screen.AllScreens)
        {
            var localBounds = new Rectangle(
                screen.Bounds.X - _snapshot.Bounds.X,
                screen.Bounds.Y - _snapshot.Bounds.Y,
                screen.Bounds.Width,
                screen.Bounds.Height);
            var centerX = localBounds.Left + localBounds.Width / 2F;
            var centerY = localBounds.Top + localBounds.Height / 2F;
            graphics.DrawString(primary, primaryFont, textBrush,
                centerX - primarySize.Width / 2, centerY - 28);
            graphics.DrawString(secondary, secondaryFont, hintBrush,
                centerX - secondarySize.Width / 2, centerY + 12);
        }
    }

    private void DrawSizeBadge(Graphics graphics, Rectangle selection)
    {
        var captureSize = _replacementCaptureImage?.Size ?? selection.Size;
        var text = $"{captureSize.Width} × {captureSize.Height}";
        using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
        var badge = GetSizeBadgeBounds(selection, text, font);
        using var background = new SolidBrush(Color.FromArgb(205, 15, 23, 42));
        using var brush = new SolidBrush(Color.White);
        graphics.FillRectangle(background, badge);
        graphics.DrawString(text, font, brush, badge.X + 5, badge.Y + 2);
    }

    private Rectangle GetSizeBadgeBounds(Rectangle selection, string text, Font font)
    {
        var textSize = TextRenderer.MeasureText(
            text,
            font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return LongCaptureEditorFrameLayout.GetSizeBadgeBounds(
            selection,
            textSize,
            ClientRectangle);
    }

    private bool IsReplacementFrameMoveHandle(Point pointer)
    {
        if (_replacementCaptureImage is null)
        {
            return false;
        }

        var text = $"{_replacementCaptureImage.Width} × {_replacementCaptureImage.Height}";
        using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
        var badge = GetSizeBadgeBounds(_selection, text, font);
        return LongCaptureEditorFrameLayout.IsMoveHandle(
            _selection,
            badge,
            pointer,
            Math.Max(5, DeviceDpi * 5 / 96));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _featureSession.Dispose();
            _annotationEditor.Dispose();
            _toolTip.Dispose();
            _replacementCaptureImage?.Dispose();
            _dimmedImage.Dispose();
        }
        base.Dispose(disposing);
    }

    bool ICaptureFeatureHost.HasSelection => _hasSelection;

    Rectangle ICaptureFeatureHost.Selection => _selection;

    Point ICaptureFeatureHost.CursorClientPosition => PointToClient(Cursor.Position);

    int ICaptureFeatureHost.Dpi => DeviceDpi;

    bool ICaptureFeatureHost.GetBooleanPreference(string id, bool defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _featurePreferences.TryGetValue(id, out var value)
            ? value
            : defaultValue;
    }

    bool ILiveCaptureFeatureHost.HasEdits =>
        _document.Count > 0 || _textEditor is not null || _replacementCaptureImage is not null;

    Rectangle ILiveCaptureFeatureHost.SelectionScreenBounds => new(
        _snapshot.Bounds.X + _selection.X,
        _snapshot.Bounds.Y + _selection.Y,
        _selection.Width,
        _selection.Height);

    void ICaptureFeatureHost.InvalidateAll() => Invalidate();

    void ICaptureFeatureHost.Invalidate(Rectangle bounds) => Invalidate(bounds, false);

    void ICaptureFeatureHost.SetCursor(Cursor cursor) => Cursor = cursor;

    void ICaptureFeatureHost.SetMouseCapture(bool capture) => Capture = capture;

    Bitmap ICaptureFeatureHost.CopyDesktopSelection()
    {
        if (!_hasSelection || _selection.IsEmpty)
        {
            throw new InvalidOperationException("尚未确认截图选区。");
        }

        var bitmap = new Bitmap(_selection.Width, _selection.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(_snapshot.Image,
            new Rectangle(Point.Empty, _selection.Size),
            _selection,
            GraphicsUnit.Pixel);
        return bitmap;
    }

    void ILiveCaptureFeatureHost.SetOverlayVisible(bool visible)
    {
        if (visible)
        {
            if (_liveCaptureOverlayParked)
            {
                if (!RestoreLiveCaptureOverlay())
                {
                    throw new InvalidOperationException(
                        "无法把截图遮罩恢复到原始虚拟桌面位置，请重新开始截图。");
                }
                return;
            }

            TopMost = true;
            Activate();
            PositionToolbar();
            _toolbar.Visible = _hasSelection;
            Invalidate();
        }
        else
        {
            _toolbar.Visible = false;
            Capture = false;
            TopMost = false;
            if (!_liveCaptureOverlayParked)
            {
                _liveCaptureOverlayParked = true;
                Bounds = GetLiveCaptureParkingBounds(_snapshot.Bounds);
                if (!Rectangle.Intersect(Bounds, _snapshot.Bounds).IsEmpty)
                {
                    var restored = RestoreLiveCaptureOverlay();
                    throw new InvalidOperationException(
                        restored
                            ? "无法把截图遮罩安全移出虚拟桌面，长截图已停止。"
                            : "截图遮罩无法安全移出或恢复，长截图已停止。");
                }
            }
        }
    }

    private bool RestoreLiveCaptureOverlay()
    {
        Bounds = _snapshot.Bounds;
        if (Bounds != _snapshot.Bounds)
        {
            SetBounds(
                _snapshot.Bounds.X,
                _snapshot.Bounds.Y,
                _snapshot.Bounds.Width,
                _snapshot.Bounds.Height,
                BoundsSpecified.All);
        }

        _liveCaptureOverlayParked = false;
        TopMost = true;
        Activate();
        PositionToolbar();
        _toolbar.Visible = _hasSelection;
        Invalidate();
        return Bounds == _snapshot.Bounds;
    }

    internal static Rectangle GetLiveCaptureParkingBounds(Rectangle virtualScreenBounds)
    {
        const int margin = 64;
        var parkedX = (int)Math.Min(
            int.MaxValue - Math.Max(1, virtualScreenBounds.Width),
            (long)virtualScreenBounds.Right + margin);
        var parkedY = (int)Math.Min(
            int.MaxValue - Math.Max(1, virtualScreenBounds.Height),
            (long)virtualScreenBounds.Bottom + margin);
        return new Rectangle(parkedX, parkedY, virtualScreenBounds.Width, virtualScreenBounds.Height);
    }

    Bitmap ILiveCaptureFeatureHost.CaptureLiveSelection()
    {
        if (!_hasSelection || _selection.IsEmpty)
        {
            throw new InvalidOperationException("尚未确认截图选区。");
        }

        var screenBounds = ((ILiveCaptureFeatureHost)this).SelectionScreenBounds;
        var bitmap = new Bitmap(
            screenBounds.Width,
            screenBounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CopyFromScreen(
                screenBounds.Location,
                Point.Empty,
                screenBounds.Size,
                CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    void ILiveCaptureFeatureHost.ReplaceCaptureResult(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentException("截图结果尺寸无效。", nameof(image));
        }

        CancelTextEditor(commit: false);
        ClearAnnotations();
        SelectTool(EditorTool.None);
        _replacementCaptureImage?.Dispose();
        _replacementCaptureImage = image;
        _replacementZoom = CaptureEditorViewportLayout.CalculateWidthFitZoom(
            _selection.Size,
            image.Size);
        _replacementScroll = Point.Empty;
        Text = "轻截 - 长截图编辑";
        PositionToolbar();
        _toolbar.Visible = true;
        Invalidate();
    }

}
