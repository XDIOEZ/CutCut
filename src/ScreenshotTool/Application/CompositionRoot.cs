using ScreenshotTool.Infrastructure;
using ScreenshotTool.Infrastructure.Modules;
using ScreenshotTool.Presentation;

namespace ScreenshotTool.Application;

internal sealed class CompositionRoot : IDisposable
{
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly ModuleHost _moduleHost;

    private CompositionRoot(MainForm mainForm, GlobalHotkeyService hotkeyService, ModuleHost moduleHost)
    {
        MainForm = mainForm;
        _hotkeyService = hotkeyService;
        _moduleHost = moduleHost;
    }

    public MainForm MainForm { get; }

    public static CompositionRoot Create()
    {
        var settingsStore = new JsonSettingsStore();
        var hotkeyService = new GlobalHotkeyService();
        var captureService = new ScreenCaptureService();
        var imageSaveService = new PngImageSaveService();
        var clipboardService = new WindowsClipboardService();
        var windowLocator = new NativeWindowLocator();
        var fileLocationService = new ExplorerFileLocationService();
        var moduleHost = new ModuleHost(Path.Combine(AppContext.BaseDirectory, "Modules"));
        var mainForm = new MainForm(
            settingsStore,
            hotkeyService,
            captureService,
            imageSaveService,
            clipboardService,
            windowLocator,
            fileLocationService,
            moduleHost);
        return new CompositionRoot(mainForm, hotkeyService, moduleHost);
    }

    public void Dispose()
    {
        MainForm.Dispose();
        _moduleHost.Dispose();
        _hotkeyService.Dispose();
    }
}
