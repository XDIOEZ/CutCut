namespace ScreenshotTool.Editing;

internal sealed record CaptureEditorToolDefinition(
    EditorTool Tool,
    string Text,
    string ToolTip,
    int Width);

internal static class CaptureEditorToolCatalog
{
    public static IReadOnlyList<CaptureEditorToolDefinition> Tools { get; } =
    [
        new(EditorTool.Rectangle, "矩形", "绘制矩形（快捷键 1）", 48),
        new(EditorTool.Ellipse, "椭圆", "绘制椭圆（快捷键 2）", 48),
        new(EditorTool.Arrow, "箭头", "绘制箭头（快捷键 3）", 48),
        new(EditorTool.Pen, "画笔", "自由画笔（快捷键 4）", 48),
        new(EditorTool.Text, "文字", "添加文字（快捷键 5）", 48),
        new(EditorTool.Mosaic, "马赛克", "涂抹马赛克（快捷键 6）", 58)
    ];

    public static IReadOnlyList<Color> Palette { get; } =
    [
        Color.FromArgb(239, 68, 68),
        Color.FromArgb(250, 204, 21),
        Color.FromArgb(34, 197, 94),
        Color.FromArgb(59, 130, 246),
        Color.White
    ];
}
