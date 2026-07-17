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

    public DrawingCoefficientsSettingsPage(DrawingToolCoefficients coefficients)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _card = new Panel
        {
            Location = Point.Empty,
            Height = 500,
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

        AddField("矩形线宽", _rectangle, 28, 108);
        AddField("椭圆线宽", _ellipse, 220, 108);
        AddField("画笔线宽", _pen, 412, 108);
        AddField("马赛克范围", _mosaic, 28, 200);
        AddField("箭身线宽", _arrowBody, 220, 200);
        AddField("箭头宽度", _arrowHeadWidth, 412, 200);
        AddField("箭头长度", _arrowHeadLength, 28, 292);

        var hint = AppTheme.CreateBodyLabel(
            $"可配置范围：{DrawingToolCoefficients.Minimum:0.0}–{DrawingToolCoefficients.Maximum:0.0}。例如基础系数 1.5、粗细 4，最终尺寸为 6。",
            700);
        hint.Location = new Point(28, 382);
        var saveButton = AppTheme.CreateButton("保存基础系数", primary: true);
        saveButton.Location = new Point(28, 430);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _card.Controls.AddRange([hint, saveButton]);

        Coefficients = coefficients;
        Resize += (_, _) => _card.Width = Math.Max(720, ClientSize.Width - 12);
        _card.Width = Math.Max(720, ClientSize.Width - 12);
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

    private void AddField(string text, NumericUpDown input, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(x, y)
        };
        input.Location = new Point(x, y + 27);
        _card.Controls.AddRange([label, input]);
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
}
