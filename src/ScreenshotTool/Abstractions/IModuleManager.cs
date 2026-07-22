using ScreenshotTool.Contracts;

namespace ScreenshotTool.Abstractions;

internal interface IModuleManager : ICaptureFeatureCatalog, IDisposable
{
    string ModulesDirectory { get; }

    ModuleRefreshResult Refresh(bool force = false);

    IReadOnlyList<ModuleInfo> GetModules();

    IReadOnlyList<ModulePackageInfo> GetInstalledPackages();

    ModuleOperationResult SetPackageEnabled(string packageName, bool enabled);

    ModuleOperationResult DeletePackage(string packageName);

    IReadOnlyList<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host);
}
