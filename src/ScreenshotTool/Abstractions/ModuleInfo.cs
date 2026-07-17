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
