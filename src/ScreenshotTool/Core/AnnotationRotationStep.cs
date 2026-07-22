namespace ScreenshotTool.Core;

internal static class AnnotationRotationStep
{
    public const int DefaultDegrees = 5;
    public const int MinimumDegrees = 1;
    public const int MaximumDegrees = 90;

    public static int Normalize(int degrees) =>
        Math.Clamp(degrees, MinimumDegrees, MaximumDegrees);
}
