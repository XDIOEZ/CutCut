namespace ScreenshotTool.Presentation;

internal sealed class SelectionRedrawGuard
{
    public bool IsRedrawRequested { get; private set; }

    public void RequestRedraw() => IsRedrawRequested = true;

    public bool TryBeginRedraw(bool hasEdits)
    {
        if (hasEdits && !IsRedrawRequested)
        {
            return false;
        }

        IsRedrawRequested = false;
        return true;
    }
}
