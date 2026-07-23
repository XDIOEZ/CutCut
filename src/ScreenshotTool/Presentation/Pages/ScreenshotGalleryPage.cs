using ScreenshotTool.Abstractions;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ScreenshotGalleryPage : UserControl
{
    private readonly IFileLocationService _fileLocationService;
    private readonly ISavedScreenshotService _savedScreenshotService;
    private readonly FlowLayoutPanel _toolbar;
    private readonly ListView _listView;
    private readonly ImageList _images;
    private readonly ContextMenuStrip _itemMenu;
    private readonly Label _countLabel;
    private readonly Label _emptyLabel;
    private string _folderPath;

    public ScreenshotGalleryPage(
        string folderPath,
        IFileLocationService fileLocationService,
        ISavedScreenshotService savedScreenshotService)
    {
        _folderPath = folderPath;
        _fileLocationService = fileLocationService;
        _savedScreenshotService = savedScreenshotService;
        BackColor = AppTheme.Canvas;

        _toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.Canvas,
            Padding = new Padding(0, 0, 0, 8)
        };
        var refreshButton = AppTheme.CreateButton("刷新");
        refreshButton.Size = new Size(82, 36);
        refreshButton.Click += (_, _) => RefreshScreenshots();
        var openFolderButton = AppTheme.CreateButton("打开保存目录");
        openFolderButton.Size = new Size(126, 36);
        openFolderButton.Click += (_, _) => OpenFolder();
        _countLabel = new Label
        {
            AutoSize = false,
            Size = new Size(240, 36),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.MutedText,
            Margin = new Padding(12, 0, 0, 0)
        };
        _toolbar.Controls.AddRange([refreshButton, openFolderButton, _countLabel]);
        Controls.Add(_toolbar);

        _images = new ImageList
        {
            ImageSize = new Size(168, 104),
            ColorDepth = ColorDepth.Depth32Bit
        };
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            LargeImageList = _images,
            MultiSelect = false,
            HideSelection = false,
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppTheme.CreateFont(8.5F),
            Padding = new Padding(8)
        };
        _itemMenu = new ContextMenuStrip
        {
            Font = AppTheme.CreateFont(9F),
            ShowImageMargin = false
        };
        var editItem = _itemMenu.Items.Add("编辑");
        editItem.Name = "EditScreenshotMenuItem";
        editItem.Click += (_, _) => EditSelectedImage();
        var deleteItem = _itemMenu.Items.Add("删除");
        deleteItem.Name = "DeleteScreenshotMenuItem";
        deleteItem.ForeColor = AppTheme.Danger;
        deleteItem.Click += (_, _) => DeleteSelectedImage();
        _itemMenu.Opening += (_, e) => e.Cancel = GetSelectedImagePath() is null;
        _listView.ContextMenuStrip = _itemMenu;
        _listView.MouseDown += (_, e) => SelectRightClickedItem(e);
        _listView.DoubleClick += (_, _) => OpenSelectedImage();
        _listView.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                OpenSelectedImage();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedImage();
                e.SuppressKeyPress = true;
            }
        };
        Controls.Add(_listView);

        _emptyLabel = new Label
        {
            Text = "保存目录中还没有截图",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = AppTheme.CreateFont(11F, FontStyle.Bold),
            ForeColor = AppTheme.MutedText,
            BackColor = AppTheme.Surface,
            Visible = false
        };
        Controls.Add(_emptyLabel);
        _emptyLabel.BringToFront();
    }

    public event EventHandler<ScreenshotEditRequestedEventArgs>? EditRequested;

    public string FolderPath
    {
        get => _folderPath;
        set => _folderPath = value;
    }

    public void RefreshScreenshots()
    {
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            _images.Images.Clear();
            if (!Directory.Exists(_folderPath))
            {
                ShowEmpty();
                return;
            }

            var files = Directory.EnumerateFiles(_folderPath)
                .Where(_savedScreenshotService.IsSupportedImage)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(60)
                .ToArray();
            foreach (var file in files)
            {
                var thumbnail = TryCreateThumbnail(file.FullName);
                if (thumbnail is null)
                {
                    continue;
                }

                var imageIndex = _images.Images.Count;
                _images.Images.Add(thumbnail);
                var item = new ListViewItem(file.Name, imageIndex)
                {
                    Tag = file.FullName,
                    ToolTipText = $"{file.LastWriteTime:g}  ·  {Math.Max(1, file.Length / 1024)} KB"
                };
                _listView.Items.Add(item);
            }

            _countLabel.Text = _listView.Items.Count == 0
                ? "暂无截图"
                : $"最近 {_listView.Items.Count} 张截图 · 双击查看 · 右键编辑或删除";
            _emptyLabel.Visible = _listView.Items.Count == 0;
            if (!_emptyLabel.Visible)
            {
                _listView.BringToFront();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _countLabel.Text = $"读取失败：{exception.Message}";
            ShowEmpty();
        }
        finally
        {
            _listView.EndUpdate();
        }
    }

    private void ShowEmpty()
    {
        _countLabel.Text = "暂无截图";
        _emptyLabel.Visible = true;
        _emptyLabel.BringToFront();
    }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_folderPath);
            _fileLocationService.OpenFolder(_folderPath);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"无法打开保存目录：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenSelectedImage()
    {
        var path = GetSelectedImagePath();
        if (path is null)
        {
            return;
        }

        try
        {
            _fileLocationService.OpenFile(path);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"无法打开截图：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void EditSelectedImage()
    {
        var path = GetSelectedImagePath();
        if (path is null)
        {
            return;
        }

        EditRequested?.Invoke(this, new ScreenshotEditRequestedEventArgs(path));
    }

    private void DeleteSelectedImage()
    {
        var path = GetSelectedImagePath();
        if (path is null)
        {
            return;
        }

        var choice = MessageBox.Show(
            this,
            $"确定删除截图“{Path.GetFileName(path)}”吗？\n\n文件将移入回收站，可以从回收站恢复。",
            "删除截图",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (choice != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _savedScreenshotService.MoveToRecycleBin(_folderPath, path);
            RefreshScreenshots();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                ArgumentException or InvalidOperationException or OperationCanceledException)
        {
            MessageBox.Show(
                this,
                $"无法删除截图：{exception.Message}",
                "删除失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            RefreshScreenshots();
        }
    }

    private void SelectRightClickedItem(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var item = _listView.GetItemAt(e.X, e.Y);
        foreach (ListViewItem selectedItem in _listView.SelectedItems)
        {
            selectedItem.Selected = false;
        }
        if (item is null)
        {
            return;
        }

        item.Selected = true;
        item.Focused = true;
    }

    private string? GetSelectedImagePath() =>
        _listView.SelectedItems.Count > 0 &&
        _listView.SelectedItems[0].Tag is string path
            ? path
            : null;

    private static Bitmap? TryCreateThumbnail(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var source = Image.FromStream(stream);
            var target = new Bitmap(168, 104, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(target);
            graphics.Clear(Color.FromArgb(241, 245, 249));
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            var scale = Math.Min(168D / source.Width, 104D / source.Height);
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            graphics.DrawImage(source, (168 - width) / 2, (104 - height) / 2, width, height);
            return target;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            return null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _itemMenu.Dispose();
            _images.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class ScreenshotEditRequestedEventArgs(string path) : EventArgs
{
    public string Path { get; } = path;
}
