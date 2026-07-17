namespace ScreenshotTool.Editing;

internal static class EditorIdleCursorPolicy
{
    public static bool UsesDrawingCursor(
        bool hasSelection,
        bool pointerInsideSelection,
        EditorTool activeTool) =>
        hasSelection && pointerInsideSelection && activeTool != EditorTool.None;
}
