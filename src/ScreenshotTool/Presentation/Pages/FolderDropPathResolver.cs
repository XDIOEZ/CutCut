namespace ScreenshotTool.Presentation.Pages;

internal static class FolderDropPathResolver
{
    public static bool TryResolve(IEnumerable<string>? droppedPaths, out string folderPath)
    {
        folderPath = string.Empty;
        if (droppedPaths is null)
        {
            return false;
        }

        var candidates = droppedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Trim('"'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        if (candidates.Length != 1)
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(candidates[0]);
            if (!Directory.Exists(fullPath))
            {
                return false;
            }

            folderPath = fullPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
