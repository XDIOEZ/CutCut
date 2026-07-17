namespace ScreenshotTool.LongCapture;

internal enum VerticalScrollDirection
{
    None,
    Down,
    Up
}

internal sealed record BidirectionalVerticalFrameMatch(
    VerticalScrollDirection Direction,
    VerticalFrameMatch Match);

/// <summary>
/// Validates vertical overlap in both directions. The existing matcher remains the single source
/// of pixel-confidence rules; reversing its arguments proves an upward movement.
/// </summary>
internal sealed class BidirectionalVerticalFrameMatcher(LongCaptureOptions options)
{
    private readonly VerticalFrameMatcher _matcher = new(options);
    private readonly bool _safetyChecksEnabled = options.SafetyChecksEnabled;

    public BidirectionalVerticalFrameMatch Match(
        LongCaptureFrame previous,
        LongCaptureFrame current)
        => MatchCore(previous, current, fixedBands: null);

    public BidirectionalVerticalFrameMatch Match(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        int fixedTop,
        int fixedBottom)
        => MatchCore(previous, current, (fixedTop, fixedBottom));

    private BidirectionalVerticalFrameMatch MatchCore(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        (int Top, int Bottom)? fixedBands)
    {
        var downward = fixedBands is { } bands
            ? _matcher.Match(previous, current, bands.Top, bands.Bottom)
            : _matcher.Match(previous, current);
        if (downward.Decision == FrameMatchDecision.NoMotion)
        {
            return new BidirectionalVerticalFrameMatch(
                VerticalScrollDirection.None,
                downward);
        }

        var upward = fixedBands is { } reverseBands
            ? _matcher.Match(current, previous, reverseBands.Top, reverseBands.Bottom)
            : _matcher.Match(current, previous);
        var downwardAccepted = downward.Decision == FrameMatchDecision.Accepted;
        var upwardAccepted = upward.Decision == FrameMatchDecision.Accepted;
        if (downwardAccepted && upwardAccepted)
        {
            if (!_safetyChecksEnabled)
            {
                var chooseDownward = downward.Confidence >= upward.Confidence;
                var selected = chooseDownward ? downward : upward;
                var direction = chooseDownward
                    ? VerticalScrollDirection.Down
                    : VerticalScrollDirection.Up;
                return new BidirectionalVerticalFrameMatch(
                    direction,
                    selected with
                    {
                        Diagnostic = $"宽松模式选择向{(chooseDownward ? "下" : "上")}" +
                                     $"滚动 {selected.ShiftY}px 的最可能接缝。"
                    });
            }

            var ambiguous = downward with
            {
                Decision = FrameMatchDecision.Ambiguous,
                ShiftY = 0,
                Confidence = Math.Max(downward.Confidence, upward.Confidence),
                RunnerUpConfidence = Math.Min(downward.Confidence, upward.Confidence),
                Diagnostic = "向上和向下都存在高置信度重叠，无法唯一确定滚动方向。"
            };
            return new BidirectionalVerticalFrameMatch(
                VerticalScrollDirection.None,
                ambiguous);
        }

        if (downwardAccepted)
        {
            return new BidirectionalVerticalFrameMatch(
                VerticalScrollDirection.Down,
                downward with
                {
                    Diagnostic = $"已验证向下滚动 {downward.ShiftY}px。"
                });
        }

        if (upwardAccepted)
        {
            return new BidirectionalVerticalFrameMatch(
                VerticalScrollDirection.Up,
                upward with
                {
                    Diagnostic = $"已验证向上滚动 {upward.ShiftY}px。"
                });
        }

        var rejection = SelectStrongerRejection(downward, upward);
        return new BidirectionalVerticalFrameMatch(
            VerticalScrollDirection.None,
            rejection);
    }

    private static VerticalFrameMatch SelectStrongerRejection(
        VerticalFrameMatch downward,
        VerticalFrameMatch upward)
    {
        var downwardPriority = GetRejectionPriority(downward.Decision);
        var upwardPriority = GetRejectionPriority(upward.Decision);
        var selected = upwardPriority > downwardPriority ||
                       (upwardPriority == downwardPriority &&
                        upward.Confidence > downward.Confidence)
            ? upward
            : downward;
        return selected with
        {
            Diagnostic = $"双向重叠验证失败：{selected.Diagnostic}"
        };
    }

    private static int GetRejectionPriority(FrameMatchDecision decision) => decision switch
    {
        FrameMatchDecision.InvalidDimensions => 4,
        FrameMatchDecision.UnsupportedFixedRegion => 3,
        FrameMatchDecision.Ambiguous => 2,
        FrameMatchDecision.InsufficientTexture => 1,
        _ => 0
    };
}
