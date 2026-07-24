using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class EditorSettingsPage : UserControl
{
    private readonly NumericUpDown _minimumWidth;
    private readonly NumericUpDown _maximumWidth;
    private readonly NumericUpDown _rotationStepDegrees;
    private readonly ComboBox _drawingCursorShape;
    private readonly CheckBox _snappingEnabled;
    private readonly NumericUpDown _snapThresholdPixels;
    private readonly NumericUpDown _ctrlDragStepPixels;
    private readonly ComboBox _annotationMoveActivationMode;
    private readonly Panel _settingsCard;
    private readonly Panel _note;
    private readonly List<Panel> _settingRows = [];
    private bool _updatingRange;

    public EditorSettingsPage(
        ToolWidthRange range,
        int rotationStepDegrees = AnnotationRotationStep.DefaultDegrees,
        DrawingCursorShape drawingCursorShape = DrawingCursorShape.Circle,
        bool snappingEnabled = AnnotationLayoutOptions.DefaultSnappingEnabled,
        int snapThresholdPixels = AnnotationLayoutOptions.DefaultSnapThresholdPixels,
        int ctrlDragStepPixels = AnnotationLayoutOptions.DefaultCtrlDragStepPixels,
        AnnotationMoveActivationMode annotationMoveActivationMode =
            AnnotationMoveActivationMode.HoldAlt)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 752,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "编辑工具设置",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 22)
        };
        var description = AppTheme.CreateBodyLabel(
            "配置绘图线宽、文字字号、滚轮旋转步进，以及画笔和马赛克的光标形状。", 660);
        description.Location = new Point(28, 58);

        _minimumWidth = CreateWidthInput();
        AddSettingRow(
            "最小粗细",
            $"允许范围 {ToolWidthRange.SupportedMinimum}–{ToolWidthRange.SupportedMaximum} 像素。",
            _minimumWidth,
            105);
        _maximumWidth = CreateWidthInput();
        AddSettingRow(
            "最大粗细",
            "工具栏调整粗细时不会超过该数值。",
            _maximumWidth,
            177);
        _rotationStepDegrees = CreateRotationStepInput();
        AddSettingRow(
            "滚轮旋转步进（度）",
            "按住 Alt 滚动一格时，元素旋转的角度。",
            _rotationStepDegrees,
            249);
        _minimumWidth.ValueChanged += HandleMinimumChanged;
        _maximumWidth.ValueChanged += HandleMaximumChanged;

        _annotationMoveActivationMode = new ComboBox
        {
            Size = new Size(196, 34),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = AppTheme.CreateFont(10F)
        };
        _annotationMoveActivationMode.Items.AddRange(
            ["按住 Alt 临时移动（推荐）", "按一下 Alt 切换移动模式"]);
        AddSettingRow(
            "Alt 移动元素方式",
            "选择按住生效，或裸按 Alt 切换。",
            _annotationMoveActivationMode,
            321);

        _drawingCursorShape = new ComboBox
        {
            Size = new Size(180, 34),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = AppTheme.CreateFont(10F)
        };
        _drawingCursorShape.Items.AddRange(["圆形（推荐）", "正方形"]);
        AddSettingRow(
            "画笔 / 马赛克光标",
            "选择绘制时显示的笔刷轮廓形状。",
            _drawingCursorShape,
            393);

        _snappingEnabled = new CheckBox
        {
            Text = "启用",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F),
            ForeColor = AppTheme.Text,
            Cursor = Cursors.Hand
        };
        AddSettingRow(
            "编辑元素吸附",
            "移动元素时自动对齐其他元素的边缘和中心。",
            _snappingEnabled,
            465);

        _snapThresholdPixels = CreateLayoutInput(
            AnnotationLayoutOptions.MinimumSnapThresholdPixels,
            AnnotationLayoutOptions.MaximumSnapThresholdPixels);
        _snappingEnabled.CheckedChanged += (_, _) =>
            _snapThresholdPixels.Enabled = _snappingEnabled.Checked;
        AddSettingRow(
            "吸附距离（像素）",
            "元素接近参考线多少像素时触发吸附。",
            _snapThresholdPixels,
            537);

        _ctrlDragStepPixels = CreateLayoutInput(
            AnnotationLayoutOptions.MinimumCtrlDragStepPixels,
            AnnotationLayoutOptions.MaximumCtrlDragStepPixels);
        AddSettingRow(
            "Ctrl 拖动步长（像素）",
            "拖动或缩放时按住 Ctrl 使用的固定步长。",
            _ctrlDragStepPixels,
            609);

        var saveButton = AppTheme.CreateButton("保存编辑设置", primary: true);
        saveButton.Location = new Point(28, 694);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        _settingsCard.Controls.AddRange([title, description, saveButton]);

        _note = new Panel
        {
            Location = new Point(0, 772),
            Height = 112,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "快捷编辑与对齐",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Success,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel(
            "Alt 移动可配置为按住临时生效或裸按切换；Ctrl 拖动使用固定步长，快速双击 Ctrl 切换吸附。Ctrl + 滚轮缩放，Alt + 滚轮旋转。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Range = range;
        RotationStepDegrees = rotationStepDegrees;
        DrawingCursorShape = drawingCursorShape;
        SnappingEnabled = snappingEnabled;
        SnapThresholdPixels = snapThresholdPixels;
        CtrlDragStepPixels = ctrlDragStepPixels;
        AnnotationMoveActivationMode = annotationMoveActivationMode;
        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? SaveRequested;

    public ToolWidthRange Range
    {
        get => ToolWidthRange.Create((int)_minimumWidth.Value, (int)_maximumWidth.Value);
        set
        {
            _updatingRange = true;
            _minimumWidth.Value = value.Minimum;
            _maximumWidth.Value = value.Maximum;
            _updatingRange = false;
        }
    }

    public int RotationStepDegrees
    {
        get => (int)_rotationStepDegrees.Value;
        set => _rotationStepDegrees.Value = AnnotationRotationStep.Normalize(value);
    }

    public DrawingCursorShape DrawingCursorShape
    {
        get => _drawingCursorShape.SelectedIndex == 1
            ? DrawingCursorShape.Square
            : DrawingCursorShape.Circle;
        set => _drawingCursorShape.SelectedIndex = value == DrawingCursorShape.Square ? 1 : 0;
    }

    public bool SnappingEnabled
    {
        get => _snappingEnabled.Checked;
        set => _snappingEnabled.Checked = value;
    }

    public int SnapThresholdPixels
    {
        get => (int)_snapThresholdPixels.Value;
        set => _snapThresholdPixels.Value = AnnotationLayoutOptions.NormalizeSnapThreshold(value);
    }

    public int CtrlDragStepPixels
    {
        get => (int)_ctrlDragStepPixels.Value;
        set => _ctrlDragStepPixels.Value = AnnotationLayoutOptions.NormalizeCtrlDragStep(value);
    }

    public AnnotationMoveActivationMode AnnotationMoveActivationMode
    {
        get => _annotationMoveActivationMode.SelectedIndex == 1
            ? AnnotationMoveActivationMode.ToggleOnAltTap
            : AnnotationMoveActivationMode.HoldAlt;
        set => _annotationMoveActivationMode.SelectedIndex =
            value == AnnotationMoveActivationMode.ToggleOnAltTap ? 1 : 0;
    }

    private void AddSettingRow(
        string title,
        string description,
        Control input,
        int top)
    {
        var row = new Panel
        {
            Location = new Point(28, top),
            Size = new Size(514, 64),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Tag = "SettingRow"
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(16, 9)
        };
        var descriptionWidth = Math.Max(180, row.ClientSize.Width - input.Width - 64);
        var descriptionLabel = AppTheme.CreateBodyLabel(description, descriptionWidth);
        descriptionLabel.Location = new Point(17, 34);
        descriptionLabel.Height = 20;
        input.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        input.Location = new Point(row.ClientSize.Width - input.Width - 18, 15);
        row.Controls.AddRange([titleLabel, descriptionLabel, input]);
        _settingRows.Add(row);
        _settingsCard.Controls.Add(row);
    }

    private static NumericUpDown CreateWidthInput() => new()
    {
        Size = new Size(142, 34),
        Minimum = ToolWidthRange.SupportedMinimum,
        Maximum = ToolWidthRange.SupportedMaximum,
        Font = AppTheme.CreateFont(10F),
        TextAlign = HorizontalAlignment.Center
    };

    private static NumericUpDown CreateRotationStepInput() => new()
    {
        Size = new Size(142, 34),
        Minimum = AnnotationRotationStep.MinimumDegrees,
        Maximum = AnnotationRotationStep.MaximumDegrees,
        Font = AppTheme.CreateFont(10F),
        TextAlign = HorizontalAlignment.Center
    };

    private static NumericUpDown CreateLayoutInput(int minimum, int maximum) => new()
    {
        Size = new Size(142, 34),
        Minimum = minimum,
        Maximum = maximum,
        Font = AppTheme.CreateFont(10F),
        TextAlign = HorizontalAlignment.Center
    };

    private void HandleMinimumChanged(object? sender, EventArgs e)
    {
        if (_updatingRange || _minimumWidth.Value <= _maximumWidth.Value)
        {
            return;
        }

        _updatingRange = true;
        _maximumWidth.Value = _minimumWidth.Value;
        _updatingRange = false;
    }

    private void HandleMaximumChanged(object? sender, EventArgs e)
    {
        if (_updatingRange || _maximumWidth.Value >= _minimumWidth.Value)
        {
            return;
        }

        _updatingRange = true;
        _minimumWidth.Value = _maximumWidth.Value;
        _updatingRange = false;
    }

    private void ResizeContent()
    {
        var width = Math.Max(570, ClientSize.Width - 28);
        _settingsCard.Width = width;
        _note.Width = width;
        var rowWidth = width - 56;
        foreach (var row in _settingRows)
        {
            row.Width = rowWidth;
        }
    }
}
