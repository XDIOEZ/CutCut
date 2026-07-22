using ScreenshotTool.Contracts;

namespace ScreenshotTool.LongCapture;

public sealed class LongCaptureModule : ScreenshotToolModuleBase, IModuleSettingsPageProvider
{
    public override string Id => "screenshot-tool.long-capture";

    public override string DisplayName => "长截图";

    public override Version Version => new(1, 1, 0);

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures() =>
        [new LongCaptureFeature()];

    public IEnumerable<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host) =>
        [new LongCaptureSettingsPage(host)];
}
