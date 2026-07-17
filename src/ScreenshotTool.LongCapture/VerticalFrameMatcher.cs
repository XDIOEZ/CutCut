namespace ScreenshotTool.LongCapture;

internal enum FrameMatchDecision
{
    Accepted,
    NoMotion,
    Ambiguous,
    InsufficientTexture,
    UnsupportedFixedRegion,
    InvalidDimensions
}

internal sealed record VerticalFrameMatch(
    FrameMatchDecision Decision,
    int ShiftY,
    int FixedTopHeight,
    int FixedBottomHeight,
    double Confidence,
    double RunnerUpConfidence,
    int InformativeSampleCount,
    string Diagnostic);

internal sealed class VerticalFrameMatcher(LongCaptureOptions options)
{
    private const int MinimumRowTexture = 42;

    public VerticalFrameMatch Match(LongCaptureFrame previous, LongCaptureFrame current)
        => MatchCore(previous, current, fixedBands: null);

    public VerticalFrameMatch Match(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        int fixedTop,
        int fixedBottom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fixedTop);
        ArgumentOutOfRangeException.ThrowIfNegative(fixedBottom);
        return MatchCore(previous, current, (fixedTop, fixedBottom));
    }

    private VerticalFrameMatch MatchCore(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        (int Top, int Bottom)? fixedBands)
    {
        if (previous.Width != current.Width || previous.Height != current.Height)
        {
            return Rejected(
                FrameMatchDecision.InvalidDimensions,
                "相邻帧尺寸发生变化。");
        }

        var samePosition = MeasureSamePosition(previous, current);
        if (samePosition.Confidence >= 0.985)
        {
            return new VerticalFrameMatch(
                FrameMatchDecision.NoMotion,
                0,
                0,
                0,
                samePosition.Confidence,
                0,
                samePosition.InformativeSamples,
                "页面内容没有产生可验证的垂直位移。");
        }

        var (fixedTop, fixedBottom) = fixedBands ?? DetectFixedBands(previous, current);
        var dynamicHeight = previous.Height - fixedTop - fixedBottom;
        if (dynamicHeight <= 0)
        {
            return Rejected(
                FrameMatchDecision.InsufficientTexture,
                "已确定的固定页头和页脚没有留下可匹配的正文区域。",
                fixedTop,
                fixedBottom);
        }
        var minimumOverlap = Math.Max(
            Math.Min(options.MinimumOverlapPixels, Math.Max(24, dynamicHeight / 2)),
            (int)Math.Ceiling(dynamicHeight * options.MinimumOverlapRatio));
        var maximumShift = dynamicHeight - minimumOverlap;
        if (maximumShift < options.MinimumShift)
        {
            return Rejected(
                FrameMatchDecision.InsufficientTexture,
                "可用于匹配的滚动内容高度不足。",
                fixedTop,
                fixedBottom);
        }

        var votes = CollectShiftVotes(
            previous,
            current,
            fixedTop,
            fixedBottom,
            maximumShift);
        var votedCandidates = votes
            .Select((count, shift) => (Shift: shift, Votes: count))
            .Where(candidate => candidate.Shift >= options.MinimumShift && candidate.Votes > 0)
            .OrderByDescending(candidate => candidate.Votes)
            .ThenBy(candidate => candidate.Shift)
            .Take(10)
            .ToArray();
        if (votedCandidates.Length == 0)
        {
            return Rejected(
                FrameMatchDecision.InsufficientTexture,
                "页面纹理不足，无法建立可靠的重叠对应关系。",
                fixedTop,
                fixedBottom);
        }

        var candidates = new HashSet<int>();
        foreach (var candidate in votedCandidates)
        {
            for (var adjustment = -2; adjustment <= 2; adjustment++)
            {
                var adjusted = candidate.Shift + adjustment;
                if (adjusted >= options.MinimumShift && adjusted <= maximumShift)
                {
                    candidates.Add(adjusted);
                }
            }
        }

        var evaluated = candidates
            .Select(shift =>
            {
                var score = MeasureShift(previous, current, shift, fixedTop, fixedBottom);
                return new CandidateScore(
                    shift,
                    GetLocalVoteSupport(votes, shift),
                    score.Confidence,
                    score.InformativeSamples);
            })
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.Votes)
            .ToArray();
        var best = evaluated[0];
        var runnerUp = evaluated
            .Where(candidate => Math.Abs(candidate.Shift - best.Shift) > 2)
            .DefaultIfEmpty(new CandidateScore(0, 0, 0, 0))
            .First();

        var minimumVotes = Math.Max(4, dynamicHeight / 180);
        var belowStrictConfidence =
            best.Confidence < options.MinimumMatchConfidence ||
            best.InformativeSamples < 300 ||
            best.Votes < minimumVotes;
        if (options.SafetyChecksEnabled && belowStrictConfidence)
        {
            return new VerticalFrameMatch(
                FrameMatchDecision.InsufficientTexture,
                0,
                fixedTop,
                fixedBottom,
                best.Confidence,
                runnerUp.Confidence,
                best.InformativeSamples,
                "最佳重叠候选未达到严格置信度门槛。");
        }

        var hasAmbiguousRunnerUp =
            runnerUp.Confidence >= best.Confidence - 0.012 &&
            runnerUp.Votes >= Math.Max(3, best.Votes * 3 / 5);
        if (options.SafetyChecksEnabled && hasAmbiguousRunnerUp)
        {
            return new VerticalFrameMatch(
                FrameMatchDecision.Ambiguous,
                0,
                fixedTop,
                fixedBottom,
                best.Confidence,
                runnerUp.Confidence,
                best.InformativeSamples,
                $"存在多个近似重叠位置（{best.Shift}px 与 {runnerUp.Shift}px）。");
        }

        var hasUnsupportedFixedRegion = HasUnsupportedFixedRegion(
            previous,
            current,
            best.Shift,
            fixedTop,
            fixedBottom);
        if (options.SafetyChecksEnabled && hasUnsupportedFixedRegion)
        {
            return new VerticalFrameMatch(
                FrameMatchDecision.UnsupportedFixedRegion,
                0,
                fixedTop,
                fixedBottom,
                best.Confidence,
                runnerUp.Confidence,
                best.InformativeSamples,
                "滚动内容内部存在固定悬浮区域，严格模式已停止以避免重复拼接。");
        }

        var usedRelaxedMatch =
            belowStrictConfidence ||
            hasAmbiguousRunnerUp ||
            hasUnsupportedFixedRegion;
        return new VerticalFrameMatch(
            FrameMatchDecision.Accepted,
            best.Shift,
            fixedTop,
            fixedBottom,
            best.Confidence,
            runnerUp.Confidence,
            best.InformativeSamples,
            usedRelaxedMatch
                ? $"宽松模式选择最可能的垂直接缝 {best.Shift}px。"
                : $"已验证垂直位移 {best.Shift}px。");
    }

    public double MeasureSamePositionSimilarity(LongCaptureFrame first, LongCaptureFrame second)
    {
        if (first.Width != second.Width || first.Height != second.Height)
        {
            return 0;
        }
        return MeasureSamePosition(first, second).Confidence;
    }

    private static ShiftScore MeasureSamePosition(
        LongCaptureFrame first,
        LongCaptureFrame second)
    {
        long cappedDifference = 0;
        var matches = 0;
        var samples = 0;
        for (var tile = 0; tile < first.TileCount; tile++)
        {
            var bounds = first.GetTileBounds(tile);
            var stepX = Math.Max(1, bounds.Width / 24);
            var stepY = Math.Max(1, first.Height / 180);
            for (var y = 0; y < first.Height; y += stepY)
            {
                for (var x = bounds.Left; x < bounds.Right; x += stepX)
                {
                    var difference = ColorDifference(
                        first.GetPixel(x, y),
                        second.GetPixel(x, y));
                    if (difference <= 4)
                    {
                        matches++;
                    }
                    cappedDifference += Math.Min(64, difference);
                    samples++;
                }
            }
        }

        if (samples == 0)
        {
            return new ShiftScore(0, 0);
        }

        var matchRatio = matches / (double)samples;
        var meanDifference = cappedDifference / (double)samples;
        var confidence = matchRatio * 0.82 +
                         (1D - meanDifference / 64D) * 0.18;
        return new ShiftScore(Math.Clamp(confidence, 0D, 1D), samples);
    }

    private static int[] CollectShiftVotes(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        int fixedTop,
        int fixedBottom,
        int maximumShift)
    {
        var votes = new int[maximumShift + 1];
        var dynamicBottom = previous.Height - fixedBottom;
        for (var tile = 0; tile < previous.TileCount; tile++)
        {
            var currentRows = new Dictionary<ulong, List<int>>();
            var currentCoarseRows = new Dictionary<uint, List<int>>();
            for (var y = fixedTop; y < dynamicBottom; y++)
            {
                if (current.GetRowTexture(y, tile) < MinimumRowTexture)
                {
                    continue;
                }

                var hash = current.GetRowHash(y, tile);
                if (!currentRows.TryGetValue(hash, out var rows))
                {
                    rows = [];
                    currentRows.Add(hash, rows);
                }
                if (rows.Count <= 4)
                {
                    rows.Add(y);
                }

                var signature = current.GetRowSignature(y, tile);
                if (!currentCoarseRows.TryGetValue(signature, out var coarseRows))
                {
                    coarseRows = [];
                    currentCoarseRows.Add(signature, coarseRows);
                }
                if (coarseRows.Count <= 4)
                {
                    coarseRows.Add(y);
                }
            }

            for (var previousY = fixedTop; previousY < dynamicBottom; previousY++)
            {
                var texture = previous.GetRowTexture(previousY, tile);
                if (texture < MinimumRowTexture)
                {
                    continue;
                }

                if (currentRows.TryGetValue(previous.GetRowHash(previousY, tile), out var rows) &&
                    rows.Count <= 4)
                {
                    foreach (var currentY in rows)
                    {
                        AddVote(votes, previousY, currentY, maximumShift,
                            2 + Math.Min(5, texture / 100));
                    }
                }


                if (currentCoarseRows.TryGetValue(
                        previous.GetRowSignature(previousY, tile),
                        out var coarseRows) &&
                    coarseRows.Count <= 4)
                {
                    foreach (var currentY in coarseRows)
                    {
                        AddVote(votes, previousY, currentY, maximumShift, 1);
                    }
                }
            }
        }
        return votes;
    }

    private static void AddVote(
        int[] votes,
        int previousY,
        int currentY,
        int maximumShift,
        int weight)
    {
        var shift = previousY - currentY;
        if (shift >= 0 && shift <= maximumShift)
        {
            votes[shift] += weight;
        }
    }

    private static int GetLocalVoteSupport(int[] votes, int shift)
    {
        var support = 0;
        var start = Math.Max(0, shift - 2);
        var end = Math.Min(votes.Length - 1, shift + 2);
        for (var candidate = start; candidate <= end; candidate++)
        {
            support = Math.Max(support, votes[candidate]);
        }
        return support;
    }

    private static (int Top, int Bottom) DetectFixedBands(
        LongCaptureFrame previous,
        LongCaptureFrame current)
    {
        var maximumBand = previous.Height / 3;
        var top = 0;
        while (top < maximumBand && RowSimilarity(previous, current, top) >= 0.985)
        {
            top++;
        }

        var bottom = 0;
        while (bottom < maximumBand - top &&
               RowSimilarity(previous, current, previous.Height - 1 - bottom) >= 0.985)
        {
            bottom++;
        }
        return (top, bottom);
    }

    private static double RowSimilarity(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        int y)
    {
        var step = Math.Max(1, previous.Width / 220);
        var matches = 0;
        var count = 0;
        var right = Math.Max(1, previous.Width - Math.Min(12, previous.Width / 12));
        for (var x = 0; x < right; x += step)
        {
            count++;
            if (ColorDifference(previous.GetPixel(x, y), current.GetPixel(x, y)) <= 4)
            {
                matches++;
            }
        }
        return count == 0 ? 0 : matches / (double)count;
    }

    private static ShiftScore MeasureShift(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        int shift,
        int fixedTop,
        int fixedBottom)
    {
        var dynamicBottom = previous.Height - fixedBottom;
        var overlapHeight = dynamicBottom - fixedTop - shift;
        if (overlapHeight <= 0)
        {
            return new ShiftScore(0, 0);
        }

        var tileScores = new List<(double Score, int Samples)>();
        for (var tile = 0; tile < previous.TileCount; tile++)
        {
            var bounds = previous.GetTileBounds(tile);
            var stepX = Math.Max(1, bounds.Width / 24);
            var stepY = Math.Max(1, overlapHeight / 180);
            var matches = 0;
            var samples = 0;
            long cappedDifference = 0;
            for (var currentY = fixedTop; currentY < dynamicBottom - shift; currentY += stepY)
            {
                var previousY = currentY + shift;
                if (Math.Max(
                        previous.GetRowTexture(previousY, tile),
                        current.GetRowTexture(currentY, tile)) < MinimumRowTexture / 2)
                {
                    continue;
                }

                for (var x = bounds.Left; x < bounds.Right; x += stepX)
                {
                    var difference = ColorDifference(
                        previous.GetPixel(x, previousY),
                        current.GetPixel(x, currentY));
                    if (difference <= 10)
                    {
                        matches++;
                    }
                    cappedDifference += Math.Min(64, difference);
                    samples++;
                }
            }

            if (samples == 0)
            {
                continue;
            }
            var matchRatio = matches / (double)samples;
            var meanDifference = cappedDifference / (double)samples;
            var score = matchRatio * 0.78 + (1D - meanDifference / 64D) * 0.22;
            tileScores.Add((Math.Clamp(score, 0D, 1D), samples));
        }

        if (tileScores.Count == 0)
        {
            return new ShiftScore(0, 0);
        }

        // Overlap matching may ignore a minority of animated/volatile columns.
        // Same-position checks use MeasureSamePosition instead, which always covers
        // every tile so a large fixed sidebar cannot hide genuine scrolling.
        var retainedTileCount = Math.Max(
            1,
            (int)Math.Ceiling(tileScores.Count * 0.70));
        var retained = tileScores
            .OrderByDescending(tile => tile.Score)
            .Take(retainedTileCount)
            .ToArray();
        return new ShiftScore(
            retained.Average(tile => tile.Score),
            retained.Sum(tile => tile.Samples));
    }

    private static bool HasUnsupportedFixedRegion(
        LongCaptureFrame previous,
        LongCaptureFrame current,
        int shift,
        int fixedTop,
        int fixedBottom)
    {
        var dynamicBottom = previous.Height - fixedBottom - shift;
        var usableRight = previous.GetTileBounds(previous.TileCount - 1).Right;
        const int sampleStep = 4;
        const int windowSamples = 5;
        var sampleRows = (dynamicBottom - fixedTop + sampleStep - 1) / sampleStep;
        var sampleColumns = (usableRight + sampleStep - 1) / sampleStep;
        if (sampleRows < windowSamples || sampleColumns < windowSamples)
        {
            return false;
        }

        // A pixel supports a fixed overlay when it is almost unchanged at the same
        // screen position but clearly disagrees with the position predicted by the
        // verified scroll shift. An integral image then finds compact clusters down
        // to roughly 20x20 physical pixels without mistaking isolated equal-colored
        // page pixels for an overlay.
        var integralStride = sampleColumns + 1;
        var integral = new int[checked((sampleRows + 1) * integralStride)];
        for (var row = 0; row < sampleRows; row++)
        {
            var y = Math.Min(dynamicBottom - 1, fixedTop + row * sampleStep);
            var rowTotal = 0;
            for (var column = 0; column < sampleColumns; column++)
            {
                var x = Math.Min(usableRight - 1, column * sampleStep);
                var samePositionDifference = ColorDifference(
                    previous.GetPixel(x, y),
                    current.GetPixel(x, y));
                var scrolledPositionDifference = ColorDifference(
                    previous.GetPixel(x, y + shift),
                    current.GetPixel(x, y));
                if (samePositionDifference <= 6 && scrolledPositionDifference >= 24)
                {
                    rowTotal++;
                }

                integral[(row + 1) * integralStride + column + 1] =
                    integral[row * integralStride + column + 1] + rowTotal;
            }
        }

        var minimumFixedSamples = (int)Math.Ceiling(
            windowSamples * windowSamples * 0.72);
        for (var row = 0; row <= sampleRows - windowSamples; row += 2)
        {
            for (var column = 0; column <= sampleColumns - windowSamples; column += 2)
            {
                var bottom = row + windowSamples;
                var right = column + windowSamples;
                var fixedSamples =
                    integral[bottom * integralStride + right] -
                    integral[row * integralStride + right] -
                    integral[bottom * integralStride + column] +
                    integral[row * integralStride + column];
                if (fixedSamples >= minimumFixedSamples)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int ColorDifference(int first, int second)
    {
        var blue = Math.Abs((first & 0xFF) - (second & 0xFF));
        var green = Math.Abs(((first >> 8) & 0xFF) - ((second >> 8) & 0xFF));
        var red = Math.Abs(((first >> 16) & 0xFF) - ((second >> 16) & 0xFF));
        return Math.Max(red, Math.Max(green, blue));
    }

    private static VerticalFrameMatch Rejected(
        FrameMatchDecision decision,
        string diagnostic,
        int fixedTop = 0,
        int fixedBottom = 0) => new(
        decision,
        0,
        fixedTop,
        fixedBottom,
        0,
        0,
        0,
        diagnostic);

    private sealed record CandidateScore(
        int Shift,
        int Votes,
        double Confidence,
        int InformativeSamples);

    private readonly record struct ShiftScore(double Confidence, int InformativeSamples);
}
