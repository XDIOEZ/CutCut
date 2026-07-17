namespace ScreenshotTool.Presentation.Theme;

internal static class AppTheme
{
    public static readonly Color Sidebar = Color.FromArgb(15, 23, 42);
    public static readonly Color SidebarHover = Color.FromArgb(30, 41, 59);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Canvas = Color.FromArgb(244, 247, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(220, 226, 236);
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);

    public static Font CreateFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", size, style);

    public static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 38,
            Padding = new Padding(16, 0, 16, 0),
            BackColor = primary ? Accent : Surface,
            ForeColor = primary ? Color.White : Text,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = CreateFont(9F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Color.FromArgb(241, 245, 249);
        return button;
    }

    public static Label CreateBodyLabel(string text, int maximumWidth = 650) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(maximumWidth, 0),
        Font = CreateFont(9.5F),
        ForeColor = MutedText
    };
}
