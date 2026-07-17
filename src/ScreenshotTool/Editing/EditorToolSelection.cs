namespace ScreenshotTool.Editing;

internal static class EditorToolSelection
{
    public static EditorTool Toggle(EditorTool current, EditorTool requested)
    {
        if (requested == EditorTool.None)
        {
            return EditorTool.None;
        }

        return current == requested ? EditorTool.None : requested;
    }
}
