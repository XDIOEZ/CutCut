using ScreenshotTool.Contracts;

namespace ScreenshotTool.Core;

internal sealed class UserPreferences
{
    public StickerSelectionMoveMode StickerSelectionMoveMode { get; set; } =
        StickerSelectionMoveMode.FollowSelection;

    public int MinimumToolWidth { get; set; } = 2;

    public int MaximumToolWidth { get; set; } = 8;

    public int LastToolWidth { get; set; } = ToolWidthRange.PreferredDefault;

    public int AnnotationRotationStepDegrees { get; set; } =
        AnnotationRotationStep.DefaultDegrees;

    public DrawingCursorShape DrawingCursorShape { get; set; } =
        DrawingCursorShape.Circle;

    public bool AnnotationSnappingEnabled { get; set; } =
        AnnotationLayoutOptions.DefaultSnappingEnabled;

    public int AnnotationSnapThresholdPixels { get; set; } =
        AnnotationLayoutOptions.DefaultSnapThresholdPixels;

    public int CtrlDragStepPixels { get; set; } =
        AnnotationLayoutOptions.DefaultCtrlDragStepPixels;

    public RecordingRegionIndicatorStyle RecordingRegionIndicatorStyle { get; set; } =
        RecordingRegionIndicatorStyle.Dashed;

    public bool ScreenRecordingCaptureSystemAudio { get; set; } =
        true;

    public bool ScreenRecordingCaptureMicrophone { get; set; } =
        true;

    public bool ScreenRecordingShowMouseClickIndicator { get; set; } =
        true;

    public int ScreenRecordingFramesPerSecond { get; set; } =
        30;

    public int ScreenRecordingVideoBitrate { get; set; } =
        8_000_000;

    public bool LongCaptureSafetyChecksEnabled { get; set; }

    public Dictionary<string, bool> ModuleBooleanPreferences { get; set; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, int> ModuleIntegerPreferences { get; set; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, string> ModuleStringPreferences { get; set; } =
        new(StringComparer.Ordinal);

    public ScreenshotFileNameMode ScreenshotFileNameMode { get; set; } =
        ScreenshotFileNameMode.DateTime;

    public bool DismissSaveNotificationBeforeCapture { get; set; } = true;

    public bool HideMainWindowDuringCapture { get; set; }

    public DrawingToolCoefficients DrawingToolCoefficients { get; set; } = new();

    public ToolWidthRange GetToolWidthRange() =>
        ToolWidthRange.Create(MinimumToolWidth, MaximumToolWidth);

    public int GetLastToolWidth() => GetToolWidthRange().Clamp(LastToolWidth);

    public bool RememberToolWidth(int toolWidth)
    {
        toolWidth = GetToolWidthRange().Clamp(toolWidth);
        if (LastToolWidth == toolWidth)
        {
            return false;
        }

        LastToolWidth = toolWidth;
        return true;
    }
}
