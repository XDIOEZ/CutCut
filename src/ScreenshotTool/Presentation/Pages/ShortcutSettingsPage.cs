using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ShortcutSettingsPage : UserControl
{
    private readonly HotkeyInputBox _hotkeyInput;
    private readonly CheckBox _startMinimized;

    public ShortcutSettingsPage(HotkeyDefinition hotkey, bool startMinimized)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        var card = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(0, 0),
            Height = 250,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(card);

        var title = new Label
        {
            Text = "全局截图快捷键",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 23)
        };
        var description = AppTheme.CreateBodyLabel("程序在后台运行时也会监听此快捷键。点击输入框后直接按下新的组合键。", 620);
        description.Location = new Point(28, 58);

        _hotkeyInput = new HotkeyInputBox
        {
            Hotkey = hotkey,
            Location = new Point(28, 99),
            Size = new Size(310, 38),
            Font = new Font("Consolas", 12F, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        _startMinimized = new CheckBox
        {
            Text = "启动后直接最小化到系统托盘",
            Checked = startMinimized,
            AutoSize = true,
            Font = AppTheme.CreateFont(9F),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 160)
        };

        var saveButton = AppTheme.CreateButton("保存快捷键", primary: true);
        saveButton.Location = new Point(28, 199);
        saveButton.Size = new Size(132, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        card.Controls.AddRange([title, description, _hotkeyInput, _startMinimized, saveButton]);

        var hint = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(0, 270),
            Height = 94,
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(20, 17, 20, 14)
        };
        var hintTitle = new Label
        {
            Text = "默认快捷键  Ctrl + Shift + X",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Accent,
            Location = new Point(20, 15)
        };
        var hintBody = AppTheme.CreateBodyLabel("如果快捷键被其他程序占用，轻截会保留原快捷键并提示重新设置。", 620);
        hintBody.Location = new Point(22, 47);
        hint.Controls.AddRange([hintTitle, hintBody]);
        Controls.Add(hint);

        Resize += (_, _) =>
        {
            card.Width = Math.Max(440, ClientSize.Width - 12);
            hint.Width = card.Width;
        };
    }

    public event EventHandler? SaveRequested;

    public HotkeyDefinition Hotkey
    {
        get => _hotkeyInput.Hotkey;
        set => _hotkeyInput.Hotkey = value;
    }

    public bool StartMinimized
    {
        get => _startMinimized.Checked;
        set => _startMinimized.Checked = value;
    }

    public event EventHandler? HotkeyInputEntered
    {
        add => _hotkeyInput.Enter += value;
        remove => _hotkeyInput.Enter -= value;
    }

    public event EventHandler? HotkeyInputLeft
    {
        add => _hotkeyInput.Leave += value;
        remove => _hotkeyInput.Leave -= value;
    }
}
