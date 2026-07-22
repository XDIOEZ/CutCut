using System.Drawing.Drawing2D;

namespace ScreenshotTool.Presentation;

internal sealed class RecordingMouseClickIndicatorForm : Form
{
    private const int IndicatorDiameter = 38;
    private const int ReleaseVisibilityMilliseconds = 140;
    private const int ExtendedTransparentStyle = 0x20;
    private const int ExtendedToolWindowStyle = 0x80;
    private const int ExtendedNoActivateStyle = 0x08000000;

    private readonly System.Windows.Forms.Timer _hideTimer;

    public RecordingMouseClickIndicatorForm()
    {
        Text = "轻截录屏鼠标点击提示";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(245, 158, 11);
        Opacity = 0.42D;
        ClientSize = new Size(IndicatorDiameter, IndicatorDiameter);
        Region = CreateCircleRegion(ClientRectangle);
        _hideTimer = new System.Windows.Forms.Timer
        {
            Interval = ReleaseVisibilityMilliseconds
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
        Shown += (_, _) => WindowCaptureProtection.TryExclude(this);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= ExtendedTransparentStyle |
                ExtendedToolWindowStyle |
                ExtendedNoActivateStyle;
            return parameters;
        }
    }

    public bool IsPressed { get; private set; }

    public void BeginClick(Point screenLocation)
    {
        IsPressed = true;
        _hideTimer.Stop();
        MoveToPointer(screenLocation);
        if (!Visible)
        {
            Show();
        }
        else
        {
            BringToFront();
        }
        WindowCaptureProtection.TryExclude(this);
    }

    public void EndClick()
    {
        if (!IsPressed)
        {
            return;
        }

        IsPressed = false;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void MoveClick(Point screenLocation)
    {
        if (IsPressed)
        {
            MoveToPointer(screenLocation);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Color.FromArgb(146, 64, 14), 2F);
        e.Graphics.DrawEllipse(border, 1, 1, Width - 3, Height - 3);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            Region?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Region CreateCircleRegion(Rectangle bounds)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(bounds);
        return new Region(path);
    }

    private void MoveToPointer(Point screenLocation) => Location = new Point(
        screenLocation.X - Width / 2,
        screenLocation.Y - Height / 2);
}
