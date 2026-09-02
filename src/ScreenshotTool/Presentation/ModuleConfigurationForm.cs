using System.Diagnostics;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation;

internal sealed class ModuleConfigurationForm : Form
{
    private readonly IReadOnlyList<IModuleSettingsPage> _pages;
    private readonly Dictionary<IModuleSettingsPage, Button> _navigationButtons = [];
    private readonly Panel _contentHost;
    private IModuleSettingsPage? _activePage;
    private bool _pagesDisposed;

    // Creates one owned configuration window for a single module package.
    public ModuleConfigurationForm(
        ModulePackageInfo package,
        IReadOnlyList<IModuleSettingsPage> pages,
        string? loadError = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(pages);

        PackageName = package.PackageName;
        PackageState = package.State;
        _pages = pages
            .OrderBy(page => page.Order)
            .ThenBy(page => page.Id, StringComparer.Ordinal)
            .ToArray();

        Text = $"{package.DisplayName} - 管理配置";
        Icon = AppIcon.Shared;
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Canvas;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 660);
        MinimumSize = new Size(720, 540);
        ShowInTaskbar = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            ColumnCount = 1,
            RowCount = 2,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(layout);

        layout.Controls.Add(CreateHeader(package), 0, 0);
        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            Margin = Padding.Empty,
            Padding = new Padding(18)
        };
        layout.Controls.Add(
            CreateConfigurationBody(package, loadError),
            0,
            1);
    }

    public string PackageName { get; }

    public ModulePackageState PackageState { get; }

    // Builds the module identity header shown above every configuration page.
    private static Control CreateHeader(ModulePackageInfo package)
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface,
            Padding = new Padding(28, 20, 28, 16),
            Margin = Padding.Empty
        };
        var title = new Label
        {
            Text = package.DisplayName,
            AutoSize = false,
            AutoEllipsis = true,
            Size = new Size(600, 32),
            Font = AppTheme.CreateFont(15F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(28, 18)
        };
        var metadata = new Label
        {
            Text = FormatMetadata(package),
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(30, 58),
            Size = new Size(700, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var state = new Label
        {
            Text = GetStateText(package.State),
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = package.State == ModulePackageState.Enabled
                ? AppTheme.Success
                : AppTheme.Danger,
            Location = new Point(28, 23)
        };
        header.Controls.AddRange([title, metadata, state]);
        header.Resize += (_, _) =>
        {
            state.Left = Math.Max(28, header.ClientSize.Width - state.Width - 28);
            title.Width = Math.Max(220, state.Left - title.Left - 18);
            metadata.Width = Math.Max(220, header.ClientSize.Width - metadata.Left - 28);
        };
        return header;
    }

    // Builds either the module pages, a disabled-state explanation, or an error state.
    private Control CreateConfigurationBody(
        ModulePackageInfo package,
        string? loadError)
    {
        if (_pages.Count == 0)
        {
            _contentHost.Controls.Add(CreateEmptyState(package, loadError));
            return _contentHost;
        }

        if (_pages.Count == 1)
        {
            SelectPage(_pages[0]);
            return _contentHost;
        }

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Canvas,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.Surface,
            Padding = new Padding(12, 18, 12, 18),
            Margin = Padding.Empty
        };
        foreach (var page in _pages)
        {
            var button = CreateNavigationButton(page);
            _navigationButtons.Add(page, button);
            navigation.Controls.Add(button);
        }

        body.Controls.Add(navigation, 0, 0);
        body.Controls.Add(_contentHost, 1, 0);
        SelectPage(_pages[0]);
        return body;
    }

    // Creates one vertically stacked page selector for modules with multiple settings pages.
    private Button CreateNavigationButton(IModuleSettingsPage page)
    {
        var button = AppTheme.CreateButton(page.Title);
        button.Name = $"ModuleConfigurationPage:{page.Id}";
        button.Size = new Size(162, 42);
        button.Margin = new Padding(0, 0, 0, 10);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Click += (_, _) => SelectPage(page);
        return button;
    }

    // Activates one module-owned page without changing its lifetime lease.
    private void SelectPage(IModuleSettingsPage page)
    {
        if (ReferenceEquals(_activePage, page))
        {
            return;
        }

        if (_activePage?.Content.Parent == _contentHost)
        {
            _contentHost.Controls.Remove(_activePage.Content);
        }

        _activePage = page;
        if (page.Content.Parent is not null)
        {
            page.Content.Parent.Controls.Remove(page.Content);
        }
        page.Content.Dock = DockStyle.Fill;
        _contentHost.Controls.Add(page.Content);
        page.Content.BringToFront();

        foreach (var item in _navigationButtons)
        {
            var selected = ReferenceEquals(item.Key, page);
            item.Value.BackColor = selected
                ? Color.FromArgb(219, 234, 254)
                : AppTheme.Surface;
            item.Value.ForeColor = selected ? AppTheme.Accent : AppTheme.Text;
            item.Value.FlatAppearance.BorderColor = selected
                ? AppTheme.Accent
                : AppTheme.Border;
        }
    }

    // Creates the explanatory panel used when a module has no loadable configuration page.
    private static Control CreateEmptyState(
        ModulePackageInfo package,
        string? loadError)
    {
        var message = !string.IsNullOrWhiteSpace(loadError)
            ? $"配置页面加载失败：{loadError}"
            : package.State switch
            {
                ModulePackageState.Disabled =>
                    "该模块当前已禁用。请先返回插件模块页启用它，再打开管理配置。",
                ModulePackageState.LoadFailed =>
                    package.ErrorMessage ?? "模块加载失败，暂时无法读取配置页面。",
                _ => "该模块暂未提供可配置参数，功能仍可正常使用。"
            };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(28)
        };
        var title = new Label
        {
            Text = !string.IsNullOrWhiteSpace(loadError)
                ? "配置加载失败"
                : package.State == ModulePackageState.Enabled
                    ? "暂无配置项"
                    : "配置暂不可用",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = package.State == ModulePackageState.Enabled &&
                        string.IsNullOrWhiteSpace(loadError)
                ? AppTheme.Text
                : AppTheme.Danger,
            Location = new Point(28, 28)
        };
        var description = AppTheme.CreateBodyLabel(message, 620);
        description.Location = new Point(30, 70);
        description.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.AddRange([title, description]);
        return card;
    }

    // Formats stable package metadata without exposing implementation details to module pages.
    private static string FormatMetadata(ModulePackageInfo package)
    {
        var version = package.Version is null ? "版本未知" : $"v{package.Version}";
        return $"{version}  ·  {package.ModuleId}  ·  文件夹 {package.PackageName}";
    }

    // Converts the module package state into a concise user-facing label.
    private static string GetStateText(ModulePackageState state) => state switch
    {
        ModulePackageState.Enabled => "已启用",
        ModulePackageState.Disabled => "已禁用",
        ModulePackageState.LoadFailed => "加载失败",
        _ => "状态未知"
    };

    // Releases every module settings-page lease when the floating window closes.
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_pagesDisposed)
        {
            _pagesDisposed = true;
            foreach (var page in _pages)
            {
                try
                {
                    if (page.Content.Parent is not null)
                    {
                        page.Content.Parent.Controls.Remove(page.Content);
                    }
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"模块配置页控件移除失败：{exception}");
                }

                try
                {
                    page.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"模块配置页租约释放失败：{exception}");
                }
            }
        }

        base.Dispose(disposing);
    }
}
