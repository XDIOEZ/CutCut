namespace ScreenshotTool.Editing;

internal sealed class AnnotationSelection
{
    private readonly List<MovableAnnotation> _items = [];

    public IReadOnlyList<MovableAnnotation> Items => _items;

    public int Count => _items.Count;

    public MovableAnnotation? Primary => _items.Count == 0 ? null : _items[^1];

    public bool RequiresAltToMove => _items.Any(annotation => annotation.RequiresAltToMove);

    public int RenderMargin => _items.Count == 0 ? 0 : _items.Max(annotation => annotation.RenderMargin);

    public Rectangle Bounds
    {
        get
        {
            if (_items.Count == 0)
            {
                return Rectangle.Empty;
            }

            var bounds = _items[0].Bounds;
            for (var index = 1; index < _items.Count; index++)
            {
                bounds = Rectangle.Union(bounds, _items[index].Bounds);
            }
            return bounds;
        }
    }

    public bool Contains(MovableAnnotation annotation) => _items.Contains(annotation);

    public void SelectOnly(MovableAnnotation annotation)
    {
        _items.Clear();
        _items.Add(annotation);
    }

    public void Add(MovableAnnotation annotation)
    {
        if (!_items.Contains(annotation))
        {
            _items.Add(annotation);
        }
    }

    public bool Remove(MovableAnnotation annotation) => _items.Remove(annotation);

    public void SelectAll(IEnumerable<MovableAnnotation> annotations)
    {
        _items.Clear();
        foreach (var annotation in annotations)
        {
            Add(annotation);
        }
    }

    public void SelectIntersecting(
        IEnumerable<MovableAnnotation> annotations,
        Rectangle selectionArea)
    {
        _items.Clear();
        foreach (var annotation in annotations)
        {
            if (selectionArea.IntersectsWith(annotation.Bounds))
            {
                Add(annotation);
            }
        }
    }

    public bool IsExactSelection(IReadOnlyCollection<MovableAnnotation> annotations) =>
        _items.Count == annotations.Count && annotations.All(_items.Contains);

    public void Clear() => _items.Clear();

    public void Prune(Func<MovableAnnotation, bool> keep) => _items.RemoveAll(annotation => !keep(annotation));
}
