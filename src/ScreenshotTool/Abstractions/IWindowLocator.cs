namespace ScreenshotTool.Abstractions;

internal interface IWindowLocator
{
    Rectangle? FindWindowAt(Point screenPoint);
}
