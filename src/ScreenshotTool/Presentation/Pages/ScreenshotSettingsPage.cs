using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ScreenshotSettingsPage : UserControl
{
    private readonly HotkeyInputBox[] _hotkeyInputs;
    private readonly CheckBox _dismissNotificationBeforeCapture;
    private readonly CheckBox _hideMainWindowDuringCapture;
    private readonly Panel _settingsCard;
    private readonly Panel _note;
    private readonly List<Panel> _settingRows = [];

    public ScreenshotSettingsPage(
        IReadOnlyList<HotkeyDefinition> hotkeys,
        bool dismissSaveNotificationBeforeCapture = true,
        bool hideMainWindowDuringCapture = false)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 544,
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
            "配置后台截图快捷键，以及开始截图时的界面行为。",
            660);
        description.Location = new Point(28, 58);

        _hotkeyInputs = Enumerable.Range(0, HotkeyBindings.MaximumCount)
            .Select(CreateHotkeyInput)
            .ToArray();
        SetHotkeys(hotkeys);
        AddSettingRow(
            "截图快捷键 1",
            "首选截图组合键；可留空。",
            CreateHotkeyEditor(_hotkeyInputs[0], 0),
            105);
        AddSettingRow(
            "截图快捷键 2",
            "另一套截图组合键；可留空。",
            CreateHotkeyEditor(_hotkeyInputs[1], 1),
            177);
        AddSettingRow(
            "截图快捷键 3",
            "第三套截图组合键；可留空。",
            CreateHotkeyEditor(_hotkeyInputs[2], 2),
            249);

        _dismissNotificationBeforeCapture = CreateCheckBox(
            dismissSaveNotificationBeforeCapture);
        AddSettingRow(
            "截图前关闭保存提示",
            "自动关闭右下角旧提示，避免它进入下一张截图。（推荐）",
            _dismissNotificationBeforeCapture,
            321);

        _hideMainWindowDuringCapture = CreateCheckBox(hideMainWindowDuringCapture);
        AddSettingRow(
            "截图时隐藏轻截主界面",
            "抓屏前立即隐藏工作台；关闭后会保留主界面，适合制作宣传截图。",
            _hideMainWindowDuringCapture,
            393);

        var saveButton = AppTheme.CreateButton("保存截图设置", primary: true);
        saveButton.Location = new Point(28, 483);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);
        _settingsCard.Controls.AddRange([title, description, saveButton]);

        _note = new Panel
        {
            Location = new Point(0, 564),
            Height = 112,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "最多绑定 3 个快捷键",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Success,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel(
            "默认使用 Ctrl + Shift + X。点击输入框后直接按下新的组合键；点击旁边的“删除”可以让该槽位保持未绑定。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? SaveRequested;

    public IReadOnlyList<HotkeyDefinition> Hotkeys => _hotkeyInputs
        .Select(input => input.Hotkey)
        .Where(hotkey => hotkey is not null)
        .Cast<HotkeyDefinition>()
        .ToArray();

    public void SetHotkeys(IEnumerable<HotkeyDefinition> hotkeys)
    {
        var normalized = HotkeyBindings.Normalize(hotkeys);
        for (var index = 0; index < _hotkeyInputs.Length; index++)
        {
            _hotkeyInputs[index].Hotkey = index < normalized.Count
                ? normalized[index]
                : null;
        }
    }

    public event EventHandler? HotkeyInputEntered
    {
        add
        {
            foreach (var input in _hotkeyInputs)
            {
                input.Enter += value;
            }
        }
        remove
        {
            foreach (var input in _hotkeyInputs)
            {
                input.Enter -= value;
            }
        }
    }

    public event EventHandler? HotkeyInputLeft
    {
        add
        {
            foreach (var input in _hotkeyInputs)
            {
                input.Leave += value;
            }
        }
        remove
        {
            foreach (var input in _hotkeyInputs)
            {
                input.Leave -= value;
            }
        }
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

    private static HotkeyInputBox CreateHotkeyInput(int index) => new()
    {
        Name = $"ScreenshotHotkeyInput{index + 1}",
        Size = new Size(220, 36),
        Font = new Font("Consolas", 10.5F, FontStyle.Bold),
        BorderStyle = BorderStyle.FixedSingle,
        TextAlign = HorizontalAlignment.Center,
        BackColor = Color.White
    };

    private Control CreateHotkeyEditor(HotkeyInputBox input, int index)
    {
        var editor = new Panel
        {
            Size = new Size(302, 36),
            BackColor = Color.Transparent,
            Tag = "SettingInput"
        };
        input.Location = Point.Empty;
        var deleteButton = AppTheme.CreateButton("删除");
        deleteButton.Name = $"DeleteScreenshotHotkeyButton{index + 1}";
        deleteButton.Location = new Point(228, 0);
        deleteButton.Size = new Size(74, 36);
        deleteButton.Click += (_, _) => input.Hotkey = null;
        editor.Controls.AddRange([input, deleteButton]);
        return editor;
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
