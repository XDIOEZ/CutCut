namespace ScreenshotTool.Presentation;

internal sealed class RecordingSelectionMarqueeForm : Form
{
    private const int ExtendedTransparentStyle = 0x20;
    private const int ExtendedToolWindowStyle = 0x80;
    private const int ExtendedNoActivateStyle = 0x08000000;
    private const double ScreenshotMarqueeOpacity = 42D / 255D;

    public RecordingSelectionMarqueeForm()
    {
        Text = "轻截录屏元素框选填充";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(14, 165, 233);
        Opacity = ScreenshotMarqueeOpacity;
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

    public void ShowMarquee(Rectangle screenBounds)
    {
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            HideMarquee();
            return;
        }

        Bounds = screenBounds;
        if (!Visible)
        {
            Show();
        }
        WindowCaptureProtection.TryExclude(this);
    }

    public void HideMarquee()
    {
        if (Visible)
        {
            Hide();
        }
    }
}
