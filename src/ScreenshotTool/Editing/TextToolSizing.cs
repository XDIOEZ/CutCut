using ScreenshotTool.Core;

namespace ScreenshotTool.Editing;

internal static class TextToolSizing
{
    public const float DefaultFontSize = 18F;
    public const float MinimumFontSize = 8F;

    public static float CalculateVisualFontSize(int toolWidth)
    {
        var normalizedWidth = Math.Clamp(
            toolWidth,
            ToolWidthRange.SupportedMinimum,
            ToolWidthRange.SupportedMaximum);
        return Math.Max(
            MinimumFontSize,
            DefaultFontSize * normalizedWidth / ToolWidthRange.PreferredDefault);
    }
}
