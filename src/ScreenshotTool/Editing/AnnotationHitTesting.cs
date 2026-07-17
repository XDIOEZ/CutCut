namespace ScreenshotTool.Editing;

internal static class AnnotationHitTesting
{
    public static bool IsNearPolyline(IReadOnlyList<Point> points, Point point, double tolerance)
    {
        if (points.Count == 0)
        {
            return false;
        }

        if (points.Count == 1)
        {
            return DistanceSquared(points[0], point) <= tolerance * tolerance;
        }

        for (var index = 1; index < points.Count; index++)
        {
            if (DistanceToSegmentSquared(point, points[index - 1], points[index]) <= tolerance * tolerance)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsNearSegment(Point point, Point start, Point end, double tolerance) =>
        DistanceToSegmentSquared(point, start, end) <= tolerance * tolerance;

    private static double DistanceToSegmentSquared(Point point, Point start, Point end)
    {
        var segmentX = end.X - start.X;
        var segmentY = end.Y - start.Y;
        if (segmentX == 0 && segmentY == 0)
        {
            return DistanceSquared(point, start);
        }

        var projection = ((double)(point.X - start.X) * segmentX +
                          (double)(point.Y - start.Y) * segmentY) /
                         ((double)segmentX * segmentX + (double)segmentY * segmentY);
        projection = Math.Clamp(projection, 0D, 1D);
        var nearestX = start.X + projection * segmentX;
        var nearestY = start.Y + projection * segmentY;
        var deltaX = point.X - nearestX;
        var deltaY = point.Y - nearestY;
        return deltaX * deltaX + deltaY * deltaY;
    }

    private static double DistanceSquared(Point first, Point second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return (double)deltaX * deltaX + (double)deltaY * deltaY;
    }
}
