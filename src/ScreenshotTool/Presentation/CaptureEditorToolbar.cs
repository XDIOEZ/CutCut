using ScreenshotTool.Contracts;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureEditorToolbar : FlowLayoutPanel
{
    private static readonly Color ToolbarBackColor = Color.FromArgb(26, 32, 44);
    private static readonly Color ButtonBackColor = Color.FromArgb(45, 55, 72);
    private static readonly Color HoverBackColor = Color.FromArgb(71, 85, 105);
    private static readonly Color ActiveBackColor = Color.FromArgb(37, 99, 235);
    private static readonly Color RecordingSelectBackColor = Color.FromArgb(180, 83, 9);
    private static readonly Color RecordingSelectHoverColor = Color.FromArgb(217, 119, 6);
    private static readonly Color RecordingSelectActiveColor = Color.FromArgb(22, 163, 74);
    private static readonly Color RecordingSelectActiveHoverColor = Color.FromArgb(21, 128, 61);
    private static readonly Color RecordingSelectActiveBorderColor = Color.FromArgb(240, 253, 244);
    private readonly Dictionary<CaptureAnnotationTool, Button> _toolButtons = [];
    private readonly Dictionary<CaptureAnnotationTool, Color> _toolInactiveColors = [];
    private readonly Dictionary<CaptureAnnotationTool, Color> _toolActiveColors = [];
    private readonly List<Button> _colorButtons = [];
    private readonly ToolTip _toolTip = new();
    private readonly int _coreControlCount;

    public CaptureEditorToolbar(
        IReadOnlyList<CaptureAnnotationToolDefinition> tools,
        IReadOnlyList<Color> palette,
        CaptureAnnotationTool? activeTool,
        Color selectedColor,
        int toolWidth,
        int minimumWidth,
        int maximumWidth,
        bool snappingEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(palette);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = false;
        FlowDirection = FlowDirection.LeftToRight;
        Padding = new Padding(7, 6, 7, 6);
        Margin = Padding.Empty;
        BackColor = ToolbarBackColor;
        Font = new Font("Microsoft YaHei UI", 8.5F);

        AddToolButton(
            CaptureAnnotationTool.Select,
            "选择",
            76,
            "录屏编辑模式：使用左键单选、多选、框选和调整元素；再次点击恢复操作屏幕",
            RecordingSelectBackColor,
            RecordingSelectHoverColor,
            RecordingSelectActiveColor,
            visible: false);
        foreach (var tool in tools)
        {
            AddToolButton(
                tool.Tool,
                tool.Text,
                tool.Width,
                tool.ToolTip,
                ButtonBackColor,
                HoverBackColor,
                ActiveBackColor,
                visible: true);
        }
        Controls.Add(CreateSeparator());

        var undo = CreateButton("撤销", 48);
        undo.Click += (_, _) => UndoClicked?.Invoke();
        _toolTip.SetToolTip(undo, "撤销上一步（Ctrl+Z）");
        Controls.Add(undo);

        WidthButton = CreateButton($"粗细 {toolWidth}", 68);
        WidthButton.Name = "WidthButton";
        WidthButton.Click += (_, _) => WidthCycleRequested?.Invoke();
        WidthButton.MouseEnter += (_, _) => WidthButton.Focus();
        _toolTip.SetToolTip(
            WidthButton,
            $"单击切换线宽/字号；悬停后滚轮调整（{minimumWidth}–{maximumWidth}）");
        Controls.Add(WidthButton);

        SnappingButton = CreateButton(string.Empty, 64);
        SnappingButton.Name = "SnappingButton";
        SnappingButton.Click += (_, _) => SnappingToggleRequested?.Invoke();
        _toolTip.SetToolTip(SnappingButton, "元素边缘与中心对齐吸附（快速双击 Ctrl 切换）");
        Controls.Add(SnappingButton);
        SetSnappingEnabled(snappingEnabled);

        foreach (var color in palette)
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
            colorButton.Click += (_, _) => ColorClicked?.Invoke(color);
            _colorButtons.Add(colorButton);
            Controls.Add(colorButton);
        }

        _coreControlCount = Controls.Count;
        SetActiveTool(activeTool);
        SetSelectedColor(selectedColor);
    }

    public event Action<CaptureAnnotationTool>? ToolClicked;

    public event Action? UndoClicked;

    public event Action? WidthCycleRequested;

    public event Action<Color>? ColorClicked;

    public event Action? SnappingToggleRequested;

    public Button WidthButton { get; }

    public Button SnappingButton { get; }

    public void SetActiveTool(CaptureAnnotationTool? activeTool)
    {
        foreach (var (tool, button) in _toolButtons)
        {
            var isActive = tool == activeTool;
            button.BackColor = isActive
                ? _toolActiveColors[tool]
                : _toolInactiveColors[tool];
            if (tool != CaptureAnnotationTool.Select)
            {
                continue;
            }

            button.Text = isActive ? "✓ 选择中" : "选择";
            button.FlatAppearance.BorderSize = isActive ? 2 : 1;
            button.FlatAppearance.BorderColor = isActive
                ? RecordingSelectActiveBorderColor
                : RecordingSelectHoverColor;
            button.FlatAppearance.MouseOverBackColor = isActive
                ? RecordingSelectActiveHoverColor
                : RecordingSelectHoverColor;
            button.AccessibleName = isActive ? "录屏选择模式已开启" : "录屏选择模式已关闭";
        }
    }

    public void SetToolVisible(CaptureAnnotationTool tool, bool visible)
    {
        if (_toolButtons.TryGetValue(tool, out var button))
        {
            button.Visible = visible;
        }
    }

    public void SetSelectedColor(Color color)
    {
        foreach (var button in _colorButtons)
        {
            var selected = button.BackColor.ToArgb() == color.ToArgb();
            button.FlatAppearance.BorderColor = selected ? Color.White : HoverBackColor;
            button.FlatAppearance.BorderSize = selected ? 2 : 1;
        }
    }

    public void SetToolWidth(int width) => WidthButton.Text = $"粗细 {width}";

    public void SetSnappingEnabled(bool enabled)
    {
        SnappingButton.Text = enabled ? "吸附 开" : "吸附 关";
        SnappingButton.BackColor = enabled ? ActiveBackColor : ButtonBackColor;
    }

    public void ClearExtensionControls()
    {
        SuspendLayout();
        try
        {
            while (Controls.Count > _coreControlCount)
            {
                var control = Controls[Controls.Count - 1];
                Controls.RemoveAt(Controls.Count - 1);
                control.Dispose();
            }
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    public void AddExtensionSeparator() => Controls.Add(CreateSeparator());

    public Label AddStatusLabel(string text, int width = 88)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = false,
            Size = new Size(width, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(2, 0, 2, 0),
            ForeColor = Color.FromArgb(248, 113, 113)
        };
        Controls.Add(label);
        return label;
    }

    public Button AddCommandButton(
        string text,
        int width,
        string? toolTip = null,
        CaptureAnnotationToolbarCommandStyle style = CaptureAnnotationToolbarCommandStyle.Default)
    {
        var backColor = style switch
        {
            CaptureAnnotationToolbarCommandStyle.Primary => ActiveBackColor,
            CaptureAnnotationToolbarCommandStyle.Danger => Color.FromArgb(220, 38, 38),
            _ => ButtonBackColor
        };
        var button = CreateButton(text, width, backColor);
        if (!string.IsNullOrWhiteSpace(toolTip))
        {
            _toolTip.SetToolTip(button, toolTip);
        }
        Controls.Add(button);
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private void AddToolButton(
        CaptureAnnotationTool tool,
        string text,
        int width,
        string toolTip,
        Color inactiveColor,
        Color hoverColor,
        Color activeColor,
        bool visible)
    {
        var button = CreateButton(text, width, inactiveColor);
        button.Visible = visible;
        button.FlatAppearance.MouseOverBackColor = hoverColor;
        button.Click += (_, _) => ToolClicked?.Invoke(tool);
        _toolTip.SetToolTip(button, toolTip);
        _toolButtons.Add(tool, button);
        _toolInactiveColors.Add(tool, inactiveColor);
        _toolActiveColors.Add(tool, activeColor);
        Controls.Add(button);
    }

    private static Button CreateButton(string text, int width, Color? backColor = null)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, 30),
            Margin = new Padding(2, 0, 2, 0),
            BackColor = backColor ?? ButtonBackColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = HoverBackColor;
        return button;
    }

    private static Panel CreateSeparator() => new()
    {
        Size = new Size(1, 24),
        BackColor = HoverBackColor,
        Margin = new Padding(5, 3, 5, 3)
    };
}

