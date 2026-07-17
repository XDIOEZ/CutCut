namespace ScreenshotTool.Core;

internal sealed class UserPreferences
{
    public StickerSelectionMoveMode StickerSelectionMoveMode { get; set; } =
        StickerSelectionMoveMode.FollowSelection;

    public int MinimumToolWidth { get; set; } = 2;

    public int MaximumToolWidth { get; set; } = 8;

    public bool LongCaptureSafetyChecksEnabled { get; set; }

    public DrawingToolCoefficients DrawingToolCoefficients { get; set; } = new();

    public ToolWidthRange GetToolWidthRange() =>
        ToolWidthRange.Create(MinimumToolWidth, MaximumToolWidth);
}
