using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class SavePathSettingsPage : UserControl
{
    private readonly TextBox _folderInput;
    private readonly Label _dropHint;
    private readonly Button _browseButton;
    private readonly ComboBox _imageFormatInput;
    private readonly Label _imageFormatHint;
    private readonly ComboBox _fileNameModeInput;
    private readonly Label _fileNameModeHint;
    private readonly CheckBox _organizeByDateInput;
    private readonly Label _dateParentLabel;
    private readonly TextBox _dateParentInput;
    private readonly Button _dateParentBrowseButton;
    private readonly Button _saveButton;
    private readonly Button _openButton;
    private readonly Panel _card;
    private readonly Panel _note;
    private readonly Label _noteBody;

    // Builds the shared screenshot and recording path settings page.
    public SavePathSettingsPage(
        string folderPath,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        bool organizeByDate = false,
        string? dateParentFolder = null,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _card = new Panel
        {
            Location = new Point(0, 0),
            Height = 530,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_card);

        var title = new Label
        {
            Text = "截图与录屏保存文件夹",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 23)
        };
        var description = AppTheme.CreateBodyLabel(
            "截图按 Ctrl + S 保存到此目录并复制到剪贴板；录屏完成后也会保存到同一目录。",
            650);
        description.Location = new Point(28, 58);

        _folderInput = new TextBox
        {
            Text = folderPath,
            Location = new Point(28, 101),
            Height = 36,
            Font = AppTheme.CreateFont(9.5F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 250, 252)
        };

        _dropHint = new Label
        {
            Text = "也可以把一个文件夹拖到路径框中",
            AutoSize = true,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.Accent,
            Location = new Point(28, 137)
        };

        _browseButton = AppTheme.CreateButton("浏览…");
        _browseButton.Size = new Size(86, 36);
        _browseButton.Top = 99;
        _browseButton.Click += (_, _) => BrowseRequested?.Invoke(this, EventArgs.Empty);

        var imageFormatLabel = new Label
        {
            Text = "图片保存格式",
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 170)
        };

        _imageFormatInput = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(28, 196),
            Size = new Size(390, 34),
            Font = AppTheme.CreateFont(9.5F),
            BackColor = Color.White
        };
        _imageFormatInput.Items.AddRange(
        [
            new ImageFormatOption(ScreenshotImageFormat.Png, "PNG（无损，推荐）"),
            new ImageFormatOption(ScreenshotImageFormat.Jpeg, "JPEG（体积较小）")
        ]);
        var normalizedImageFormat = ScreenshotImageFormatPolicy.Normalize(imageFormat);
        _imageFormatInput.SelectedItem = _imageFormatInput.Items
            .Cast<ImageFormatOption>()
            .First(option => option.Format == normalizedImageFormat);
        _imageFormatInput.SelectedIndexChanged += (_, _) => UpdateImageFormatPresentation();

        _imageFormatHint = AppTheme.CreateBodyLabel(string.Empty, 650);
        _imageFormatHint.Font = AppTheme.CreateFont(8.5F);
        _imageFormatHint.Location = new Point(30, 234);

        var fileNameModeLabel = new Label
        {
            Text = "图片命名规则",
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 276)
        };

        _fileNameModeInput = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(28, 302),
            Size = new Size(390, 34),
            Font = AppTheme.CreateFont(9.5F),
            BackColor = Color.White
        };
        _fileNameModeInput.Items.AddRange(
        [
            new FileNameModeOption(ScreenshotFileNameMode.DateTime, "日期 + 时间"),
            new FileNameModeOption(ScreenshotFileNameMode.Sequence, "当前目录序号（0、1、2…）"),
            new FileNameModeOption(ScreenshotFileNameMode.ImageText, "图片内输入的文字")
        ]);
        _fileNameModeInput.SelectedItem = _fileNameModeInput.Items
            .Cast<FileNameModeOption>()
            .First(option => option.Mode == fileNameMode);
        _fileNameModeInput.SelectedIndexChanged += (_, _) => UpdateFileNameModeHint();

        _fileNameModeHint = AppTheme.CreateBodyLabel(string.Empty, 650);
        _fileNameModeHint.Font = AppTheme.CreateFont(8.5F);
        _fileNameModeHint.Location = new Point(30, 340);

        _organizeByDateInput = new CheckBox
        {
            Text = "按日期自动创建子文件夹",
            Checked = organizeByDate,
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 382)
        };
        var organizeByDateHint = AppTheme.CreateBodyLabel(
            "启用后，截图和录屏都会保存到父目录下当天的文件夹，例如 2026-08-11；同一天自动复用，跨天自动新建。",
            650);
        organizeByDateHint.Font = AppTheme.CreateFont(8.5F);
        organizeByDateHint.Location = new Point(30, 410);
        _organizeByDateInput.CheckedChanged += (_, _) => UpdateDateParentVisibility();

        _dateParentLabel = new Label
        {
            Text = "日期分类父文件夹",
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 462)
        };
        _dateParentInput = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(dateParentFolder) ? folderPath : dateParentFolder,
            Location = new Point(28, 488),
            Height = 36,
            Font = AppTheme.CreateFont(9.5F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        _dateParentBrowseButton = AppTheme.CreateButton("浏览…");
        _dateParentBrowseButton.Size = new Size(86, 36);
        _dateParentBrowseButton.Top = 486;
        _dateParentBrowseButton.Click += (_, _) =>
            DateParentBrowseRequested?.Invoke(this, EventArgs.Empty);

        _saveButton = AppTheme.CreateButton("保存设置", primary: true);
        _saveButton.Location = new Point(28, 556);
        _saveButton.Size = new Size(118, 38);
        _saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        _openButton = AppTheme.CreateButton("打开文件夹");
        _openButton.Location = new Point(158, 556);
        _openButton.Size = new Size(118, 38);
        _openButton.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _card.Controls.AddRange(
            [
                title,
                description,
                _folderInput,
                _dropHint,
                _browseButton,
                imageFormatLabel,
                _imageFormatInput,
                _imageFormatHint,
                fileNameModeLabel,
                _fileNameModeInput,
                _fileNameModeHint,
                _organizeByDateInput,
                organizeByDateHint,
                _dateParentLabel,
                _dateParentInput,
                _dateParentBrowseButton,
                _saveButton,
                _openButton
            ]);

        EnableFolderDrop(_card);
        EnableFolderDrop(_folderInput);
        EnableFolderDrop(_dropHint);

        _note = new Panel
        {
            Location = new Point(0, 550),
            Height = 112,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "保存与复制",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Success,
            Location = new Point(20, 15)
        };
        _noteBody = AppTheme.CreateBodyLabel(string.Empty, 650);
        _noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, _noteBody]);
        Controls.Add(_note);
        UpdateImageFormatPresentation();
        UpdateDateParentVisibility();

        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? BrowseRequested;
    public event EventHandler? DateParentBrowseRequested;
    public event EventHandler? OpenRequested;
    public event EventHandler? SaveRequested;

    public string FolderPath
    {
        get => _folderInput.Text;
        set => _folderInput.Text = value;
    }

    public ScreenshotFileNameMode FileNameMode =>
        (_fileNameModeInput.SelectedItem as FileNameModeOption)?.Mode ??
        ScreenshotFileNameMode.DateTime;

    public ScreenshotImageFormat ImageFormat =>
        (_imageFormatInput.SelectedItem as ImageFormatOption)?.Format ??
        ScreenshotImageFormat.Png;

    public bool OrganizeByDate => _organizeByDateInput.Checked;

    public string DateParentFolder
    {
        get => _dateParentInput.Text;
        set => _dateParentInput.Text = value;
    }

    // Keeps every setting row readable when the settings workspace is resized.
    private void ResizeContent()
    {
        var width = Math.Max(460, ClientSize.Width - 30);
        _card.Width = width;
        _note.Width = width;
        _browseButton.Left = width - _browseButton.Width - 28;
        _dateParentBrowseButton.Left = width - _dateParentBrowseButton.Width - 28;
        _folderInput.Width = Math.Max(240, _browseButton.Left - _folderInput.Left - 12);
        _dateParentInput.Width = Math.Max(
            240,
            _dateParentBrowseButton.Left - _dateParentInput.Left - 12);
        _imageFormatInput.Width = Math.Min(430, Math.Max(280, width - 56));
        _fileNameModeInput.Width = Math.Min(430, Math.Max(280, width - 56));
    }

    // Shows the optional date-parent row and reflows the action and note sections vertically.
    private void UpdateDateParentVisibility()
    {
        var visible = _organizeByDateInput.Checked;
        _dateParentLabel.Visible = visible;
        _dateParentInput.Visible = visible;
        _dateParentBrowseButton.Visible = visible;
        var actionTop = visible ? 556 : 470;
        _saveButton.Top = actionTop;
        _openButton.Top = actionTop;
        _card.Height = actionTop + 60;
        _note.Top = _card.Bottom + 20;
    }

    // Updates format guidance, filename examples, and the shared save summary.
    private void UpdateImageFormatPresentation()
    {
        _imageFormatHint.Text = ImageFormat == ScreenshotImageFormat.Jpeg
            ? "使用高质量有损压缩，文件更小；透明区域会以白色保存。"
            : "无损保存，文字、细线和透明区域保持完整，适合继续编辑。";
        UpdateFileNameModeHint();
        _noteBody.Text =
            $"截图、贴图与收藏图片按当前 {GetImageFormatDisplayName()} 格式和命名规则保存；Ctrl + S 仍会复制最终画面。录屏继续保存 MP4。";
    }

    // Updates filename guidance with the extension selected by the user.
    private void UpdateFileNameModeHint()
    {
        var extension = ScreenshotImageFormatPolicy.GetFileExtension(ImageFormat);
        _fileNameModeHint.Text = FileNameMode switch
        {
            ScreenshotFileNameMode.Sequence =>
                $"从目录中已有的数字图片继续递增；没有数字文件时从 0{extension} 开始。",
            ScreenshotFileNameMode.ImageText =>
                "组合图片内的文字元素并清理非法字符；没有可用文字时自动改用日期 + 时间。",
            _ => $"示例：截图_2026-07-21_14-30-00-123{extension}"
        };
    }

    // Returns the user-facing name of the currently selected image format.
    private string GetImageFormatDisplayName() =>
        ImageFormat == ScreenshotImageFormat.Jpeg ? "JPEG（.jpg）" : "PNG（.png）";

    // Enables folder drag-and-drop on one part of the path settings row.
    private void EnableFolderDrop(Control target)
    {
        target.AllowDrop = true;
        target.DragEnter += HandleFolderDragEnter;
        target.DragOver += HandleFolderDragEnter;
        target.DragLeave += HandleFolderDragLeave;
        target.DragDrop += HandleFolderDragDrop;
    }

    // Shows whether the dragged data can resolve to exactly one folder.
    private void HandleFolderDragEnter(object? sender, DragEventArgs e)
    {
        var canAccept = FolderDropPathResolver.TryResolve(GetDroppedPaths(e.Data), out _);
        e.Effect = canAccept ? DragDropEffects.Link : DragDropEffects.None;
        _dropHint.Text = canAccept
            ? "松开鼠标即可引用此文件夹"
            : "请只拖入一个真实文件夹";
        _dropHint.ForeColor = canAccept ? AppTheme.Success : AppTheme.Danger;
    }

    // Restores the idle drag-and-drop hint after the pointer leaves the row.
    private void HandleFolderDragLeave(object? sender, EventArgs e) => ResetDropHint();

    // Applies a valid dropped folder to the editable save path.
    private void HandleFolderDragDrop(object? sender, DragEventArgs e)
    {
        if (!FolderDropPathResolver.TryResolve(GetDroppedPaths(e.Data), out var folderPath))
        {
            _dropHint.Text = "引用失败：请只拖入一个真实文件夹";
            _dropHint.ForeColor = AppTheme.Danger;
            return;
        }

        FolderPath = folderPath;
        _folderInput.SelectionStart = _folderInput.TextLength;
        _folderInput.SelectionLength = 0;
        _dropHint.Text = "已引用文件夹，点击“保存设置”后生效";
        _dropHint.ForeColor = AppTheme.Success;
        e.Effect = DragDropEffects.Link;
    }

    // Restores the default folder drop guidance.
    private void ResetDropHint()
    {
        _dropHint.Text = "也可以把一个文件夹拖到路径框中";
        _dropHint.ForeColor = AppTheme.Accent;
    }

    // Reads Explorer file-drop paths without converting unrelated clipboard formats.
    private static string[]? GetDroppedPaths(IDataObject? data) =>
        data?.GetDataPresent(DataFormats.FileDrop, autoConvert: true) == true
            ? data.GetData(DataFormats.FileDrop, autoConvert: true) as string[]
            : null;

    private sealed record ImageFormatOption(ScreenshotImageFormat Format, string Text)
    {
        // Displays the localized label in the format combo box.
        public override string ToString() => Text;
    }

    private sealed record FileNameModeOption(ScreenshotFileNameMode Mode, string Text)
    {
        // Displays the localized label in the naming-rule combo box.
        public override string ToString() => Text;
    }
}
