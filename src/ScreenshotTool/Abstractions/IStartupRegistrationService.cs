namespace ScreenshotTool.Abstractions;

internal interface IStartupRegistrationService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

internal interface IStartupEntryStore
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}
