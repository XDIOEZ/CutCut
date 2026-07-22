using ScreenshotTool.Contracts;

namespace ScreenshotTool.LongCapture;

internal sealed class LongCaptureSettingsPage : UserControl, IModuleSettingsPage
{
    private static readonly Color Canvas = Color.FromArgb(244, 247, 252);
    private static readonly Color Surface = Color.White;
    private static readonly Color TextColor = Color.FromArgb(15, 23, 42);
    private static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);

    private readonly IModuleSettingsHost _settings;
    private readonly CheckBox _safetyChecks;
    private readonly Panel _settingsCard;
    private readonly Panel _note;

    public LongCaptureSettingsPage(IModuleSettingsHost settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        BackColor = Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 270,
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "长截图拼接方式",
            AutoSize = true,
            Font = CreateFont(12F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(26, 22)
        };
        var description = CreateBodyLabel(
            "默认使用宽松拼接：优先生成结果，不因低置信度接缝或固定悬浮内容主动停止。",
            660);
        description.Location = new Point(28, 58);

        var optionPanel = new Panel
        {
            Location = new Point(28, 100),
            Height = 82,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };
        _safetyChecks = new CheckBox
        {
            Text = "启用安全截图（严格重叠校验）",
            AutoSize = true,
            Font = CreateFont(9.5F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(16, 12),
            Checked = settings.GetBoolean(
                LongCapturePreferences.SafetyChecksId,
                LongCapturePreferences.DefaultSafetyChecks)
        };
        var optionBody = CreateBodyLabel(
            "开启后，连续无法确认接缝时会停止并询问是否保留已验证部分；关闭后会选择最可能的接缝或跳过异常帧继续。",
            620);
        optionBody.Location = new Point(38, 41);
        optionBody.Click += (_, _) => _safetyChecks.Checked = !_safetyChecks.Checked;
        optionPanel.Click += (_, _) => _safetyChecks.Checked = !_safetyChecks.Checked;
        optionPanel.Controls.AddRange([_safetyChecks, optionBody]);

        var saveButton = CreateButton("保存长截图设置");
        saveButton.Location = new Point(28, 204);
        saveButton.Size = new Size(154, 38);
        saveButton.Click += (_, _) => SaveSettings();

        _settingsCard.Controls.AddRange([title, description, optionPanel, saveButton]);

        _note = new Panel
        {
            Location = new Point(0, 290),
            Height = 112,
            BackColor = Color.FromArgb(255, 247, 237),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "宽松模式可能产生错位",
            AutoSize = true,
            Font = CreateFont(9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(194, 65, 12),
            Location = new Point(20, 15)
        };
        var noteBody = CreateBodyLabel(
            "关闭安全截图后，动画、重复纹理、悬浮按钮或过快滚动都可能造成重复、断层或错位；尺寸和内存上限仍会保留，避免程序崩溃。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public string Id => "screenshot-tool.long-capture.settings";
    public string Title => "长截图";
    public string Description => "选择宽松拼接或严格安全校验";
    public int Order => 300;
    public Control Content => this;

    internal bool SafetyChecksEnabled
    {
        get => _safetyChecks.Checked;
        set => _safetyChecks.Checked = value;
    }

    private void SaveSettings()
    {
        try
        {
            _settings.SetBoolean(
                LongCapturePreferences.SafetyChecksId,
                SafetyChecksEnabled);
            _settings.Save();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(
                this,
                $"长截图设置保存失败：{exception.Message}",
                "保存失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static Font CreateFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", size, style, GraphicsUnit.Point);

    private static Label CreateBodyLabel(string text, int width) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(width, 44),
        Font = CreateFont(9F),
        ForeColor = MutedText
    };

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            Font = CreateFont(9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
        return button;
    }

    private void ResizeContent()
    {
        var width = Math.Max(500, ClientSize.Width - 12);
        _settingsCard.Width = width;
        _note.Width = width;
        foreach (var panel in _settingsCard.Controls.OfType<Panel>())
        {
            panel.Width = width - 56;
        }
    }
}
