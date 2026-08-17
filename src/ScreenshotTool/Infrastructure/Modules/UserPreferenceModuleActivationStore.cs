using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class UserPreferenceModuleActivationStore(
    AppSettings settings,
    ISettingsStore settingsStore) : IModuleActivationPreferenceStore
{
    private readonly object _sync = new();

    public bool TryGet(string packageName, out ModuleActivationPreference preference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

        lock (_sync)
        {
            return settings.Preferences.ModuleActivationPreferences.TryGetValue(
                packageName,
                out preference!);
        }
    }

    public void Set(string packageName, ModuleActivationPreference preference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentNullException.ThrowIfNull(preference);

        lock (_sync)
        {
            var preferences = settings.Preferences.ModuleActivationPreferences;
            var hadPrevious = preferences.TryGetValue(packageName, out var previous);
            if (hadPrevious && previous == preference)
            {
                return;
            }

            preferences[packageName] = preference;
            try
            {
                settingsStore.Save(settings);
            }
            catch
            {
                preferences = settings.Preferences.ModuleActivationPreferences;
                if (hadPrevious)
                {
                    preferences[packageName] = previous!;
                }
                else
                {
                    preferences.Remove(packageName);
                }
                throw;
            }
        }
    }

    public void Remove(string packageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

        lock (_sync)
        {
            var preferences = settings.Preferences.ModuleActivationPreferences;
            if (!preferences.Remove(packageName, out var previous))
            {
                return;
            }

            try
            {
                settingsStore.Save(settings);
            }
            catch
            {
                preferences = settings.Preferences.ModuleActivationPreferences;
                preferences[packageName] = previous;
                throw;
            }
        }
    }
}
