namespace ScreenshotTool.ScreenRecording;

internal sealed record RecordingTarget(
    Rectangle ScreenBounds,
    Rectangle DisplayBounds,
    string DisplayDeviceName)
{
    public static bool TryCreate(Rectangle requestedBounds, out RecordingTarget? target)
    {
        target = null;
        if (requestedBounds.Width < 2 || requestedBounds.Height < 2)
        {
            return false;
        }

        var display = Screen.AllScreens.FirstOrDefault(screen =>
            screen.Bounds.Contains(requestedBounds));
        if (display is null)
        {
            return false;
        }

        return TryCreateForDisplay(
            requestedBounds,
            display.Bounds,
            display.DeviceName,
            out target);
    }

    internal static bool TryCreateForDisplay(
        Rectangle requestedBounds,
        Rectangle displayBounds,
        string displayDeviceName,
        out RecordingTarget? target)
    {
        target = null;
        if (!displayBounds.Contains(requestedBounds) ||
            requestedBounds.Width < 2 ||
            requestedBounds.Height < 2 ||
            string.IsNullOrWhiteSpace(displayDeviceName))
        {
            return false;
        }

        var width = requestedBounds.Width & ~1;
        var height = requestedBounds.Height & ~1;
        if (width < 2 || height < 2)
        {
            return false;
        }

        var normalized = new Rectangle(
            requestedBounds.X,
            requestedBounds.Y,
            width,
            height);
        target = new RecordingTarget(normalized, displayBounds, displayDeviceName);
        return true;
    }

    public Rectangle DisplayRelativeBounds => new(
        ScreenBounds.X - DisplayBounds.X,
        ScreenBounds.Y - DisplayBounds.Y,
        ScreenBounds.Width,
        ScreenBounds.Height);
}
