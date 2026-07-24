using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Application;

internal sealed class StartupRegistrationService(
    IStartupEntryStore entryStore,
    string executablePath) : IStartupRegistrationService
{
    internal const string EntryName = "LightShotCN";
    private readonly string _expectedCommand = StartupCommandBuilder.Build(executablePath);

    public bool HasRegistration => entryStore.GetValue(EntryName) is not null;

    public bool IsEnabled => string.Equals(
        entryStore.GetValue(EntryName),
        _expectedCommand,
        StringComparison.OrdinalIgnoreCase);

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            entryStore.SetValue(EntryName, _expectedCommand);
            return;
        }

        entryStore.DeleteValue(EntryName);
    }
}
