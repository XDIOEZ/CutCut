using ScreenshotTool.Core;

namespace ScreenshotTool.Abstractions;

internal interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? Pressed;
    bool TryRegister(HotkeyDefinition hotkey, out string? error);
    void Unregister();
}
