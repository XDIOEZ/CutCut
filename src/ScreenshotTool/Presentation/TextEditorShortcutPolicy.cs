namespace ScreenshotTool.Presentation;

internal enum TextEditorShortcutAction
{
    None,
    SaveScreenshot
}

internal static class TextEditorShortcutPolicy
{
    public static TextEditorShortcutAction Resolve(Keys keyCode, bool controlPressed) =>
        controlPressed && keyCode == Keys.S
            ? TextEditorShortcutAction.SaveScreenshot
            : TextEditorShortcutAction.None;
}
