namespace ScreenshotTool.Editing;

internal static class TemporaryAnnotationMoveMode
{
    public static bool ShouldTryMove(EditorTool currentTool, bool altPressed) =>
        currentTool == EditorTool.None || altPressed;

    public static bool ShouldPreserveTool(EditorTool currentTool, bool altPressed) =>
        currentTool != EditorTool.None && altPressed;
}
