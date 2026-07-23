namespace ScreenshotTool.Presentation.Pages;

internal enum ScreenshotGallerySortMode
{
    SavedTimeDescending,
    SavedTimeAscending,
    NameAscending,
    NameDescending
}

internal sealed record ScreenshotGalleryEntry(
    string FullName,
    string Name,
    DateTime LastWriteTime,
    DateTime LastWriteTimeUtc,
    long Length);

internal sealed record ScreenshotGalleryQueryResult(
    IReadOnlyList<ScreenshotGalleryEntry> Entries,
    int MatchCount);

internal static class ScreenshotGalleryQuery
{
    private static readonly StringComparer NameComparer =
        StringComparer.CurrentCultureIgnoreCase;

    public static ScreenshotGalleryQueryResult Apply(
        IEnumerable<ScreenshotGalleryEntry> entries,
        string? searchText,
        ScreenshotGallerySortMode sortMode,
        int maximumCount)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (maximumCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                maximumCount,
                "最大显示数量必须大于零。");
        }

        var normalizedSearchText = searchText?.Trim() ?? string.Empty;
        var matches = entries
            .Where(entry =>
                normalizedSearchText.Length == 0 ||
                entry.Name.Contains(
                    normalizedSearchText,
                    StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
        var ordered = sortMode switch
        {
            ScreenshotGallerySortMode.SavedTimeAscending => matches
                .OrderBy(entry => entry.LastWriteTimeUtc)
                .ThenBy(entry => entry.Name, NameComparer),
            ScreenshotGallerySortMode.NameAscending => matches
                .OrderBy(entry => entry.Name, NameComparer)
                .ThenByDescending(entry => entry.LastWriteTimeUtc),
            ScreenshotGallerySortMode.NameDescending => matches
                .OrderByDescending(entry => entry.Name, NameComparer)
                .ThenByDescending(entry => entry.LastWriteTimeUtc),
            _ => matches
                .OrderByDescending(entry => entry.LastWriteTimeUtc)
                .ThenBy(entry => entry.Name, NameComparer)
        };
        return new ScreenshotGalleryQueryResult(
            ordered.Take(maximumCount).ToArray(),
            matches.Length);
    }
}
