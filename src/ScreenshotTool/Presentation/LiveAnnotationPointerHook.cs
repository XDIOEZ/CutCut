using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenshotTool.Presentation;

internal enum LiveAnnotationPointerEventKind
{
    Move,
    LeftDown,
    LeftUp,
    RightDown,
    RightUp,
    MiddleDown,
    MiddleUp,
    XButtonDown,
    XButtonUp,
    Wheel,
    HorizontalWheel
}

internal readonly record struct LiveAnnotationPointerEvent(
    LiveAnnotationPointerEventKind Kind,
    Point ScreenLocation,
    int WheelDelta = 0);

internal sealed class LiveAnnotationPointerHook : IDisposable
{
    private const int LowLevelMouseHook = 14;
    private const int MouseMove = 0x0200;
    private const int LeftButtonDown = 0x0201;
    private const int LeftButtonUp = 0x0202;
    private const int RightButtonDown = 0x0204;
    private const int RightButtonUp = 0x0205;
    private const int MiddleButtonDown = 0x0207;
    private const int MiddleButtonUp = 0x0208;
    private const int MouseWheel = 0x020A;
    private const int XButtonDown = 0x020B;
    private const int XButtonUp = 0x020C;
    private const int MouseHorizontalWheel = 0x020E;
    private readonly Func<LiveAnnotationPointerEvent, bool> _handleEvent;
    private readonly LowLevelMouseProcedure _procedure;
    private nint _hook;
    private bool _disposed;

    public LiveAnnotationPointerHook(Func<LiveAnnotationPointerEvent, bool> handleEvent)
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
            LowLevelMouseHook,
            _procedure,
            GetModuleHandle(null),
            0);
        if (_hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "无法启动实时批注鼠标输入捕获。");
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
                var nativeEvent = Marshal.PtrToStructure<LowLevelMouseEvent>(dataPointer);
                var kind = (int)message switch
                {
                    MouseMove => LiveAnnotationPointerEventKind.Move,
                    LeftButtonDown => LiveAnnotationPointerEventKind.LeftDown,
                    LeftButtonUp => LiveAnnotationPointerEventKind.LeftUp,
                    RightButtonDown => LiveAnnotationPointerEventKind.RightDown,
                    RightButtonUp => LiveAnnotationPointerEventKind.RightUp,
                    MiddleButtonDown => LiveAnnotationPointerEventKind.MiddleDown,
                    MiddleButtonUp => LiveAnnotationPointerEventKind.MiddleUp,
                    MouseWheel => LiveAnnotationPointerEventKind.Wheel,
                    XButtonDown => LiveAnnotationPointerEventKind.XButtonDown,
                    XButtonUp => LiveAnnotationPointerEventKind.XButtonUp,
                    MouseHorizontalWheel => LiveAnnotationPointerEventKind.HorizontalWheel,
                    _ => (LiveAnnotationPointerEventKind?)null
                };
                if (kind is not null)
                {
                    var wheelDelta = kind is LiveAnnotationPointerEventKind.Wheel or
                        LiveAnnotationPointerEventKind.HorizontalWheel
                        ? unchecked((short)(nativeEvent.MouseData >> 16))
                        : 0;
                    if (_handleEvent(new LiveAnnotationPointerEvent(
                            kind.Value,
                            new Point(nativeEvent.Point.X, nativeEvent.Point.Y),
                            wheelDelta)))
                    {
                        return 1;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // The session can close while the final pointer message is in flight.
            }
            catch (Exception)
            {
                // Never allow a managed input callback failure to escape into user32.
            }
        }

        return CallNextHookEx(_hook, code, message, dataPointer);
    }

    private delegate nint LowLevelMouseProcedure(int code, nint message, nint dataPointer);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelMouseEvent
    {
        public readonly NativePoint Point;
        public readonly uint MouseData;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelMouseProcedure procedure,
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
