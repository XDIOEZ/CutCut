namespace ScreenshotTool.Abstractions;

internal sealed record ModuleInfo(
    string Id,
    string DisplayName,
    Version Version,
    string AssemblyPath);

internal sealed record ModuleRefreshResult(
    IReadOnlyList<ModuleInfo> Modules,
    IReadOnlyList<string> Errors,
    bool Changed);

internal enum ModulePackageState
{
    Enabled,
    Disabled,
    LoadFailed
}

internal sealed record ModulePackageInfo(
    string PackageName,
    string ModuleId,
    string DisplayName,
    Version? Version,
    string DirectoryPath,
    ModulePackageState State,
    string? ErrorMessage = null);

internal sealed record ModuleOperationResult(
    bool Succeeded,
    string Message,
    ModuleRefreshResult? RefreshResult = null);
