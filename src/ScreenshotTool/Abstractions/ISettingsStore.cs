using ScreenshotTool.Core;

namespace ScreenshotTool.Abstractions;

internal interface ISettingsStore
{
    string ProfileId { get; }

    AppSettings Load();
    void Save(AppSettings settings);
}
