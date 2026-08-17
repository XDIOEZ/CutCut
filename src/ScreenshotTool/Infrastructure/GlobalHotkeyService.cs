using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure;

internal sealed class GlobalHotkeyService : NativeWindow, IGlobalHotkeyService
{
    private const int FirstHotkeyId = 0x5343;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private readonly HashSet<int> _registeredHotkeyIds = [];
    private bool _disposed;

    public GlobalHotkeyService()
    {
        CreateHandle(new CreateParams
        {
            Caption = "ScreenshotTool.HotkeyWindow",
            Parent = new IntPtr(-3)
        });
    }

    public event EventHandler? Pressed;

    public bool TryRegister(IReadOnlyList<HotkeyDefinition> hotkeys, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkeys);
        error = null;
        Unregister();

        if (hotkeys.Count > HotkeyBindings.MaximumCount)
        {
            error = $"最多只能绑定 {HotkeyBindings.MaximumCount} 个截图快捷键。";
            return false;
        }
        if (hotkeys.Any(hotkey => !hotkey.IsValid))
        {
            error = "快捷键至少需要一个 Ctrl、Shift、Alt 或 Win 修饰键。";
            return false;
        }
        if (hotkeys.Distinct().Count() != hotkeys.Count)
        {
            error = "截图快捷键不能重复绑定。";
            return false;
        }

        for (var index = 0; index < hotkeys.Count; index++)
        {
            var hotkey = hotkeys[index];
            var hotkeyId = FirstHotkeyId + index;
            var modifiers = (uint)hotkey.Modifiers | ModNoRepeat;
            if (RegisterHotKey(Handle, hotkeyId, modifiers, (uint)hotkey.VirtualKey))
            {
                _registeredHotkeyIds.Add(hotkeyId);
                continue;
            }

            var nativeError = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            Unregister();
            error = $"快捷键 {hotkey.ToDisplayText()} 注册失败，可能已被其他程序占用。\n{nativeError}";
            return false;
        }

        return true;
    }

    public void Unregister()
    {
        if (_registeredHotkeyIds.Count == 0 || Handle == IntPtr.Zero)
        {
            return;
        }

        foreach (var hotkeyId in _registeredHotkeyIds)
        {
            UnregisterHotKey(Handle, hotkeyId);
        }
        _registeredHotkeyIds.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && _registeredHotkeyIds.Contains(m.WParam.ToInt32()))
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
