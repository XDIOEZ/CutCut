namespace ScreenshotTool.LongCapture;

internal enum StableFrameSampleStatus
{
    Stable,
    Interrupted,
    TimedOut
}

internal sealed record StableFrameSampleResult(
    StableFrameSampleStatus Status,
    Bitmap? Frame);

/// <summary>
/// Captures only a frame that is proven stable across two consecutive screen samples.
/// </summary>
internal sealed class StableFrameSampler(LongCaptureOptions options)
{
    private readonly VerticalFrameMatcher _matcher = new(options);

    public async Task<StableFrameSampleResult> CaptureAsync(
        ILongCaptureFrameSource frameSource,
        Func<bool> shouldInterrupt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frameSource);
        ArgumentNullException.ThrowIfNull(shouldInterrupt);
        var startedAt = Environment.TickCount64;
        LongCaptureFrame? previous = null;
        try
        {
            await Task.Delay(options.StabilizeIntervalMilliseconds, cancellationToken);
            if (shouldInterrupt())
            {
                return new StableFrameSampleResult(
                    StableFrameSampleStatus.Interrupted,
                    null);
            }

            var firstBitmap = frameSource.CaptureFrame();
            previous = await Task.Run(
                () => new LongCaptureFrame(firstBitmap),
                CancellationToken.None);
            while (Environment.TickCount64 - startedAt <
                   options.StabilizeTimeoutMilliseconds)
            {
                await Task.Delay(options.StabilizeIntervalMilliseconds, cancellationToken);
                if (shouldInterrupt())
                {
                    return new StableFrameSampleResult(
                        StableFrameSampleStatus.Interrupted,
                        null);
                }

                var currentBitmap = frameSource.CaptureFrame();
                var comparison = await Task.Run(
                    () =>
                    {
                        var current = new LongCaptureFrame(currentBitmap);
                        try
                        {
                            var similarity = _matcher.MeasureSamePositionSimilarity(
                                previous,
                                current);
                            return (Frame: current, Similarity: similarity);
                        }
                        catch
                        {
                            current.Dispose();
                            throw;
                        }
                    },
                    CancellationToken.None);
                var current = comparison.Frame;
                var similarity = comparison.Similarity;
                if (similarity >= options.MinimumStableSimilarity)
                {
                    Bitmap stable;
                    try
                    {
                        stable = await Task.Run(
                            () => Clone(current.Image),
                            CancellationToken.None);
                    }
                    finally
                    {
                        current.Dispose();
                    }
                    return new StableFrameSampleResult(
                        StableFrameSampleStatus.Stable,
                        stable);
                }

                previous.Dispose();
                previous = current;
            }

            return new StableFrameSampleResult(
                StableFrameSampleStatus.TimedOut,
                null);
        }
        finally
        {
            previous?.Dispose();
        }
    }

    private static Bitmap Clone(Bitmap source) =>
        source.Clone(
            new Rectangle(Point.Empty, source.Size),
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
}
