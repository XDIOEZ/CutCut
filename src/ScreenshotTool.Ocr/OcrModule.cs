using ScreenshotTool.Contracts;

namespace ScreenshotTool.Ocr;

public sealed class OcrModule : ScreenshotToolModuleBase
{
    public static Version MinimumHostVersion { get; } = new(1, 11, 0);

    public override string Id => "screenshot-tool.ocr";

    public override string DisplayName => "本地 OCR 文字识别";

    public override Version Version => new(1, 1, 0);

    public override void Initialize(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HostVersion < MinimumHostVersion)
        {
            throw new NotSupportedException(
                $"OCR 模块需要轻截 {MinimumHostVersion} 或更高版本，当前主程序版本为 {context.HostVersion}。请同时更新轻截基础程序。");
        }
    }

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures() =>
        [new OcrFeature(new WindowsOcrRecognizer())];
}
