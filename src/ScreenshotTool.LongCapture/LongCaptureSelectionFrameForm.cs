using System.Runtime.InteropServices;

namespace ScreenshotTool.LongCapture;

/// <summary>
/// Shows the original capture bounds while leaving the whole selected interior physically absent
/// from the window region, so wheel and pointer input reach the target application unchanged.
/// </summary>
internal sealed class LongCaptureSelectionFrameForm : Form
{
    private const int BorderThickness = 3;
    private const int WindowMessageNonClientHitTest = 0x0084;
    private const int HitTestTransparent = -1;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    public LongCaptureSelectionFrameForm(Rectangle selectionScreenBounds)
    {
        if (selectionScreenBounds.Width <= 0 || selectionScreenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionScreenBounds));
        }

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(0, 174, 255);
        FormBorderStyle = FormBorderStyle.None;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        var outerBounds = selectionScreenBounds;
        outerBounds.Inflate(BorderThickness, BorderThickness);
        Bounds = outerBounds;
        var windowRegion = new Region(ClientRectangle);
        windowRegion.Exclude(new Rectangle(
            BorderThickness,
            BorderThickness,
            selectionScreenBounds.Width,
            selectionScreenBounds.Height));
        Region = windowRegion;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void ShowFrame()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(LongCaptureSelectionFrameForm));
        }

        if (!Visible)
        {
            Show();
        }
        SetWindowPos(
            Handle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WindowMessageNonClientHitTest)
        {
            message.Result = new IntPtr(HitTestTransparent);
            return;
        }

        base.WndProc(ref message);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
