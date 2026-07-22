namespace ScreenshotTool.Presentation.Shell;

internal sealed record AppPage(
    string Id,
    string Title,
    string Description,
    Control Content,
    int Order = 1000);
