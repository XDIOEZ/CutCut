using System.ComponentModel;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ScreenshotTool.LongCapture;

internal enum ManualLongCaptureInputKind
{
    Wheel,
    Finish,
    Cancel
}

internal sealed record ManualLongCaptureInput(
    ManualLongCaptureInputKind Kind,
    int WheelDelta = 0);

/// <summary>
/// Observes physical wheel and cancellation keys during a manual long capture. Mouse wheel messages
/// and Enter always continue to the target application; only Escape is consumed by the session.
/// </summary>
internal sealed class LongCaptureInputMonitor : IDisposable
{
    private const int WhKeyboardLowLevel = 13;
    private const int WhMouseLowLevel = 14;
    private const int WindowMessageKeyDown = 0x0100;
    private const int WindowMessageKeyUp = 0x0101;
    private const int WindowMessageSystemKeyDown = 0x0104;
    private const int WindowMessageSystemKeyUp = 0x0105;
    private const int WindowMessageMouseWheel = 0x020A;
    private const uint VirtualKeyEscape = 0x1B;
    private const int ErrorInvalidHookHandle = 1404;
    private static readonly ConcurrentBag<GCHandle> FailedUnhookRoots = [];

    private readonly Rectangle _selectionScreenBounds;
    private readonly HookProcedure _mouseProcedure;
    private readonly HookProcedure _keyboardProcedure;
    private readonly HashSet<uint> _pressedCommandKeys = [];
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private bool _disposed;

    public LongCaptureInputMonitor(Rectangle selectionScreenBounds)
    {
        if (selectionScreenBounds.Width <= 0 || selectionScreenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionScreenBounds));
        }

        _selectionScreenBounds = selectionScreenBounds;
        _mouseProcedure = MouseHookCallback;
        _keyboardProcedure = KeyboardHookCallback;
        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(
            WhMouseLowLevel,
            _mouseProcedure,
            module,
            0);
        if (_mouseHook == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "无法监听长截图滚轮输入。");
        }

        _keyboardHook = SetWindowsHookEx(
            WhKeyboardLowLevel,
            _keyboardProcedure,
            module,
            0);
        if (_keyboardHook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            ReleaseHook(ref _mouseHook, _mouseProcedure);
            throw new Win32Exception(error, "无法监听长截图取消按键。");
        }
    }

    public event EventHandler<ManualLongCaptureInput>? InputReceived;

    internal static ManualLongCaptureInputKind? GetKeyboardCommand(uint virtualKey) =>
        virtualKey == VirtualKeyEscape
            ? ManualLongCaptureInputKind.Cancel
            : null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseHook(ref _keyboardHook, _keyboardProcedure);
        ReleaseHook(ref _mouseHook, _mouseProcedure);
        _pressedCommandKeys.Clear();
    }

    private IntPtr MouseHookCallback(int code, IntPtr message, IntPtr dataPointer)
    {
        if (!_disposed &&
            code >= 0 &&
            message.ToInt64() == WindowMessageMouseWheel)
        {
            var data = Marshal.PtrToStructure<MouseLowLevelHookData>(dataPointer);
            var point = new Point(data.Point.X, data.Point.Y);
            if (_selectionScreenBounds.Contains(point))
            {
                var delta = unchecked((short)((data.MouseData >> 16) & 0xFFFF));
                RaiseInput(new ManualLongCaptureInput(
                    ManualLongCaptureInputKind.Wheel,
                    delta));
            }
        }

        return CallNextHookEx(_mouseHook, code, message, dataPointer);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr message, IntPtr dataPointer)
    {
        if (_disposed || code < 0)
        {
            return CallNextHookEx(_keyboardHook, code, message, dataPointer);
        }

        var messageValue = message.ToInt64();
        var isKeyDown = messageValue is WindowMessageKeyDown or
            WindowMessageSystemKeyDown;
        var isKeyUp = messageValue is WindowMessageKeyUp or
            WindowMessageSystemKeyUp;
        if (!isKeyDown && !isKeyUp)
        {
            return CallNextHookEx(_keyboardHook, code, message, dataPointer);
        }

        var data = Marshal.PtrToStructure<KeyboardLowLevelHookData>(dataPointer);
        var command = GetKeyboardCommand(data.VirtualKey);
        if (command is null)
        {
            return CallNextHookEx(_keyboardHook, code, message, dataPointer);
        }

        if (isKeyDown && _pressedCommandKeys.Add(data.VirtualKey))
        {
            RaiseInput(new ManualLongCaptureInput(command.Value));
        }
        else if (isKeyUp && _pressedCommandKeys.Remove(data.VirtualKey))
        {
            return new IntPtr(1);
        }

        return isKeyDown
            ? new IntPtr(1)
            : CallNextHookEx(_keyboardHook, code, message, dataPointer);
    }

    private void RaiseInput(ManualLongCaptureInput input)
    {
        try
        {
            InputReceived?.Invoke(this, input);
        }
        catch
        {
            // Hooks must return immediately. Session code observes errors outside the callback.
        }
    }

    private static void ReleaseHook(ref IntPtr hook, HookProcedure procedure)
    {
        var handle = hook;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (UnhookWindowsHookEx(handle) ||
            Marshal.GetLastWin32Error() == ErrorInvalidHookHandle)
        {
            hook = IntPtr.Zero;
            return;
        }

        // If Windows reports a real unhook failure, keep the delegate target rooted so a late
        // native callback cannot jump into a collected module method after hot unload.
        FailedUnhookRoots.Add(GCHandle.Alloc(procedure));
        hook = IntPtr.Zero;
    }

    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr dataPointer);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MouseLowLevelHookData
    {
        public readonly NativePoint Point;
        public readonly uint MouseData;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly UIntPtr ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardLowLevelHookData
    {
        public readonly uint VirtualKey;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly UIntPtr ExtraInformation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        HookProcedure procedure,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook,
        int code,
        IntPtr message,
        IntPtr dataPointer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
