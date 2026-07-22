using ScreenshotTool.Abstractions;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ModuleManagementPage : UserControl
{
    private static readonly Uri ModuleDownloadsUri = new(
        "https://xdioez.github.io/CutCut/modules.html");

    private readonly IModuleManager _moduleManager;
    private readonly IFileLocationService _fileLocationService;
    private readonly FlowLayoutPanel _content;
    private readonly Panel _introCard;
    private readonly List<Control> _packageCards = [];

    public ModuleManagementPage(
        IModuleManager moduleManager,
        IFileLocationService fileLocationService)
    {
        _moduleManager = moduleManager;
        _fileLocationService = fileLocationService;
        BackColor = AppTheme.Canvas;

        _content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.Canvas,
            Padding = Padding.Empty
        };
        Controls.Add(_content);

        _introCard = CreateIntroCard();
        _content.Controls.Add(_introCard);
        Resize += (_, _) => ResizeCards();
        RefreshPackages();
    }

    public event EventHandler<ModuleOperationCompletedEventArgs>? OperationCompleted;

    public void RefreshPackages()
    {
        foreach (var card in _packageCards)
        {
            _content.Controls.Remove(card);
            card.Dispose();
        }
        _packageCards.Clear();

        try
        {
            var packages = _moduleManager.GetInstalledPackages();
            if (packages.Count == 0)
            {
                AddPackageCard(CreateEmptyCard());
            }
            else
            {
                foreach (var package in packages)
                {
                    AddPackageCard(CreatePackageCard(package));
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            AddPackageCard(CreateErrorCard(exception.Message));
        }

        ResizeCards();
    }

    private Panel CreateIntroCard()
    {
        var card = new Panel
        {
            Height = 226,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 16)
        };
        var title = new Label
        {
            Text = "已安装的插件模块",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 21)
        };
        var description = AppTheme.CreateBodyLabel(
            "模块可以单独启用、禁用或永久删除。禁用不会删除文件，可随时重新启用。",
            660);
        description.Location = new Point(28, 56);

        var folderRow = CreateActionRow(
            "模块安装目录",
            "将下载的模块文件夹放入这里，轻截会自动识别。",
            "打开目录",
            (_, _) => OpenModulesDirectory());
        folderRow.Location = new Point(27, 91);

        var downloadsRow = CreateActionRow(
            "模块下载页面",
            "永久删除后如需恢复，请从发布页面重新下载模块。",
            "前往下载",
            (_, _) => OpenModuleDownloads());
        downloadsRow.Location = new Point(27, 157);

        card.Controls.AddRange([title, description, folderRow, downloadsRow]);
        card.Resize += (_, _) =>
        {
            folderRow.Width = Math.Max(420, card.ClientSize.Width - 54);
            downloadsRow.Width = Math.Max(420, card.ClientSize.Width - 54);
        };
        return card;
    }

    private Panel CreatePackageCard(ModulePackageInfo package)
    {
        var card = new Panel
        {
            Height = 226,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 16)
        };
        var title = new Label
        {
            Text = package.DisplayName,
            AutoSize = true,
            Font = AppTheme.CreateFont(11F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 20)
        };
        var metadata = new Label
        {
            Text = FormatMetadata(package),
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(28, 51),
            Size = new Size(560, 22)
        };

        var enabled = package.State == ModulePackageState.Enabled;
        var stateRow = CreateActionRow(
            "运行状态",
            GetStateDescription(package),
            enabled ? "禁用模块" : "启用模块",
            (_, _) => ChangeEnabledState(package, !enabled),
            enabled ? AppTheme.Success : AppTheme.MutedText);
        stateRow.Location = new Point(27, 82);

        var deleteRow = CreateActionRow(
            "永久删除",
            "删除模块文件夹及其中的全部文件，此操作无法撤销。",
            "永久删除",
            (_, _) => ConfirmAndDelete(package),
            AppTheme.Danger);
        deleteRow.Location = new Point(27, 150);

        card.Controls.AddRange([title, metadata, stateRow, deleteRow]);
        card.Resize += (_, _) =>
        {
            metadata.Width = Math.Max(320, card.ClientSize.Width - 56);
            stateRow.Width = Math.Max(420, card.ClientSize.Width - 54);
            deleteRow.Width = Math.Max(420, card.ClientSize.Width - 54);
        };
        return card;
    }

    private static Panel CreateActionRow(
        string title,
        string description,
        string buttonText,
        EventHandler click,
        Color? accent = null)
    {
        var row = new Panel
        {
            Size = new Size(620, 58),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = accent ?? AppTheme.Text,
            Location = new Point(14, 8)
        };
        var descriptionLabel = new Label
        {
            Text = description,
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(8F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(15, 31),
            Size = new Size(430, 18)
        };
        var button = AppTheme.CreateButton(buttonText);
        button.Size = new Size(106, 34);
        button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        button.Location = new Point(row.ClientSize.Width - button.Width - 13, 11);
        if (accent == AppTheme.Danger)
        {
            button.ForeColor = AppTheme.Danger;
            button.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
        }
        button.Click += click;
        row.Resize += (_, _) =>
        {
            button.Left = row.ClientSize.Width - button.Width - 13;
            descriptionLabel.Width = Math.Max(180, button.Left - descriptionLabel.Left - 12);
        };
        row.Controls.AddRange([titleLabel, descriptionLabel, button]);
        return row;
    }

    private static Panel CreateEmptyCard()
    {
        var card = CreateMessageCard(
            "还没有安装插件模块",
            "你可以使用上方按钮打开模块目录，或前往模块下载页面选择需要的扩展。",
            AppTheme.MutedText);
        card.Height = 112;
        return card;
    }

    private static Panel CreateErrorCard(string message) => CreateMessageCard(
        "无法读取模块列表",
        message,
        AppTheme.Danger);

    private static Panel CreateMessageCard(string title, string message, Color color)
    {
        var card = new Panel
        {
            Height = 126,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 16)
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = AppTheme.CreateFont(10.5F, FontStyle.Bold),
            ForeColor = color,
            Location = new Point(26, 24)
        };
        var messageLabel = AppTheme.CreateBodyLabel(message, 650);
        messageLabel.Location = new Point(28, 57);
        card.Controls.AddRange([titleLabel, messageLabel]);
        return card;
    }

    private void ChangeEnabledState(ModulePackageInfo package, bool enabled)
    {
        var result = _moduleManager.SetPackageEnabled(package.PackageName, enabled);
        HandleOperationResult(result);
    }

    private void ConfirmAndDelete(ModulePackageInfo package)
    {
        var choice = MessageBox.Show(
            this,
            $"将永久删除插件模块“{package.DisplayName}”及其中的全部文件。\n\n" +
            "此操作无法撤销。如果以后需要重新安装，必须前往轻截发布页面重新下载该模块。\n\n" +
            "是否继续永久删除？",
            "确认永久删除插件模块",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (choice != DialogResult.Yes)
        {
            return;
        }

        var result = _moduleManager.DeletePackage(package.PackageName);
        HandleOperationResult(result);
    }

    private void HandleOperationResult(ModuleOperationResult result)
    {
        RefreshPackages();
        OperationCompleted?.Invoke(this, new ModuleOperationCompletedEventArgs(result));
        if (!result.Succeeded)
        {
            MessageBox.Show(
                this,
                result.Message,
                "插件模块操作失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenModulesDirectory()
    {
        try
        {
            Directory.CreateDirectory(_moduleManager.ModulesDirectory);
            _fileLocationService.OpenFolder(_moduleManager.ModulesDirectory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, $"无法打开模块目录：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenModuleDownloads()
    {
        try
        {
            _fileLocationService.OpenWebPage(ModuleDownloadsUri);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, $"无法打开模块下载页面：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddPackageCard(Control card)
    {
        _packageCards.Add(card);
        _content.Controls.Add(card);
    }

    private void ResizeCards()
    {
        var width = Math.Max(
            520,
            _content.ClientSize.Width - _content.Padding.Horizontal -
            SystemInformation.VerticalScrollBarWidth - 2);
        _introCard.Width = width;
        foreach (var card in _packageCards)
        {
            card.Width = width;
        }
    }

    private static string FormatMetadata(ModulePackageInfo package)
    {
        var version = package.Version is null ? "版本未知" : $"v{package.Version}";
        return $"{version}  ·  {package.ModuleId}  ·  文件夹 {package.PackageName}";
    }

    private static string GetStateDescription(ModulePackageInfo package) => package.State switch
    {
        ModulePackageState.Enabled => "已启用，将在新的截图会话中提供功能。",
        ModulePackageState.Disabled => "已禁用，模块文件仍保留在本地。",
        ModulePackageState.LoadFailed => package.ErrorMessage ?? "加载失败，可尝试重新启用。",
        _ => "状态未知"
    };
}

internal sealed class ModuleOperationCompletedEventArgs(ModuleOperationResult result) : EventArgs
{
    public ModuleOperationResult Result { get; } = result;
}
