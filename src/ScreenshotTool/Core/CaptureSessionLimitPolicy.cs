namespace ScreenshotTool.Core;

internal static class CaptureSessionLimitPolicy
{
    public const int DefaultMaximum = 3;
    public const int Minimum = 1;
    public const int Maximum = 10;

    public static int Normalize(int value) =>
        value is >= Minimum and <= Maximum ? value : DefaultMaximum;
}
