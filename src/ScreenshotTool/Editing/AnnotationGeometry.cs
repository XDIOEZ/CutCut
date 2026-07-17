namespace ScreenshotTool.Editing;

internal static class AnnotationGeometry
{
    public static IReadOnlyList<Point> ScalePoints(
        IReadOnlyList<Point> points,
        Rectangle originalBounds,
        Rectangle resizedBounds)
    {
        if (points.Count == 0 || originalBounds.IsEmpty || resizedBounds.IsEmpty)
        {
            return points.ToArray();
        }

        var scaled = new Point[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var fallbackRatio = points.Count == 1
                ? 0D
                : index / (double)(points.Count - 1);
            scaled[index] = new Point(
                ScaleAxis(
                    points[index].X,
                    originalBounds.Left,
                    originalBounds.Width,
                    resizedBounds.Left,
                    resizedBounds.Width,
                    fallbackRatio),
                ScaleAxis(
                    points[index].Y,
                    originalBounds.Top,
                    originalBounds.Height,
                    resizedBounds.Top,
                    resizedBounds.Height,
                    fallbackRatio));
        }
        return scaled;
    }

    private static int ScaleAxis(
        int value,
        int originalStart,
        int originalLength,
        int resizedStart,
        int resizedLength,
        double fallbackRatio)
    {
        var ratio = originalLength <= 1
            ? fallbackRatio
            : (value - originalStart) / (double)(originalLength - 1);
        return resizedStart + (int)Math.Round(
            Math.Clamp(ratio, 0D, 1D) * Math.Max(0, resizedLength - 1),
            MidpointRounding.AwayFromZero);
    }
}
