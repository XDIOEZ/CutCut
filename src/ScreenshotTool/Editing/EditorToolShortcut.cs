namespace ScreenshotTool.Editing;

internal static class EditorToolShortcut
{
    public static EditorTool Resolve(Keys keyCode) => keyCode switch
    {
        Keys.D1 or Keys.NumPad1 => EditorTool.Rectangle,
        Keys.D2 or Keys.NumPad2 => EditorTool.Ellipse,
        Keys.D3 or Keys.NumPad3 => EditorTool.Arrow,
        Keys.D4 or Keys.NumPad4 => EditorTool.Pen,
        Keys.D5 or Keys.NumPad5 => EditorTool.Text,
        Keys.D6 or Keys.NumPad6 => EditorTool.Mosaic,
        _ => EditorTool.None
    };
}
