using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Application;

internal sealed class StartupWorkspaceService(
    ISettingsStore settingsStore,
    Version currentVersion)
{
    public StartupWorkspaceLaunch PrepareLaunch()
    {
        var settings = settingsStore.Load();
        var reason = StartupWorkspacePolicy.DetermineReason(
            settings.LastLaunchedVersion,
            currentVersion);
        if (reason == StartupWorkspaceReason.None)
        {
            return new StartupWorkspaceLaunch(settings, reason);
        }

        settings.LastLaunchedVersion = StartupWorkspacePolicy.CreateVersionMarker(currentVersion);
        try
        {
            settingsStore.Save(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep startup usable when the profile is temporarily read-only. The in-memory
            // marker will be persisted by the next successful settings save.
        }

        return new StartupWorkspaceLaunch(settings, reason);
    }
}

internal readonly record struct StartupWorkspaceLaunch(
    AppSettings Settings,
    StartupWorkspaceReason Reason);
