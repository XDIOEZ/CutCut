using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenshotTool.Presentation;

internal sealed class LiveAnnotationKeyboardHook : IDisposable
{
    private const int LowLevelKeyboardHook = 13;
    private const int KeyDown = 0x0100;
    private const int KeyUp = 0x0101;
    private const int SystemKeyDown = 0x0104;
    private const int SystemKeyUp = 0x0105;
    private readonly Func<Keys, bool, bool> _handleEvent;
    private readonly LowLevelKeyboardProcedure _procedure;
    private nint _hook;
    private bool _disposed;

    public LiveAnnotationKeyboardHook(Func<Keys, bool, bool> handleEvent)
    {
        _handleEvent = handleEvent ?? throw new ArgumentNullException(nameof(handleEvent));
        _procedure = HookCallback;
    }

    public bool IsStarted => _hook != nint.Zero;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsStarted)
        {
            return;
        }

        _hook = SetWindowsHookEx(
            LowLevelKeyboardHook,
            _procedure,
            GetModuleHandle(null),
            0);
        if (_hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "无法启动全局键盘输入监听。");
        }
    }

    public void Stop()
    {
        if (_hook == nint.Zero)
        {
            return;
        }

        var hook = _hook;
        _hook = nint.Zero;
        UnhookWindowsHookEx(hook);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
    }

    private nint HookCallback(int code, nint message, nint dataPointer)
    {
        if (code >= 0)
        {
            try
            {
                var isDown = (int)message switch
                {
                    KeyDown or SystemKeyDown => true,
                    KeyUp or SystemKeyUp => false,
                    _ => (bool?)null
                };
                if (isDown is not null)
                {
                    var nativeEvent = Marshal.PtrToStructure<LowLevelKeyboardEvent>(dataPointer);
                    if (_handleEvent((Keys)nativeEvent.VirtualKeyCode, isDown.Value))
                    {
                        return 1;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // The session can close while the final keyboard message is in flight.
            }
            catch (Exception)
            {
                // Never allow a managed input callback failure to escape into user32.
            }
        }

        return CallNextHookEx(_hook, code, message, dataPointer);
    }

    private delegate nint LowLevelKeyboardProcedure(int code, nint message, nint dataPointer);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelKeyboardEvent
    {
        public readonly uint VirtualKeyCode;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProcedure procedure,
        nint module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(
        nint hook,
        int code,
        nint message,
        nint dataPointer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);
}
