using ScreenshotTool.Contracts;

namespace ScreenshotTool.Favorites;

internal sealed class FavoritesSettingsPage : UserControl, IModuleSettingsPage
{
    private static readonly Color Canvas = Color.FromArgb(244, 247, 252);
    private static readonly Color Surface = Color.White;
    private static readonly Color TextColor = Color.FromArgb(15, 23, 42);
    private static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);

    private readonly IModuleSettingsHost _settings;
    private readonly Panel _settingsCard;
    private readonly Panel _folderRow;
    private readonly TextBox _folderPath;
    private readonly Button _browseButton;
    private readonly Panel _note;

    // Builds the single-column folder configuration owned entirely by the module.
    public FavoritesSettingsPage(IModuleSettingsHost settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        BackColor = Canvas;
        AutoScroll = true;

        _settingsCard = new Panel
        {
            Location = Point.Empty,
            Height = 320,
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_settingsCard);

        var title = new Label
        {
            Text = "收藏夹设置",
            AutoSize = true,
            Font = CreateFont(12F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(26, 22)
        };
        var description = CreateBodyLabel(
            "截图完成后点击“收藏”，最终图片会直接保存到下面的独立文件夹。",
            660);
        description.Location = new Point(28, 58);

        _folderRow = new Panel
        {
            Location = new Point(28, 105),
            Size = new Size(620, 112),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };
        var folderTitle = new Label
        {
            Text = "收藏文件夹",
            AutoSize = true,
            Font = CreateFont(9.5F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(16, 12)
        };
        var folderDescription = CreateBodyLabel(
            "收藏图片不会进入普通保存目录，也不会套用按日期自动分组。",
            560);
        folderDescription.Location = new Point(17, 39);
        folderDescription.Height = 22;
        _folderPath = new TextBox
        {
            Location = new Point(17, 70),
            Size = new Size(455, 30),
            Font = CreateFont(9.5F),
            Text = FavoritesPreferences.ResolveStoredFolder(
                settings.GetString(
                    FavoritesPreferences.FolderId,
                    FavoritesPreferences.GetDefaultFolder()))
        };
        _browseButton = CreateButton("选择文件夹");
        _browseButton.Location = new Point(487, 68);
        _browseButton.Size = new Size(112, 34);
        _browseButton.Click += (_, _) => BrowseFolder();
        _folderRow.Controls.AddRange([
            folderTitle,
            folderDescription,
            _folderPath,
            _browseButton
        ]);

        var saveButton = CreateButton("保存收藏夹设置");
        saveButton.Location = new Point(28, 246);
        saveButton.Size = new Size(164, 40);
        saveButton.Click += (_, _) => SaveSettings();
        _settingsCard.Controls.AddRange([
            title,
            description,
            _folderRow,
            saveButton
        ]);

        _note = new Panel
        {
            Location = new Point(0, 340),
            Height = 112,
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "收藏与普通保存互不影响",
            AutoSize = true,
            Font = CreateFont(9.5F, FontStyle.Bold),
            ForeColor = Accent,
            Location = new Point(20, 15)
        };
        var noteBody = CreateBodyLabel(
            "更改目录后从下一次截图生效。图片仍沿用轻截当前的保存格式和命名方式；目录不存在时会在首次收藏时自动创建。",
            660);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public string Id => "screenshot-tool.favorites.settings";
    public string Title => "收藏夹";
    public string Description => "配置收藏图片的独立保存文件夹";
    public int Order => 200;
    public Control Content => this;

    internal string FolderPath
    {
        get => _folderPath.Text;
        set => _folderPath.Text = value;
    }

    // Opens the native folder picker without coupling the module to host presentation services.
    private void BrowseFolder()
    {
        var currentFolder = FavoritesPreferences.ResolveStoredFolder(_folderPath.Text);
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择截图收藏文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(currentFolder)
                ? currentFolder
                : FavoritesPreferences.GetDefaultFolder()
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderPath.Text = dialog.SelectedPath;
        }
    }

    // Validates and persists the folder while leaving actual image IO to the host.
    private void SaveSettings()
    {
        try
        {
            var folder = FavoritesPreferences.NormalizeFolder(_folderPath.Text);
            _settings.SetString(FavoritesPreferences.FolderId, folder);
            _settings.Save();
            _folderPath.Text = folder;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException)
        {
            MessageBox.Show(
                this,
                $"收藏文件夹保存失败：{exception.Message}",
                "保存失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // Creates a module-local font without referencing the host theme assembly.
    private static Font CreateFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", size, style, GraphicsUnit.Point);

    // Creates body copy with consistent wrapping and muted emphasis.
    private static Label CreateBodyLabel(string text, int width) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(width, 44),
        Font = CreateFont(9F),
        ForeColor = MutedText
    };

    // Creates an action button that follows the module page's visual language.
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

    // Keeps the folder setting on one vertical row while adapting to window width.
    private void ResizeContent()
    {
        var width = Math.Max(540, ClientSize.Width - 12);
        _settingsCard.Width = width;
        _note.Width = width;
        _folderRow.Width = width - 56;
        _browseButton.Left = _folderRow.ClientSize.Width - _browseButton.Width - 17;
        _folderPath.Width = Math.Max(
            220,
            _browseButton.Left - _folderPath.Left - 14);
    }
}
