namespace ScreenshotTool.Presentation;

internal static class SelectionMoveInvalidation
{
    public static IReadOnlyList<Rectangle> GetMovedVisualAreas(
        Rectangle previousSelection,
        Rectangle currentSelection,
        IEnumerable<Rectangle> previousVisualAreas,
        Point offset)
    {
        var dirtyAreas = new List<Rectangle>();
        foreach (var previousArea in previousVisualAreas)
        {
            AddClipped(dirtyAreas, previousArea, previousSelection);

            var currentArea = previousArea;
            currentArea.Offset(offset);
            AddClipped(dirtyAreas, currentArea, currentSelection);
        }
        return dirtyAreas;
    }

    private static void AddClipped(
        ICollection<Rectangle> dirtyAreas,
        Rectangle visualArea,
        Rectangle selection)
    {
        var clipped = Rectangle.Intersect(visualArea, selection);
        if (!clipped.IsEmpty)
        {
            dirtyAreas.Add(clipped);
        }
    }
}
