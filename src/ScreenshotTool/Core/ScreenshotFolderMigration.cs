namespace ScreenshotTool.Core;

internal sealed record ScreenshotFolderMigrationResult(int MovedCount, IReadOnlyList<string> FailedFiles);

internal static class ScreenshotFolderMigration
{
    public static IReadOnlyList<string> FindImages(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImage)
            .ToArray();
    }

    public static ScreenshotFolderMigrationResult MoveImages(
        IEnumerable<string> imagePaths,
        string destinationFolder)
    {
        var sources = imagePaths.ToArray();
        try
        {
            Directory.CreateDirectory(destinationFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new ScreenshotFolderMigrationResult(
                0,
                sources.Select(path => Path.GetFileName(path) ?? path).ToArray());
        }

        var movedCount = 0;
        var failedFiles = new List<string>();
        foreach (var sourcePath in sources)
        {
            try
            {
                var destinationPath = GetAvailableDestinationPath(
                    destinationFolder,
                    Path.GetFileName(sourcePath));
                File.Move(sourcePath, destinationPath);
                movedCount++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                failedFiles.Add(Path.GetFileName(sourcePath) ?? sourcePath);
            }
        }

        return new ScreenshotFolderMigrationResult(movedCount, failedFiles);
    }

    private static string GetAvailableDestinationPath(string destinationFolder, string fileName)
    {
        var destinationPath = Path.Combine(destinationFolder, fileName);
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 1;
        do
        {
            destinationPath = Path.Combine(destinationFolder, $"{name}_{suffix++}{extension}");
        }
        while (File.Exists(destinationPath));

        return destinationPath;
    }

    private static bool IsSupportedImage(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";
}
