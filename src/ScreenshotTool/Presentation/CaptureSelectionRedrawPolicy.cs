namespace ScreenshotTool.Presentation;

internal static class CaptureSelectionRedrawPolicy
{
    public static bool AllowsSelectionRedraw(bool isLongCaptureEditing) =>
        !isLongCaptureEditing;
}
