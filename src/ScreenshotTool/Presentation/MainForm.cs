using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;
using ScreenshotTool.Presentation.Pages;
using ScreenshotTool.Presentation.Shell;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation;

internal sealed class MainForm : Form
{
    private const string GalleryPageId = "gallery";

    private readonly ISettingsStore _settingsStore;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IScreenCaptureService _captureService;
    private readonly IImageSaveService _imageSaveService;
    private readonly IClipboardService _clipboardService;
    private readonly IWindowLocator _windowLocator;
    private readonly IFileLocationService _fileLocationService;
    private readonly IModuleManager _moduleManager;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _moduleRefreshTimer;
    private readonly AppShellControl _shell;
    private readonly ShortcutSettingsPage _shortcutPage;
    private readonly StickerBehaviorSettingsPage _stickerBehaviorPage;
    private readonly LongCaptureSettingsPage _longCapturePage;
    private readonly EditorSettingsPage _editorSettingsPage;
    private readonly DrawingCoefficientsSettingsPage _drawingCoefficientsPage;
    private readonly SavePathSettingsPage _savePathPage;
    private readonly ScreenshotGalleryPage _galleryPage;
    private readonly bool _backgroundIntegrationEnabled;
    private AppSettings _settings;
    private bool _isCapturing;
    private bool _isExiting;
    private bool _initialMinimizeHandled;
    private string? _pendingHotkeyError;
    private string? _pendingSavedScreenshotPath;

