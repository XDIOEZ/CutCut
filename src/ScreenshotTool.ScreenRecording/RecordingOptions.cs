using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal sealed record RecordingOptions(
    bool CaptureSystemAudio,
    bool CaptureMicrophone,
    bool ShowMouseClickIndicator,
    int FramesPerSecond,
    int VideoBitrate,
    CaptureRegionIndicatorStyle RegionIndicatorStyle)
{
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
                    (int)ScreenRecordingPreferences.DefaultRegionIndicatorStyle)));
    }
}
