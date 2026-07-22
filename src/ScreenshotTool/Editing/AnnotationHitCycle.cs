namespace ScreenshotTool.Editing;

internal sealed class AnnotationHitCycle
{
    private Point? _anchor;
    private MovableAnnotation[] _candidates = [];
    private int _selectedIndex = -1;

    public MovableAnnotation? SelectNext(
        Point point,
        IReadOnlyList<MovableAnnotation> candidates,
        int regionTolerance)
    {
        if (candidates.Count == 0)
        {
            Reset();
            return null;
        }

        var isSameCycle = IsWithinRegion(point, Math.Max(0, regionTolerance)) &&
                          HasSameCandidates(candidates);
        if (!isSameCycle)
        {
            _anchor = point;
            _candidates = candidates.ToArray();
            _selectedIndex = 0;
            return _candidates[0];
        }

        _selectedIndex = (_selectedIndex + 1) % _candidates.Length;
        return _candidates[_selectedIndex];
    }

    public void Reset()
    {
        _anchor = null;
        _candidates = [];
        _selectedIndex = -1;
    }

    private bool IsWithinRegion(Point point, int tolerance)
    {
        if (_anchor is not { } anchor)
        {
            return false;
        }

        var offsetX = (long)point.X - anchor.X;
        var offsetY = (long)point.Y - anchor.Y;
        return offsetX * offsetX + offsetY * offsetY <= (long)tolerance * tolerance;
    }

    private bool HasSameCandidates(IReadOnlyList<MovableAnnotation> candidates)
    {
        if (_candidates.Length != candidates.Count)
        {
            return false;
        }

        for (var index = 0; index < _candidates.Length; index++)
        {
            if (!ReferenceEquals(_candidates[index], candidates[index]))
            {
                return false;
            }
        }

        return true;
    }
}
