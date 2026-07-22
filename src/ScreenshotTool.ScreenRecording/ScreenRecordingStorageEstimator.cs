namespace ScreenshotTool.ScreenRecording;

internal static class ScreenRecordingStorageEstimator
{
    private const int AudioBitrate = 128_000;

    public static long EstimateBytes(
        int videoBitrate,
        bool includesAudio,
        TimeSpan duration)
    {
        if (videoBitrate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(videoBitrate));
        }
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        var totalBitrate = (long)videoBitrate + (includesAudio ? AudioBitrate : 0L);
        return checked((long)Math.Ceiling(totalBitrate * duration.TotalSeconds / 8D));
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        return bytes >= 1_000_000_000L
            ? $"{bytes / 1_000_000_000D:0.00} GB"
            : $"{bytes / 1_000_000D:0.##} MB";
    }
}
