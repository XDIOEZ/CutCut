namespace ScreenshotTool.Abstractions;

internal interface IModuleManager : ICaptureFeatureCatalog, IDisposable
{
    string ModulesDirectory { get; }

    ModuleRefreshResult Refresh(bool force = false);

    IReadOnlyList<ModuleInfo> GetModules();
}
