namespace ScreenshotTool.Core;

internal readonly record struct ToolWidthRange(int Minimum, int Maximum)
{
    public const int SupportedMinimum = 1;
    public const int SupportedMaximum = 64;
    public const int PreferredDefault = 4;

    public static ToolWidthRange Create(int minimum, int maximum)
    {
        minimum = Math.Clamp(minimum, SupportedMinimum, SupportedMaximum);
        maximum = Math.Clamp(maximum, SupportedMinimum, SupportedMaximum);
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return new ToolWidthRange(minimum, maximum);
    }

    public int Clamp(int value) => Math.Clamp(value, Minimum, Maximum);

    public int GetNextPreset(int current)
    {
        current = Clamp(current);
        if (Minimum == Maximum)
        {
            return Minimum;
        }

        var middle = Clamp(PreferredDefault);
        if (current < middle && middle > Minimum)
        {
            return middle;
        }

        return current < Maximum ? Maximum : Minimum;
    }
}
