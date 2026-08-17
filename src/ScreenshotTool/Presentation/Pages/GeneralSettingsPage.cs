using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class GeneralSettingsPage : UserControl
{
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _startMinimized;
    private readonly Panel _settingsCard;
    private readonly List<Panel> _settingRows = [];

    public GeneralSettingsPage(bool startMinimized, bool startWithWindows)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 326,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "通用设置",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 22)
        };
        var description = AppTheme.CreateBodyLabel(
            "配置轻截的开机启动与工作台显示方式。",
            660);
        description.Location = new Point(28, 58);

        _startWithWindows = CreateCheckBox(startWithWindows);
        AddSettingRow(
            "开机自动启动",
            "登录 Windows 后自动启动轻截并进入系统托盘，无需管理员权限。",
            _startWithWindows,
            105);

        _startMinimized = CreateCheckBox(startMinimized);
        AddSettingRow(
            "手动启动后最小化",
            "平时双击轻截启动时也直接进入系统托盘，不显示主窗口。",
            _startMinimized,
            177);

        var saveButton = AppTheme.CreateButton("保存通用设置", primary: true);
        saveButton.Location = new Point(28, 263);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _settingsCard.Controls.AddRange([title, description, saveButton]);

        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? SaveRequested;

    public bool StartMinimized
    {
        get => _startMinimized.Checked;
        set => _startMinimized.Checked = value;
    }

    public bool StartWithWindows
    {
        get => _startWithWindows.Checked;
        set => _startWithWindows.Checked = value;
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
        var descriptionLabel = AppTheme.CreateBodyLabel(description, 410);
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
        var rowWidth = width - 56;
        foreach (var row in _settingRows)
        {
            row.Width = rowWidth;
        }
    }
}
