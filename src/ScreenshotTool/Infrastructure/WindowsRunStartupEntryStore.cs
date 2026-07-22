using Microsoft.Win32;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class WindowsRunStartupEntryStore : IStartupEntryStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(
            name,
            defaultValue: null,
            RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true) ??
                        throw new IOException("无法打开当前用户的 Windows 启动项。");
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
