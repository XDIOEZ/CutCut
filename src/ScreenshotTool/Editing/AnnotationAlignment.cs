namespace ScreenshotTool.Editing;

internal static class AnnotationAlignment
{
    public static Point QuantizeOffset(Point offset, int stepPixels) => new(
        Quantize(offset.X, stepPixels),
        Quantize(offset.Y, stepPixels));

    public static Point SnapMoveOffset(
        Rectangle movingBounds,
        Point desiredOffset,
        IEnumerable<Rectangle> stationaryBounds,
        int thresholdPixels)
    {
        if (movingBounds.IsEmpty || thresholdPixels <= 0)
        {
            return desiredOffset;
        }

        var movedBounds = movingBounds;
        movedBounds.Offset(desiredOffset);
        var bounds = stationaryBounds.Where(candidate => !candidate.IsEmpty).ToArray();
        if (bounds.Length == 0)
        {
            return desiredOffset;
        }

        var correctionX = FindBestCorrection(
            GetHorizontalGuides(movedBounds),
            bounds.SelectMany(GetHorizontalGuides),
            thresholdPixels);
        var correctionY = FindBestCorrection(
            GetVerticalGuides(movedBounds),
            bounds.SelectMany(GetVerticalGuides),
            thresholdPixels);
        return new Point(
            desiredOffset.X + correctionX,
            desiredOffset.Y + correctionY);
    }

    public static Point SnapResizePoint(
        Point pointer,
        StickerHitTarget target,
        IEnumerable<Rectangle> stationaryBounds,
        int thresholdPixels)
    {
        if (!StickerLayout.IsResizeHandle(target) || thresholdPixels <= 0)
        {
            return pointer;
        }

        var bounds = stationaryBounds.Where(candidate => !candidate.IsEmpty).ToArray();
        if (bounds.Length == 0)
        {
            return pointer;
        }

        var x = pointer.X;
        var y = pointer.Y;
        if (StickerLayout.AdjustsHorizontalEdge(target))
        {
            x += FindBestCorrection(
                [pointer.X],
                bounds.SelectMany(GetHorizontalGuides),
                thresholdPixels);
        }
        if (StickerLayout.AdjustsVerticalEdge(target))
        {
            y += FindBestCorrection(
                [pointer.Y],
                bounds.SelectMany(GetVerticalGuides),
                thresholdPixels);
        }
        return new Point(x, y);
    }

    private static int Quantize(int value, int stepPixels)
    {
        var step = Math.Max(1, stepPixels);
        return (int)Math.Round(value / (double)step, MidpointRounding.AwayFromZero) * step;
    }

    private static int FindBestCorrection(
        IEnumerable<int> movingGuides,
        IEnumerable<int> stationaryGuides,
        int thresholdPixels)
    {
        var best = 0;
        var bestDistance = thresholdPixels + 1;
        var targets = stationaryGuides.ToArray();
        foreach (var moving in movingGuides)
        {
            foreach (var target in targets)
            {
                var correction = target - moving;
                var distance = Math.Abs(correction);
                if (distance < bestDistance)
                {
                    best = correction;
                    bestDistance = distance;
                }
            }
        }
        return bestDistance <= thresholdPixels ? best : 0;
    }

    private static IEnumerable<int> GetHorizontalGuides(Rectangle bounds)
    {
        yield return bounds.Left;
        yield return bounds.Left + bounds.Width / 2;
        yield return bounds.Right;
    }

    private static IEnumerable<int> GetVerticalGuides(Rectangle bounds)
    {
        yield return bounds.Top;
        yield return bounds.Top + bounds.Height / 2;
        yield return bounds.Bottom;
    }
}