    public MainForm(
        ISettingsStore settingsStore,
        IGlobalHotkeyService hotkeyService,
        IScreenCaptureService captureService,
        IImageSaveService imageSaveService,
        IClipboardService clipboardService,
        IWindowLocator windowLocator,
        IFileLocationService fileLocationService,
        IModuleManager moduleManager,
        bool enableBackgroundIntegration = true)
    {
        _settingsStore = settingsStore;
        _hotkeyService = hotkeyService;
        _captureService = captureService;
        _imageSaveService = imageSaveService;
        _clipboardService = clipboardService;
        _windowLocator = windowLocator;
        _fileLocationService = fileLocationService;
        _moduleManager = moduleManager;
        _backgroundIntegrationEnabled = enableBackgroundIntegration;
        _settings = settingsStore.Load();

        Text = "轻截 - 截图工作台";
        Font = AppTheme.CreateFont(9F);
        BackColor = AppTheme.Canvas;
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(880, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;

        _shell = new AppShellControl();
        Controls.Add(_shell);

        _shortcutPage = new ShortcutSettingsPage(_settings.GetHotkey(), _settings.StartMinimized);
        _stickerBehaviorPage = new StickerBehaviorSettingsPage(
            _settings.Preferences.StickerSelectionMoveMode);
        _longCapturePage = new LongCaptureSettingsPage(
            _settings.Preferences.LongCaptureSafetyChecksEnabled);
        _editorSettingsPage = new EditorSettingsPage(_settings.GetToolWidthRange());
        _drawingCoefficientsPage = new DrawingCoefficientsSettingsPage(
            _settings.Preferences.DrawingToolCoefficients);
        _savePathPage = new SavePathSettingsPage(_settings.OutputFolder);
        _galleryPage = new ScreenshotGalleryPage(_settings.OutputFolder, _fileLocationService);
        ComposePages();
        WirePageEvents();

        _trayIcon = BuildTrayIcon(enableBackgroundIntegration);
        _moduleRefreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _moduleRefreshTimer.Tick += (_, _) => RefreshModules(force: false, showResult: false);
        if (enableBackgroundIntegration)
        {
            _moduleRefreshTimer.Start();
            RefreshModules(force: false, showResult: false);
            _hotkeyService.Pressed += (_, _) => BeginCapture();
        }

        Shown += HandleShown;
        FormClosing += HandleFormClosing;
        if (enableBackgroundIntegration)
        {
            RegisterInitialHotkey();
        }
    }

    private void ComposePages()
    {
        _shell.AddPage(new AppPage(
            "paste",
            "信息粘贴",
            "设置图片和文字贴纸在截图框移动时的行为",
            _stickerBehaviorPage));

        _shell.AddPage(new AppPage(
            "shortcut",
            "快捷键设置",
            "设置后台唤起截图的全局组合键",
            _shortcutPage));

        _shell.AddPage(new AppPage(
            "long-capture",
            "长截图",
            "选择宽松拼接或严格安全校验",
            _longCapturePage));

        _shell.AddPage(new AppPage(
            "copy",
            "图片复制",
            "快速把编辑后的截图送到剪贴板",
            new FeatureGuidePage(
                "FAST COPY",
                "完成编辑后，一键复制",
                "截图框确认后按 Ctrl + C，最终画面会立即复制到剪贴板并结束截图。",
                "Ctrl + C",
                [
                    ("包含全部编辑", "矩形、箭头、文字、马赛克和粘贴贴纸都会进入最终图片。"),
                    ("剪贴板自动重试", "剪贴板被其他程序短暂占用时会自动重试，减少复制失败。"),
                    ("保存并复制", "需要同时保存文件时使用 Ctrl + S，保存完成后也会复制图片。")
                ])));

        _shell.AddPage(new AppPage(
            "save",
            "保存路径",
            "设置截图文件夹并快速打开保存位置",
            _savePathPage));

        _shell.AddPage(new AppPage(
            "edit",
            "图片修改",
            "设置画笔、形状和马赛克等编辑工具的粗细范围",
            _editorSettingsPage));

        _shell.AddPage(new AppPage(
            "drawing-coefficients",
            "绘制系数",
            "配置各绘制元素的基础尺寸，工具栏粗细作为统一倍率",
            _drawingCoefficientsPage));

        _shell.AddPage(new AppPage(
            GalleryPageId,
            "查看截图",
            "浏览保存目录中的最近截图，双击即可查看",
            _galleryPage));
    }

    private void WirePageEvents()
    {
        _shell.CaptureRequested += (_, _) => BeginCapture();
        _shell.PageChanged += (_, pageId) =>
        {
            if (pageId == GalleryPageId)
            {
                _galleryPage.RefreshScreenshots();
            }
        };
        _shortcutPage.SaveRequested += SaveSettings;
        _stickerBehaviorPage.SaveRequested += SaveSettings;
        _longCapturePage.SaveRequested += SaveSettings;
        _editorSettingsPage.SaveRequested += SaveSettings;
        _drawingCoefficientsPage.SaveRequested += SaveSettings;
        _shortcutPage.HotkeyInputEntered += (_, _) => _hotkeyService.Unregister();
        _shortcutPage.HotkeyInputLeft += (_, _) =>
        {
            if (!_isCapturing)
            {
                _hotkeyService.TryRegister(_settings.GetHotkey(), out _);
            }
        };
        _savePathPage.SaveRequested += SaveSettings;
        _savePathPage.BrowseRequested += BrowseFolder;
        _savePathPage.OpenRequested += OpenOutputFolder;
    }

    private NotifyIcon BuildTrayIcon(bool visible)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("立即截图", null, (_, _) => BeginCapture());
        menu.Items.Add("打开工作台", null, (_, _) => ShowFromTray());
        menu.Items.Add("打开保存目录", null, OpenOutputFolder);
        menu.Items.Add("打开模块目录", null, OpenModulesDirectory);
        menu.Items.Add("重新加载模块", null, (_, _) => RefreshModules(force: true, showResult: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());

        var icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = $"轻截（{_settings.GetHotkey().ToDisplayText()}）",
            Visible = visible,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        icon.BalloonTipClicked += HandleTrayBalloonTipClicked;
        icon.BalloonTipClosed += (_, _) => _pendingSavedScreenshotPath = null;
        return icon;
    }

    private void RegisterInitialHotkey()
    {
        if (_hotkeyService.TryRegister(_settings.GetHotkey(), out var error))
        {
            _shell.ShowStatus($"后台监听：{_settings.GetHotkey().ToDisplayText()}", AppTheme.Success);
            return;
        }

        _shell.ShowStatus("全局快捷键注册失败", AppTheme.Danger);
        _pendingHotkeyError = error;
    }

    private void SaveSettings(object? sender, EventArgs e)
    {
        var folder = _savePathPage.FolderPath.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(this, "请选择截图保存文件夹。", "设置不完整",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var oldHotkey = _settings.GetHotkey();
        var newHotkey = _shortcutPage.Hotkey;

        try
        {
            var toolWidthRange = _editorSettingsPage.Range;
            var candidate = new AppSettings
            {
                OutputFolder = Path.GetFullPath(folder),
                StartMinimized = _shortcutPage.StartMinimized,
                HotkeyModifiers = newHotkey.Modifiers,
                HotkeyVirtualKey = newHotkey.VirtualKey,
                Preferences = new UserPreferences
                {
                    StickerSelectionMoveMode = _stickerBehaviorPage.Mode,
                    MinimumToolWidth = toolWidthRange.Minimum,
                    MaximumToolWidth = toolWidthRange.Maximum,
                    LongCaptureSafetyChecksEnabled = _longCapturePage.SafetyChecksEnabled,
                    DrawingToolCoefficients = _drawingCoefficientsPage.Coefficients
                }
            };
            Directory.CreateDirectory(candidate.OutputFolder);

            IReadOnlyList<string> imagesToMove = [];
            if (!AreSameFolder(_settings.OutputFolder, candidate.OutputFolder))
            {
                var previousImages = ScreenshotFolderMigration.FindImages(_settings.OutputFolder);
                if (previousImages.Count > 0)
                {
                    var moveChoice = MessageBox.Show(
                        this,
                        $"旧保存路径中有 {previousImages.Count} 张图片。是否将它们移动到新的保存路径？\n\n旧路径：{_settings.OutputFolder}\n新路径：{candidate.OutputFolder}",
                        "移动之前的图片",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                    if (moveChoice == DialogResult.Cancel)
                    {
                        return;
                    }

                    if (moveChoice == DialogResult.Yes)
                    {
                        imagesToMove = previousImages;
                    }
                }
            }

            if (!_hotkeyService.TryRegister(newHotkey, out var error))
            {
                _hotkeyService.TryRegister(oldHotkey, out _);
                _shortcutPage.Hotkey = oldHotkey;
                MessageBox.Show(this, error, "快捷键不可用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _settingsStore.Save(candidate);
            _settings = candidate;
            _savePathPage.FolderPath = candidate.OutputFolder;
            _galleryPage.FolderPath = candidate.OutputFolder;
            _trayIcon.Text = $"轻截（{newHotkey.ToDisplayText()}）";
            if (imagesToMove.Count > 0)
            {
                var migration = ScreenshotFolderMigration.MoveImages(
                    imagesToMove,
                    candidate.OutputFolder);
                _galleryPage.RefreshScreenshots();
                _shell.ShowStatus($"设置已保存，已移动 {migration.MovedCount} 张图片", AppTheme.Success);
                if (migration.FailedFiles.Count > 0)
                {
                    MessageBox.Show(
                        this,
                        $"已移动 {migration.MovedCount} 张图片，另有 {migration.FailedFiles.Count} 张移动失败。失败的图片仍保留在旧路径中。",
                        "部分图片未能移动",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            else
            {
                _shell.ShowStatus("设置已保存", AppTheme.Success);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _hotkeyService.TryRegister(oldHotkey, out _);
            MessageBox.Show(this, $"设置保存失败：{exception.Message}", "保存失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool AreSameFolder(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        StringComparison.OrdinalIgnoreCase);

    private void BrowseFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择截图保存文件夹",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_savePathPage.FolderPath)
                ? _savePathPage.FolderPath
                : _settings.OutputFolder,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _savePathPage.FolderPath = dialog.SelectedPath;
        }
    }

    private void OpenOutputFolder(object? sender, EventArgs e)
    {
        try
        {
            var folder = string.IsNullOrWhiteSpace(_savePathPage.FolderPath)
                ? _settings.OutputFolder
                : _savePathPage.FolderPath.Trim();
            Directory.CreateDirectory(folder);
            _fileLocationService.OpenFolder(folder);
        }
        catch (Exception exception) when (IsFileLocationException(exception))
        {
            MessageBox.Show(this, $"无法打开保存目录：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenModulesDirectory(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_moduleManager.ModulesDirectory);
            _fileLocationService.OpenFolder(_moduleManager.ModulesDirectory);
        }
        catch (Exception exception) when (IsFileLocationException(exception))
        {
            MessageBox.Show(this, $"无法打开模块目录：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshModules(bool force, bool showResult)
    {
        try
        {
            var result = _moduleManager.Refresh(force);
            if (result.Errors.Count > 0)
            {
                _shell.ShowStatus($"模块加载失败：{result.Errors[0]}", AppTheme.Danger);
                if (showResult)
                {
                    MessageBox.Show(this, string.Join(Environment.NewLine, result.Errors), "模块加载失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            if (showResult || result.Changed)
            {
                _shell.ShowStatus($"已加载 {result.Modules.Count} 个扩展模块", AppTheme.Success);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (showResult)
            {
                MessageBox.Show(this, $"刷新模块失败：{exception.Message}", "模块刷新失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private async void BeginCapture()
    {
        if (_isCapturing || IsDisposed)
        {
            return;
        }

        _isCapturing = true;
        var restoreMainWindow = Visible && WindowState != FormWindowState.Minimized;
        Hide();

        try
        {
            await Task.Yield();
            DwmFlush();
            await Task.Delay(90);
            using var snapshot = _captureService.CaptureDesktop();
            using var overlay = new CaptureOverlayForm(
                snapshot,
                _imageSaveService,
                _clipboardService,
                _windowLocator,
                _moduleManager,
                SelectionMoveAnnotationStrategyFactory.Create(
                    _settings.Preferences.StickerSelectionMoveMode),
                new ToolWidthController(_settings.GetToolWidthRange()),
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [CaptureFeaturePreferenceIds.LongCaptureSafetyChecks] =
                        _settings.Preferences.LongCaptureSafetyChecksEnabled
                },
                _settings.OutputFolder,
                _settings.Preferences.DrawingToolCoefficients);
            overlay.ScreenshotSaved += (_, path) =>
            {
                _shell.ShowStatus($"已保存：{Path.GetFileName(path)}", AppTheme.Success);
                ShowScreenshotSavedNotification(path);
                if (_shell.SelectedPageId == GalleryPageId)
                {
                    _galleryPage.RefreshScreenshots();
                }
            };
            overlay.ShowDialog();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"截图失败：{exception.Message}", "截图失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isCapturing = false;
            if (restoreMainWindow && !_isExiting)
            {
                ShowFromTray();
            }
        }
    }

    private void HandleShown(object? sender, EventArgs e)
    {
        if (_initialMinimizeHandled)
        {
            return;
        }

        _initialMinimizeHandled = true;
        if (_pendingHotkeyError is not null)
        {
            MessageBox.Show(this, _pendingHotkeyError, "快捷键不可用",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _pendingHotkeyError = null;
            return;
        }

        if (_settings.StartMinimized)
        {
            BeginInvoke(() =>
            {
                Hide();
                _hotkeyService.TryRegister(_settings.GetHotkey(), out _);
                _pendingSavedScreenshotPath = null;
                _trayIcon.ShowBalloonTip(1800, "轻截正在后台运行",
                    $"按 {_settings.GetHotkey().ToDisplayText()} 开始截图", ToolTipIcon.Info);
            });
        }
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_backgroundIntegrationEnabled)
        {
            _isExiting = true;
            return;
        }

        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _hotkeyService.TryRegister(_settings.GetHotkey(), out _);
        _pendingSavedScreenshotPath = null;
        _trayIcon.ShowBalloonTip(1600, "轻截仍在运行",
            $"按 {_settings.GetHotkey().ToDisplayText()} 截图；右键托盘图标可退出。", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        if (IsDisposed)
        {
            return;
        }

        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ShowScreenshotSavedNotification(string path)
    {
        _pendingSavedScreenshotPath = Path.GetFullPath(path);
        _trayIcon.ShowBalloonTip(
            3000,
            "截图保存成功",
            $"{Path.GetFileName(path)}\n点击打开文件所在位置",
            ToolTipIcon.Info);
    }

    private void HandleTrayBalloonTipClicked(object? sender, EventArgs e)
    {
        var path = _pendingSavedScreenshotPath;
        _pendingSavedScreenshotPath = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _fileLocationService.ShowFileInFolder(path);
        }
        catch (Exception exception) when (IsFileLocationException(exception))
        {
            MessageBox.Show($"无法打开截图位置：{exception.Message}", "打开失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _trayIcon.Visible = false;
        Close();
    }

    internal void RequestRestartExit() => ExitApplication();

    private static bool IsFileLocationException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _moduleRefreshTimer.Stop();
            _moduleRefreshTimer.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
