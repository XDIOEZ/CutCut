namespace ScreenshotTool.Abstractions;

internal interface IModuleActivationPreferenceStore
{
    bool TryGet(string packageName, out ModuleActivationPreference preference);

    void Set(string packageName, ModuleActivationPreference preference);

    void Remove(string packageName);
}
