using ScreenshotTool.Abstractions;
using ScreenshotTool.Infrastructure;
using ScreenshotTool.Infrastructure.Modules;
using ScreenshotTool.Presentation;

namespace ScreenshotTool.Application;

internal sealed class CompositionRoot : IDisposable
{
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly ModuleHost _moduleHost;
    private readonly IApplicationUpdateService _applicationUpdateService;

    private CompositionRoot(
        MainForm mainForm,
        GlobalHotkeyService hotkeyService,
        ModuleHost moduleHost,
        IApplicationUpdateService applicationUpdateService)
    {
        MainForm = mainForm;
        _hotkeyService = hotkeyService;
        _moduleHost = moduleHost;
        _applicationUpdateService = applicationUpdateService;
    }

    public MainForm MainForm { get; }

    public static CompositionRoot Create(bool startInBackground = false)
    {
        var settingsStore = new JsonSettingsStore();
        var currentVersion =
            typeof(CompositionRoot).Assembly.GetName().Version ?? new Version(1, 0, 0);
        var startupWorkspace = new StartupWorkspaceService(
            settingsStore,
            currentVersion)
            .PrepareLaunch();
        var hotkeyService = new GlobalHotkeyService();
        var captureService = new ScreenCaptureService();
        var imageSaveService = new PngImageSaveService();
        var clipboardService = new WindowsClipboardService();
        var windowLocator = new NativeWindowLocator();
        var fileLocationService = new ExplorerFileLocationService();
        var startupRegistrationService = new StartupRegistrationService(
            new WindowsRunStartupEntryStore(),
            Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
        var moduleHost = new ModuleHost(Path.Combine(AppContext.BaseDirectory, "Modules"));
        var applicationUpdateService = new GitHubReleaseApplicationUpdateService(
            currentVersion,
            AppContext.BaseDirectory,
            Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
        var pendingUpdateResult = applicationUpdateService.TakePendingApplyResult();
        var mainForm = new MainForm(
            settingsStore,
            hotkeyService,
            captureService,
            imageSaveService,
            clipboardService,
            windowLocator,
            fileLocationService,
            moduleHost,
            startupRegistrationService,
            applicationUpdateService,
            pendingUpdateResult,
            initialSettings: startupWorkspace.Settings,
            startupWorkspaceReason: startupWorkspace.Reason,
            startInBackground: startInBackground);
        return new CompositionRoot(
            mainForm,
            hotkeyService,
            moduleHost,
            applicationUpdateService);
    }

    public void Dispose()
    {
        MainForm.Dispose();
        _moduleHost.Dispose();
        _hotkeyService.Dispose();
        _applicationUpdateService.Dispose();
    }
}
