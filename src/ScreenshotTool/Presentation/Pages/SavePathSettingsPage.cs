using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class SavePathSettingsPage : UserControl
{
    private readonly TextBox _folderInput;
    private readonly Label _dropHint;
    private readonly Button _browseButton;
    private readonly ComboBox _fileNameModeInput;
    private readonly Label _fileNameModeHint;
    private readonly Panel _card;
    private readonly Panel _note;

    public SavePathSettingsPage(
        string folderPath,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _card = new Panel
        {
            Location = new Point(0, 0),
            Height = 342,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(26, 22, 26, 22)
        };
        Controls.Add(_card);

        var title = new Label
        {
            Text = "截图保存文件夹",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 23)
        };
        var description = AppTheme.CreateBodyLabel("按 Ctrl + S 后，截图会按下方规则命名并保存到此目录，同时复制到剪贴板。", 650);
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

        var fileNameModeLabel = new Label
        {
            Text = "图片命名规则",
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 170)
        };

        _fileNameModeInput = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(28, 196),
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
        _fileNameModeHint.Location = new Point(30, 234);
        UpdateFileNameModeHint();

        var saveButton = AppTheme.CreateButton("保存设置", primary: true);
        saveButton.Location = new Point(28, 280);
        saveButton.Size = new Size(118, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        var openButton = AppTheme.CreateButton("打开文件夹");
        openButton.Location = new Point(158, 280);
        openButton.Size = new Size(118, 38);
        openButton.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _card.Controls.AddRange(
            [
                title,
                description,
                _folderInput,
                _dropHint,
                _browseButton,
                fileNameModeLabel,
                _fileNameModeInput,
                _fileNameModeHint,
                saveButton,
                openButton
            ]);

        EnableFolderDrop(_card);
        EnableFolderDrop(_folderInput);
        EnableFolderDrop(_dropHint);

        _note = new Panel
        {
            Location = new Point(0, 362),
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
        var noteBody = AppTheme.CreateBodyLabel(
            "Ctrl + S 会按当前规则保存 PNG 并复制到剪贴板；只需要复制最终画面时可在截图编辑器中按 Ctrl + C。",
            650);
        noteBody.Location = new Point(22, 47);
        _note.Controls.AddRange([noteTitle, noteBody]);
        Controls.Add(_note);

        Resize += (_, _) => ResizeContent();
        ResizeContent();
    }

    public event EventHandler? BrowseRequested;
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

    private void ResizeContent()
    {
        var width = Math.Max(460, ClientSize.Width - 30);
        _card.Width = width;
        _note.Width = width;
        _browseButton.Left = width - _browseButton.Width - 28;
        _folderInput.Width = Math.Max(240, _browseButton.Left - _folderInput.Left - 12);
        _fileNameModeInput.Width = Math.Min(430, Math.Max(280, width - 56));
    }

    private void UpdateFileNameModeHint()
    {
        _fileNameModeHint.Text = FileNameMode switch
        {
            ScreenshotFileNameMode.Sequence =>
                "从目录中已有的数字 PNG 继续递增；没有数字文件时从 0.png 开始。",
            ScreenshotFileNameMode.ImageText =>
                "组合图片内的文字元素并清理非法字符；没有可用文字时自动改用日期 + 时间。",
            _ => "示例：截图_2026-07-21_14-30-00-123.png"
        };
    }

    private void EnableFolderDrop(Control target)
    {
        target.AllowDrop = true;
        target.DragEnter += HandleFolderDragEnter;
        target.DragOver += HandleFolderDragEnter;
        target.DragLeave += HandleFolderDragLeave;
        target.DragDrop += HandleFolderDragDrop;
    }

    private void HandleFolderDragEnter(object? sender, DragEventArgs e)
    {
        var canAccept = FolderDropPathResolver.TryResolve(GetDroppedPaths(e.Data), out _);
        e.Effect = canAccept ? DragDropEffects.Link : DragDropEffects.None;
        _dropHint.Text = canAccept
            ? "松开鼠标即可引用此文件夹"
            : "请只拖入一个真实文件夹";
        _dropHint.ForeColor = canAccept ? AppTheme.Success : AppTheme.Danger;
    }

    private void HandleFolderDragLeave(object? sender, EventArgs e) => ResetDropHint();

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

    private void ResetDropHint()
    {
        _dropHint.Text = "也可以把一个文件夹拖到路径框中";
        _dropHint.ForeColor = AppTheme.Accent;
    }

    private static string[]? GetDroppedPaths(IDataObject? data) =>
        data?.GetDataPresent(DataFormats.FileDrop, autoConvert: true) == true
            ? data.GetData(DataFormats.FileDrop, autoConvert: true) as string[]
            : null;

    private sealed record FileNameModeOption(ScreenshotFileNameMode Mode, string Text)
    {
        public override string ToString() => Text;
    }
}
