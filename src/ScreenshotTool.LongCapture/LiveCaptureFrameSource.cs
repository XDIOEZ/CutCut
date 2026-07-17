using ScreenshotTool.Contracts;

namespace ScreenshotTool.LongCapture;

internal sealed class LiveCaptureFrameSource(ILiveCaptureFeatureHost host) :
    ILongCaptureFrameSource
{
    public Bitmap CaptureFrame() => host.CaptureLiveSelection();
}
