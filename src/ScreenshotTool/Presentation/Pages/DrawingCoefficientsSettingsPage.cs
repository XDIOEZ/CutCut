using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class DrawingCoefficientsSettingsPage : UserControl
{
    private readonly NumericUpDown _rectangle = CreateInput();
    private readonly NumericUpDown _ellipse = CreateInput();
    private readonly NumericUpDown _arrowBody = CreateInput();
    private readonly NumericUpDown _arrowHeadWidth = CreateInput();
    private readonly NumericUpDown _arrowHeadLength = CreateInput();
    private readonly NumericUpDown _pen = CreateInput();
    private readonly NumericUpDown _mosaic = CreateInput();
    private readonly Panel _card;
    private readonly List<Panel> _settingRows = [];

    public DrawingCoefficientsSettingsPage(DrawingToolCoefficients coefficients)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _card = new Panel
        {
            Location = Point.Empty,
            Height = 705,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_card);

        var title = new Label
        {
            Text = "绘制元素基础系数",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 22)
        };
        var description = AppTheme.CreateBodyLabel(
            "最终尺寸 = 基础系数 × 截图工具栏粗细。箭头头部宽度、长度和箭身可分别配置。",
            700);
        description.Location = new Point(28, 58);
        _card.Controls.AddRange([title, description]);

        AddField("矩形线宽", _rectangle, 105);
        AddField("椭圆线宽", _ellipse, 173);
        AddField("画笔线宽", _pen, 241);
        AddField("马赛克范围", _mosaic, 309);
        AddField("箭身线宽", _arrowBody, 377);
        AddField("箭头宽度", _arrowHeadWidth, 445);
        AddField("箭头长度", _arrowHeadLength, 513);

        var hint = AppTheme.CreateBodyLabel(
            $"可配置范围：{DrawingToolCoefficients.Minimum:0.0}–{DrawingToolCoefficients.Maximum:0.0}。例如基础系数 1.5、粗细 4，最终尺寸为 6。",
            700);
        hint.Location = new Point(28, 592);
        var saveButton = AppTheme.CreateButton("保存基础系数", primary: true);
        saveButton.Location = new Point(28, 641);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _card.Controls.AddRange([hint, saveButton]);

        Coefficients = coefficients;
        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? SaveRequested;

    public DrawingToolCoefficients Coefficients
    {
        get => new()
        {
            Rectangle = _rectangle.Value,
            Ellipse = _ellipse.Value,
            ArrowBody = _arrowBody.Value,
            ArrowHeadWidth = _arrowHeadWidth.Value,
            ArrowHeadLength = _arrowHeadLength.Value,
            Pen = _pen.Value,
            Mosaic = _mosaic.Value
        };
        set
        {
            _rectangle.Value = value.Rectangle;
            _ellipse.Value = value.Ellipse;
            _arrowBody.Value = value.ArrowBody;
            _arrowHeadWidth.Value = value.ArrowHeadWidth;
            _arrowHeadLength.Value = value.ArrowHeadLength;
            _pen.Value = value.Pen;
            _mosaic.Value = value.Mosaic;
        }
    }

    private void AddField(string text, NumericUpDown input, int top)
    {
        var row = new Panel
        {
            Location = new Point(28, top),
            Size = new Size(664, 60),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Tag = "SettingRow"
        };
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(16, 20)
        };
        input.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        input.Location = new Point(row.ClientSize.Width - input.Width - 18, 13);
        row.Controls.AddRange([label, input]);
        _settingRows.Add(row);
        _card.Controls.Add(row);
    }

    private static NumericUpDown CreateInput() => new()
    {
        Size = new Size(150, 34),
        Minimum = DrawingToolCoefficients.Minimum,
        Maximum = DrawingToolCoefficients.Maximum,
        DecimalPlaces = 2,
        Increment = 0.1M,
        Font = AppTheme.CreateFont(10F),
        TextAlign = HorizontalAlignment.Center
    };

    private void ResizeContent()
    {
        _card.Width = Math.Max(640, ClientSize.Width - 28);
        var rowWidth = _card.Width - 56;
        foreach (var row in _settingRows)
        {
            row.Width = rowWidth;
        }
    }
}
