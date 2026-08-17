namespace ScreenshotTool.Core;

internal static class HotkeyBindings
{
    public const int MaximumCount = 3;

    public static IReadOnlyList<HotkeyDefinition> Normalize(
        IEnumerable<HotkeyDefinition>? hotkeys) =>
        (hotkeys ?? [])
        .Where(hotkey => hotkey is not null && hotkey.IsValid)
        .Distinct()
        .Take(MaximumCount)
        .ToArray();

    public static string ToDisplayText(IEnumerable<HotkeyDefinition>? hotkeys)
    {
        var normalized = Normalize(hotkeys);
        return normalized.Count == 0
            ? "未绑定"
            : string.Join(" / ", normalized.Select(hotkey => hotkey.ToDisplayText()));
    }

    public static string ToCompactDisplayText(IEnumerable<HotkeyDefinition>? hotkeys)
    {
        var normalized = Normalize(hotkeys);
        return normalized.Count switch
        {
            0 => "未绑定",
            1 => normalized[0].ToDisplayText(),
            _ => $"{normalized[0].ToDisplayText()} 等 {normalized.Count} 个"
        };
    }
}