internal sealed class CaptureEditorToolbarWindow : Form
{
    private readonly CaptureEditorToolbar _toolbar;
    private readonly Rectangle _anchorBounds;

    public CaptureEditorToolbarWindow(
        CaptureEditorToolbar toolbar,
        Rectangle anchorBounds)
    {
        _toolbar = toolbar;
        _anchorBounds = anchorBounds;

        Text = "轻截核心编辑工具栏";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = toolbar.BackColor;
        AutoScaleMode = AutoScaleMode.None;
        Controls.Add(toolbar);

        Shown += (_, _) =>
        {
            WindowCaptureProtection.TryExclude(this);
            RefreshLayoutAndPosition();
        };
    }

    protected override bool ShowWithoutActivation => true;

    public void RefreshLayoutAndPosition()
    {
        _toolbar.PerformLayout();
        var preferred = _toolbar.GetPreferredSize(Size.Empty);
        _toolbar.Location = Point.Empty;
        _toolbar.Size = preferred;
        ClientSize = preferred;

        var screen = Screen.FromRectangle(_anchorBounds);
        var workArea = screen.WorkingArea;
        var left = Math.Clamp(
            _anchorBounds.Right - preferred.Width,
            workArea.Left + 8,
            Math.Max(workArea.Left + 8, workArea.Right - preferred.Width - 8));
        var below = _anchorBounds.Bottom + 8;
        var top = below + preferred.Height <= workArea.Bottom
            ? below
            : _anchorBounds.Top - preferred.Height - 8;
        top = Math.Clamp(
            top,
            workArea.Top + 8,
            Math.Max(workArea.Top + 8, workArea.Bottom - preferred.Height - 8));
        Location = new Point(left, top);
    }
}
