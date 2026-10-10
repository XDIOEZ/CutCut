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

    public const string ResolutionModeId = "screenshot-tool.screen-recording.resolution-mode";
    public const string OutputWidthId = "screenshot-tool.screen-recording.output-width";
    public const string OutputHeightId = "screenshot-tool.screen-recording.output-height";
    public const string MaximumOutputEdgeId = "screenshot-tool.screen-recording.maximum-output-edge";
    public const string AudioBitrateId = "screenshot-tool.screen-recording.audio-bitrate";
    public const string MonoAudioId = "screenshot-tool.screen-recording.mono-audio";

    public const int DefaultOutputWidth = 640;
    public const int DefaultOutputHeight = 360;
    public const int DefaultMaximumOutputEdge = 320;
    public const int DefaultAudioBitrate = 128_000;

    public const bool DefaultCaptureSystemAudio = true;
    public const bool DefaultCaptureMicrophone = true;
    public const bool DefaultShowMouseClickIndicator = true;
    public const int DefaultFramesPerSecond = 30;
    public const int DefaultVideoBitrate = 8_000_000;
    public const CaptureRegionIndicatorStyle DefaultRegionIndicatorStyle =
        CaptureRegionIndicatorStyle.Dashed;

    public static IReadOnlyList<int> SupportedFramesPerSecond { get; } = [1, 5, 10, 15, 24, 30, 60];

    public static IReadOnlyList<int> SupportedVideoBitrates { get; } =
        [32_000, 64_000, 128_000, 256_000, 512_000, 1_000_000,
            2_000_000, 4_000_000, 8_000_000, 12_000_000, 20_000_000];

    public static IReadOnlyList<int> SupportedAudioBitrates { get; } =
        [96_000, 128_000, 160_000, 192_000];

    public static int NormalizeAudioBitrate(int value) => FindNearest(value, SupportedAudioBitrates);

    public static int NormalizeOutputDimension(int value) => Math.Clamp(value, 16, 8192) & ~1;

    public static int NormalizeMaximumOutputEdge(int value) => Math.Clamp(value, 64, 8192) & ~1;

    public static RecordingResolutionMode NormalizeResolutionMode(int value) =>
        Enum.IsDefined(typeof(RecordingResolutionMode), value)
            ? (RecordingResolutionMode)value
            : RecordingResolutionMode.Original;

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
