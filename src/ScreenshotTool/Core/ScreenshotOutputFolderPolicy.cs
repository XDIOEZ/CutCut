using System.Globalization;

namespace ScreenshotTool.Core;

internal static class ScreenshotOutputFolderPolicy
{
    public const string DateFolderFormat = "yyyy-MM-dd";

    public static string Resolve(string parentFolder, bool organizeByDate, DateTime capturedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentFolder);

        return organizeByDate
            ? Path.Combine(
                parentFolder,
                capturedAt.ToString(DateFolderFormat, CultureInfo.InvariantCulture))
            : parentFolder;
    }
}
