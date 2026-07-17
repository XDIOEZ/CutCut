using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation.Pages;

internal sealed class FeatureGuidePage : UserControl
{
    private readonly FlowLayoutPanel _layout;

    public FeatureGuidePage(
        string eyebrow,
        string headline,
        string description,
        string shortcut,
        IReadOnlyList<(string Title, string Description)> items)
    {
        BackColor = AppTheme.Canvas;
        AutoScroll = true;

        _layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 0, 10, 12),
            BackColor = AppTheme.Canvas
        };
        Controls.Add(_layout);

        var hero = new Panel
        {
            Height = 158,
            BackColor = AppTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 18),
            Padding = new Padding(24, 20, 24, 18)
        };
        var eyebrowLabel = new Label
        {
            Text = eyebrow,
            AutoSize = true,
            Font = AppTheme.CreateFont(8.5F, FontStyle.Bold),
            ForeColor = AppTheme.Accent,
            Location = new Point(24, 19)
        };
        var headlineLabel = new Label
        {
            Text = headline,
            AutoSize = true,
            Font = AppTheme.CreateFont(15F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Location = new Point(24, 48)
        };
        var descriptionLabel = AppTheme.CreateBodyLabel(description, 610);
        descriptionLabel.Location = new Point(26, 84);
        var shortcutLabel = new Label
        {
            Text = shortcut,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Padding = new Padding(12, 6, 12, 6),
            BackColor = Color.FromArgb(239, 246, 255),
            ForeColor = AppTheme.Accent,
            Font = new Font("Consolas", 10F, FontStyle.Bold),
            Location = new Point(600, 22)
        };
        hero.Resize += (_, _) => shortcutLabel.Left = Math.Max(350, hero.ClientSize.Width - shortcutLabel.Width - 24);
        hero.Controls.AddRange([eyebrowLabel, headlineLabel, descriptionLabel, shortcutLabel]);
        _layout.Controls.Add(hero);

        var sectionTitle = new Label
        {
            Text = "功能说明",
            AutoSize = true,
            Font = AppTheme.CreateFont(11F, FontStyle.Bold),
            ForeColor = AppTheme.Text,
            Margin = new Padding(2, 0, 0, 10)
        };
        _layout.Controls.Add(sectionTitle);

        foreach (var (title, itemDescription) in items)
        {
            var card = new Panel
            {
                Height = 76,
                BackColor = AppTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(18, 13, 18, 10)
            };
            var titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Font = AppTheme.CreateFont(9.5F, FontStyle.Bold),
                ForeColor = AppTheme.Text,
                Location = new Point(18, 12)
            };
            var bodyLabel = AppTheme.CreateBodyLabel(itemDescription, 650);
            bodyLabel.Location = new Point(20, 39);
            card.Controls.AddRange([titleLabel, bodyLabel]);
            _layout.Controls.Add(card);
        }

        Resize += (_, _) => ResizeCards();
        ResizeCards();
    }

    private void ResizeCards()
    {
        var width = Math.Max(420, ClientSize.Width - 24);
        foreach (Control control in _layout.Controls)
        {
            control.Width = width;
        }
    }
}
