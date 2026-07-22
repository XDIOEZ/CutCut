namespace ScreenshotTool.Core;

internal static class AnnotationLayoutOptions
{
    public const bool DefaultSnappingEnabled = true;
    public const int DefaultSnapThresholdPixels = 8;
    public const int MinimumSnapThresholdPixels = 1;
    public const int MaximumSnapThresholdPixels = 48;
    public const int DefaultCtrlDragStepPixels = 10;
    public const int MinimumCtrlDragStepPixels = 1;
    public const int MaximumCtrlDragStepPixels = 100;

    public static int NormalizeSnapThreshold(int value) => Math.Clamp(
        value,
        MinimumSnapThresholdPixels,
        MaximumSnapThresholdPixels);

    public static int NormalizeCtrlDragStep(int value) => Math.Clamp(
        value,
        MinimumCtrlDragStepPixels,
        MaximumCtrlDragStepPixels);
}
