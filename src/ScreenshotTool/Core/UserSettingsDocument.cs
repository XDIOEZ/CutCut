namespace ScreenshotTool.Core;

internal sealed class UserSettingsDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string ProfileId { get; set; } = "local";

    public AppSettings Settings { get; set; } = new();
}
