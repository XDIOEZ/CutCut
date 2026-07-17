namespace ScreenshotTool.Presentation;

internal enum CaptureEscapeAction
{
    CompleteTextEditing,
    ClearElementSelection,
    CancelDrawingTool,
    FinishSelectionAdjustment,
    CloseCapture
}

internal static class CaptureEscapePolicy
{
    public static CaptureEscapeAction Resolve(
        bool isTextEditing,
        bool hasElementSelection,
        bool hasDrawingTool,
        bool isAdjustingSelection)
    {
        if (isTextEditing)
        {
            return CaptureEscapeAction.CompleteTextEditing;
        }

        if (hasElementSelection)
        {
            return CaptureEscapeAction.ClearElementSelection;
        }

        if (hasDrawingTool)
        {
            return CaptureEscapeAction.CancelDrawingTool;
        }

        return isAdjustingSelection
            ? CaptureEscapeAction.FinishSelectionAdjustment
            : CaptureEscapeAction.CloseCapture;
    }
}
