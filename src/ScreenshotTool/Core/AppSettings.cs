namespace ScreenshotTool.Core;

internal sealed class AppSettings
{
    private bool _startWithWindows;

    public string OutputFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "轻截");

    public HotkeyModifiers HotkeyModifiers { get; set; } = HotkeyDefinition.Default.Modifiers;

    public int HotkeyVirtualKey { get; set; } = HotkeyDefinition.Default.VirtualKey;

    public List<HotkeyDefinition>? Hotkeys { get; set; }

    public bool StartMinimized { get; set; }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            _startWithWindows = value;
            HasStartWithWindowsPreference = true;
        }
    }

    internal bool HasStartWithWindowsPreference { get; private set; }

    public string? LastLaunchedVersion { get; set; }

    public UserPreferences Preferences { get; set; } = new();

    public HotkeyDefinition GetHotkey() => new(HotkeyModifiers, HotkeyVirtualKey);

    public IReadOnlyList<HotkeyDefinition> GetHotkeys()
    {
        if (Hotkeys is not null)
        {
            return HotkeyBindings.Normalize(Hotkeys);
        }

        var legacyHotkey = GetHotkey();
        return legacyHotkey.IsValid
            ? [legacyHotkey]
            : [HotkeyDefinition.Default];
    }

    public void SetHotkey(HotkeyDefinition hotkey)
    {
        SetHotkeys([hotkey]);
    }

    public void SetHotkeys(IEnumerable<HotkeyDefinition> hotkeys)
    {
        var normalized = HotkeyBindings.Normalize(hotkeys);
        Hotkeys = normalized.ToList();
        if (normalized.Count > 0)
        {
            HotkeyModifiers = normalized[0].Modifiers;
            HotkeyVirtualKey = normalized[0].VirtualKey;
        }
    }

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        OutputFolder = settings.OutputFolder;
        HotkeyModifiers = settings.HotkeyModifiers;
        HotkeyVirtualKey = settings.HotkeyVirtualKey;
        Hotkeys = settings.Hotkeys?.ToList();
        StartMinimized = settings.StartMinimized;
        StartWithWindows = settings.StartWithWindows;
        LastLaunchedVersion = settings.LastLaunchedVersion;
        Preferences = settings.Preferences;
    }

    public string GetScreenshotParentFolder() =>
        Preferences.OrganizeScreenshotsByDate &&
        !string.IsNullOrWhiteSpace(Preferences.ScreenshotDateParentFolder)
            ? Preferences.ScreenshotDateParentFolder
            : OutputFolder;

    public ToolWidthRange GetToolWidthRange() =>
        Preferences.GetToolWidthRange();
}
