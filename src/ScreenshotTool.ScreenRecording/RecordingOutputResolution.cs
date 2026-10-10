namespace ScreenshotTool.ScreenRecording;

internal enum RecordingResolutionMode
{
    Original,
    Custom,
    LimitLongestEdge
}

internal static class RecordingOutputResolution
{
    public static Size Resolve(Size source, RecordingOptions options)
    {
        if (source.Width < 2 || source.Height < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }
        if (options.ResolutionMode == RecordingResolutionMode.Custom)
        {
            return new Size(
                ScreenRecordingPreferences.NormalizeOutputDimension(options.OutputWidth),
                ScreenRecordingPreferences.NormalizeOutputDimension(options.OutputHeight));
        }
        var scale = options.ResolutionMode == RecordingResolutionMode.LimitLongestEdge
            ? Math.Min(1D, ScreenRecordingPreferences.NormalizeMaximumOutputEdge(options.MaximumOutputEdge) /
                (double)Math.Max(source.Width, source.Height))
            : 1D;
        // H.264 requires even dimensions. The recording selection stays in desktop pixels.
        return new Size(
            Math.Max(2, (int)(source.Width * scale) & ~1),
            Math.Max(2, (int)(source.Height * scale) & ~1));
    }
}
