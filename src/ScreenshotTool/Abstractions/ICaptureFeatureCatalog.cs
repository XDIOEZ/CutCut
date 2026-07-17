using ScreenshotTool.Contracts;

namespace ScreenshotTool.Abstractions;

internal interface ICaptureFeatureCatalog
{
    IReadOnlyList<ICaptureFeature> CreateCaptureFeatures();
}
