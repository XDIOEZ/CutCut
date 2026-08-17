using System.Runtime.InteropServices;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureBackgroundRefreshHotkeyRegistration : IDisposable
{
    private const int WindowMessageHotkey = 0x0312;
    private const int HotkeyId = 0x4352;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierNoRepeat = 0x4000;
    private readonly nint _windowHandle;
    private bool _disposed;

    public CaptureBackgroundRefreshHotkeyRegistration(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentException("截图浮层窗口句柄无效。", nameof(windowHandle));
        }

        _windowHandle = windowHandle;
    }

    public bool IsRegistered { get; private set; }

    public int LastError { get; private set; }

    public bool SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (enabled)
        {
            if (!IsRegistered)
            {
                IsRegistered = RegisterHotKey(
                    _windowHandle,
                    HotkeyId,
                    ModifierControl | ModifierNoRepeat,
                    (uint)Keys.R);
                LastError = IsRegistered ? 0 : Marshal.GetLastWin32Error();
            }
            return IsRegistered;
        }

        if (IsRegistered)
        {
            var unregistered = UnregisterHotKey(_windowHandle, HotkeyId);
            LastError = unregistered ? 0 : Marshal.GetLastWin32Error();
            IsRegistered = !unregistered;
        }
        return false;
    }

    public bool Matches(Message message) =>
        IsRegistered &&
        message.Msg == WindowMessageHotkey &&
        message.WParam.ToInt32() == HotkeyId;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsRegistered)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            IsRegistered = false;
        }
        _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint windowHandle, int id);
}
