using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Presentation;

internal sealed class LiveAnnotationSessionFactory(
    IClipboardService clipboardService,
    DrawingToolCoefficients drawingToolCoefficients,
    int rotationStepDegrees,
    DrawingCursorShape drawingCursorShape,
    bool annotationSnappingEnabled = AnnotationLayoutOptions.DefaultSnappingEnabled,
    int annotationSnapThresholdPixels = AnnotationLayoutOptions.DefaultSnapThresholdPixels,
    int ctrlDragStepPixels = AnnotationLayoutOptions.DefaultCtrlDragStepPixels,
    AnnotationMoveActivationMode annotationMoveActivationMode =
        AnnotationMoveActivationMode.HoldAlt)
{
    public ICaptureAnnotationSession Create(
        Rectangle screenBounds,
        ToolWidthController widthController,
        Color initialColor,
        Action<Color> colorChanged,
        CaptureAnnotationSessionOptions? options = null)
    {
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(screenBounds));
        }

        options ??= new CaptureAnnotationSessionOptions();
        var source = CaptureSource(screenBounds);
        try
        {
            return new LiveAnnotationSessionForm(
                screenBounds,
                source,
                clipboardService,
                widthController,
                drawingToolCoefficients,
                rotationStepDegrees,
                drawingCursorShape,
                initialColor,
                colorChanged,
                annotationSnappingEnabled,
                annotationSnapThresholdPixels,
                ctrlDragStepPixels,
                ToRecordingRegionIndicatorStyle(options.RegionIndicatorStyle),
                options.ShowMouseClickIndicator,
                annotationMoveActivationMode);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private static RecordingRegionIndicatorStyle ToRecordingRegionIndicatorStyle(
        CaptureRegionIndicatorStyle style) => style switch
        {
            CaptureRegionIndicatorStyle.Solid => RecordingRegionIndicatorStyle.Solid,
            CaptureRegionIndicatorStyle.None => RecordingRegionIndicatorStyle.None,
            _ => RecordingRegionIndicatorStyle.Dashed
        };

    private static Bitmap CaptureSource(Rectangle screenBounds)
    {
        var source = new Bitmap(
            screenBounds.Width,
            screenBounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(source);
            graphics.CopyFromScreen(
                screenBounds.Location,
                Point.Empty,
                screenBounds.Size,
                CopyPixelOperation.SourceCopy);
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }
}

internal sealed class LiveAnnotationSessionForm : Form, ICaptureAnnotationToolbarSession
{
    private const int ExtendedStyleIndex = -20;
    private const nint TransparentStyle = 0x20;
    private const nint NoActivateStyle = 0x08000000;
    private static readonly Color TransparentColor = Color.FromArgb(1, 0, 1);
    private static readonly IReadOnlyList<CaptureAnnotationToolDefinition> ToolDefinitions =
    [
        .. CaptureEditorToolCatalog.Tools.Select(tool => new CaptureAnnotationToolDefinition(
            ToContractTool(tool.Tool),
            tool.Text,
            tool.ToolTip,
            tool.Width))
    ];

    private readonly Bitmap _source;
    private readonly Bitmap _inputSurface;
    private readonly IClipboardService _clipboardService;
    private readonly ToolWidthController _widthController;
    private readonly CaptureAnnotationEditor _editor;
    private readonly DrawingCursorIndicator _drawingCursorIndicator;
    private readonly LiveAnnotationPointerHook _pointerHook;
    private readonly LiveAnnotationPointerHook _clickPreviewPointerHook;
    private readonly RecordingMouseClickIndicatorForm _mouseClickIndicator;
    private readonly RecordingSelectionMarqueeForm _selectionMarqueeFill;
    private readonly int _rotationStepDegrees;
    private readonly int _annotationSnapThresholdPixels;
    private readonly int _ctrlDragStepPixels;
    private readonly AnnotationMoveActivationState _annotationMoveActivationState;
    private readonly RecordingRegionIndicatorStyle _recordingRegionIndicatorStyle;
    private readonly bool _showMouseClickIndicator;
    private readonly ControlDoubleTapDetector _controlDoubleTapDetector = new();
    private readonly Action<Color> _colorChanged;
    private readonly LiveAnnotationContentForm _content;
    private readonly CaptureEditorToolbar _toolbar;
    private readonly CaptureEditorToolbarWindow _toolbarWindow;
    private readonly Dictionary<string, Button> _toolbarCommandButtons =
        new(StringComparer.Ordinal);
    private readonly List<Point> _draftPoints = [];
    private readonly Dictionary<MovableAnnotation, Rectangle> _movableOriginBounds = [];
    private readonly List<Rectangle> _stationaryMovableBounds = [];

    private CaptureAnnotationTool _activeTool = CaptureAnnotationTool.Operation;
    private Color _color;
    private Point _startPoint;
    private Point _currentPoint;
    private bool _isDrawing;
    private bool _isPendingMarquee;
    private bool _isSelectingMarquee;
    private Point _marqueeStart;
    private Rectangle _marqueeBounds;
    private StickerHitTarget _activeMovableTarget;
    private Point _movableDragOrigin;
    private Rectangle _movableGroupOriginBounds;
    private MovableAnnotation? _controlClickToggleCandidate;
    private bool _movableInteractionDidDrag;
    private TransparentTextEditorControl? _textEditor;
    private MovableAnnotation? _textDoubleClickCandidate;
    private Point _textDoubleClickPoint;
    private long _textDoubleClickAt;
    private bool _closing;
    private Label? _toolbarStatusLabel;
    private Point _hookLastLeftDownPoint;
    private long _hookLastLeftDownAt;
    private bool _annotationSnappingEnabled;

    public LiveAnnotationSessionForm(
        Rectangle screenBounds,
        Bitmap source,
        IClipboardService clipboardService,
        ToolWidthController widthController,
        DrawingToolCoefficients drawingToolCoefficients,
        int rotationStepDegrees,
        DrawingCursorShape drawingCursorShape,
        Color initialColor,
        Action<Color> colorChanged,
        bool annotationSnappingEnabled = AnnotationLayoutOptions.DefaultSnappingEnabled,
        int annotationSnapThresholdPixels = AnnotationLayoutOptions.DefaultSnapThresholdPixels,
        int ctrlDragStepPixels = AnnotationLayoutOptions.DefaultCtrlDragStepPixels,
        RecordingRegionIndicatorStyle recordingRegionIndicatorStyle =
            RecordingRegionIndicatorStyle.Dashed,
        bool showMouseClickIndicator = true,
        AnnotationMoveActivationMode annotationMoveActivationMode =
            AnnotationMoveActivationMode.HoldAlt)
    {
        _source = source;
        _inputSurface = CreateTransparentSurface(screenBounds.Size);
        _clipboardService = clipboardService;
        _widthController = widthController;
        _editor = new CaptureAnnotationEditor(drawingToolCoefficients);
        _drawingCursorIndicator = new DrawingCursorIndicator(drawingCursorShape);
        _pointerHook = new LiveAnnotationPointerHook(HandlePointerHookEvent);
        _clickPreviewPointerHook = new LiveAnnotationPointerHook(
            HandleClickPreviewPointerHookEvent);
        _mouseClickIndicator = new RecordingMouseClickIndicatorForm();
        _selectionMarqueeFill = new RecordingSelectionMarqueeForm();
        _rotationStepDegrees = AnnotationRotationStep.Normalize(rotationStepDegrees);
        _annotationSnappingEnabled = annotationSnappingEnabled;
        _annotationSnapThresholdPixels = AnnotationLayoutOptions.NormalizeSnapThreshold(
            annotationSnapThresholdPixels);
        _ctrlDragStepPixels = AnnotationLayoutOptions.NormalizeCtrlDragStep(ctrlDragStepPixels);
        _annotationMoveActivationState = new AnnotationMoveActivationState(
            annotationMoveActivationMode);
        _recordingRegionIndicatorStyle = Enum.IsDefined(recordingRegionIndicatorStyle)
            ? recordingRegionIndicatorStyle
            : RecordingRegionIndicatorStyle.Dashed;
        _showMouseClickIndicator = showMouseClickIndicator;
        _color = initialColor;
        _colorChanged = colorChanged;

        Text = "轻截核心批注输入层";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screenBounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = TransparentColor;
        TransparencyKey = TransparentColor;
        AllowTransparency = true;
        DoubleBuffered = false;
        KeyPreview = true;
        Cursor = Cursors.Default;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.StandardClick |
            ControlStyles.StandardDoubleClick,
            true);

        _content = new LiveAnnotationContentForm(screenBounds, RenderContent);
        _toolbar = new CaptureEditorToolbar(
            ToolDefinitions,
            CaptureEditorToolCatalog.Palette,
            activeTool: null,
            initialColor,
            widthController.Current,
            widthController.Range.Minimum,
            widthController.Range.Maximum,
            _annotationSnappingEnabled);
        _toolbar.ToolClicked += tool =>
        {
            ActiveTool = ActiveTool == tool ? CaptureAnnotationTool.Operation : tool;
            if (ActiveTool != CaptureAnnotationTool.Operation)
            {
                BringToFrontForEditing();
            }
        };
        _toolbar.UndoClicked += () => Undo();
        _toolbar.WidthCycleRequested += () =>
        {
            if (CycleWidth())
            {
                _toolbar.SetToolWidth(ToolWidth);
            }
        };
        _toolbar.WidthButton.MouseWheel += (_, e) =>
        {
            var steps = e.Delta / SystemInformation.MouseWheelScrollDelta;
            if (steps == 0)
            {
                steps = Math.Sign(e.Delta);
            }
            if (AdjustWidth(steps))
            {
                _toolbar.SetToolWidth(ToolWidth);
            }
        };
        _toolbar.ColorClicked += color => Color = color;
        _toolbar.SnappingToggleRequested += ToggleAnnotationSnapping;
        _toolbarWindow = new CaptureEditorToolbarWindow(_toolbar, screenBounds);
        _toolbarWindow.Owner = this;
        _toolbarWindow.KeyDown += HandleKeyDown;
        _toolbarWindow.KeyUp += HandleKeyUp;
        Shown += HandleShown;
        FormClosing += HandleFormClosing;
        MouseDown += HandleMouseDown;
        MouseDoubleClick += HandleMouseDoubleClick;
        MouseMove += HandleMouseMove;
        MouseUp += HandleMouseUp;
        MouseWheel += HandleMouseWheel;
        MouseLeave += (_, _) => HideDrawingCursorIndicator();
        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;
    }

    public IReadOnlyList<CaptureAnnotationToolDefinition> Tools => ToolDefinitions;

    public IReadOnlyList<Color> Palette => CaptureEditorToolCatalog.Palette;

    public event EventHandler<CaptureAnnotationToolbarCommandEventArgs>? ToolbarCommandInvoked;

    public CaptureAnnotationTool ActiveTool
    {
        get => _activeTool;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            CancelTextEditor(commit: true);
            CancelPointerOperation();
            if (_activeTool != value)
            {
                _editor.ResetHitCycle();
            }
            _activeTool = value;
            if (value == CaptureAnnotationTool.Operation || IsDrawingTool(value))
            {
                _editor.Selection.Clear();
            }
            HideDrawingCursorIndicator();
            Cursor = value switch
            {
                CaptureAnnotationTool.Operation or CaptureAnnotationTool.Select => Cursors.Default,
                CaptureAnnotationTool.Pen or CaptureAnnotationTool.Mosaic => Cursors.Default,
                _ => Cursors.Cross
            };
            UpdateInputMode();
            if (_toolbarWindow.Visible)
            {
                _toolbarWindow.BringToFront();
            }
            _toolbar.SetActiveTool(value == CaptureAnnotationTool.Operation ? null : value);
            InvalidateLayers();
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            _colorChanged(value);
            _toolbar.SetSelectedColor(value);
        }
    }

    public int ToolWidth => AnnotationWidth;

    public int MinimumWidth => _widthController.Range.Minimum;

    public int MaximumWidth => _widthController.Range.Maximum;

    public int AnnotationCount => _editor.Document.Count;

    internal CaptureAnnotationEditor Editor => _editor;

    internal Rectangle ToolbarBounds => _toolbarWindow.Bounds;

    internal CaptureEditorToolbar Toolbar => _toolbar;

    internal Form ToolbarWindow => _toolbarWindow;

    internal bool PointerHookStarted => _pointerHook.IsStarted;

    internal bool ClickPreviewHookStarted => _clickPreviewPointerHook.IsStarted;

    internal bool MouseClickIndicatorVisible => _mouseClickIndicator.Visible;

    internal bool MouseClickIndicatorPressed => _mouseClickIndicator.IsPressed;

    internal Point MouseClickIndicatorCenter => new(
        _mouseClickIndicator.Left + _mouseClickIndicator.Width / 2,
        _mouseClickIndicator.Top + _mouseClickIndicator.Height / 2);

    internal bool MarqueeFillVisible => _selectionMarqueeFill.Visible;

    internal double MarqueeFillOpacity => _selectionMarqueeFill.Opacity;

    internal bool IsSelectingMarquee => _isSelectingMarquee;

    internal Rectangle MarqueeBounds => _marqueeBounds;

    internal Rectangle LastContentInvalidationBounds { get; private set; }

    internal Rectangle LastInputInvalidationBounds { get; private set; }

    protected override bool ShowWithoutActivation => ActiveTool == CaptureAnnotationTool.Operation;

    public bool AdjustWidth(int steps)
    {
        var changed = _widthController.Adjust(steps);
        if (changed)
        {
            _toolbar.SetToolWidth(ToolWidth);
            UpdateDrawingCursorIndicator(PointToClient(Cursor.Position));
        }
        return changed;
    }

    public bool CycleWidth()
    {
        var changed = _widthController.CyclePreset();
        if (changed)
        {
            _toolbar.SetToolWidth(ToolWidth);
            UpdateDrawingCursorIndicator(PointToClient(Cursor.Position));
        }
        return changed;
    }

    public bool Undo()
    {
        CancelTextEditor(commit: false);
        var changed = _editor.Undo();
        if (changed)
        {
            _activeMovableTarget = StickerHitTarget.None;
            InvalidateLayers();
        }
        return changed;
    }

    public void Clear()
    {
        CancelTextEditor(commit: false);
        CancelPointerOperation();
        _editor.Clear();
        InvalidateLayers();
    }

    public void BringToFrontForEditing()
    {
        TopMost = true;
        BringToFront();
        Activate();
        Focus();
        _toolbarWindow.BringToFront();
    }

    public void ConfigureToolbar(
        string? statusText,
        IReadOnlyList<CaptureAnnotationToolbarCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var commandSnapshot = commands.ToArray();
        var commandIds = new HashSet<string>(StringComparer.Ordinal);
        if (commandSnapshot.Any(command =>
                string.IsNullOrWhiteSpace(command.Id) || !commandIds.Add(command.Id)))
        {
            throw new ArgumentException(
                "批注工具栏命令 ID 必须非空且唯一。",
                nameof(commands));
        }
        RunOnUiThread(() =>
        {
            _toolbar.ClearExtensionControls();
            _toolbarCommandButtons.Clear();
            _toolbarStatusLabel = null;
            if (!string.IsNullOrWhiteSpace(statusText) || commandSnapshot.Length > 0)
            {
                _toolbar.AddExtensionSeparator();
            }
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                _toolbarStatusLabel = _toolbar.AddStatusLabel(statusText);
            }
            foreach (var command in commandSnapshot)
            {
                _toolbarCommandButtons.Add(
                    command.Id,
                    _toolbar.AddCommandButton(
                        command.Text,
                        Math.Max(42, command.Width),
                        command.ToolTip,
                        command.Style));
                var commandId = command.Id;
                _toolbarCommandButtons[commandId].Click += (_, _) =>
                    ToolbarCommandInvoked?.Invoke(
                        this,
                        new CaptureAnnotationToolbarCommandEventArgs(commandId));
            }
            _toolbarWindow.RefreshLayoutAndPosition();
        });
    }

    public void SetToolVisible(CaptureAnnotationTool tool, bool visible)
    {
        if (!Enum.IsDefined(tool) || tool == CaptureAnnotationTool.Operation)
        {
            throw new ArgumentOutOfRangeException(nameof(tool));
        }

        RunOnUiThread(() =>
        {
            if (!visible && ActiveTool == tool)
            {
                ActiveTool = CaptureAnnotationTool.Operation;
            }
            _toolbar.SetToolVisible(tool, visible);
            _toolbarWindow.RefreshLayoutAndPosition();
        });
    }

    public void UpdateToolbarStatus(string? statusText) => RunOnUiThread(() =>
    {
        if (_toolbarStatusLabel is not null)
        {
            _toolbarStatusLabel.Text = statusText ?? string.Empty;
        }
    });

    public void UpdateToolbarCommand(string commandId, string text, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        RunOnUiThread(() =>
        {
            if (!_toolbarCommandButtons.TryGetValue(commandId, out var button))
            {
                return;
            }
            button.Text = text;
            button.Enabled = enabled;
        });
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // The persistent surface already contains the transparent-key background.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var clip = Rectangle.Intersect(ClientRectangle, e.ClipRectangle);
        if (clip.IsEmpty)
        {
            return;
        }
        using (var surfaceGraphics = Graphics.FromImage(_inputSurface))
        {
            surfaceGraphics.SetClip(clip);
            surfaceGraphics.CompositingMode = CompositingMode.SourceCopy;
            using var background = new SolidBrush(TransparentColor);
            surfaceGraphics.FillRectangle(background, clip);
            surfaceGraphics.CompositingMode = CompositingMode.SourceOver;
            RecordingRegionIndicator.Draw(
                surfaceGraphics,
                ClientRectangle,
                _recordingRegionIndicatorStyle);
            surfaceGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            _editor.DrawSelection(surfaceGraphics, GetHandleSize());
            DrawMarquee(surfaceGraphics);
            _drawingCursorIndicator.Draw(surfaceGraphics);
        }
        e.Graphics.DrawImage(
            _inputSurface,
            clip,
            clip,
            GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _drawingCursorIndicator.Dispose();
            _pointerHook.Dispose();
            _clickPreviewPointerHook.Dispose();
            _mouseClickIndicator.Dispose();
            _selectionMarqueeFill.Dispose();
            _editor.Dispose();
            _inputSurface.Dispose();
            _source.Dispose();
            _content.Dispose();
            _toolbarWindow.Dispose();
        }
        base.Dispose(disposing);
    }

    private void HandleShown(object? sender, EventArgs e)
    {
        _content.Show();
        _toolbarWindow.Show();
        BringToFront();
        _toolbarWindow.BringToFront();
        WindowCaptureProtection.TryExclude(this);
        UpdateInputMode();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _pointerHook.Stop();
        _clickPreviewPointerHook.Stop();
        _mouseClickIndicator.Close();
        _selectionMarqueeFill.Close();
        CancelTextEditor(commit: true);
        _toolbarWindow.Close();
        _content.Close();
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed)
        {
            return;
        }
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }
        action();
    }

    internal void RenderContent(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        _editor.Render(graphics, _source);
        if (!_isDrawing)
        {
            return;
        }

        var draft = BuildDraft();
        draft?.Render(graphics, _source);
        draft?.Dispose();
    }

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left ||
            (ActiveTool == CaptureAnnotationTool.Operation && !IsAnnotationMoveActive()))
        {
            return;
        }

        if (IsAltPressed())
        {
            _annotationMoveActivationState.MarkAltUsedAsModifier();
        }

        TrackTextDoubleClickCandidate(e.Location);
        CancelTextEditor(commit: true);

        if (ActiveTool == CaptureAnnotationTool.Select || IsAnnotationMoveActive())
        {
            var controlPressed = IsControlPressed();
            if (controlPressed)
            {
                _controlDoubleTapDetector.CancelCurrentTap();
            }
            _controlClickToggleCandidate = controlPressed
                ? FindSelectedMovableHit(e.Location)
                : null;
            if (TryBeginMovableInteraction(
                e.Location,
                controlPressed && _controlClickToggleCandidate is null))
            {
                return;
            }
            if (ActiveTool == CaptureAnnotationTool.Select)
            {
                BeginMarquee(e.Location);
                return;
            }
        }

        if (ActiveTool == CaptureAnnotationTool.Text)
        {
            BeginTextEditor(e.Location);
            return;
        }

        var editorTool = ToEditorTool(ActiveTool);
        if (editorTool == EditorTool.None)
        {
            return;
        }

        _editor.Selection.Clear();
        _isDrawing = true;
        _startPoint = ClampPoint(e.Location);
        _currentPoint = _startPoint;
        _draftPoints.Clear();
        _draftPoints.Add(_startPoint);
        Capture = true;
        InvalidateLayers();
    }

    internal bool HandlePointerHookEvent(LiveAnnotationPointerEvent pointerEvent)
    {
        if (_closing || IsDisposed)
        {
            return false;
        }

        var clientPoint = PointToClient(pointerEvent.ScreenLocation);
        if (_showMouseClickIndicator &&
            pointerEvent.Kind == LiveAnnotationPointerEventKind.Move &&
            _mouseClickIndicator.IsPressed)
        {
            _mouseClickIndicator.MoveClick(pointerEvent.ScreenLocation);
        }
        if (_showMouseClickIndicator &&
            pointerEvent.Kind == LiveAnnotationPointerEventKind.LeftUp)
        {
            _mouseClickIndicator.EndClick();
        }
        if (ActiveTool == CaptureAnnotationTool.Operation)
        {
            return false;
        }

        var pointerOperationActive = Capture ||
            _isDrawing ||
            _isPendingMarquee ||
            _isSelectingMarquee ||
            _activeMovableTarget != StickerHitTarget.None;
        if (!pointerOperationActive &&
            _toolbarWindow.Bounds.Contains(pointerEvent.ScreenLocation))
        {
            // When a large recording selection contains the toolbar, the low-level hook
            // must leave toolbar clicks to WinForms instead of turning them into annotations.
            return false;
        }

        if (_textEditor is not null &&
            RectangleToScreen(_textEditor.Bounds).Contains(pointerEvent.ScreenLocation))
        {
            return false;
        }

        if (_showMouseClickIndicator &&
            pointerEvent.Kind == LiveAnnotationPointerEventKind.LeftDown &&
            ClientRectangle.Contains(clientPoint))
        {
            _mouseClickIndicator.BeginClick(pointerEvent.ScreenLocation);
        }

        if (!ClientRectangle.Contains(clientPoint) && !pointerOperationActive)
        {
            return false;
        }

        switch (pointerEvent.Kind)
        {
            case LiveAnnotationPointerEventKind.Move:
                HandleMouseMove(
                    this,
                    new MouseEventArgs(MouseButtons.None, 0, clientPoint.X, clientPoint.Y, 0));
                // The hook observes movement for drawing, but must let Windows move the
                // physical cursor. Button presses remain consumed so the app below is not clicked.
                return false;
            case LiveAnnotationPointerEventKind.LeftDown:
                {
                    var now = Environment.TickCount64;
                    var doubleClick = now - _hookLastLeftDownAt <= SystemInformation.DoubleClickTime &&
                        IsWithinDoubleClickRegion(_hookLastLeftDownPoint, clientPoint);
                    HandleMouseDown(
                        this,
                        new MouseEventArgs(MouseButtons.Left, doubleClick ? 2 : 1, clientPoint.X, clientPoint.Y, 0));
                    if (doubleClick)
                    {
                        HandleMouseDoubleClick(
                            this,
                            new MouseEventArgs(MouseButtons.Left, 2, clientPoint.X, clientPoint.Y, 0));
                        _hookLastLeftDownAt = 0;
                    }
                    else
                    {
                        _hookLastLeftDownPoint = clientPoint;
                        _hookLastLeftDownAt = now;
                    }
                    return true;
                }
            case LiveAnnotationPointerEventKind.LeftUp:
                HandleMouseUp(
                    this,
                    new MouseEventArgs(MouseButtons.Left, 1, clientPoint.X, clientPoint.Y, 0));
                return true;
            case LiveAnnotationPointerEventKind.Wheel:
                HandleMouseWheel(
                    this,
                    new MouseEventArgs(
                        MouseButtons.None,
                        0,
                        clientPoint.X,
                        clientPoint.Y,
                        pointerEvent.WheelDelta));
                return true;
            case LiveAnnotationPointerEventKind.RightDown:
            case LiveAnnotationPointerEventKind.RightUp:
            case LiveAnnotationPointerEventKind.MiddleDown:
            case LiveAnnotationPointerEventKind.MiddleUp:
            case LiveAnnotationPointerEventKind.XButtonDown:
            case LiveAnnotationPointerEventKind.XButtonUp:
            case LiveAnnotationPointerEventKind.HorizontalWheel:
                return ActiveTool == CaptureAnnotationTool.Select;
            default:
                return false;
        }
    }

    private bool HandleClickPreviewPointerHookEvent(LiveAnnotationPointerEvent pointerEvent)
    {
        if (_closing || IsDisposed || !_showMouseClickIndicator)
        {
            return false;
        }

        if (pointerEvent.Kind == LiveAnnotationPointerEventKind.Move)
        {
            _mouseClickIndicator.MoveClick(pointerEvent.ScreenLocation);
            return false;
        }

        if (pointerEvent.Kind == LiveAnnotationPointerEventKind.LeftUp)
        {
            _mouseClickIndicator.EndClick();
            return false;
        }
        if (pointerEvent.Kind != LiveAnnotationPointerEventKind.LeftDown)
        {
            return false;
        }

        var clientPoint = PointToClient(pointerEvent.ScreenLocation);
        if (ClientRectangle.Contains(clientPoint) &&
            !_toolbarWindow.Bounds.Contains(pointerEvent.ScreenLocation) &&
            (_textEditor is null ||
             !RectangleToScreen(_textEditor.Bounds).Contains(pointerEvent.ScreenLocation)))
        {
            _mouseClickIndicator.BeginClick(pointerEvent.ScreenLocation);
        }
        return false;
    }

    private void HandleMouseMove(object? sender, MouseEventArgs e)
    {
        var point = ClampPoint(e.Location);
        if (_activeMovableTarget != StickerHitTarget.None &&
            _editor.Selection.Primary is { } activeMovable)
        {
            var previousDirtyBounds = GetSelectionInvalidationBounds();
            var pointerOffset = new Point(
                point.X - _movableDragOrigin.X,
                point.Y - _movableDragOrigin.Y);
            var controlStepActive = IsControlPressed();
            _movableInteractionDidDrag |= IsManualDrag(_movableDragOrigin, point);
            if (controlStepActive)
            {
                pointerOffset = AnnotationAlignment.QuantizeOffset(
                    pointerOffset,
                    _ctrlDragStepPixels);
            }
            if (_activeMovableTarget == StickerHitTarget.Move)
            {
                var actualOffset = GroupMoveLayout.ClampOffset(
                    _movableGroupOriginBounds,
                    pointerOffset,
                    ClientRectangle);
                if (_annotationSnappingEnabled && !controlStepActive)
                {
                    actualOffset = AnnotationAlignment.SnapMoveOffset(
                        _movableGroupOriginBounds,
                        actualOffset,
                        _stationaryMovableBounds,
                        _annotationSnapThresholdPixels);
                    actualOffset = GroupMoveLayout.ClampOffset(
                        _movableGroupOriginBounds,
                        actualOffset,
                        ClientRectangle);
                }
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
                ResizeMovable(activeMovable, origin, point);
            }
            InvalidateLayers(UnionDirtyBounds(
                previousDirtyBounds,
                GetSelectionInvalidationBounds()));
            return;
        }

        if (_isPendingMarquee)
        {
            if (IsManualDrag(_marqueeStart, point))
            {
                var previousSelectionBounds = GetSelectionInvalidationBounds();
                _isPendingMarquee = false;
                _isSelectingMarquee = true;
                _editor.Selection.Clear();
                _marqueeBounds = Geometry.Normalize(_marqueeStart, point);
                UpdateMarqueeFill();
                InvalidateInput(UnionDirtyBounds(
                    previousSelectionBounds,
                    GetMarqueeInvalidationBounds()));
            }
            return;
        }

        if (_isSelectingMarquee)
        {
            var previousMarqueeBounds = GetMarqueeInvalidationBounds();
            _marqueeBounds = Geometry.Normalize(_marqueeStart, point);
            UpdateMarqueeFill();
            InvalidateInput(UnionDirtyBounds(
                previousMarqueeBounds,
                GetMarqueeInvalidationBounds()));
            return;
        }

        if (_isDrawing)
        {
            var previousPoint = _currentPoint;
            var previousDraftBounds = IsFreehandTool(ActiveTool)
                ? Rectangle.Empty
                : GetDraftInvalidationBounds();
            _currentPoint = point;
            if (ActiveTool is CaptureAnnotationTool.Pen or CaptureAnnotationTool.Mosaic &&
                (_draftPoints.Count == 0 || _draftPoints[^1] != point))
            {
                _draftPoints.Add(point);
            }
            var dirtyBounds = IsFreehandTool(ActiveTool)
                ? GetFreehandSegmentInvalidationBounds(previousPoint, point)
                : UnionDirtyBounds(previousDraftBounds, GetDraftInvalidationBounds());
            InvalidateContent(dirtyBounds);
            return;
        }

        UpdateIdleCursor(e.Location);
    }

    private void HandleMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Capture = false;
        if (_activeMovableTarget != StickerHitTarget.None)
        {
            var previousSelectionBounds = GetSelectionInvalidationBounds();
            _activeMovableTarget = StickerHitTarget.None;
            if (!_movableInteractionDidDrag && _controlClickToggleCandidate is { } toggleCandidate)
            {
                _editor.Selection.Remove(toggleCandidate);
            }
            _controlClickToggleCandidate = null;
            _movableInteractionDidDrag = false;
            _movableOriginBounds.Clear();
            _stationaryMovableBounds.Clear();
            UpdateIdleCursor(e.Location);
            InvalidateInput(UnionDirtyBounds(
                previousSelectionBounds,
                GetSelectionInvalidationBounds()));
            return;
        }

        if (_isPendingMarquee)
        {
            var previousSelectionBounds = GetSelectionInvalidationBounds();
            _isPendingMarquee = false;
            var movable = _editor.FindNextMovableAt(
                _marqueeStart,
                GetHitTolerance(),
                GetHitTolerance());
            if (movable is null)
            {
                _editor.Selection.Clear();
            }
            else
            {
                _editor.Selection.SelectOnly(movable);
            }
            InvalidateInput(UnionDirtyBounds(
                previousSelectionBounds,
                GetSelectionInvalidationBounds()));
            return;
        }

        if (_isSelectingMarquee)
        {
            var previousDirtyBounds = UnionDirtyBounds(
                GetMarqueeInvalidationBounds(),
                GetSelectionInvalidationBounds());
            _isSelectingMarquee = false;
            _selectionMarqueeFill.HideMarquee();
            _editor.SelectIntersecting(_marqueeBounds);
            _marqueeBounds = Rectangle.Empty;
            InvalidateInput(UnionDirtyBounds(
                previousDirtyBounds,
                GetSelectionInvalidationBounds()));
            return;
        }

        if (!_isDrawing)
        {
            return;
        }

        var previousPoint = _currentPoint;
        var previousDraftBounds = IsFreehandTool(ActiveTool)
            ? Rectangle.Empty
            : GetDraftInvalidationBounds();
        _currentPoint = ClampPoint(e.Location);
        if (IsFreehandTool(ActiveTool) &&
            (_draftPoints.Count == 0 || _draftPoints[^1] != _currentPoint))
        {
            _draftPoints.Add(_currentPoint);
        }
        var finalDraftBounds = GetDraftInvalidationBounds();
        _isDrawing = false;
        _editor.AddDraft(
            ToEditorTool(ActiveTool),
            _startPoint,
            _currentPoint,
            _draftPoints,
            Color,
            AnnotationWidth);
        _draftPoints.Clear();
        InvalidateContent(IsFreehandTool(ActiveTool)
            ? GetFreehandSegmentInvalidationBounds(previousPoint, _currentPoint)
            : UnionDirtyBounds(previousDraftBounds, finalDraftBounds));
    }

    private void HandleMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _textEditor is not null)
        {
            return;
        }

        var candidate = _textDoubleClickCandidate;
        _textDoubleClickCandidate = null;
        if (candidate is not null &&
            _editor.Document.Contains(candidate) &&
            candidate.HitTest(e.Location, GetHitTolerance()))
        {
            BeginExistingTextEditor(candidate);
        }
    }

    private void HandleMouseWheel(object? sender, MouseEventArgs e)
    {
        if (IsControlPressed())
        {
            _controlDoubleTapDetector.CancelCurrentTap();
            ScaleAnnotationUnderPointer(e);
        }
        else if (IsAltPressed())
        {
            _annotationMoveActivationState.MarkAltUsedAsModifier();
            RotateAnnotationUnderPointer(e);
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        _controlDoubleTapDetector.RegisterKeyDown(e.KeyCode, Environment.TickCount64);

        if (IsAltKey(e.KeyCode))
        {
            _annotationMoveActivationState.HandleAltKeyDown();
        }
        else if (e.Alt)
        {
            _annotationMoveActivationState.MarkAltUsedAsModifier();
        }

        if (IsAltKey(e.KeyCode) || IsControlKey(e.KeyCode))
        {
            UpdateIdleCursor(PointToClient(Cursor.Position));
        }

        if (_textEditor is not null)
        {
            if (e.Control && !e.Alt && e.KeyCode == Keys.Z)
            {
                _textEditor.UndoTextChange();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelTextEditor(commit: true);
                e.SuppressKeyPress = true;
            }
            return;
        }

        if (!e.Control && !e.Alt && !e.Shift && TryResolveShortcut(e.KeyCode, out var tool))
        {
            ActiveTool = ActiveTool == tool ? CaptureAnnotationTool.Operation : tool;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            _editor.SelectAll();
            ActiveTool = CaptureAnnotationTool.Select;
            InvalidateLayers();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.V)
        {
            PasteClipboardContent();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            HandleEscape();
            e.SuppressKeyPress = true;
        }
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        if (_controlDoubleTapDetector.RegisterKeyUp(e.KeyCode, Environment.TickCount64))
        {
            ToggleAnnotationSnapping();
            e.SuppressKeyPress = true;
        }
        if (IsAltKey(e.KeyCode))
        {
            _annotationMoveActivationState.HandleAltKeyUp();
        }
        if (IsAltKey(e.KeyCode) || IsControlKey(e.KeyCode))
        {
            UpdateIdleCursor(PointToClient(Cursor.Position));
        }
    }

    private bool TryBeginMovableInteraction(Point point, bool multiSelect)
    {
        var previousSelectionBounds = GetSelectionInvalidationBounds();
        var selected = _editor.Selection.Count == 1 ? _editor.Selection.Primary : null;
        var target = selected is null
            ? StickerHitTarget.None
            : HitTestMovable(selected, point);
        var movable = selected;
        if (target == StickerHitTarget.None)
        {
            movable = _editor.FindTopMovableAt(point, GetHitTolerance());
            target = movable is null ? StickerHitTarget.None : StickerHitTarget.Move;
        }
        if (movable is null)
        {
            return false;
        }

        if (multiSelect)
        {
            if (_editor.Selection.Contains(movable))
            {
                _editor.Selection.Remove(movable);
            }
            else
            {
                _editor.Selection.Add(movable);
            }
            InvalidateInput(UnionDirtyBounds(
                previousSelectionBounds,
                GetSelectionInvalidationBounds()));
            return true;
        }

        if (!_editor.Selection.Contains(movable))
        {
            _editor.Selection.SelectOnly(movable);
            target = StickerHitTarget.Move;
        }
        else if (_editor.Selection.Count > 1)
        {
            target = StickerHitTarget.Move;
        }

        if (target == StickerHitTarget.Move &&
            _editor.Selection.RequiresAltToMove &&
            ActiveTool != CaptureAnnotationTool.Select &&
            !IsAnnotationMoveActive() &&
            !IsControlPressed())
        {
            InvalidateInput(UnionDirtyBounds(
                previousSelectionBounds,
                GetSelectionInvalidationBounds()));
            return true;
        }

        _activeMovableTarget = target;
        _movableInteractionDidDrag = false;
        _movableDragOrigin = point;
        _movableOriginBounds.Clear();
        foreach (var annotation in _editor.Selection.Items)
        {
            _movableOriginBounds.Add(annotation, annotation.Bounds);
        }
        _stationaryMovableBounds.Clear();
        var moving = _movableOriginBounds.Keys.ToHashSet();
        _stationaryMovableBounds.AddRange(_editor.Document.GetMovableAnnotations()
            .Where(annotation => !moving.Contains(annotation))
            .Select(annotation => annotation.VisualBounds));
        _movableGroupOriginBounds = _editor.Selection.Bounds;
        Capture = true;
        Cursor = StickerLayout.GetCursor(target);
        InvalidateInput(UnionDirtyBounds(
            previousSelectionBounds,
            GetSelectionInvalidationBounds()));
        return true;
    }

    private void ResizeMovable(MovableAnnotation annotation, Rectangle origin, Point point)
    {
        var resizePoint = AnnotationRotation.ToUnrotatedPoint(
            point,
            origin,
            annotation.RotationDegrees);
        if (IsControlPressed())
        {
            var resizeOrigin = AnnotationRotation.ToUnrotatedPoint(
                _movableDragOrigin,
                origin,
                annotation.RotationDegrees);
            var resizeOffset = AnnotationAlignment.QuantizeOffset(
                new Point(
                    resizePoint.X - resizeOrigin.X,
                    resizePoint.Y - resizeOrigin.Y),
                _ctrlDragStepPixels);
            resizePoint = new Point(
                resizeOrigin.X + resizeOffset.X,
                resizeOrigin.Y + resizeOffset.Y);
        }
        else if (_annotationSnappingEnabled && Math.Abs(annotation.RotationDegrees) < 0.001F)
        {
            resizePoint = AnnotationAlignment.SnapResizePoint(
                resizePoint,
                _activeMovableTarget,
                _stationaryMovableBounds,
                _annotationSnapThresholdPixels);
        }
        var resizedBounds = annotation.PreserveAspectRatioWhenResizing &&
            StickerLayout.IsCorner(_activeMovableTarget)
            ? StickerLayout.Resize(origin, _activeMovableTarget, resizePoint, ClientRectangle)
            : AnnotationResizeLayout.Resize(
                origin,
                _activeMovableTarget,
                resizePoint,
                ClientRectangle);
        resizedBounds = AnnotationRotation.PreserveOppositeCorner(
            origin,
            resizedBounds,
            _activeMovableTarget,
            annotation.RotationDegrees);
        var offset = GroupMoveLayout.ClampOffset(
            AnnotationRotation.GetRotatedBounds(resizedBounds, annotation.RotationDegrees),
            Point.Empty,
            ClientRectangle);
        resizedBounds.Offset(offset);
        annotation.SetBounds(resizedBounds);
    }

    private void BeginMarquee(Point point)
    {
        _isPendingMarquee = true;
        _isSelectingMarquee = false;
        _marqueeStart = ClampPoint(point);
        _marqueeBounds = Rectangle.Empty;
        _selectionMarqueeFill.HideMarquee();
        Capture = true;
    }

    private void DrawMarquee(Graphics graphics)
    {
        if (!_isSelectingMarquee || _marqueeBounds.IsEmpty)
        {
            return;
        }

        using var border = new Pen(Color.FromArgb(125, 211, 252), 1.4F)
        {
            DashStyle = DashStyle.Dash
        };
        graphics.DrawRectangle(
            border,
            _marqueeBounds.X,
            _marqueeBounds.Y,
            Math.Max(1, _marqueeBounds.Width - 1),
            Math.Max(1, _marqueeBounds.Height - 1));
    }

    private void UpdateMarqueeFill()
    {
        if (!_isSelectingMarquee || _marqueeBounds.IsEmpty)
        {
            _selectionMarqueeFill.HideMarquee();
            return;
        }

        var wasVisible = _selectionMarqueeFill.Visible;
        _selectionMarqueeFill.ShowMarquee(RectangleToScreen(_marqueeBounds));
        if (!wasVisible)
        {
            BringToFront();
            _toolbarWindow.BringToFront();
        }
    }

    private void TrackTextDoubleClickCandidate(Point point)
    {
        var now = Environment.TickCount64;
        if (_textDoubleClickCandidate is not null &&
            now - _textDoubleClickAt <= SystemInformation.DoubleClickTime &&
            IsWithinDoubleClickRegion(_textDoubleClickPoint, point))
        {
            return;
        }

        _textDoubleClickCandidate = null;
        if (_editor.Selection.Count != 1 ||
            _editor.Selection.Primary is not { } selected ||
            !TextAnnotationEditSession.CanEdit(selected) ||
            !selected.HitTest(point, GetHitTolerance()))
        {
            return;
        }
        _textDoubleClickCandidate = selected;
        _textDoubleClickPoint = point;
        _textDoubleClickAt = now;
    }

    private void BeginTextEditor(Point location)
    {
        var minimumSize = new Size(
            Math.Min(120, ClientSize.Width),
            Math.Min(38, ClientSize.Height));
        ShowTextEditor(
            ClampPoint(location),
            minimumSize,
            Color,
            TextToolSizing.CalculateVisualFontSize(AnnotationWidth));
    }

    private void BeginExistingTextEditor(MovableAnnotation annotation)
    {
        if (!_editor.TryBeginTextEdit(annotation, out var descriptor) || descriptor is null)
        {
            return;
        }

        _editor.Selection.Clear();
        var editorBounds = descriptor.Bounds;
        editorBounds.Inflate(
            TransparentTextEditorControl.ContentPadding.Width,
            TransparentTextEditorControl.ContentPadding.Height);
        ShowTextEditor(
            editorBounds.Location,
            editorBounds.Size,
            descriptor.ForegroundColor,
            descriptor.FontSize);
        _textEditor!.Text = descriptor.Text;
        _textEditor.SelectText(descriptor.Text.Length, 0);
        InvalidateLayers();
    }

    private void ShowTextEditor(
        Point location,
        Size minimumSize,
        Color foregroundColor,
        float fontSize)
    {
        _textEditor = new TransparentTextEditorControl(
            location,
            minimumSize,
            foregroundColor,
            ClientRectangle,
            _clipboardService,
            fontSize,
            null,
            IsAnnotationMoveActive,
            _annotationMoveActivationState.MarkAltUsedAsModifier);
        _textEditor.CommitRequested += (_, _) => CancelTextEditor(commit: true);
        Controls.Add(_textEditor);
        _textEditor.BringToFront();
        _textEditor.Focus();
    }

    private void CancelTextEditor(bool commit)
    {
        if (_textEditor is null)
        {
            return;
        }

        var editor = _textEditor;
        _textEditor = null;
        if (_editor.ActiveTextEditAnnotation is not null)
        {
            var annotation = _editor.EndTextEdit(
                commit,
                editor.TextContentBounds,
                editor.Text,
                editor.TextFontSize);
            if (annotation is not null && _editor.Document.Contains(annotation))
            {
                _editor.Selection.SelectOnly(annotation);
            }
        }
        else if (commit && !string.IsNullOrWhiteSpace(editor.Text))
        {
            var annotation = new TextAnnotation(
                editor.TextContentBounds,
                editor.Text.TrimEnd(),
                editor.ForeColor,
                editor.TextFontSize);
            _editor.AddAndSelect(annotation);
        }
        Controls.Remove(editor);
        editor.Dispose();
        Focus();
        InvalidateLayers();
    }

    private void PasteClipboardContent()
    {
        var anchor = ClampPoint(PointToClient(Cursor.Position));
        var image = _clipboardService.GetImage();
        if (image is not null)
        {
            var added = false;
            try
            {
                var bounds = StickerLayout.CreateInitialBounds(image.Size, ClientRectangle, anchor);
                if (!bounds.IsEmpty)
                {
                    _editor.AddSticker(image, bounds);
                    added = true;
                    ActiveTool = CaptureAnnotationTool.Select;
                }
            }
            finally
            {
                if (!added)
                {
                    image.Dispose();
                }
            }
            InvalidateLayers();
            return;
        }

        var text = _clipboardService.GetText()?.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        ActiveTool = CaptureAnnotationTool.Text;
        BeginTextEditor(anchor);
        _textEditor!.InsertText(text);
    }

    private void RotateAnnotationUnderPointer(MouseEventArgs e)
    {
        var annotation = _editor.FindTopMovableAt(e.Location, GetHitTolerance());
        if (annotation is null)
        {
            return;
        }
        var targets = _editor.Selection.GetTransformTargets(annotation);
        var previousDirtyBounds = GetAnnotationsInvalidationBounds(targets);
        var delta = AnnotationRotation.GetWheelDeltaDegrees(
            e.Delta,
            _rotationStepDegrees,
            SystemInformation.MouseWheelScrollDelta);
        foreach (var target in targets)
        {
            target.RotateBy(delta);
        }
        InvalidateLayers(UnionDirtyBounds(
            previousDirtyBounds,
            GetAnnotationsInvalidationBounds(targets)));
    }

    private void ScaleAnnotationUnderPointer(MouseEventArgs e)
    {
        var annotation = _editor.FindTopMovableAt(e.Location, GetHitTolerance());
        if (annotation is null)
        {
            return;
        }
        var targets = _editor.Selection.GetTransformTargets(annotation);
        var previousDirtyBounds = GetAnnotationsInvalidationBounds(targets);
        var bounds = AnnotationScaling.ScaleGroupAt(
            targets,
            e.Location,
            AnnotationScaling.GetWheelScaleFactor(
                e.Delta,
                SystemInformation.MouseWheelScrollDelta),
            ClientRectangle);
        foreach (var target in targets)
        {
            target.SetBounds(bounds[target]);
        }
        InvalidateLayers(UnionDirtyBounds(
            previousDirtyBounds,
            GetAnnotationsInvalidationBounds(targets)));
    }

    private void DeleteSelected()
    {
        var previousDirtyBounds = GetSelectionInvalidationBounds();
        _editor.DeleteSelected();
        _activeMovableTarget = StickerHitTarget.None;
        _movableOriginBounds.Clear();
        _stationaryMovableBounds.Clear();
        InvalidateLayers(previousDirtyBounds);
    }

    private void HandleEscape()
    {
        if (_textEditor is not null)
        {
            CancelTextEditor(commit: true);
        }
        else if (_editor.Selection.Count > 0)
        {
            _editor.Selection.Clear();
            InvalidateLayers();
        }
        else if (ActiveTool is not CaptureAnnotationTool.Operation and not CaptureAnnotationTool.Select)
        {
            ActiveTool = CaptureAnnotationTool.Select;
        }
        else
        {
            ActiveTool = CaptureAnnotationTool.Operation;
        }
    }

    private Annotation? BuildDraft() => _editor.BuildDraft(
        ToEditorTool(ActiveTool),
        _startPoint,
        _currentPoint,
        _draftPoints,
        Color,
        AnnotationWidth);

    private void UpdateIdleCursor(Point point)
    {
        if (UpdateDrawingCursorIndicator(point))
        {
            return;
        }
        if (IsAnnotationMoveActive())
        {
            if (_editor.Selection.Count == 1 && _editor.Selection.Primary is { } selected)
            {
                var target = HitTestMovable(selected, point);
                if (target is not StickerHitTarget.None and not StickerHitTarget.Move)
                {
                    Cursor = StickerLayout.GetCursor(target);
                    return;
                }
            }

            Cursor = _editor.FindTopMovableAt(point, GetHitTolerance()) is null
                ? Cursors.Default
                : Cursors.SizeAll;
            return;
        }
        if (ActiveTool == CaptureAnnotationTool.Operation)
        {
            Cursor = Cursors.Default;
            return;
        }
        if (ActiveTool == CaptureAnnotationTool.Select)
        {
            if (_editor.Selection.Count == 1 && _editor.Selection.Primary is { } selected)
            {
                var target = HitTestMovable(selected, point);
                if (target is not StickerHitTarget.None and not StickerHitTarget.Move)
                {
                    Cursor = StickerLayout.GetCursor(target);
                    return;
                }
            }
            var hovered = _editor.FindTopMovableAt(point, GetHitTolerance());
            Cursor = hovered is not null && IsAnnotationMoveActive()
                ? Cursors.SizeAll
                : hovered is not null && IsControlPressed()
                    ? Cursors.Hand
                    : Cursors.Default;
            return;
        }
        Cursor = Cursors.Cross;
    }

    private bool UpdateDrawingCursorIndicator(Point point)
    {
        var editorTool = ToEditorTool(ActiveTool);
        if (!DrawingCursorIndicator.Supports(editorTool) ||
            !ClientRectangle.Contains(point) ||
            _textEditor is not null ||
            IsAnnotationMoveActive() ||
            IsControlPressed())
        {
            HideDrawingCursorIndicator();
            return false;
        }
        var dirty = _drawingCursorIndicator.Update(
            point,
            _editor.GetDrawingCursorDiameter(editorTool, AnnotationWidth),
            1D);
        _drawingCursorIndicator.HideSystemCursor();
        Invalidate(dirty);
        return true;
    }

    private void HideDrawingCursorIndicator()
    {
        var dirty = _drawingCursorIndicator.Hide();
        _drawingCursorIndicator.ShowSystemCursor();
        if (!dirty.IsEmpty)
        {
            Invalidate(dirty);
        }
    }

    private void CancelPointerOperation()
    {
        _isDrawing = false;
        _isPendingMarquee = false;
        _isSelectingMarquee = false;
        _marqueeBounds = Rectangle.Empty;
        _selectionMarqueeFill.HideMarquee();
        _activeMovableTarget = StickerHitTarget.None;
        _draftPoints.Clear();
        _movableOriginBounds.Clear();
        _stationaryMovableBounds.Clear();
        Capture = false;
    }

    private StickerHitTarget HitTestMovable(MovableAnnotation annotation, Point point) =>
        AnnotationHandleLayout.HitTest(
            annotation,
            point,
            GetHandleSize(),
            GetHitTolerance());

    private MovableAnnotation? FindSelectedMovableHit(Point point)
    {
        if (_editor.Selection.Count == 1 &&
            _editor.Selection.Primary is { } primary &&
            HitTestMovable(primary, point) != StickerHitTarget.None)
        {
            return primary;
        }

        var hovered = _editor.FindTopMovableAt(point, GetHitTolerance());
        return hovered is not null && _editor.Selection.Contains(hovered) ? hovered : null;
    }

    private void ToggleAnnotationSnapping()
    {
        _annotationSnappingEnabled = !_annotationSnappingEnabled;
        _toolbar.SetSnappingEnabled(_annotationSnappingEnabled);
        _toolbarWindow.RefreshLayoutAndPosition();
    }

    private int GetHandleSize() => Math.Max(9, DeviceDpi * 9 / 96);

    private int GetHitTolerance() => Math.Max(6, DeviceDpi * 6 / 96);

    private int AnnotationWidth => _widthController.Current;

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, Math.Max(0, ClientSize.Width - 1)),
        Math.Clamp(point.Y, 0, Math.Max(0, ClientSize.Height - 1)));

    private Rectangle GetSelectionInvalidationBounds() =>
        GetAnnotationsInvalidationBounds(_editor.Selection.Items);

    private Rectangle GetAnnotationsInvalidationBounds(
        IEnumerable<MovableAnnotation> annotations)
    {
        var dirtyBounds = Rectangle.Empty;
        foreach (var annotation in annotations)
        {
            var area = annotation.VisualBounds;
            if (area.IsEmpty)
            {
                continue;
            }
            area.Inflate(
                annotation.RenderMargin + GetHandleSize() + 3,
                annotation.RenderMargin + GetHandleSize() + 3);
            dirtyBounds = UnionDirtyBounds(dirtyBounds, area);
        }
        return ClipDirtyBounds(dirtyBounds);
    }

    private Rectangle GetDraftInvalidationBounds()
    {
        if (!_isDrawing)
        {
            return Rectangle.Empty;
        }

        using var draft = BuildDraft();
        if (draft is null || draft.VisualBounds.IsEmpty)
        {
            return Rectangle.Empty;
        }
        var bounds = draft.VisualBounds;
        bounds.Inflate(draft.RenderMargin + 3, draft.RenderMargin + 3);
        return ClipDirtyBounds(bounds);
    }

    private Rectangle GetFreehandSegmentInvalidationBounds(Point start, Point end)
    {
        var bounds = Geometry.Normalize(start, end);
        if (bounds.Width == 0)
        {
            bounds.Width = 1;
        }
        if (bounds.Height == 0)
        {
            bounds.Height = 1;
        }
        var diameter = _editor.GetDrawingCursorDiameter(
            ToEditorTool(ActiveTool),
            AnnotationWidth);
        var margin = Math.Max(4, (int)Math.Ceiling(diameter / 2F) + 3);
        bounds.Inflate(margin, margin);
        return ClipDirtyBounds(bounds);
    }

    private Rectangle GetMarqueeInvalidationBounds()
    {
        var bounds = _marqueeBounds;
        if (!bounds.IsEmpty)
        {
            bounds.Inflate(3, 3);
        }
        return ClipDirtyBounds(bounds);
    }

    private Rectangle ClipDirtyBounds(Rectangle bounds) => bounds.IsEmpty
        ? Rectangle.Empty
        : Rectangle.Intersect(ClientRectangle, bounds);

    private static Rectangle UnionDirtyBounds(Rectangle first, Rectangle second)
    {
        if (first.IsEmpty)
        {
            return second;
        }
        return second.IsEmpty ? first : Rectangle.Union(first, second);
    }

    private void InvalidateLayers()
    {
        if (!_content.IsDisposed)
        {
            _content.Invalidate();
        }
        LastContentInvalidationBounds = ClientRectangle;
        LastInputInvalidationBounds = ClientRectangle;
        Invalidate();
    }

    private void InvalidateLayers(Rectangle dirtyBounds)
    {
        InvalidateContent(dirtyBounds);
        InvalidateInput(dirtyBounds);
    }

    private void InvalidateContent(Rectangle dirtyBounds)
    {
        var clipped = ClipDirtyBounds(dirtyBounds);
        if (clipped.IsEmpty || _content.IsDisposed)
        {
            return;
        }
        LastContentInvalidationBounds = clipped;
        _content.Invalidate(clipped);
    }

    private void InvalidateInput(Rectangle dirtyBounds)
    {
        var clipped = ClipDirtyBounds(dirtyBounds);
        if (clipped.IsEmpty)
        {
            return;
        }
        LastInputInvalidationBounds = clipped;
        Invalidate(clipped);
    }

    private void UpdateInputMode()
    {
        if (!IsHandleCreated)
        {
            return;
        }
        var style = GetWindowLongPtr(Handle, ExtendedStyleIndex);
        if (ActiveTool == CaptureAnnotationTool.Operation)
        {
            style |= TransparentStyle | NoActivateStyle;
            _pointerHook.Stop();
            if (_showMouseClickIndicator)
            {
                _clickPreviewPointerHook.Start();
            }
            else
            {
                _clickPreviewPointerHook.Stop();
            }
        }
        else
        {
            style &= ~(TransparentStyle | NoActivateStyle);
            _clickPreviewPointerHook.Stop();
            _pointerHook.Start();
        }
        SetWindowLongPtr(Handle, ExtendedStyleIndex, style);
    }

    private static bool TryResolveShortcut(Keys keyCode, out CaptureAnnotationTool tool)
    {
        tool = keyCode switch
        {
            Keys.D1 or Keys.NumPad1 => CaptureAnnotationTool.Rectangle,
            Keys.D2 or Keys.NumPad2 => CaptureAnnotationTool.Ellipse,
            Keys.D3 or Keys.NumPad3 => CaptureAnnotationTool.Arrow,
            Keys.D4 or Keys.NumPad4 => CaptureAnnotationTool.Pen,
            Keys.D5 or Keys.NumPad5 => CaptureAnnotationTool.Text,
            Keys.D6 or Keys.NumPad6 => CaptureAnnotationTool.Mosaic,
            _ => CaptureAnnotationTool.Operation
        };
        return tool != CaptureAnnotationTool.Operation;
    }

    private static EditorTool ToEditorTool(CaptureAnnotationTool tool) => tool switch
    {
        CaptureAnnotationTool.Rectangle => EditorTool.Rectangle,
        CaptureAnnotationTool.Ellipse => EditorTool.Ellipse,
        CaptureAnnotationTool.Arrow => EditorTool.Arrow,
        CaptureAnnotationTool.Pen => EditorTool.Pen,
        CaptureAnnotationTool.Text => EditorTool.Text,
        CaptureAnnotationTool.Mosaic => EditorTool.Mosaic,
        _ => EditorTool.None
    };

    private static CaptureAnnotationTool ToContractTool(EditorTool tool) => tool switch
    {
        EditorTool.Rectangle => CaptureAnnotationTool.Rectangle,
        EditorTool.Ellipse => CaptureAnnotationTool.Ellipse,
        EditorTool.Arrow => CaptureAnnotationTool.Arrow,
        EditorTool.Pen => CaptureAnnotationTool.Pen,
        EditorTool.Text => CaptureAnnotationTool.Text,
        EditorTool.Mosaic => CaptureAnnotationTool.Mosaic,
        _ => CaptureAnnotationTool.Select
    };

    private static bool IsDrawingTool(CaptureAnnotationTool tool) =>
        tool is not CaptureAnnotationTool.Operation and not CaptureAnnotationTool.Select;

    private static bool IsFreehandTool(CaptureAnnotationTool tool) =>
        tool is CaptureAnnotationTool.Pen or CaptureAnnotationTool.Mosaic;

    private static bool IsManualDrag(Point first, Point second) =>
        Math.Abs(second.X - first.X) >= Math.Max(2, SystemInformation.DragSize.Width / 2) ||
        Math.Abs(second.Y - first.Y) >= Math.Max(2, SystemInformation.DragSize.Height / 2);

    private static bool IsWithinDoubleClickRegion(Point first, Point second)
    {
        var size = SystemInformation.DoubleClickSize;
        return Math.Abs(second.X - first.X) <= Math.Max(1, size.Width / 2) &&
               Math.Abs(second.Y - first.Y) <= Math.Max(1, size.Height / 2);
    }

    private static bool IsAltPressed() => (ModifierKeys & Keys.Alt) == Keys.Alt;

    private bool IsAnnotationMoveActive() =>
        _annotationMoveActivationState.IsActive(IsAltPressed());

    private static bool IsAltKey(Keys keyCode) =>
        keyCode is Keys.Menu or Keys.LMenu or Keys.RMenu;

    private static bool IsControlPressed() =>
        (ModifierKeys & Keys.Control) == Keys.Control;

    private static bool IsControlKey(Keys keyCode) =>
        keyCode is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey;

    private static Bitmap CreateTransparentSurface(Size size)
    {
        var surface = new Bitmap(
            size.Width,
            size.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(surface);
        graphics.Clear(TransparentColor);
        return surface;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint newValue);
}

