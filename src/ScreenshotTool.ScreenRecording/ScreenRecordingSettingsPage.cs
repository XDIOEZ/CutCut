using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal sealed class ScreenRecordingSettingsPage : UserControl, IModuleSettingsPage
{
    private static readonly Color Canvas = Color.FromArgb(244, 247, 252);
    private static readonly Color Surface = Color.White;
    private static readonly Color TextColor = Color.FromArgb(15, 23, 42);
    private static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);

    private readonly IModuleSettingsHost _settings;
    private readonly CheckBox _captureSystemAudio;
    private readonly CheckBox _captureMicrophone;
    private readonly CheckBox _showMouseClickIndicator;
    private readonly ComboBox _framesPerSecond;
    private readonly ComboBox _videoBitrate;
    private readonly ComboBox _regionIndicatorStyle;
    private readonly Label _oneMinuteSize;
    private readonly Label _tenMinuteSize;
    private readonly Label _oneHourSize;
    private readonly Panel _settingsCard;
    private readonly Panel _note;
    private readonly GroupBox _storageGroup;
    private readonly List<Panel> _settingRows = [];

    public ScreenRecordingSettingsPage(IModuleSettingsHost settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        BackColor = Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 805,
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "录屏设置",
            AutoSize = true,
            Font = CreateFont(12F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(26, 22)
        };
        var description = CreateBodyLabel(
            "配置录制声音、鼠标点击提示、画面帧率与码率，以及录屏范围提示。",
            660);
        description.Location = new Point(28, 58);

        _captureSystemAudio = CreateCheckBox();
        AddSettingRow(
            "录制系统声音",
            "录入应用、网页和系统提示音。",
            _captureSystemAudio,
            105);

        _captureMicrophone = CreateCheckBox();
        AddSettingRow(
            "录制麦克风",
            "录入当前系统默认麦克风。",
            _captureMicrophone,
            177);

        _showMouseClickIndicator = CreateCheckBox();
        AddSettingRow(
            "显示左键黄色圆圈",
            "按住左键时在现场和最终视频中持续跟随鼠标。",
            _showMouseClickIndicator,
            249);

        _framesPerSecond = CreateComboBox(170);
        foreach (var value in ScreenRecordingPreferences.SupportedFramesPerSecond)
        {
            _framesPerSecond.Items.Add($"{value} FPS");
        }
        AddSettingRow(
            "录制帧率",
            "帧率越高，动态画面越流畅，同时会增加编码负载。",
            _framesPerSecond,
            321);

        _videoBitrate = CreateComboBox(170);
        _videoBitrate.DisplayMember = nameof(BitrateItem.DisplayText);
        foreach (var value in ScreenRecordingPreferences.SupportedVideoBitrates)
        {
            _videoBitrate.Items.Add(new BitrateItem(value, FormatBitrate(value)));
        }
        AddSettingRow(
            "视频码率",
            "码率越高，画质越清晰，生成的 MP4 也越大。",
            _videoBitrate,
            393);

        _regionIndicatorStyle = CreateComboBox(170);
        _regionIndicatorStyle.Items.AddRange(["实线", "虚线（推荐）", "不显示"]);
        AddSettingRow(
            "录屏范围提示",
            "只在录制现场提示当前范围，不会写入最终视频。",
            _regionIndicatorStyle,
            465);

        _storageGroup = new GroupBox
        {
            Text = "预计储存占用",
            Location = new Point(28, 549),
            Size = new Size(620, 166),
            ForeColor = TextColor,
            Font = CreateFont(9F)
        };
        _storageGroup.Controls.Add(CreateDurationLabel("1 分钟", 27));
        _storageGroup.Controls.Add(CreateDurationLabel("10 分钟", 59));
        _storageGroup.Controls.Add(CreateDurationLabel("1 小时", 91));
        _oneMinuteSize = CreateSizeLabel(25);
        _tenMinuteSize = CreateSizeLabel(57);
        _oneHourSize = CreateSizeLabel(89);
        _storageGroup.Controls.AddRange([_oneMinuteSize, _tenMinuteSize, _oneHourSize]);
        _storageGroup.Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(20, 130),
            Size = new Size(570, 20),
            Text = "按目标视频码率和 128 kbps 音频估算，实际大小会随画面复杂度浮动。",
            ForeColor = MutedText,
            Font = CreateFont(8F)
        });

        var saveButton = CreateButton("保存录屏设置");
        saveButton.Location = new Point(28, 737);
        saveButton.Size = new Size(154, 38);
        saveButton.Click += (_, _) => SaveSettings();

        _settingsCard.Controls.AddRange([
            title,
            description,
            _storageGroup,
            saveButton
        ]);

        _note = new Panel
        {
            Location = new Point(0, 825),
            Height = 104,
            BackColor = Color.FromArgb(254, 242, 242),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "录制提示",
            AutoSize = true,
            Font = CreateFont(9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38),
            Location = new Point(20, 15)
        };
        var noteBody = CreateBodyLabel(
            "保存后从下一次录屏生效。范围提示不进入 MP4；黄色点击圆圈会同时显示在录制现场和最终视频中。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        CaptureSystemAudio = settings.GetBoolean(
            ScreenRecordingPreferences.CaptureSystemAudioId,
            ScreenRecordingPreferences.DefaultCaptureSystemAudio);
        CaptureMicrophone = settings.GetBoolean(
            ScreenRecordingPreferences.CaptureMicrophoneId,
            ScreenRecordingPreferences.DefaultCaptureMicrophone);
        ShowMouseClickIndicator = settings.GetBoolean(
            ScreenRecordingPreferences.ShowMouseClickIndicatorId,
            ScreenRecordingPreferences.DefaultShowMouseClickIndicator);
        FramesPerSecond = settings.GetInteger(
            ScreenRecordingPreferences.FramesPerSecondId,
            ScreenRecordingPreferences.DefaultFramesPerSecond);
        VideoBitrate = settings.GetInteger(
            ScreenRecordingPreferences.VideoBitrateId,
            ScreenRecordingPreferences.DefaultVideoBitrate);
        RegionIndicatorStyle = ScreenRecordingPreferences.NormalizeRegionIndicatorStyle(
            settings.GetInteger(
                ScreenRecordingPreferences.RegionIndicatorStyleId,
                (int)ScreenRecordingPreferences.DefaultRegionIndicatorStyle));

        _captureSystemAudio.CheckedChanged += (_, _) => UpdateStorageEstimate();
        _captureMicrophone.CheckedChanged += (_, _) => UpdateStorageEstimate();
        _videoBitrate.SelectedIndexChanged += (_, _) => UpdateStorageEstimate();
        Resize += (_, _) => ResizeContent();
        ResizeContent();
        UpdateStorageEstimate();
    }

    public string Id => "screenshot-tool.screen-recording.settings";
    public string Title => "录屏设置";
    public string Description => "设置录制声音、帧率、码率与范围提示";
    public int Order => 350;
    public Control Content => this;

    internal bool CaptureSystemAudio
    {
        get => _captureSystemAudio.Checked;
        set => _captureSystemAudio.Checked = value;
    }

    internal bool CaptureMicrophone
    {
        get => _captureMicrophone.Checked;
        set => _captureMicrophone.Checked = value;
    }

    internal bool ShowMouseClickIndicator
    {
        get => _showMouseClickIndicator.Checked;
        set => _showMouseClickIndicator.Checked = value;
    }

    internal int FramesPerSecond
    {
        get => _framesPerSecond.SelectedIndex == 1 ? 60 : 30;
        set => _framesPerSecond.SelectedIndex =
            ScreenRecordingPreferences.NormalizeFramesPerSecond(value) == 60 ? 1 : 0;
    }

    internal int VideoBitrate
    {
        get => (_videoBitrate.SelectedItem as BitrateItem)?.BitsPerSecond ??
               ScreenRecordingPreferences.DefaultVideoBitrate;
        set
        {
            var normalized = ScreenRecordingPreferences.NormalizeVideoBitrate(value);
            _videoBitrate.SelectedIndex = ScreenRecordingPreferences.SupportedVideoBitrates
                .ToList()
                .IndexOf(normalized);
        }
    }

    internal CaptureRegionIndicatorStyle RegionIndicatorStyle
    {
        get => _regionIndicatorStyle.SelectedIndex switch
        {
            0 => CaptureRegionIndicatorStyle.Solid,
            2 => CaptureRegionIndicatorStyle.None,
            _ => CaptureRegionIndicatorStyle.Dashed
        };
        set => _regionIndicatorStyle.SelectedIndex = value switch
        {
            CaptureRegionIndicatorStyle.Solid => 0,
            CaptureRegionIndicatorStyle.None => 2,
            _ => 1
        };
    }

    internal string OneMinuteEstimate => _oneMinuteSize.Text;

    private void SaveSettings()
    {
        try
        {
            _settings.SetBoolean(
                ScreenRecordingPreferences.CaptureSystemAudioId,
                CaptureSystemAudio);
            _settings.SetBoolean(
                ScreenRecordingPreferences.CaptureMicrophoneId,
                CaptureMicrophone);
            _settings.SetBoolean(
                ScreenRecordingPreferences.ShowMouseClickIndicatorId,
                ShowMouseClickIndicator);
            _settings.SetInteger(
                ScreenRecordingPreferences.FramesPerSecondId,
                FramesPerSecond);
            _settings.SetInteger(
                ScreenRecordingPreferences.VideoBitrateId,
                VideoBitrate);
            _settings.SetInteger(
                ScreenRecordingPreferences.RegionIndicatorStyleId,
                (int)RegionIndicatorStyle);
            _settings.Save();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(
                this,
                $"录屏设置保存失败：{exception.Message}",
                "保存失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateStorageEstimate()
    {
        var includesAudio = CaptureSystemAudio || CaptureMicrophone;
        _oneMinuteSize.Text = EstimateText(includesAudio, TimeSpan.FromMinutes(1));
        _tenMinuteSize.Text = EstimateText(includesAudio, TimeSpan.FromMinutes(10));
        _oneHourSize.Text = EstimateText(includesAudio, TimeSpan.FromHours(1));
    }

    private string EstimateText(bool includesAudio, TimeSpan duration) =>
        ScreenRecordingStorageEstimator.FormatBytes(
            ScreenRecordingStorageEstimator.EstimateBytes(
                VideoBitrate,
                includesAudio,
                duration));

    private static string FormatBitrate(int bitsPerSecond) => bitsPerSecond switch
    {
        2_000_000 => "2 Mbps（省空间）",
        4_000_000 => "4 Mbps（流畅）",
        8_000_000 => "8 Mbps（高清）",
        12_000_000 => "12 Mbps（高质量）",
        _ => "20 Mbps（极高）"
    };

    private static Font CreateFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", size, style, GraphicsUnit.Point);

    private void AddSettingRow(
        string title,
        string description,
        Control input,
        int top)
    {
        var row = new Panel
        {
            Location = new Point(28, top),
            Size = new Size(620, 64),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Tag = "SettingRow"
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = CreateFont(9.5F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(16, 9)
        };
        var descriptionLabel = new Label
        {
            Text = description,
            AutoSize = false,
            Size = new Size(300, 20),
            Font = CreateFont(8.5F),
            ForeColor = MutedText,
            Location = new Point(17, 34)
        };
        input.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        input.Location = new Point(row.ClientSize.Width - input.Width - 18, 15);
        row.Controls.AddRange([titleLabel, descriptionLabel, input]);
        _settingRows.Add(row);
        _settingsCard.Controls.Add(row);
    }

    private static CheckBox CreateCheckBox() => new()
    {
        Text = "启用",
        AutoSize = true,
        Font = CreateFont(9.5F),
        ForeColor = TextColor,
        Cursor = Cursors.Hand
    };

    private static Label CreateBodyLabel(string text, int width) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(width, 44),
        Font = CreateFont(9F),
        ForeColor = MutedText
    };

    private static ComboBox CreateComboBox(int width) => new()
    {
        Size = new Size(width, 34),
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = CreateFont(10F)
    };

    private static Label CreateDurationLabel(string text, int top) => new()
    {
        AutoSize = false,
        Location = new Point(20, top),
        Size = new Size(200, 19),
        Text = text,
        ForeColor = MutedText
    };

    private static Label CreateSizeLabel(int top) => new()
    {
        AutoSize = false,
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
        Location = new Point(430, top),
        Size = new Size(165, 28),
        TextAlign = ContentAlignment.MiddleRight,
        Font = CreateFont(10F, FontStyle.Bold),
        ForeColor = Accent
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
        var width = Math.Max(570, ClientSize.Width - 28);
        _settingsCard.Width = width;
        _note.Width = width;
        var rowWidth = width - 56;
        foreach (var row in _settingRows)
        {
            row.Width = rowWidth;
        }
        _storageGroup.Width = rowWidth;
    }

    private sealed record BitrateItem(int BitsPerSecond, string DisplayText);
}
