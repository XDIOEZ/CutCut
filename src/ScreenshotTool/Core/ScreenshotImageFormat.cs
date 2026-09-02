namespace ScreenshotTool.Core;

internal enum ScreenshotImageFormat
{
    Png,
    Jpeg
}

internal static class ScreenshotImageFormatPolicy
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".png", ".jpg", ".jpeg"],
        StringComparer.OrdinalIgnoreCase);

    // Falls back to lossless PNG when a settings file contains an unsupported value.
    public static ScreenshotImageFormat Normalize(ScreenshotImageFormat format) =>
        Enum.IsDefined(format) ? format : ScreenshotImageFormat.Png;

    // Returns the stable file extension used when saving the selected image format.
    public static string GetFileExtension(ScreenshotImageFormat format) =>
        Normalize(format) == ScreenshotImageFormat.Jpeg ? ".jpg" : ".png";

    // Identifies files produced by one of the user-selectable screenshot formats.
    public static bool IsSupportedFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        SupportedExtensions.Contains(Path.GetExtension(fileName));
}
