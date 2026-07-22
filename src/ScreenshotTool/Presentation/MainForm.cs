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
    private readonly StickerBehaviorSettingsPage _stickerBehaviorPage;
    private readonly EditorSettingsPage _editorSettingsPage;
    private readonly DrawingCoefficientsSettingsPage _drawingCoefficientsPage;
    private readonly ScreenshotSettingsPage _screenshotSettingsPage;
    private readonly SavePathSettingsPage _savePathPage;
    private readonly ScreenshotGalleryPage _galleryPage;
    private readonly Dictionary<string, IModuleSettingsPage> _moduleSettingsPages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _backgroundIntegrationEnabled;
    private AppSettings _settings;
    private bool _isCapturing;
    private bool _isExiting;
    private bool _initialMinimizeHandled;
    private string? _pendingHotkeyError;
    private SavedArtifactNotificationForm? _savedArtifactNotification;

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
        Icon = AppIcon.Shared;

        _shell = new AppShellControl();
        Controls.Add(_shell);

        _stickerBehaviorPage = new StickerBehaviorSettingsPage(
            _settings.Preferences.StickerSelectionMoveMode);
        _editorSettingsPage = new EditorSettingsPage(
            _settings.GetToolWidthRange(),
            _settings.Preferences.AnnotationRotationStepDegrees,
            _settings.Preferences.DrawingCursorShape,
            _settings.Preferences.AnnotationSnappingEnabled,
            _settings.Preferences.AnnotationSnapThresholdPixels,
            _settings.Preferences.CtrlDragStepPixels);
        _drawingCoefficientsPage = new DrawingCoefficientsSettingsPage(
            _settings.Preferences.DrawingToolCoefficients);
        _screenshotSettingsPage = new ScreenshotSettingsPage(
            _settings.GetHotkey(),
            _settings.StartMinimized,
            _settings.Preferences.DismissSaveNotificationBeforeCapture,
            _settings.Preferences.HideMainWindowDuringCapture);
        _savePathPage = new SavePathSettingsPage(
            _settings.OutputFolder,
            _settings.Preferences.ScreenshotFileNameMode);
        _galleryPage = new ScreenshotGalleryPage(_settings.OutputFolder, _fileLocationService);
        ComposePages();
        WirePageEvents();

        _trayIcon = BuildTrayIcon(enableBackgroundIntegration);
        _moduleRefreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _moduleRefreshTimer.Tick += (_, _) => RefreshModules(force: false, showResult: false);
        RefreshModules(force: false, showResult: false);
        if (enableBackgroundIntegration)
        {
            _moduleRefreshTimer.Start();
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
            _stickerBehaviorPage,
            100));

        _shell.AddPage(new AppPage(
            "screenshot-settings",
            "截图设置",
            "设置截图前提示与主窗口显示行为",
            _screenshotSettingsPage,
            200));

        _shell.AddPage(new AppPage(
            "save",
            "保存路径",
            "设置截图文件夹并快速打开保存位置",
            _savePathPage,
            500));

        _shell.AddPage(new AppPage(
            "edit",
            "图片修改",
            "设置绘图粗细、元素吸附、Ctrl 拖动步长与旋转步进",
            _editorSettingsPage,
            600));

        _shell.AddPage(new AppPage(
            "drawing-coefficients",
            "绘制系数",
            "配置各绘制元素的基础尺寸，工具栏粗细作为统一倍率",
            _drawingCoefficientsPage,
            700));

        _shell.AddPage(new AppPage(
            GalleryPageId,
            "查看截图",
            "浏览保存目录中的最近截图，双击即可查看",
            _galleryPage,
            800));
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
        _stickerBehaviorPage.SaveRequested += SaveSettings;
        _editorSettingsPage.SaveRequested += SaveSettings;
        _drawingCoefficientsPage.SaveRequested += SaveSettings;
        _screenshotSettingsPage.SaveRequested += SaveSettings;
        _screenshotSettingsPage.HotkeyInputEntered += (_, _) => _hotkeyService.Unregister();
        _screenshotSettingsPage.HotkeyInputLeft += (_, _) =>
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
            Icon = AppIcon.Shared,
            Text = $"轻截（{_settings.GetHotkey().ToDisplayText()}）",
            Visible = visible,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
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
        var newHotkey = _screenshotSettingsPage.Hotkey;

        try
        {
            var toolWidthRange = _editorSettingsPage.Range;
            var candidate = new AppSettings
            {
                OutputFolder = Path.GetFullPath(folder),
                StartMinimized = _screenshotSettingsPage.StartMinimized,
                HotkeyModifiers = newHotkey.Modifiers,
                HotkeyVirtualKey = newHotkey.VirtualKey,
                Preferences = new UserPreferences
                {
                    StickerSelectionMoveMode = _stickerBehaviorPage.Mode,
                    MinimumToolWidth = toolWidthRange.Minimum,
                    MaximumToolWidth = toolWidthRange.Maximum,
                    LastToolWidth = _settings.Preferences.GetLastToolWidth(),
                    AnnotationRotationStepDegrees = _editorSettingsPage.RotationStepDegrees,
                    DrawingCursorShape = _editorSettingsPage.DrawingCursorShape,
                    AnnotationSnappingEnabled = _editorSettingsPage.SnappingEnabled,
                    AnnotationSnapThresholdPixels = _editorSettingsPage.SnapThresholdPixels,
                    CtrlDragStepPixels = _editorSettingsPage.CtrlDragStepPixels,
                    ModuleBooleanPreferences = new Dictionary<string, bool>(
                        _settings.Preferences.ModuleBooleanPreferences,
                        StringComparer.Ordinal),
                    ModuleIntegerPreferences = new Dictionary<string, int>(
                        _settings.Preferences.ModuleIntegerPreferences,
                        StringComparer.Ordinal),
                    ModuleStringPreferences = new Dictionary<string, string>(
                        _settings.Preferences.ModuleStringPreferences,
                        StringComparer.Ordinal),
                    ScreenshotFileNameMode = _savePathPage.FileNameMode,
                    DismissSaveNotificationBeforeCapture =
                        _screenshotSettingsPage.DismissSaveNotificationBeforeCapture,
                    HideMainWindowDuringCapture =
                        _screenshotSettingsPage.HideMainWindowDuringCapture,
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
                _screenshotSettingsPage.Hotkey = oldHotkey;
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
            if (result.Changed || force)
            {
                SyncModuleSettingsPages();
            }
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

    private void SyncModuleSettingsPages()
    {
        foreach (var current in _moduleSettingsPages)
        {
            _shell.RemovePage(current.Key);
            DisposeModuleSettingsPage(current.Value);
        }
        _moduleSettingsPages.Clear();

        foreach (var page in _moduleManager.CreateSettingsPages(
                     new MainFormModuleSettingsHost(this)))
        {
            try
            {
                var id = page.Id;
                if (_shell.ContainsPage(id) || _moduleSettingsPages.ContainsKey(id))
                {
                    DisposeModuleSettingsPage(page);
                    _shell.ShowStatus($"模块设置页 ID 重复：{id}", AppTheme.Danger);
                    continue;
                }

                _shell.AddPage(new AppPage(
                    id,
                    page.Title,
                    page.Description,
                    page.Content,
                    page.Order));
                _moduleSettingsPages.Add(id, page);
            }
            catch (Exception exception)
            {
                DisposeModuleSettingsPage(page);
                _shell.ShowStatus(
                    $"模块设置页加载失败：{exception.Message}",
                    AppTheme.Danger);
            }
        }
    }

    private static void DisposeModuleSettingsPage(IModuleSettingsPage page)
    {
        try
        {
            page.Dispose();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"模块设置页释放失败：{exception}");
        }
    }

    private async void BeginCapture()
    {
        if (_isCapturing || IsDisposed)
        {
            return;
        }

        ApplySavedArtifactNotificationCaptureStartPolicy();
        _isCapturing = true;
        var mainWindowWasVisible = Visible && WindowState != FormWindowState.Minimized;
        var hideMainWindow = MainWindowCaptureVisibilityPolicy.ShouldHide(
            _settings.Preferences.HideMainWindowDuringCapture,
            Visible,
            WindowState);
        var originalOpacity = Opacity;
        var originalWindowState = WindowState;
        var captureProtectionApplied = false;
        if (hideMainWindow)
        {
            captureProtectionApplied = WindowCaptureProtection.TryExclude(this);
            DwmFlush();
            Opacity = 0D;
            DwmFlush();
            captureProtectionApplied =
                WindowCaptureProtection.TryExclude(this) || captureProtectionApplied;
            Hide();
            DwmFlush();
        }

        try
        {
            await Task.Yield();
            DwmFlush();
            await Task.Delay(90);
            using var snapshot = _captureService.CaptureDesktop();
            var toolWidthController = new ToolWidthController(
                _settings.GetToolWidthRange(),
                _settings.Preferences.GetLastToolWidth());
            var annotationSessionFactory = new LiveAnnotationSessionFactory(
                _clipboardService,
                _settings.Preferences.DrawingToolCoefficients,
                _settings.Preferences.AnnotationRotationStepDegrees,
                _settings.Preferences.DrawingCursorShape,
                _settings.Preferences.AnnotationSnappingEnabled,
                _settings.Preferences.AnnotationSnapThresholdPixels,
                _settings.Preferences.CtrlDragStepPixels);
            using var overlay = new CaptureOverlayForm(
                snapshot,
                _imageSaveService,
                _clipboardService,
                _windowLocator,
                _moduleManager,
                SelectionMoveAnnotationStrategyFactory.Create(
                    _settings.Preferences.StickerSelectionMoveMode),
                toolWidthController,
                annotationSessionFactory,
                _settings.Preferences.ModuleBooleanPreferences,
                _settings.Preferences.ModuleIntegerPreferences,
                _settings.OutputFolder,
                _settings.Preferences.ScreenshotFileNameMode,
                _settings.Preferences.DrawingToolCoefficients,
                _settings.Preferences.AnnotationRotationStepDegrees,
                _settings.Preferences.DrawingCursorShape,
                _settings.Preferences.AnnotationSnappingEnabled,
                _settings.Preferences.AnnotationSnapThresholdPixels,
                _settings.Preferences.CtrlDragStepPixels);
            overlay.ArtifactSaved += (_, path) =>
            {
                _shell.ShowStatus($"已保存：{Path.GetFileName(path)}", AppTheme.Success);
                ShowSavedArtifactNotification(path);
                if (_shell.SelectedPageId == GalleryPageId)
                {
                    _galleryPage.RefreshScreenshots();
                }
            };
            overlay.ShowDialog();
            SaveLastToolWidth(toolWidthController.Current);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"截图失败：{exception.Message}", "截图失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isCapturing = false;
            Opacity = originalOpacity;
            if (hideMainWindow && mainWindowWasVisible && !_isExiting)
            {
                Show();
                WindowState = originalWindowState;
                Activate();
            }
            if (captureProtectionApplied)
            {
                WindowCaptureProtection.TryAllow(this);
            }
        }
    }

    private void SaveLastToolWidth(int toolWidth)
    {
        if (!_settings.Preferences.RememberToolWidth(toolWidth))
        {
            return;
        }

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _shell.ShowStatus($"粗细参数保存失败：{exception.Message}", AppTheme.Danger);
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

    private void ShowSavedArtifactNotification(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            if (IsHandleCreated)
            {
                BeginInvoke((Action)(() => ShowSavedArtifactNotification(fullPath)));
            }
            return;
        }

        _savedArtifactNotification?.Close();
        var notification = new SavedArtifactNotificationForm(fullPath);
        notification.OpenRequested += HandleSavedArtifactOpenRequested;
        notification.FormClosed += (_, _) =>
        {
            notification.OpenRequested -= HandleSavedArtifactOpenRequested;
            if (ReferenceEquals(_savedArtifactNotification, notification))
            {
                _savedArtifactNotification = null;
            }
            notification.Dispose();
        };
        _savedArtifactNotification = notification;
        notification.Show();
    }

    internal void ApplySavedArtifactNotificationCaptureStartPolicy()
    {
        if (!_settings.Preferences.DismissSaveNotificationBeforeCapture)
        {
            return;
        }

        _savedArtifactNotification?.Close();
    }

    private void HandleSavedArtifactOpenRequested(object? sender, string path)
    {
        try
        {
            _fileLocationService.ShowFileInFolder(path);
        }
        catch (Exception exception) when (IsFileLocationException(exception))
        {
            MessageBox.Show($"无法打开文件位置：{exception.Message}", "打开失败",
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
            _savedArtifactNotification?.Close();
            _savedArtifactNotification?.Dispose();
            _savedArtifactNotification = null;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            foreach (var page in _moduleSettingsPages)
            {
                _shell.RemovePage(page.Key);
                DisposeModuleSettingsPage(page.Value);
            }
            _moduleSettingsPages.Clear();
        }

        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    private sealed class MainFormModuleSettingsHost(MainForm owner) : IModuleSettingsHost
    {
        public bool GetBoolean(string id, bool defaultValue) =>
            owner._settings.Preferences.ModuleBooleanPreferences.TryGetValue(id, out var value)
                ? value
                : defaultValue;

        public int GetInteger(string id, int defaultValue) =>
            owner._settings.Preferences.ModuleIntegerPreferences.TryGetValue(id, out var value)
                ? value
                : defaultValue;

        public string GetString(string id, string defaultValue) =>
            owner._settings.Preferences.ModuleStringPreferences.TryGetValue(id, out var value)
                ? value
                : defaultValue;

        public void SetBoolean(string id, bool value) =>
            owner._settings.Preferences.ModuleBooleanPreferences[id] = value;

        public void SetInteger(string id, int value) =>
            owner._settings.Preferences.ModuleIntegerPreferences[id] = value;

        public void SetString(string id, string value) =>
            owner._settings.Preferences.ModuleStringPreferences[id] = value;

        public void Save()
        {
            owner._settingsStore.Save(owner._settings);
            owner._shell.ShowStatus("模块设置已保存", AppTheme.Success);
        }
    }
}

internal static class MainWindowCaptureVisibilityPolicy
{
    public static bool ShouldHide(
        bool hideMainWindowDuringCapture,
        bool isVisible,
        FormWindowState windowState) =>
        hideMainWindowDuringCapture &&
        isVisible &&
        windowState != FormWindowState.Minimized;
}
