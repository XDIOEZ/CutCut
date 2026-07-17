namespace ScreenshotTool.Editing;

internal enum CaptureSelectAllAction
{
    SelectEditingElements,
    ExpandSelectionToFullScreen
}

internal static class CaptureSelectAllPolicy
{
    public static CaptureSelectAllAction Resolve(
        int editingElementCount,
        bool allEditingElementsSelected) =>
        editingElementCount > 0 && !allEditingElementsSelected
            ? CaptureSelectAllAction.SelectEditingElements
            : CaptureSelectAllAction.ExpandSelectionToFullScreen;
}
