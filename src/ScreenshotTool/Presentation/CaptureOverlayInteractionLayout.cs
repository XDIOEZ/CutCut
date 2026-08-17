namespace ScreenshotTool.Presentation;

internal static class CaptureOverlayInteractionLayout
{
    public static IReadOnlyList<Rectangle> GetInteractiveAreas(
        Rectangle clientBounds,
        Rectangle selection,
        int selectionMargin,
        Rectangle toolbarBounds,
        bool toolbarVisible,
        Rectangle textEditorBounds,
        Rectangle sizeBadgeBounds)
    {
        if (clientBounds.IsEmpty || selection.IsEmpty)
        {
            return [];
        }

        var areas = new List<Rectangle>();
        AddClipped(areas, Inflate(selection, selectionMargin), clientBounds);
        if (toolbarVisible)
        {
            AddClipped(areas, Inflate(toolbarBounds, 2), clientBounds);
        }
        AddClipped(areas, textEditorBounds, clientBounds);
        AddClipped(areas, sizeBadgeBounds, clientBounds);
        return areas;
    }

    public static bool Contains(IReadOnlyList<Rectangle> areas, Point point) =>
        areas.Any(area => area.Contains(point));

    public static bool ShouldStartSelectionRedraw(
        bool hasSelection,
        bool selectionRedrawAllowed,
        bool controlPressed,
        bool leftButtonDown,
        Rectangle clientBounds,
        IReadOnlyList<Rectangle> interactiveAreas,
        Point pointer) =>
        hasSelection &&
        selectionRedrawAllowed &&
        controlPressed &&
        leftButtonDown &&
        clientBounds.Contains(pointer) &&
        !Contains(interactiveAreas, pointer);

    private static Rectangle Inflate(Rectangle bounds, int margin)
    {
        bounds.Inflate(Math.Max(0, margin), Math.Max(0, margin));
        return bounds;
    }

    private static void AddClipped(
        ICollection<Rectangle> areas,
        Rectangle bounds,
        Rectangle clientBounds)
    {
        bounds.Intersect(clientBounds);
        if (!bounds.IsEmpty)
        {
            areas.Add(bounds);
        }
    }
}
