using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Infrastructure;
using ScreenshotTool.ScreenRecording;

internal static class RecordingCompressionTests
{
    public static void Run()
    {
        var previousContext = SynchronizationContext.Current;
        try
        {
            RunCore();
        }
        finally
        {
            // Constructing WinForms controls must not redirect the console suite's early awaits.
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static void RunCore()
    {
        var defaults = RecordingOptions.FromHost(new TestCaptureFeatureHost());
        Require(defaults.FramesPerSecond == 30 && defaults.VideoBitrate == 8_000_000 &&
                defaults.AudioBitrate == 128_000 && !defaults.MonoAudio &&
                defaults.ResolutionMode == RecordingResolutionMode.Original,
            "旧设置缺少新字段时保留原录制质量");
        Require(RecordingOutputResolution.Resolve(new Size(1920, 1080), defaults) == new Size(1920, 1080),
            "原分辨率不缩放");
        var compressed = defaults.WithSuperCompression();
        Require(RecordingOutputResolution.Resolve(new Size(1920, 1080), compressed) == new Size(320, 180),
            "超级压缩等比缩小横屏");
        Require(RecordingOutputResolution.Resolve(new Size(1080, 1920), compressed) == new Size(180, 320),
            "超级压缩等比缩小竖屏");
        Require(RecordingOutputResolution.Resolve(new Size(100, 80), compressed) == new Size(100, 80),
            "最长边限制不放大小选区");
        Require(RecordingOutputResolution.Resolve(new Size(639, 479), compressed) == new Size(320, 238),
            "输出归一化偶数尺寸");
        Require(RecordingOutputResolution.Resolve(new Size(1920, 1080), defaults with
        {
            ResolutionMode = RecordingResolutionMode.Custom,
            OutputWidth = 801,
            OutputHeight = 601
        }) == new Size(800, 600), "自定义尺寸与选区分离");
        Require(ScreenRecordingStorageEstimator.EstimateBytes(64_000, true, TimeSpan.FromMinutes(1), 96_000) == 1_200_000,
            "压缩有声储存估算");
        Require(ScreenRecordingStorageEstimator.EstimateBytes(64_000, false, TimeSpan.FromMinutes(1), 96_000) == 480_000,
            "压缩静音储存估算");

        var settingsHost = new TestModuleSettingsHost();
        settingsHost.SetBoolean(ScreenRecordingPreferences.CaptureSystemAudioId, true);
        settingsHost.SetBoolean(ScreenRecordingPreferences.CaptureMicrophoneId, false);
        using (var page = new ScreenRecordingSettingsPage(settingsHost))
        {
            page.SelectSuperCompression();
            Require(page.CaptureSystemAudio && !page.CaptureMicrophone, "压缩预设保留用户声音来源选择");
            Require(page.EncodingOptions.FramesPerSecond == 5 && page.VideoBitrate == 64_000 &&
                    page.AudioBitrate == 96_000 && page.MonoAudio && page.MaximumOutputEdge == 320,
                "预设统一更新视频和声音参数");
            Require(page.OneMinuteEstimate == "1.2 MB", "页面读取实际音频码率估算");
            page.CaptureSystemAudio = false;
            Require(page.OneMinuteEstimate == "0.48 MB", "页面关闭声音后即时更新估算");
            page.ResolutionMode = RecordingResolutionMode.Custom;
            page.OutputWidth = 801;
            page.OutputHeight = 601;
            page.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<Button>())
                .Single(button => button.Text == "保存录屏设置").PerformClick();
        }
        using (var reopened = new ScreenRecordingSettingsPage(settingsHost))
        {
            Require(reopened.ResolutionMode == RecordingResolutionMode.Custom && reopened.OutputWidth == 800 &&
                    reopened.OutputHeight == 600 && reopened.FramesPerSecond == 5 && reopened.VideoBitrate == 64_000 &&
                    reopened.AudioBitrate == 96_000 && reopened.MonoAudio,
                "重新打开页面恢复所有压缩参数");
        }

        var folder = Path.Combine(Path.GetTempPath(), "CutCutRecordingCompression", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(folder, "compression-test");
            var settings = new AppSettings();
            var integers = settings.Preferences.ModuleIntegerPreferences;
            integers[ScreenRecordingPreferences.FramesPerSecondId] = 5;
            integers[ScreenRecordingPreferences.VideoBitrateId] = 64_000;
            integers[ScreenRecordingPreferences.AudioBitrateId] = 96_000;
            integers[ScreenRecordingPreferences.ResolutionModeId] = (int)RecordingResolutionMode.Custom;
            integers[ScreenRecordingPreferences.OutputWidthId] = 800;
            integers[ScreenRecordingPreferences.OutputHeightId] = 600;
            settings.Preferences.ModuleBooleanPreferences[ScreenRecordingPreferences.MonoAudioId] = true;
            store.Save(settings);
            var loaded = store.Load();
            var restored = RecordingOptions.FromHost(new TestCaptureFeatureHost(
                integerPreferences: loaded.Preferences.ModuleIntegerPreferences,
                booleanPreferences: loaded.Preferences.ModuleBooleanPreferences));
            Require(restored.FramesPerSecond == 5 && restored.VideoBitrate == 64_000 && restored.AudioBitrate == 96_000 &&
                    restored.MonoAudio && restored.OutputWidth == 800 && restored.OutputHeight == 600,
                "真实 JSON 保存加载与录制参数链保持低码率");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
