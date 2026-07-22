using System.Drawing.Drawing2D;
using ScreenshotTool.Core;

namespace ScreenshotTool.Presentation;

internal static class RecordingRegionIndicator
{
    internal static readonly Color BorderColor = Color.FromArgb(239, 68, 68);

    public static void Draw(
        Graphics graphics,
        Rectangle bounds,
        RecordingRegionIndicatorStyle style)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (style == RecordingRegionIndicatorStyle.None || bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        using var pen = new Pen(BorderColor, 2F)
        {
            Alignment = PenAlignment.Inset,
            DashStyle = style == RecordingRegionIndicatorStyle.Solid
                ? DashStyle.Solid
                : DashStyle.Dash
        };
        graphics.DrawRectangle(
            pen,
            bounds.Left,
            bounds.Top,
            bounds.Width - 1,
            bounds.Height - 1);
    }
}
