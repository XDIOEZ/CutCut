namespace ScreenshotTool.Presentation;

internal enum TextEditorEnterAction
{
    Commit,
    InsertLineBreak
}

internal static class TextEditorEnterPolicy
{
    public static TextEditorEnterAction Resolve(bool controlPressed) => controlPressed
        ? TextEditorEnterAction.InsertLineBreak
        : TextEditorEnterAction.Commit;
}
