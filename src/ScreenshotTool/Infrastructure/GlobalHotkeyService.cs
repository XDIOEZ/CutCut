using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure;

internal sealed class GlobalHotkeyService : NativeWindow, IGlobalHotkeyService
{
    private const int HotkeyId = 0x5343;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private bool _registered;
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

    public bool TryRegister(HotkeyDefinition hotkey, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        error = null;
        Unregister();

        if (!hotkey.IsValid)
        {
            error = "快捷键至少需要一个 Ctrl、Shift、Alt 或 Win 修饰键。";
            return false;
        }

        var modifiers = (uint)hotkey.Modifiers | ModNoRepeat;
        if (!RegisterHotKey(Handle, HotkeyId, modifiers, (uint)hotkey.VirtualKey))
        {
            var nativeError = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            error = $"快捷键 {hotkey.ToDisplayText()} 注册失败，可能已被其他程序占用。\n{nativeError}";
            return false;
        }

        _registered = true;
        return true;
    }

    public void Unregister()
    {
        if (!_registered || Handle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
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
