using ScreenshotTool.Core;

namespace ScreenshotTool.Abstractions;

internal interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? Pressed;
    bool TryRegister(IReadOnlyList<HotkeyDefinition> hotkeys, out string? error);
    void Unregister();
}
