using ScreenshotTool.Core;

namespace ScreenshotTool.Abstractions;

internal interface IScreenCaptureService
{
    DesktopSnapshot CaptureDesktop();
}
