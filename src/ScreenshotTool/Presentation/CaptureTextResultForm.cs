using ScreenshotTool.Abstractions;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureTextResultForm : Form
{
    private const int WindowGap = 12;
    private readonly IClipboardService _clipboardService;
    private readonly TextBox _textBox;
    private readonly Label _statusLabel;

    public CaptureTextResultForm(
        string title,
        string text,
        Rectangle anchorScreenBounds,
        IClipboardService clipboardService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(clipboardService);

        _clipboardService = clipboardService;

        Text = title;
        AccessibleName = title;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppTheme.Canvas;
        ClientSize = new Size(440, 320);
        Font = AppTheme.CreateFont(9F);
        FormBorderStyle = FormBorderStyle.Sizable;
        Icon = AppIcon.Shared;
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(340, 240);
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        var header = new Label
        {
            AutoSize = true,
            Font = AppTheme.CreateFont(11F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(18, 16),
            Text = title
        };
        _statusLabel = new Label
        {
            AutoSize = true,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(19, 45),
            Text = CreateStatusText(text)
        };
        _textBox = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppTheme.CreateFont(10F),
            ForeColor = AppTheme.Text,
            Location = new Point(18, 72),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(ClientSize.Width - 36, ClientSize.Height - 132),
            Text = NormalizeLineEndings(text),
            WordWrap = true
        };
        _textBox.TextChanged += (_, _) => _statusLabel.Text = CreateStatusText(_textBox.Text);

        var copyButton = AppTheme.CreateButton("复制结果", primary: true);
        copyButton.AccessibleName = "复制识别结果";
        copyButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        copyButton.Location = new Point(ClientSize.Width - 116, ClientSize.Height - 48);
        copyButton.Size = new Size(98, 34);
        copyButton.Click += (_, _) => CopyText();

        var closeButton = AppTheme.CreateButton("关闭");
        closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        closeButton.Location = new Point(ClientSize.Width - 214, ClientSize.Height - 48);
        closeButton.Size = new Size(88, 34);
        closeButton.Click += (_, _) => Close();

        Controls.Add(header);
        Controls.Add(_statusLabel);
        Controls.Add(_textBox);
        Controls.Add(closeButton);
        Controls.Add(copyButton);

        AcceptButton = copyButton;
        CancelButton = closeButton;

        var workingArea = Screen.FromRectangle(anchorScreenBounds).WorkingArea;
        Location = CalculateLocation(anchorScreenBounds, workingArea, Size);
        Shown += (_, _) =>
        {
            _textBox.SelectAll();
            _textBox.Focus();
        };
    }

    internal string ResultText => _textBox.Text;

    internal static Point CalculateLocation(
        Rectangle anchor,
        Rectangle workingArea,
        Size windowSize)
    {
        var right = anchor.Right + WindowGap;
        var left = anchor.Left - WindowGap - windowSize.Width;
        var maximumX = Math.Max(workingArea.Left, workingArea.Right - windowSize.Width);
        var maximumY = Math.Max(workingArea.Top, workingArea.Bottom - windowSize.Height);
        var x = right + windowSize.Width <= workingArea.Right
            ? right
            : left >= workingArea.Left
                ? left
                : Math.Clamp(anchor.Right - windowSize.Width, workingArea.Left, maximumX);
        var y = Math.Clamp(anchor.Top, workingArea.Top, maximumY);
        return new Point(x, y);
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private static string CreateStatusText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "没有识别结果，可以直接在下方输入。";
        }

        var characterCount = text.Count(character => !char.IsWhiteSpace(character));
        return $"共 {characterCount} 个非空白字符，可继续编辑或复制。";
    }

    private void CopyText()
    {
        if (string.IsNullOrEmpty(_textBox.Text))
        {
            System.Media.SystemSounds.Beep.Play();
            return;
        }

        try
        {
            _clipboardService.SetText(_textBox.Text);
            _statusLabel.Text = "结果已复制到剪贴板。";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"复制结果失败：{exception.Message}",
                "无法复制",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
