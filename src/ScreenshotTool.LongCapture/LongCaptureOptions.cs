namespace ScreenshotTool.LongCapture;

internal sealed record LongCaptureOptions
{
    public bool SafetyChecksEnabled { get; init; }

    public int InitialTargetSettleMilliseconds { get; init; } = 140;

    public int MinimumShift { get; init; } = 4;

    public int MinimumOverlapPixels { get; init; } = 96;

    public double MinimumOverlapRatio { get; init; } = 0.30;

    public double MinimumMatchConfidence { get; init; } = 0.90;

    public double MinimumStableSimilarity { get; init; } = 0.965;

    public int StabilizeIntervalMilliseconds { get; init; } = 90;

    public int StabilizeTimeoutMilliseconds { get; init; } = 1200;

    public int IntermediateCaptureIntervalMilliseconds { get; init; } = 90;

    public int WheelQuietMilliseconds { get; init; } = 180;

    public int MaximumQueuedIntermediateFrames { get; init; } = 16;

    public long MaximumQueuedIntermediatePixels { get; init; } = 48_000_000;

    public int PreviewRefreshIntervalMilliseconds { get; init; } = 180;

    public int MaximumPreviewWidth { get; init; } = 640;

    public int MaximumPreviewHeight { get; init; } = 1600;

    public int MaximumFrames { get; init; } = 120;

    public int MaximumHeight { get; init; } = 30000;

    public long MaximumPixels { get; init; } = 120_000_000;

    public int ConsecutiveNoMotionLimit { get; init; } = 2;

    public int ConsecutiveStableRejectionLimit { get; init; } = 2;
}
