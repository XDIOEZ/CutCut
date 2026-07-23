using ScreenshotTool.Abstractions;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ScreenshotGalleryPage : UserControl
{
    private readonly IFileLocationService _fileLocationService;
    private readonly ISavedScreenshotService _savedScreenshotService;
    private readonly TableLayoutPanel _toolbar;
    private readonly ListView _listView;
    private readonly ImageList _images;
    private readonly ContextMenuStrip _itemMenu;
    private readonly ContextMenuStrip _sortMenu;
    private readonly TextBox _searchInput;
    private readonly Button _sortButton;
    private readonly System.Windows.Forms.Timer _searchTimer;
    private readonly Label _countLabel;
    private readonly Label _emptyLabel;
    private ScreenshotGallerySortMode _sortMode =
        ScreenshotGallerySortMode.SavedTimeDescending;
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

        _toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            ColumnCount = 5,
            RowCount = 2,
            BackColor = AppTheme.Canvas,
            Padding = new Padding(0, 0, 0, 8)
        };
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 218F));
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
        _toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        _toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

        var refreshButton = AppTheme.CreateButton("刷新");
        refreshButton.Dock = DockStyle.Fill;
        refreshButton.Margin = new Padding(0, 0, 6, 4);
        refreshButton.Click += (_, _) => RefreshScreenshots();
        var openFolderButton = AppTheme.CreateButton("打开保存目录");
        openFolderButton.Dock = DockStyle.Fill;
        openFolderButton.Margin = new Padding(0, 0, 6, 4);
        openFolderButton.Click += (_, _) => OpenFolder();

        var searchContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(9, 7, 9, 5),
            Margin = new Padding(6, 0, 6, 4)
        };
        _searchInput = new TextBox
        {
            Name = "ScreenshotSearchInput",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.Text,
            Font = AppTheme.CreateFont(9F),
            PlaceholderText = "按文件名搜索"
        };
        searchContainer.Controls.Add(_searchInput);
        var searchLabel = new Label
        {
            Dock = DockStyle.Left,
            Width = 42,
            Text = "搜索",
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.CreateFont(8.5F)
        };
        searchContainer.Controls.Add(searchLabel);

        _sortButton = AppTheme.CreateButton("时间：新→旧");
        _sortButton.Name = "ScreenshotSortButton";
        _sortButton.Dock = DockStyle.Fill;
        _sortButton.Margin = new Padding(0, 0, 0, 4);
        _sortMenu = CreateSortMenu();
        _sortButton.Click += (_, _) =>
            _sortMenu.Show(_sortButton, new Point(0, _sortButton.Height));

        _searchTimer = new System.Windows.Forms.Timer
        {
            Interval = 180
        };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            RefreshScreenshots();
        };
        _searchInput.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        _searchInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape || _searchInput.TextLength == 0)
            {
                return;
            }

            _searchInput.Clear();
            e.SuppressKeyPress = true;
        };

        _countLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.MutedText,
            Margin = new Padding(0, 2, 0, 0)
        };
        _toolbar.Controls.Add(refreshButton, 0, 0);
        _toolbar.Controls.Add(openFolderButton, 1, 0);
        _toolbar.Controls.Add(searchContainer, 3, 0);
        _toolbar.Controls.Add(_sortButton, 4, 0);
        _toolbar.Controls.Add(_countLabel, 0, 1);
        _toolbar.SetColumnSpan(_countLabel, 5);
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
        _searchTimer.Stop();
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            _images.Images.Clear();
            if (!Directory.Exists(_folderPath))
            {
                ShowEmpty("保存目录中还没有截图");
                return;
            }

            var entries = Directory.EnumerateFiles(_folderPath)
                .Where(_savedScreenshotService.IsSupportedImage)
                .Select(path => new FileInfo(path))
                .Select(file => new ScreenshotGalleryEntry(
                    file.FullName,
                    file.Name,
                    file.LastWriteTime,
                    file.LastWriteTimeUtc,
                    file.Length))
                .ToArray();
            var result = ScreenshotGalleryQuery.Apply(
                entries,
                _searchInput.Text,
                _sortMode,
                maximumCount: 60);
            foreach (var entry in result.Entries)
            {
                var thumbnail = TryCreateThumbnail(entry.FullName);
                if (thumbnail is null)
                {
                    continue;
                }

                var imageIndex = _images.Images.Count;
                _images.Images.Add(thumbnail);
                var item = new ListViewItem(entry.Name, imageIndex)
                {
                    Tag = entry.FullName,
                    ToolTipText =
                        $"{entry.LastWriteTime:g}  ·  {Math.Max(1, entry.Length / 1024)} KB"
                };
                _listView.Items.Add(item);
            }

            _countLabel.Text = CreateCountText(
                result.MatchCount,
                _listView.Items.Count,
                entries.Length,
                _searchInput.Text);
            _emptyLabel.Visible = _listView.Items.Count == 0;
            _emptyLabel.Text = entries.Length == 0
                ? "保存目录中还没有截图"
                : "没有找到匹配的截图";
            if (!_emptyLabel.Visible)
            {
                _listView.BringToFront();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowEmpty("无法读取截图");
            _countLabel.Text = $"读取失败：{exception.Message}";
        }
        finally
        {
            _listView.EndUpdate();
        }
    }

    private void ShowEmpty(string message)
    {
        _countLabel.Text = "暂无截图";
        _emptyLabel.Text = message;
        _emptyLabel.Visible = true;
        _emptyLabel.BringToFront();
    }

    private ContextMenuStrip CreateSortMenu()
    {
        var menu = new ContextMenuStrip
        {
            Font = AppTheme.CreateFont(9F),
            ShowImageMargin = false,
            ShowCheckMargin = true
        };
        AddSortMenuItem(
            menu,
            "保存时间（最新优先）",
            ScreenshotGallerySortMode.SavedTimeDescending);
        AddSortMenuItem(
            menu,
            "保存时间（最早优先）",
            ScreenshotGallerySortMode.SavedTimeAscending);
        menu.Items.Add(new ToolStripSeparator());
        AddSortMenuItem(
            menu,
            "名称（A → Z）",
            ScreenshotGallerySortMode.NameAscending);
        AddSortMenuItem(
            menu,
            "名称（Z → A）",
            ScreenshotGallerySortMode.NameDescending);
        UpdateSortMenuChecks(menu);
        return menu;
    }

    private void AddSortMenuItem(
        ContextMenuStrip menu,
        string text,
        ScreenshotGallerySortMode sortMode)
    {
        var item = new ToolStripMenuItem(text)
        {
            Tag = sortMode
        };
        item.Click += (_, _) => ApplySort(sortMode);
        menu.Items.Add(item);
    }

    private void ApplySort(ScreenshotGallerySortMode sortMode)
    {
        _sortMode = sortMode;
        _sortButton.Text = sortMode switch
        {
            ScreenshotGallerySortMode.SavedTimeAscending => "时间：旧→新",
            ScreenshotGallerySortMode.NameAscending => "名称：A→Z",
            ScreenshotGallerySortMode.NameDescending => "名称：Z→A",
            _ => "时间：新→旧"
        };
        UpdateSortMenuChecks(_sortMenu);
        RefreshScreenshots();
    }

    private void UpdateSortMenuChecks(ContextMenuStrip menu)
    {
        foreach (var item in menu.Items.OfType<ToolStripMenuItem>())
        {
            item.Checked =
                item.Tag is ScreenshotGallerySortMode sortMode &&
                sortMode == _sortMode;
        }
    }

    private static string CreateCountText(
        int matchCount,
        int visibleCount,
        int totalCount,
        string searchText)
    {
        const string hint = "双击查看 · 右键编辑或删除";
        if (totalCount == 0)
        {
            return "暂无截图";
        }
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            return matchCount == 0
                ? $"未找到匹配截图 · {hint}"
                : $"找到 {matchCount} 张，显示 {visibleCount} 张 · {hint}";
        }
        return totalCount > visibleCount
            ? $"共 {totalCount} 张，显示前 {visibleCount} 张 · {hint}"
            : $"共 {visibleCount} 张截图 · {hint}";
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
            _searchTimer.Dispose();
            _sortMenu.Dispose();
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
