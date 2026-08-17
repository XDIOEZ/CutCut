namespace ScreenshotTool.Presentation;

internal static class CaptureBackgroundRefreshPolicy
{
    public static bool IsShortcut(
        Keys keyCode,
        bool controlPressed,
        bool altPressed,
        bool shiftPressed) =>
        keyCode == Keys.R &&
        controlPressed &&
        !altPressed &&
        !shiftPressed;

    public static bool CanRefresh(
        bool hasSelection,
        bool hasLiveScreenBackground,
        bool overlayAvailable,
        bool interactionIdle,
        bool refreshInProgress,
        Rectangle selection,
        Point pointer) =>
        hasSelection &&
        hasLiveScreenBackground &&
        overlayAvailable &&
        interactionIdle &&
        !refreshInProgress &&
        !selection.IsEmpty &&
        selection.Contains(pointer);
}
