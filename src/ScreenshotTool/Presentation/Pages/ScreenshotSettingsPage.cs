using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ScreenshotSettingsPage : UserControl
{
    private readonly HotkeyInputBox _hotkeyInput;
    private readonly CheckBox _startMinimized;
    private readonly CheckBox _dismissNotificationBeforeCapture;
    private readonly CheckBox _hideMainWindowDuringCapture;
    private readonly Panel _settingsCard;
    private readonly Panel _note;
    private readonly List<Panel> _settingRows = [];

    public ScreenshotSettingsPage(
        HotkeyDefinition hotkey,
        bool startMinimized,
        bool dismissSaveNotificationBeforeCapture = true,
        bool hideMainWindowDuringCapture = false)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 474,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "截图设置",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 22)
        };
        var description = AppTheme.CreateBodyLabel(
            "配置后台截图快捷键、启动方式，以及开始截图时的界面行为。",
            660);
        description.Location = new Point(28, 58);

        _hotkeyInput = new HotkeyInputBox
        {
            Hotkey = hotkey,
            Size = new Size(220, 36),
            Font = new Font("Consolas", 10.5F, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center,
            BackColor = Color.White
        };
        AddSettingRow(
            "全局截图快捷键",
            "后台运行时按下此组合键开始截图。",
            _hotkeyInput,
            105);

        _startMinimized = CreateCheckBox(startMinimized);
        AddSettingRow(
            "启动后最小化",
            "启动轻截后直接进入系统托盘，不显示主窗口。",
            _startMinimized,
            177);

        _dismissNotificationBeforeCapture = CreateCheckBox(
            dismissSaveNotificationBeforeCapture);
        AddSettingRow(
            "截图前关闭保存提示",
            "自动关闭右下角旧提示，避免它进入下一张截图。（推荐）",
            _dismissNotificationBeforeCapture,
            249);

        _hideMainWindowDuringCapture = CreateCheckBox(hideMainWindowDuringCapture);
        AddSettingRow(
            "截图时隐藏轻截主界面",
            "抓屏前立即隐藏工作台；关闭后会保留主界面，适合制作宣传截图。",
            _hideMainWindowDuringCapture,
            321);

        var saveButton = AppTheme.CreateButton("保存截图设置", primary: true);
        saveButton.Location = new Point(28, 413);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _settingsCard.Controls.AddRange([title, description, saveButton]);

        _note = new Panel
        {
            Location = new Point(0, 494),
            Height = 112,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "默认快捷键  Ctrl + Shift + X",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Success,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel(
            "如果新快捷键被其他程序占用，轻截会保留原快捷键并提示重新设置。点击输入框后直接按下新的组合键即可。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Resize += (_, _) => ResizeContent();
        ResizeContent();
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

    public bool DismissSaveNotificationBeforeCapture
    {
        get => _dismissNotificationBeforeCapture.Checked;
        set => _dismissNotificationBeforeCapture.Checked = value;
    }

    public bool HideMainWindowDuringCapture
    {
        get => _hideMainWindowDuringCapture.Checked;
        set => _hideMainWindowDuringCapture.Checked = value;
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
        var descriptionWidth = input is CheckBox
            ? 410
            : Math.Max(180, row.ClientSize.Width - input.Width - 64);
        var descriptionLabel = AppTheme.CreateBodyLabel(description, descriptionWidth);
        descriptionLabel.Location = new Point(17, 34);
        descriptionLabel.Height = 20;
        input.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        input.Location = new Point(
            row.ClientSize.Width - input.Width - 18,
            (row.ClientSize.Height - input.Height) / 2);
        row.Controls.AddRange([titleLabel, descriptionLabel, input]);
        _settingRows.Add(row);
        _settingsCard.Controls.Add(row);
    }

    private static CheckBox CreateCheckBox(bool value) => new()
    {
        Text = "启用",
        Checked = value,
        AutoSize = true,
        Font = AppTheme.CreateFont(9.5F),
        ForeColor = AppTheme.Text,
        Cursor = Cursors.Hand
    };

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
