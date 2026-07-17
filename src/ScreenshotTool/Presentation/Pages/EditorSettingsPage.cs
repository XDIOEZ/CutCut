using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class EditorSettingsPage : UserControl
{
    private readonly NumericUpDown _minimumWidth;
    private readonly NumericUpDown _maximumWidth;
    private readonly Panel _settingsCard;
    private readonly Panel _note;
    private bool _updatingRange;

    public EditorSettingsPage(ToolWidthRange range)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 300,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "编辑工具粗细范围",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 22)
        };
        var description = AppTheme.CreateBodyLabel(
            "用于矩形、椭圆、箭头、画笔、马赛克以及后续接入的可调粗细工具。", 660);
        description.Location = new Point(28, 58);

        var minimumLabel = CreateFieldLabel("最小粗细", new Point(28, 105));
        _minimumWidth = CreateWidthInput(new Point(28, 132));
        var maximumLabel = CreateFieldLabel("最大粗细", new Point(210, 105));
        _maximumWidth = CreateWidthInput(new Point(210, 132));
        _minimumWidth.ValueChanged += HandleMinimumChanged;
        _maximumWidth.ValueChanged += HandleMaximumChanged;

        var rangeHint = AppTheme.CreateBodyLabel(
            $"允许设置范围：{ToolWidthRange.SupportedMinimum}–{ToolWidthRange.SupportedMaximum} 像素。", 620);
        rangeHint.Location = new Point(28, 182);

        var saveButton = AppTheme.CreateButton("保存粗细范围", primary: true);
        saveButton.Location = new Point(28, 224);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        _settingsCard.Controls.AddRange(
            [title, description, minimumLabel, _minimumWidth, maximumLabel, _maximumWidth, rangeHint, saveButton]);

        _note = new Panel
        {
            Location = new Point(0, 320),
            Height = 112,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "滚轮只在粗细按钮上生效",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Success,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel(
            "截图编辑时，将鼠标悬停在工具栏的“粗细”按钮上滚动：向上增大、向下减小。鼠标位于其他区域时不会修改粗细。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Range = range;
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

    private static Label CreateFieldLabel(string text, Point location) => new()
    {
        Text = text,
        AutoSize = true,
        Font = AppTheme.CreateFont(9F, FontStyle.Bold),
        ForeColor = AppTheme.Text,
        Location = location
    };

    private static NumericUpDown CreateWidthInput(Point location) => new()
    {
        Location = location,
        Size = new Size(142, 34),
        Minimum = ToolWidthRange.SupportedMinimum,
        Maximum = ToolWidthRange.SupportedMaximum,
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
        var width = Math.Max(500, ClientSize.Width - 12);
        _settingsCard.Width = width;
        _note.Width = width;
    }
}
