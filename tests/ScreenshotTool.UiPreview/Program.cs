using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Presentation;

namespace ScreenshotTool.UiPreview;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var previewFolder = Path.Combine(Path.GetTempPath(), "LightShotUiPreview");
        var form = new MainForm(
            new PreviewSettingsStore(previewFolder),
            new PreviewHotkeyService(),
            new PreviewCaptureService(),
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(),
            enableBackgroundIntegration: false)
        {
            Text = "轻截 - 界面预览"
        };

        System.Windows.Forms.Application.Run(form);
    }
}

internal sealed class PreviewSettingsStore(string outputFolder) : ISettingsStore
{
    private AppSettings _settings = new() { OutputFolder = outputFolder };

    public string ProfileId => "preview";

    public AppSettings Load() => _settings;

    public void Save(AppSettings settings) => _settings = settings;
}

internal sealed class PreviewHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? Pressed
    {
        add { }
        remove { }
    }

    public bool TryRegister(HotkeyDefinition hotkey, out string? error)
    {
        error = null;
        return true;
    }

    public void Unregister()
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class PreviewCaptureService : IScreenCaptureService
{
    public DesktopSnapshot CaptureDesktop() => throw new NotSupportedException("界面预览不执行截图。");
}

internal sealed class PreviewImageSaveService : IImageSaveService
{
    public string SavePng(Bitmap image, string outputFolder) =>
        throw new NotSupportedException("界面预览不保存截图。");
}

internal sealed class PreviewClipboardService : IClipboardService
{
    public void SetImage(Image image)
    {
    }

    public Bitmap? GetImage() => null;

    public string? GetText() => null;

    public void SetText(string text)
    {
    }
}

internal sealed class PreviewWindowLocator : IWindowLocator
{
    public Rectangle? FindWindowAt(Point screenPoint) => null;
}

internal sealed class PreviewFileLocationService : IFileLocationService
{
    public void OpenFolder(string folderPath)
    {
    }

    public void ShowFileInFolder(string filePath)
    {
    }

    public void OpenFile(string filePath)
    {
    }
}

internal sealed class PreviewModuleManager : IModuleManager
{
    public string ModulesDirectory => Path.Combine(Path.GetTempPath(), "LightShotUiPreviewModules");

    public ModuleRefreshResult Refresh(bool force = false) => new([], [], false);

    public IReadOnlyList<ModuleInfo> GetModules() => [];

    public IReadOnlyList<ICaptureFeature> CreateCaptureFeatures() => [];

    public void Dispose()
    {
    }
}
