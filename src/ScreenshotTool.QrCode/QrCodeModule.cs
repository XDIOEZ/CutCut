using ScreenshotTool.Contracts;

namespace ScreenshotTool.QrCode;

public sealed class QrCodeModule : ScreenshotToolModuleBase
{
    public static Version MinimumHostVersion { get; } = new(1, 11, 0);

    public override string Id => "screenshot-tool.qr-code";

    public override string DisplayName => "二维码扫描";

    public override Version Version => new(1, 0, 0);

    public override void Initialize(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HostVersion < MinimumHostVersion)
        {
            throw new NotSupportedException(
                $"二维码扫描模块需要轻截 {MinimumHostVersion} 或更高版本，当前主程序版本为 {context.HostVersion}。请同时更新轻截基础程序。");
        }
    }

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures() =>
        [new QrCodeFeature(new ZxingQrCodeScanner())];
}
