namespace ScreenshotTool.Editing;

internal static class TemporaryAnnotationMoveMode
{
    public static bool ShouldTryMove(EditorTool currentTool, bool moveModeActive) =>
        currentTool == EditorTool.None || moveModeActive;

    public static bool ShouldPreserveTool(EditorTool currentTool, bool moveModeActive) =>
        currentTool != EditorTool.None && moveModeActive;
}
