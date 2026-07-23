using ScreenshotTool.Abstractions;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class ApplicationUpdatePage : UserControl
{
    private readonly IApplicationUpdateService? _updateService;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly FlowLayoutPanel _content;
    private readonly Panel _updateCard;
    private readonly Panel _noteCard;
    private readonly Label _versionValue;
    private readonly Label _updateSourceValue;
    private readonly Label _stateDescription;
    private readonly Label _progressLabel;
    private readonly Button _actionButton;
    private readonly string _currentVersionText;
    private ApplicationUpdateInfo? _availableUpdate;
    private bool _busy;
    private bool _lifetimeCancellationDisposed;

    public ApplicationUpdatePage(
        Version currentVersion,
        IApplicationUpdateService? updateService)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        _updateService = updateService;
        _currentVersionText = FormatVersion(currentVersion);

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

        _updateCard = new Panel
        {
            Height = 366,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 16)
        };
        var title = new Label
        {
            Text = "软件更新",
            AutoSize = true,
            Font = AppTheme.CreateFont(12F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(26, 21)
        };
        var description = AppTheme.CreateBodyLabel(
            "从 GitHub Releases 检查正式版，校验完成后原地更新并自动重启轻截。",
            680);
        description.Location = new Point(28, 56);

        var currentVersionRow = CreateInformationRow(
            "版本信息",
            "对比正在运行的版本与 GitHub 最新正式版。",
            $"当前 v{_currentVersionText}",
            out _versionValue);
        currentVersionRow.Location = new Point(27, 94);

        var sourceRow = CreateInformationRow(
            "更新来源",
            "只读取 XDIOEZ/CutCut 的最新正式 Release。",
            "GitHub Releases",
            out _updateSourceValue);
        sourceRow.Location = new Point(27, 164);

        var actionRow = new Panel
        {
            Location = new Point(27, 234),
            Size = new Size(620, 62),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Tag = "SettingRow"
        };
        var stateTitle = new Label
        {
            Text = "更新状态",
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(14, 8)
        };
        _stateDescription = new Label
        {
            Text = updateService is null
                ? "界面预览不连接 GitHub。"
                : "点击按钮检查是否有新版本。",
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(8F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(15, 34),
            Size = new Size(420, 18)
        };
        _actionButton = AppTheme.CreateButton("检查更新", primary: true);
        _actionButton.Size = new Size(126, 36);
        _actionButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _actionButton.Location = new Point(actionRow.ClientSize.Width - _actionButton.Width - 13, 12);
        _actionButton.Enabled = updateService is not null;
        _actionButton.Click += HandleActionButtonClick;
        actionRow.Resize += (_, _) =>
        {
            _actionButton.Left = actionRow.ClientSize.Width - _actionButton.Width - 13;
            _stateDescription.Width = Math.Max(
                180,
                _actionButton.Left - _stateDescription.Left - 12);
        };
        actionRow.Controls.AddRange([stateTitle, _stateDescription, _actionButton]);

        _progressLabel = new Label
        {
            Text = "更新不会上传截图、设置或其他本地内容。",
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(8.5F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(29, 319),
            Size = new Size(650, 22)
        };
        _updateCard.Controls.AddRange(
            [title, description, currentVersionRow, sourceRow, actionRow, _progressLabel]);
        _updateCard.Resize += (_, _) =>
        {
            var rowWidth = Math.Max(420, _updateCard.ClientSize.Width - 54);
            currentVersionRow.Width = rowWidth;
            sourceRow.Width = rowWidth;
            actionRow.Width = rowWidth;
            _progressLabel.Width = Math.Max(320, _updateCard.ClientSize.Width - 58);
        };
        _content.Controls.Add(_updateCard);

        _noteCard = new Panel
        {
            Height = 132,
            BackColor = Color.FromArgb(239, 246, 255),
            Margin = Padding.Empty,
            Padding = new Padding(20, 16, 20, 14)
        };
        var noteTitle = new Label
        {
            Text = "更新过程",
            AutoSize = true,
            Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
            ForeColor = AppTheme.Accent,
            Location = new Point(20, 15)
        };
        var noteBody = AppTheme.CreateBodyLabel(
            "轻截会下载适合当前电脑的完整包并核对 GitHub SHA-256。确认无误后退出当前进程、覆盖程序文件并自动重启；如果替换失败，会尝试恢复原版本。已永久删除的插件不会被重新安装。",
            680);
        noteBody.Location = new Point(22, 47);
        _noteCard.Controls.AddRange([noteTitle, noteBody]);
        _content.Controls.Add(_noteCard);

        Resize += (_, _) => ResizeCards();
        ResizeCards();
    }

    public event EventHandler? ExitRequested;

    internal string StateText => _stateDescription.Text;

    private async void HandleActionButtonClick(object? sender, EventArgs e)
    {
        if (_busy || _updateService is null)
        {
            return;
        }

        if (_availableUpdate is null)
        {
            await CheckForUpdatesAsync();
        }
        else
        {
            await DownloadAndInstallAsync(_availableUpdate);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        SetBusy("正在连接 GitHub…");
        try
        {
            var result = await _updateService!.CheckForUpdatesAsync(
                _lifetimeCancellation.Token);
            _availableUpdate = result.AvailableUpdate;
            if (_availableUpdate is null)
            {
                _versionValue.Text = $"v{_currentVersionText}（最新）";
                _stateDescription.Text = "当前已经是最新正式版。";
                _stateDescription.ForeColor = AppTheme.Success;
                _progressLabel.Text =
                    $"GitHub 最新版本：v{FormatVersion(result.LatestVersion)}，" +
                    $"发布于 {FormatPublishedAt(result.PublishedAt)}。";
                _actionButton.Text = "重新检查";
                _actionButton.Width = 126;
                return;
            }

            var packageLabel = _availableUpdate.PackageKind ==
                               ApplicationUpdatePackageKind.Lightweight
                ? "轻量更新包"
                : "便携更新包（含运行库）";
            _versionValue.Text =
                $"v{_currentVersionText} → v{FormatVersion(_availableUpdate.Version)}";
            _updateSourceValue.Text = packageLabel;
            _stateDescription.Text =
                $"发现 v{FormatVersion(_availableUpdate.Version)}，可以直接更新。";
            _stateDescription.ForeColor = AppTheme.Accent;
            _progressLabel.Text =
                $"{_availableUpdate.ReleaseName} · " +
                $"{FormatBytes(_availableUpdate.PackageSize)} · " +
                $"{FormatPublishedAt(_availableUpdate.PublishedAt)}";
            _actionButton.Text = "下载并安装";
            _actionButton.Width = 138;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is ApplicationUpdateException or HttpRequestException or IOException)
        {
            ShowFailure(exception.Message);
        }
        finally
        {
            SetIdle();
        }
    }

    private async Task DownloadAndInstallAsync(ApplicationUpdateInfo update)
    {
        var choice = MessageBox.Show(
            this,
            $"将从 GitHub 下载 v{FormatVersion(update.Version)}，" +
            "校验完成后轻截会自动退出、更新并重新启动。\n\n是否继续？",
            "下载并安装更新",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);
        if (choice != DialogResult.Yes)
        {
            return;
        }

        SetBusy("准备下载…");
        var progress = new Progress<ApplicationUpdateProgress>(value =>
        {
            var total = value.TotalBytes > 0 ? value.TotalBytes : update.PackageSize;
            var percentage = total > 0
                ? Math.Clamp(value.BytesReceived * 100D / total, 0D, 100D)
                : 0D;
            _stateDescription.Text = $"正在下载并校验：{percentage:0}%";
            _progressLabel.Text =
                $"{FormatBytes(value.BytesReceived)} / {FormatBytes(total)}";
        });

        try
        {
            var prepared = await _updateService!.DownloadAndPrepareAsync(
                update,
                progress,
                _lifetimeCancellation.Token);
            _stateDescription.Text = "校验完成，正在启动更新程序…";
            _progressLabel.Text = "轻截即将退出，更新完成后会自动重新打开。";
            _updateService.StartApplying(prepared, Environment.ProcessId);
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is ApplicationUpdateException or HttpRequestException or IOException)
        {
            ShowFailure(exception.Message);
            SetIdle();
        }
    }

    private void SetBusy(string text)
    {
        _busy = true;
        _actionButton.Enabled = false;
        _stateDescription.Text = text;
        _stateDescription.ForeColor = AppTheme.Accent;
    }

    private void SetIdle()
    {
        if (IsDisposed)
        {
            return;
        }

        _busy = false;
        _actionButton.Enabled = _updateService is not null;
    }

    private void ShowFailure(string message)
    {
        _stateDescription.Text = "更新失败，请稍后重试。";
        _stateDescription.ForeColor = AppTheme.Danger;
        _progressLabel.Text = message;
        _actionButton.Text = _availableUpdate is null ? "重新检查" : "重试安装";
        _actionButton.Width = 126;
    }

    private static Panel CreateInformationRow(
        string title,
        string description,
        string value,
        out Label valueLabel)
    {
        var row = new Panel
        {
            Size = new Size(620, 62),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Tag = "SettingRow"
        };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(14, 8)
        };
        var descriptionLabel = new Label
        {
            Text = description,
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(8F),
            ForeColor = AppTheme.MutedText,
            Location = new Point(15, 34),
            Size = new Size(420, 18)
        };
        valueLabel = new Label
        {
            Text = value,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true,
            Font = AppTheme.CreateFont(9F, FontStyle.Bold),
            ForeColor = AppTheme.Accent,
            Location = new Point(440, 17),
            Size = new Size(160, 26)
        };
        var capturedValueLabel = valueLabel;
        row.Resize += (_, _) =>
        {
            capturedValueLabel.Left = row.ClientSize.Width - capturedValueLabel.Width - 15;
            descriptionLabel.Width = Math.Max(
                180,
                capturedValueLabel.Left - descriptionLabel.Left - 12);
        };
        row.Controls.AddRange([titleLabel, descriptionLabel, valueLabel]);
        return row;
    }

    private void ResizeCards()
    {
        var scrollbarWidth = _content.VerticalScroll.Visible
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
        var width = Math.Max(
            570,
            _content.ClientSize.Width - _content.Padding.Horizontal - scrollbarWidth - 2);
        _updateCard.Width = width;
        _noteCard.Width = width;
    }

    private static string FormatVersion(Version version) =>
        $"{version.Major}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";

    private static string FormatPublishedAt(DateTimeOffset value) =>
        value == default ? "发布时间未知" : value.ToLocalTime().ToString("yyyy-MM-dd");

    private static string FormatBytes(long bytes) =>
        bytes <= 0 ? "未知大小" : $"{bytes / 1024D / 1024D:0.##} MiB";

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_lifetimeCancellationDisposed)
        {
            _lifetimeCancellationDisposed = true;
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
        }

        base.Dispose(disposing);
    }
}
