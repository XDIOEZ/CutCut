namespace ScreenshotTool.Core;

internal sealed class AppSettings
{
    public string OutputFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "轻截");

    public HotkeyModifiers HotkeyModifiers { get; set; } = HotkeyDefinition.Default.Modifiers;

    public int HotkeyVirtualKey { get; set; } = HotkeyDefinition.Default.VirtualKey;

    public bool StartMinimized { get; set; }

    public string? LastLaunchedVersion { get; set; }

    public UserPreferences Preferences { get; set; } = new();

    public HotkeyDefinition GetHotkey() => new(HotkeyModifiers, HotkeyVirtualKey);

    public void SetHotkey(HotkeyDefinition hotkey)
    {
        HotkeyModifiers = hotkey.Modifiers;
        HotkeyVirtualKey = hotkey.VirtualKey;
    }

    public ToolWidthRange GetToolWidthRange() =>
        Preferences.GetToolWidthRange();
}
