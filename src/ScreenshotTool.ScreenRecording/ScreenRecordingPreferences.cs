using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal static class ScreenRecordingPreferences
{
    public const string CaptureSystemAudioId =
        "screenshot-tool.screen-recording.capture-system-audio";

    public const string CaptureMicrophoneId =
        "screenshot-tool.screen-recording.capture-microphone";

    public const string ShowMouseClickIndicatorId =
        "screenshot-tool.screen-recording.show-mouse-click-indicator";

    public const string FramesPerSecondId =
        "screenshot-tool.screen-recording.frames-per-second";

    public const string VideoBitrateId =
        "screenshot-tool.screen-recording.video-bitrate";

    public const string RegionIndicatorStyleId =
        "screenshot-tool.screen-recording.region-indicator-style";

    public const bool DefaultCaptureSystemAudio = true;
    public const bool DefaultCaptureMicrophone = true;
    public const bool DefaultShowMouseClickIndicator = true;
    public const int DefaultFramesPerSecond = 30;
    public const int DefaultVideoBitrate = 8_000_000;
    public const CaptureRegionIndicatorStyle DefaultRegionIndicatorStyle =
        CaptureRegionIndicatorStyle.Dashed;

    public static IReadOnlyList<int> SupportedFramesPerSecond { get; } = [30, 60];

    public static IReadOnlyList<int> SupportedVideoBitrates { get; } =
        [2_000_000, 4_000_000, 8_000_000, 12_000_000, 20_000_000];

    public static int NormalizeFramesPerSecond(int value) =>
        FindNearest(value, SupportedFramesPerSecond);

    public static int NormalizeVideoBitrate(int value) =>
        FindNearest(value, SupportedVideoBitrates);

    public static CaptureRegionIndicatorStyle NormalizeRegionIndicatorStyle(int value) =>
        Enum.IsDefined(typeof(CaptureRegionIndicatorStyle), value)
            ? (CaptureRegionIndicatorStyle)value
            : DefaultRegionIndicatorStyle;

    private static int FindNearest(int value, IReadOnlyList<int> supportedValues)
    {
        var nearest = supportedValues[0];
        var nearestDistance = long.MaxValue;
        foreach (var supportedValue in supportedValues)
        {
            var distance = Math.Abs((long)supportedValue - value);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = supportedValue;
            nearestDistance = distance;
        }
        return nearest;
    }
}
