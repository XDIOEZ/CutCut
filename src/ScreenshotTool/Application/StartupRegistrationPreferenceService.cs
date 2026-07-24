using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Application;

internal sealed class StartupRegistrationPreferenceService(
    ISettingsStore settingsStore,
    IStartupRegistrationService startupRegistrationService)
{
    public string? Synchronize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            if (settings.HasStartWithWindowsPreference)
            {
                startupRegistrationService.SetEnabled(settings.StartWithWindows);
                return null;
            }

            var migratedPreference = startupRegistrationService.HasRegistration;
            settings.StartWithWindows = migratedPreference;
            if (migratedPreference)
            {
                startupRegistrationService.SetEnabled(enabled: true);
            }

            settingsStore.Save(settings);
            return null;
        }
        catch (Exception exception) when (IsSynchronizationException(exception))
        {
            return exception.Message;
        }
    }

    private static bool IsSynchronizationException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or
        System.Security.SecurityException;
}
