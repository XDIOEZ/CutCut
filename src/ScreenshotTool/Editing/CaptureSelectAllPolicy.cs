namespace ScreenshotTool.Editing;

internal enum CaptureSelectAllAction
{
    SelectEditingElements,
    ExpandCaptureSelection
}

internal static class CaptureSelectAllPolicy
{
    public static CaptureSelectAllAction Resolve(
        int editingElementCount,
        bool allEditingElementsSelected) =>
        editingElementCount > 0 && !allEditingElementsSelected
            ? CaptureSelectAllAction.SelectEditingElements
            : CaptureSelectAllAction.ExpandCaptureSelection;

    public static Rectangle ResolveSelectionTarget(
        Rectangle currentSelection,
        Rectangle virtualDesktopScreenBounds,
        Rectangle currentDisplayScreenBounds)
    {
        if (virtualDesktopScreenBounds.Width <= 0 || virtualDesktopScreenBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(virtualDesktopScreenBounds),
                "虚拟桌面范围必须有效。");
        }

        var virtualDesktopSelection = new Rectangle(
            Point.Empty,
            virtualDesktopScreenBounds.Size);
        if (currentSelection == virtualDesktopSelection)
        {
            return virtualDesktopSelection;
        }

        var clippedDisplay = Rectangle.Intersect(
            virtualDesktopScreenBounds,
            currentDisplayScreenBounds);
        if (clippedDisplay.IsEmpty)
        {
            return virtualDesktopSelection;
        }

        var currentDisplaySelection = new Rectangle(
            clippedDisplay.X - virtualDesktopScreenBounds.X,
            clippedDisplay.Y - virtualDesktopScreenBounds.Y,
            clippedDisplay.Width,
            clippedDisplay.Height);
        return currentSelection == currentDisplaySelection
            ? virtualDesktopSelection
            : currentDisplaySelection;
    }
}
