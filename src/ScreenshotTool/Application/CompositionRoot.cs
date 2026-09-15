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
    private readonly MyMemoryTextTranslationService _textTranslationService;

    private CompositionRoot(
        MainForm mainForm,
        GlobalHotkeyService hotkeyService,
        ModuleHost moduleHost,
        IApplicationUpdateService applicationUpdateService,
        MyMemoryTextTranslationService textTranslationService)
    {
        MainForm = mainForm;
        _hotkeyService = hotkeyService;
        _moduleHost = moduleHost;
        _applicationUpdateService = applicationUpdateService;
        _textTranslationService = textTranslationService;
    }

    public MainForm MainForm { get; }

    public static CompositionRoot Create(bool startInBackground = false)
    {
        var settingsStore = new JsonSettingsStore();
        var currentVersion =
            typeof(CompositionRoot).Assembly.GetName().Version ?? new Version(1, 0, 0);
        var startupRegistrationService = new StartupRegistrationService(
            new WindowsRunStartupEntryStore(),
            Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
        var startupWorkspace = new StartupWorkspaceService(
            settingsStore,
            currentVersion)
            .PrepareLaunch();
        var startupRegistrationError = new StartupRegistrationPreferenceService(
            settingsStore,
            startupRegistrationService)
            .Synchronize(startupWorkspace.Settings);
        var hotkeyService = new GlobalHotkeyService();
        var captureService = new ScreenCaptureService();
        var imageSaveService = new ImageSaveService();
        var clipboardService = new WindowsClipboardService();
        var windowLocator = new NativeWindowLocator();
        var fileLocationService = new ExplorerFileLocationService();
        var savedScreenshotService = new SavedScreenshotService();
        var moduleImageHost = new ModuleImageHostProxy();
        var moduleActivationPreferences = new UserPreferenceModuleActivationStore(
            startupWorkspace.Settings,
            settingsStore);
        var moduleHost = new ModuleHost(
            Path.Combine(AppContext.BaseDirectory, "Modules"),
            moduleImageHost,
            moduleActivationPreferences);
        var applicationUpdateService = new GitHubReleaseApplicationUpdateService(
            currentVersion,
            AppContext.BaseDirectory,
            Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
        var textTranslationService = new MyMemoryTextTranslationService();
        var pendingUpdateResult = applicationUpdateService.TakePendingApplyResult();
        var mainForm = new MainForm(
            settingsStore,
            hotkeyService,
            captureService,
            imageSaveService,
            clipboardService,
            windowLocator,
            fileLocationService,
            savedScreenshotService,
            moduleHost,
            startupRegistrationService,
            applicationUpdateService,
            pendingUpdateResult,
            initialSettings: startupWorkspace.Settings,
            startupWorkspaceReason: startupWorkspace.Reason,
            startInBackground: startInBackground,
            startupRegistrationError: startupRegistrationError,
            textTranslationService: textTranslationService);
        moduleImageHost.Attach(mainForm);
        moduleImageHost.AttachTextRecognition(moduleHost, clipboardService.SetText);
        return new CompositionRoot(
            mainForm,
            hotkeyService,
            moduleHost,
            applicationUpdateService,
            textTranslationService);
    }

    public void Dispose()
    {
        MainForm.Dispose();
        _moduleHost.Dispose();
        _hotkeyService.Dispose();
        _applicationUpdateService.Dispose();
        _textTranslationService.Dispose();
    }
}