internal sealed class LiveAnnotationContentForm : Form
{
    private const int ExtendedStyleIndex = -20;
    private const nint TransparentStyle = 0x20;
    private const nint NoActivateStyle = 0x08000000;
    private static readonly Color TransparentColor = Color.FromArgb(1, 0, 1);
    private readonly Action<Graphics> _render;
    private readonly Bitmap _surface;

    public LiveAnnotationContentForm(Rectangle screenBounds, Action<Graphics> render)
    {
        _render = render;
        _surface = new Bitmap(
            screenBounds.Width,
            screenBounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(_surface))
        {
            graphics.Clear(TransparentColor);
        }
        Text = "轻截核心批注内容层";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screenBounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = TransparentColor;
        TransparencyKey = TransparentColor;
        AllowTransparency = true;
        DoubleBuffered = false;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint,
            true);
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var style = GetWindowLongPtr(Handle, ExtendedStyleIndex);
        SetWindowLongPtr(Handle, ExtendedStyleIndex, style | TransparentStyle | NoActivateStyle);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Repaint from the persistent surface in one blit to avoid transparent flicker.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var clip = Rectangle.Intersect(ClientRectangle, e.ClipRectangle);
        if (clip.IsEmpty)
        {
            return;
        }
        using (var surfaceGraphics = Graphics.FromImage(_surface))
        {
            surfaceGraphics.SetClip(clip);
            surfaceGraphics.CompositingMode = CompositingMode.SourceCopy;
            using var background = new SolidBrush(TransparentColor);
            surfaceGraphics.FillRectangle(background, clip);
            surfaceGraphics.CompositingMode = CompositingMode.SourceOver;
            _render(surfaceGraphics);
        }
        e.Graphics.DrawImage(_surface, clip, clip, GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _surface.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint newValue);
}

internal static class WindowCaptureProtection
{
    private const uint CaptureAllowed = 0x00000000;
    private const uint ExcludeFromCapture = 0x00000011;

    public static bool TryExclude(Form form) => TrySet(form, ExcludeFromCapture);

    public static bool TryAllow(Form form) => TrySet(form, CaptureAllowed);

    private static bool TrySet(Form form, uint affinity)
    {
        try
        {
            return SetWindowDisplayAffinity(form.Handle, affinity);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
}
