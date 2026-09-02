namespace ScreenshotTool.Favorites;

internal static class FavoritesPreferences
{
    public const string FolderId = "screenshot-tool.favorites.folder";

    // Returns the user-scoped default without assuming a fixed drive or profile name.
    public static string GetDefaultFolder()
    {
        var picturesFolder = Environment.GetFolderPath(
            Environment.SpecialFolder.MyPictures);
        var parentFolder = string.IsNullOrWhiteSpace(picturesFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : picturesFolder;
        if (string.IsNullOrWhiteSpace(parentFolder))
        {
            parentFolder = AppContext.BaseDirectory;
        }

        return Path.Combine(parentFolder, "轻截收藏夹");
    }

    // Normalizes a path entered by the user and rejects invalid values for visible feedback.
    public static string NormalizeFolder(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(value.Trim()));
    }

    // Falls back to the default only when a persisted preference is no longer a valid path.
    public static string ResolveStoredFolder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GetDefaultFolder();
        }

        try
        {
            return NormalizeFolder(value);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return GetDefaultFolder();
        }
    }
}
