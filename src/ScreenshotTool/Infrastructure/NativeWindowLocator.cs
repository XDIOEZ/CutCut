using System.Runtime.InteropServices;
using System.Text;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class NativeWindowLocator : IWindowLocator
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const int GwlExstyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;

    private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "DV2ControlHost",
        "MultitaskingViewFrame"
    };

    public Rectangle? FindWindowAt(Point screenPoint)
    {
        Rectangle? result = null;
        var currentProcessId = (uint)Environment.ProcessId;

        EnumWindows((windowHandle, _) =>
        {
            if (!IsEligible(windowHandle, currentProcessId) ||
                !TryGetVisibleBounds(windowHandle, out var bounds) ||
                bounds.Width < 24 ||
                bounds.Height < 24 ||
                !bounds.Contains(screenPoint))
            {
                return true;
            }

            bounds.Intersect(SystemInformation.VirtualScreen);
            if (bounds.Width < 24 || bounds.Height < 24)
            {
                return true;
            }

            result = bounds;
            return false;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsEligible(IntPtr windowHandle, uint currentProcessId)
    {
        if (!IsWindowVisible(windowHandle) || IsIconic(windowHandle))
        {
            return false;
        }

        GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0 || processId == currentProcessId)
        {
            return false;
        }

        var extendedStyle = GetWindowLongPtr(windowHandle, GwlExstyle).ToInt64();
        if ((extendedStyle & (WsExToolWindow | WsExNoActivate)) != 0)
        {
            return false;
        }

        if (GetWindowTextLength(windowHandle) <= 0)
        {
            return false;
        }

        var className = new StringBuilder(128);
        GetClassName(windowHandle, className, className.Capacity);
        if (IgnoredWindowClasses.Contains(className.ToString()))
        {
            return false;
        }

        var cloaked = 0;
        return DwmGetWindowAttribute(
                   windowHandle,
                   DwmwaCloaked,
                   out cloaked,
                   Marshal.SizeOf<int>()) != 0 || cloaked == 0;
    }

    private static bool TryGetVisibleBounds(IntPtr windowHandle, out Rectangle bounds)
    {
        NativeRect nativeBounds;
        var result = DwmGetWindowAttribute(
            windowHandle,
            DwmwaExtendedFrameBounds,
            out nativeBounds,
            Marshal.SizeOf<NativeRect>());

        if (result != 0 && !GetWindowRect(windowHandle, out nativeBounds))
        {
            bounds = Rectangle.Empty;
            return false;
        }

        bounds = Rectangle.FromLTRB(
            nativeBounds.Left,
            nativeBounds.Top,
            nativeBounds.Right,
            nativeBounds.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maximumCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect bounds);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        out int value,
        int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        out NativeRect value,
        int valueSize);
}
