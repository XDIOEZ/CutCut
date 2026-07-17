using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class StickerBehaviorSettingsPage : UserControl
{
    private readonly RadioButton _followSelection;
    private readonly RadioButton _keepScreenPosition;
    private readonly Panel _settingsCard;
    private readonly Panel _note;

    public StickerBehaviorSettingsPage(StickerSelectionMoveMode mode)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 350,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "移动截图框时，贴纸如何处理",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 22)
        };
        var description = AppTheme.CreateBodyLabel(
            "此设置同时作用于图片贴纸、粘贴文字和工具栏添加的文字。", 660);
        description.Location = new Point(28, 58);

        var followPanel = CreateOptionPanel(new Point(28, 94));
        _followSelection = CreateOption(
            "跟随截图框（推荐）",
            "按住右键移动截图框时，图片贴纸、粘贴文字和工具栏文字会保持相对位置并一起移动。",
            followPanel);

        var keepPanel = CreateOptionPanel(new Point(28, 178));
        _keepScreenPosition = CreateOption(
            "保持屏幕位置",
            "截图框移动时贴纸停在原处；越界部分仅暂时隐藏，不会删除，移回截图框后会重新出现。",
            keepPanel);

        var saveButton = AppTheme.CreateButton("保存贴纸设置", primary: true);
        saveButton.Location = new Point(28, 284);
        saveButton.Size = new Size(142, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        _settingsCard.Controls.AddRange(
            [title, description, followPanel, keepPanel, saveButton]);

        _note = new Panel
        {
            Location = new Point(0, 370),
            Height = 104,
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "越界内容不会丢失",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Accent,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel(
            "编辑时只显示截图框内的部分；最终保存和复制时也只输出截图框范围，但原贴纸对象会一直保留到本次截图结束。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Mode = mode;
        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? SaveRequested;

    public StickerSelectionMoveMode Mode
    {
        get => _keepScreenPosition.Checked
            ? StickerSelectionMoveMode.KeepScreenPosition
            : StickerSelectionMoveMode.FollowSelection;
        set
        {
            _keepScreenPosition.Checked = value == StickerSelectionMoveMode.KeepScreenPosition;
            _followSelection.Checked = !_keepScreenPosition.Checked;
        }
    }

    private static Panel CreateOptionPanel(Point location) => new()
    {
        Location = location,
        Height = 72,
        BackColor = Color.FromArgb(248, 250, 252),
        BorderStyle = BorderStyle.FixedSingle
    };

    private static RadioButton CreateOption(string title, string description, Panel parent)
    {
        var option = new RadioButton
        {
            Text = title,
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(16, 11)
        };
        var body = AppTheme.CreateBodyLabel(description, 620);
        body.Location = new Point(38, 39);
        body.Click += (_, _) => option.Checked = true;
        parent.Click += (_, _) => option.Checked = true;
        parent.Controls.AddRange([option, body]);
        return option;
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
