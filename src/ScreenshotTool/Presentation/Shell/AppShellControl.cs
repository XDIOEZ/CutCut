using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Shell;

internal sealed class AppShellControl : UserControl
{
    private readonly FlowLayoutPanel _navigation;
    private readonly Panel _contentHost;
    private readonly Label _pageTitle;
    private readonly Label _pageDescription;
    private readonly Label _statusLabel;
    private readonly Label _versionLabel;
    private readonly Dictionary<string, (AppPage Page, Button Button)> _pages =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedPageId;

    public AppShellControl()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.Canvas;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 214F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Sidebar,
            Padding = new Padding(14, 0, 14, 18),
            Margin = Padding.Empty
        };
        root.Controls.Add(sidebar, 0, 0);

        var brand = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = AppTheme.Sidebar };
        var brandMark = new Label
        {
            Text = "✂",
            AutoSize = true,
            Font = new Font("Segoe UI Symbol", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 165, 250),
            Location = new Point(8, 24)
        };
        var brandName = new Label
        {
            Text = "轻截",
            AutoSize = true,
            Font = AppTheme.CreateFont(17F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(52, 22)
        };
        var brandCaption = new Label
        {
            Text = "轻量截图工作台",
            AutoSize = true,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(54, 53)
        };
        _versionLabel = new Label
        {
            Text = FormatVersion(typeof(AppShellControl).Assembly.GetName().Version),
            AutoSize = true,
            Font = AppTheme.CreateFont(8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 165, 250),
            Location = new Point(132, 31)
        };
        brand.Controls.AddRange([brandMark, brandName, _versionLabel, brandCaption]);
        sidebar.Controls.Add(brand);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 98, BackColor = AppTheme.Sidebar };
        var captureButton = AppTheme.CreateButton("立即截图", primary: true);
        captureButton.Dock = DockStyle.Top;
        captureButton.Height = 42;
        captureButton.Click += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
        var footerText = new Label
        {
            Text = "关闭窗口后继续在后台运行",
            Dock = DockStyle.Bottom,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = AppTheme.CreateFont(8F),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        footer.Controls.Add(footerText);
        footer.Controls.Add(captureButton);
        sidebar.Controls.Add(footer);

        _navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AppTheme.Sidebar,
            Padding = new Padding(0, 5, 0, 5)
        };
        sidebar.Controls.Add(_navigation);
        _navigation.BringToFront();
        _navigation.Resize += (_, _) => UpdateNavigationButtonWidths();

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            Margin = Padding.Empty
        };
        root.Controls.Add(right, 1, 0);

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94F));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        right.Controls.Add(rightLayout);

        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface,
            Padding = new Padding(34, 18, 28, 14),
            Margin = Padding.Empty
        };
        rightLayout.Controls.Add(header, 0, 0);

        _pageTitle = new Label
        {
            AutoSize = true,
            Font = AppTheme.CreateFont(17F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(34, 17)
        };
        _pageDescription = new Label
        {
            AutoSize = true,
            Font = AppTheme.CreateFont(9F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(36, 53)
        };
        _statusLabel = new Label
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.Success,
            Location = new Point(500, 27),
            Size = new Size(260, 32)
        };
        header.Resize += (_, _) => _statusLabel.Left = Math.Max(340, header.ClientSize.Width - _statusLabel.Width - 28);
        header.Controls.AddRange([_pageTitle, _pageDescription, _statusLabel]);

        var separator = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = AppTheme.Border };
        header.Controls.Add(separator);

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            Padding = new Padding(30, 26, 30, 28),
            Margin = Padding.Empty
        };
        rightLayout.Controls.Add(_contentHost, 0, 1);
    }

    public event EventHandler? CaptureRequested;
    public event EventHandler<string>? PageChanged;

    public string? SelectedPageId => _selectedPageId;

    internal string VersionText => _versionLabel.Text;

    public void AddPage(AppPage page)
    {
        if (_pages.ContainsKey(page.Id))
        {
            throw new InvalidOperationException($"页面 ID 重复：{page.Id}");
        }

        page.Content.Dock = DockStyle.Fill;
        page.Content.Visible = false;
        _contentHost.Controls.Add(page.Content);

        var button = new Button
        {
            Text = page.Title,
            Size = new Size(1, 52),
            Margin = new Padding(0, 3, 0, 3),
            Padding = new Padding(18, 0, 8, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.Sidebar,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = AppTheme.CreateFont(10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AppTheme.SidebarHover;
        button.Click += (_, _) => SelectPage(page.Id);
        var insertionIndex = _pages.Values.Count(existing =>
            existing.Page.Order <= page.Order);
        _navigation.Controls.Add(button);
        _navigation.Controls.SetChildIndex(button, insertionIndex);
        _pages.Add(page.Id, (page, button));
        UpdateNavigationButtonWidths();

        if (_selectedPageId is null)
        {
            SelectPage(page.Id);
        }
    }

    public bool ContainsPage(string id) => _pages.ContainsKey(id);

    public bool RemovePage(string id)
    {
        if (!_pages.Remove(id, out var removed))
        {
            return false;
        }

        var wasSelected = string.Equals(
            _selectedPageId,
            id,
            StringComparison.OrdinalIgnoreCase);
        _contentHost.Controls.Remove(removed.Page.Content);
        _navigation.Controls.Remove(removed.Button);
        removed.Button.Dispose();

        if (wasSelected)
        {
            _selectedPageId = null;
            var replacement = _pages.Values
                .OrderBy(entry => entry.Page.Order)
                .ThenBy(entry => entry.Page.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (replacement.Page is not null)
            {
                SelectPage(replacement.Page.Id);
            }
            else
            {
                _pageTitle.Text = string.Empty;
                _pageDescription.Text = string.Empty;
            }
        }

        UpdateNavigationButtonWidths();
        return true;
    }

    public void SelectPage(string id)
    {
        if (!_pages.TryGetValue(id, out var selected))
        {
            return;
        }

        foreach (var pair in _pages)
        {
            var active = string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase);
            pair.Value.Page.Content.Visible = active;
            pair.Value.Button.BackColor = active ? AppTheme.Accent : AppTheme.Sidebar;
            pair.Value.Button.ForeColor = active ? Color.White : Color.FromArgb(203, 213, 225);
            pair.Value.Button.FlatAppearance.MouseOverBackColor = active ? AppTheme.AccentHover : AppTheme.SidebarHover;
            pair.Value.Button.FlatAppearance.MouseDownBackColor = active ? AppTheme.AccentHover : AppTheme.SidebarHover;
            if (active)
            {
                pair.Value.Page.Content.BringToFront();
            }
        }

        _selectedPageId = id;
        _pageTitle.Text = selected.Page.Title;
        _pageDescription.Text = selected.Page.Description;
        PageChanged?.Invoke(this, id);
    }

    public void ShowStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private void UpdateNavigationButtonWidths()
    {
        var availableWidth = Math.Max(
            100,
            _navigation.ClientSize.Width -
            _navigation.Padding.Horizontal -
            SystemInformation.VerticalScrollBarWidth -
            2);
        foreach (var button in _navigation.Controls.OfType<Button>())
        {
            button.Width = availableWidth;
        }
    }

    internal static string FormatVersion(Version? version) => version is null
        ? "v0.0.0"
        : $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
}
