namespace ScreenshotTool.Core;

[Flags]
internal enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

internal sealed record HotkeyDefinition(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static HotkeyDefinition Default { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, (int)Keys.X);

    public bool IsValid => Modifiers != HotkeyModifiers.None && VirtualKey is > 0 and <= 0xFF;

    public string ToDisplayText()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(((Keys)VirtualKey).ToString());
        return string.Join(" + ", parts);
    }
}
