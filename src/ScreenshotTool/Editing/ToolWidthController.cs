using ScreenshotTool.Core;

namespace ScreenshotTool.Editing;

internal sealed class ToolWidthController
{
    private readonly ToolWidthRange _range;

    public ToolWidthController(ToolWidthRange range, int initialWidth = ToolWidthRange.PreferredDefault)
    {
        _range = range;
        Current = range.Clamp(initialWidth);
    }

    public int Current { get; private set; }

    public ToolWidthRange Range => _range;

    public bool Adjust(int steps)
    {
        if (steps == 0)
        {
            return false;
        }

        var next = _range.Clamp(Current + steps);
        if (next == Current)
        {
            return false;
        }

        Current = next;
        return true;
    }

    public bool CyclePreset()
    {
        var next = _range.GetNextPreset(Current);
        if (next == Current)
        {
            return false;
        }

        Current = next;
        return true;
    }
}
