using System.Runtime.InteropServices;

namespace ScreenshotTool.LongCapture;

internal sealed class WindowsScrollDriver : ILongCaptureScrollDriver
{
    private const int WheelDelta = 120;
    private const uint InputMouse = 0;
    private const uint MouseEventWheel = 0x0800;
    private const uint GetAncestorRoot = 2;
    private const uint WindowMessageMouseWheel = 0x020A;
    private const uint GetMouseWheelRouting = 0x201C;
    private const uint MouseWheelRoutingMousePosition = 2;
    private const int VirtualKeyEscape = 0x1B;
    private readonly Point _target;
    private readonly Point _originalCursor;
    private readonly bool _moveCursor;
    private readonly bool _activateTarget;
    private readonly bool _restoreCursor;
    private IntPtr _targetWindow;
    private IntPtr _rootWindow;
    private ScrollTargetPreparationResult? _preparation;
    private bool _cursorMoved;
    private bool _disposed;

    public WindowsScrollDriver(Rectangle selectionScreenBounds)
        : this(
            selectionScreenBounds,
            moveCursor: true,
            activateTarget: true,
            restoreCursor: true)
    {
    }

    internal WindowsScrollDriver(
        Rectangle selectionScreenBounds,
        bool moveCursor,
        bool activateTarget,
        bool restoreCursor = true)
    {
        _target = new Point(
            selectionScreenBounds.Left + selectionScreenBounds.Width / 2,
            selectionScreenBounds.Top + selectionScreenBounds.Height / 2);
        _originalCursor = Cursor.Position;
        _moveCursor = moveCursor;
        _activateTarget = activateTarget;
        _restoreCursor = restoreCursor;
    }

    public bool IsUserCancellationRequested =>
        (GetAsyncKeyState(VirtualKeyEscape) & 0x8000) != 0;

    public async ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_preparation is not null)
        {
            return _preparation;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_moveCursor)
        {
            Cursor.Position = _target;
            _cursorMoved = true;
        }
        _targetWindow = WindowFromPoint(_target);
        if (_targetWindow == IntPtr.Zero)
        {
            _preparation = new ScrollTargetPreparationResult(
                false,
                ScrollInputMode.SystemInput,
                false,
                "选区中心没有可接收滚轮的窗口。请让选区中心落在可滚动正文或列表上。");
            return _preparation;
        }

        _rootWindow = GetAncestor(_targetWindow, GetAncestorRoot);
        if (_rootWindow == IntPtr.Zero)
        {
            _rootWindow = _targetWindow;
        }

        var foregroundRequested =
            _activateTarget && SetForegroundWindow(_rootWindow);
        var foregroundConfirmed = GetForegroundWindow() == _rootWindow;
        for (var attempt = 0;
             _activateTarget && attempt < 6 && !foregroundConfirmed;
             attempt++)
        {
            await Task.Delay(25, cancellationToken);
            foregroundConfirmed = GetForegroundWindow() == _rootWindow;
        }

        var routingKnown = SystemParametersInfo(
            GetMouseWheelRouting,
            0,
            out var wheelRouting,
            0);
        var routesByMousePosition =
            routingKnown && wheelRouting == MouseWheelRoutingMousePosition;
        var preferredInput = _activateTarget &&
                             (foregroundConfirmed || routesByMousePosition)
            ? ScrollInputMode.SystemInput
            : ScrollInputMode.TargetedWindowMessage;
        var routingDescription = routingKnown
            ? wheelRouting switch
            {
                0 => "焦点窗口",
                1 => "混合路由",
                MouseWheelRoutingMousePosition => "鼠标所在窗口",
                _ => $"未知值 {wheelRouting}"
            }
            : $"读取失败（Win32 {Marshal.GetLastWin32Error()}）";

        _preparation = new ScrollTargetPreparationResult(
            true,
            preferredInput,
            foregroundConfirmed,
            $"目标控件 0x{_targetWindow.ToInt64():X}，前台切换{(_activateTarget ? (foregroundRequested ? "请求成功" : "请求被系统拒绝") : "未请求")}，" +
            $"前台状态{(foregroundConfirmed ? "已确认" : "未确认")}，滚轮路由：{routingDescription}。");
        return _preparation;
    }

    public async ValueTask<ScrollInputResult> ScrollDownAsync(
        ScrollInputMode mode,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var preparation = await PrepareTargetAsync(cancellationToken);
        if (!preparation.Succeeded)
        {
            return new ScrollInputResult(
                false,
                mode,
                preparation.Diagnostic);
        }

        if (!IsWindow(_targetWindow) || !IsWindow(_rootWindow))
        {
            return new ScrollInputResult(
                false,
                mode,
                "长截图开始后，选区中心对应的目标窗口已关闭或失效。为避免滚动到其他窗口，捕获已安全停止。");
        }

        if (mode == ScrollInputMode.TargetedWindowMessage)
        {
            return PostTargetedWheel(
                _targetWindow,
                $"定向滚轮消息已发送到选区中心控件。{preparation.Diagnostic}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var input = new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    // Begin with one detent. A larger fixed wheel delta can move
                    // short viewports farther than the matcher can prove, leaving
                    // no overlap at all. Accuracy is more important than frame count.
                    MouseData = unchecked((uint)-WheelDelta),
                    Flags = MouseEventWheel
                }
            }
        };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) == 1)
        {
            return new ScrollInputResult(
                true,
                ScrollInputMode.SystemInput,
                $"系统滚轮输入已写入输入流；后续仍会用画面位移确认目标是否真正滚动。{preparation.Diagnostic}");
        }

        var error = Marshal.GetLastWin32Error();
        return PostTargetedWheel(
            _targetWindow,
            $"系统滚轮输入失败（Win32 {error}），已自动改用定向滚轮消息。");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_restoreCursor && _cursorMoved && Cursor.Position == _target)
        {
            Cursor.Position = _originalCursor;
        }
    }

    private ScrollInputResult PostTargetedWheel(IntPtr targetWindow, string successDiagnostic)
    {
        var wheelParameter = unchecked((nint)((uint)(ushort)(short)-WheelDelta << 16));
        var positionParameter = unchecked((nint)(
            (uint)(ushort)_target.X |
            ((uint)(ushort)_target.Y << 16)));
        if (PostMessage(
                targetWindow,
                WindowMessageMouseWheel,
                wheelParameter,
                positionParameter))
        {
            return new ScrollInputResult(
                true,
                ScrollInputMode.TargetedWindowMessage,
                successDiagnostic);
        }

        var error = Marshal.GetLastWin32Error();
        return new ScrollInputResult(
            false,
            ScrollInputMode.TargetedWindowMessage,
            $"无法向选区中心控件发送滚轮消息（Win32 {error}）。");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        [In] Input[] inputs,
        int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        out uint value,
        uint updateFlags);
}
