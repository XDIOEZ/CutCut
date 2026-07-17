using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class SavePathSettingsPage : UserControl
{
    private readonly TextBox _folderInput;
    private readonly Label _dropHint;
    private readonly Panel _card;
    private readonly Panel _note;

    public SavePathSettingsPage(string folderPath)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _card = new Panel
        {
            Location = new Point(0, 0),
            Height = 260,
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
        var description = AppTheme.CreateBodyLabel("按 Ctrl + S 后，截图会以时间命名保存到此目录，并同步复制到剪贴板。", 650);
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

        var browseButton = AppTheme.CreateButton("浏览…");
        browseButton.Size = new Size(86, 36);
        browseButton.Top = 99;
        browseButton.Click += (_, _) => BrowseRequested?.Invoke(this, EventArgs.Empty);

        var saveButton = AppTheme.CreateButton("保存路径", primary: true);
        saveButton.Location = new Point(28, 178);
        saveButton.Size = new Size(118, 38);
        saveButton.Click += (_, _) => SaveRequested?.Invoke(this, EventArgs.Empty);

        var openButton = AppTheme.CreateButton("打开文件夹");
        openButton.Location = new Point(158, 178);
        openButton.Size = new Size(118, 38);
        openButton.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _card.Controls.AddRange(
            [title, description, _folderInput, _dropHint, browseButton, saveButton, openButton]);

        EnableFolderDrop(_card);
        EnableFolderDrop(_folderInput);
        EnableFolderDrop(_dropHint);

        _note = new Panel
        {
            Location = new Point(0, 280),
            Height = 104,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "保存与剪贴板同步完成",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Success,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel("保存成功后右下角会显示通知，点击通知可以打开目录并选中刚保存的图片。", 650);
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

    private void ResizeContent()
    {
        var width = Math.Max(460, ClientSize.Width - 12);
        _card.Width = width;
        _note.Width = width;
        var browseButton = _card.Controls.OfType<Button>().First(button => button.Text == "浏览…");
        browseButton.Left = width - browseButton.Width - 28;
        _folderInput.Width = Math.Max(240, browseButton.Left - _folderInput.Left - 12);
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
        _dropHint.Text = "已引用文件夹，点击“保存路径”后生效";
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
}
