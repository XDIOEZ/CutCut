using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal sealed record RecordingOptions(
    bool CaptureSystemAudio,
    bool CaptureMicrophone,
    bool ShowMouseClickIndicator,
    int FramesPerSecond,
    int VideoBitrate,
    CaptureRegionIndicatorStyle RegionIndicatorStyle,
    RecordingResolutionMode ResolutionMode = RecordingResolutionMode.Original,
    int OutputWidth = ScreenRecordingPreferences.DefaultOutputWidth,
    int OutputHeight = ScreenRecordingPreferences.DefaultOutputHeight,
    int MaximumOutputEdge = ScreenRecordingPreferences.DefaultMaximumOutputEdge,
    int AudioBitrate = ScreenRecordingPreferences.DefaultAudioBitrate,
    bool MonoAudio = false)
{
    public RecordingOptions WithSuperCompression() => this with
    {
        ResolutionMode = RecordingResolutionMode.LimitLongestEdge,
        MaximumOutputEdge = 320,
        FramesPerSecond = 5,
        VideoBitrate = 64_000,
        AudioBitrate = 96_000,
        MonoAudio = true
    };

    public static RecordingOptions FromHost(ICaptureFeatureHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new RecordingOptions(
            host.GetBooleanPreference(
                ScreenRecordingPreferences.CaptureSystemAudioId,
                ScreenRecordingPreferences.DefaultCaptureSystemAudio),
            host.GetBooleanPreference(
                ScreenRecordingPreferences.CaptureMicrophoneId,
                ScreenRecordingPreferences.DefaultCaptureMicrophone),
            host.GetBooleanPreference(
                ScreenRecordingPreferences.ShowMouseClickIndicatorId,
                ScreenRecordingPreferences.DefaultShowMouseClickIndicator),
            ScreenRecordingPreferences.NormalizeFramesPerSecond(
                host.GetIntegerPreference(
                    ScreenRecordingPreferences.FramesPerSecondId,
                    ScreenRecordingPreferences.DefaultFramesPerSecond)),
            ScreenRecordingPreferences.NormalizeVideoBitrate(
                host.GetIntegerPreference(
                    ScreenRecordingPreferences.VideoBitrateId,
                    ScreenRecordingPreferences.DefaultVideoBitrate)),
            ScreenRecordingPreferences.NormalizeRegionIndicatorStyle(
                host.GetIntegerPreference(
                    ScreenRecordingPreferences.RegionIndicatorStyleId,
                (int)ScreenRecordingPreferences.DefaultRegionIndicatorStyle)),
            ScreenRecordingPreferences.NormalizeResolutionMode(host.GetIntegerPreference(
                ScreenRecordingPreferences.ResolutionModeId, (int)RecordingResolutionMode.Original)),
            ScreenRecordingPreferences.NormalizeOutputDimension(host.GetIntegerPreference(
                ScreenRecordingPreferences.OutputWidthId, ScreenRecordingPreferences.DefaultOutputWidth)),
            ScreenRecordingPreferences.NormalizeOutputDimension(host.GetIntegerPreference(
                ScreenRecordingPreferences.OutputHeightId, ScreenRecordingPreferences.DefaultOutputHeight)),
            ScreenRecordingPreferences.NormalizeMaximumOutputEdge(host.GetIntegerPreference(
                ScreenRecordingPreferences.MaximumOutputEdgeId, ScreenRecordingPreferences.DefaultMaximumOutputEdge)),
            ScreenRecordingPreferences.NormalizeAudioBitrate(host.GetIntegerPreference(
                ScreenRecordingPreferences.AudioBitrateId, ScreenRecordingPreferences.DefaultAudioBitrate)),
            host.GetBooleanPreference(ScreenRecordingPreferences.MonoAudioId, false));
    }
}
