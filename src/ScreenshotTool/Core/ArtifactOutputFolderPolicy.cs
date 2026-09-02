using System.Globalization;

namespace ScreenshotTool.Core;

internal static class ArtifactOutputFolderPolicy
{
    public const string DateFolderFormat = "yyyy-MM-dd";

    // Resolves the shared screenshot and recording folder for the supplied local time.
    public static string Resolve(string parentFolder, bool organizeByDate, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentFolder);

        return organizeByDate
            ? Path.Combine(
                parentFolder,
                createdAt.ToString(DateFolderFormat, CultureInfo.InvariantCulture))
            : parentFolder;
    }
}
