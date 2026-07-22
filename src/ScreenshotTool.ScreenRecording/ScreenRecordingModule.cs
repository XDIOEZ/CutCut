using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

public sealed class ScreenRecordingModule : ScreenshotToolModuleBase, IModuleSettingsPageProvider
{
    public static Version MinimumHostVersion { get; } = new(1, 10, 0);

    private string? _moduleDirectory;

    public override string Id => "screenshot-tool.screen-recording";

    public override string DisplayName => "录屏与实时批注";

    public override Version Version => new(1, 7, 0);

    public override void Initialize(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HostVersion < MinimumHostVersion)
        {
            throw new NotSupportedException(
                $"录屏模块需要轻截 {MinimumHostVersion} 或更高版本，当前主程序版本为 {context.HostVersion}。请同时更新轻截基础程序。");
        }

        _moduleDirectory = context.ModuleDirectory;
    }

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures()
    {
        if (string.IsNullOrWhiteSpace(_moduleDirectory))
        {
            throw new InvalidOperationException("录屏模块尚未初始化。");
        }

        return [new ScreenRecordingFeature(Path.Combine(_moduleDirectory, "Recorder"))];
    }

    public IEnumerable<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host) =>
        [new ScreenRecordingSettingsPage(host)];
}
