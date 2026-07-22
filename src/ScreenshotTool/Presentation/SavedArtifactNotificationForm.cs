using System.Drawing.Drawing2D;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation;

internal sealed class SavedArtifactNotificationForm : Form
{
    private const int AutoCloseMilliseconds = 6000;
    private readonly System.Windows.Forms.Timer _autoCloseTimer = new()
    {
        Interval = AutoCloseMilliseconds
    };
    private bool _openRequested;

    public SavedArtifactNotificationForm(string artifactPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArtifactPath = Path.GetFullPath(artifactPath);

        Text = GetTitle(ArtifactPath);
        AccessibleName = $"{Text}，点击打开文件所在位置";
        AccessibleRole = AccessibleRole.PushButton;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppTheme.Surface;
        ClientSize = new Size(390, 104);
        Cursor = Cursors.Hand;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        _autoCloseTimer.Tick += (_, _) => Close();
    }

    public event EventHandler<string>? OpenRequested;

    public string ArtifactPath { get; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int csDropShadow = 0x00020000;
            const int wsExToolWindow = 0x00000080;
            var parameters = base.CreateParams;
            parameters.ClassStyle |= csDropShadow;
            parameters.ExStyle |= wsExToolWindow;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PositionAtWorkingAreaBottomRight();
        _autoCloseTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var borderPen = new Pen(AppTheme.Border);
        e.Graphics.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

        using var accentBrush = new SolidBrush(AppTheme.Success);
        e.Graphics.FillRectangle(accentBrush, 0, 0, 6, ClientSize.Height);
        e.Graphics.FillEllipse(accentBrush, 22, 24, 34, 34);

        using var checkPen = new Pen(Color.White, 2.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        e.Graphics.DrawLines(checkPen, new Point[]
        {
            new Point(31, 41),
            new Point(37, 47),
            new Point(48, 34)
        });

        using var titleFont = AppTheme.CreateFont(10.5F, FontStyle.Bold);
        using var fileFont = AppTheme.CreateFont(9F);
        using var hintFont = AppTheme.CreateFont(8.5F);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            titleFont,
            new Rectangle(70, 16, ClientSize.Width - 88, 25),
            AppTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(
            e.Graphics,
            Path.GetFileName(ArtifactPath),
            fileFont,
            new Rectangle(70, 44, ClientSize.Width - 88, 22),
            AppTheme.MutedText,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(
            e.Graphics,
            "点击打开文件所在位置并选中该文件",
            hintFont,
            new Rectangle(70, 72, ClientSize.Width - 88, 20),
            AppTheme.Accent,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            RequestOpen();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    internal static string GetTitle(string path) =>
        string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase)
            ? "录屏保存成功"
            : "截图保存成功";

    internal void RequestOpen()
    {
        if (_openRequested)
        {
            return;
        }

        _openRequested = true;
        OpenRequested?.Invoke(this, ArtifactPath);
        Close();
    }

    private void PositionAtWorkingAreaBottomRight()
    {
        const int margin = 18;
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            workingArea.Right - Width - margin,
            workingArea.Bottom - Height - margin);
    }
}
