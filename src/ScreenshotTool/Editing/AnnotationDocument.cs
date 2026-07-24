namespace ScreenshotTool.Editing;

internal sealed class AnnotationDocument : IDisposable
{
    private readonly List<Annotation> _annotations = [];

    public int Count => _annotations.Count;

    public void Add(Annotation annotation) => _annotations.Add(annotation);

    public bool Undo()
    {
        if (_annotations.Count == 0)
        {
            return false;
        }

        var index = _annotations.Count - 1;
        _annotations[index].Dispose();
        _annotations.RemoveAt(index);
        return true;
    }

    public bool Contains(Annotation annotation) => _annotations.Contains(annotation);

    public int Remove(IEnumerable<Annotation> annotations)
    {
        var targets = annotations.ToHashSet();
        var removed = 0;
        for (var index = _annotations.Count - 1; index >= 0; index--)
        {
            if (!targets.Contains(_annotations[index]))
            {
                continue;
            }

            _annotations[index].Dispose();
            _annotations.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    public MovableAnnotation? FindTopMovableAt(Point point, int tolerance = 0)
    {
        for (var index = _annotations.Count - 1; index >= 0; index--)
        {
            if (_annotations[index] is MovableAnnotation movable &&
                movable.HitTest(point, tolerance))
            {
                return movable;
            }
        }

        return null;
    }

    public IReadOnlyList<MovableAnnotation> FindMovablesAt(Point point, int tolerance = 0)
    {
        var matches = new List<MovableAnnotation>();
        for (var index = _annotations.Count - 1; index >= 0; index--)
        {
            if (_annotations[index] is MovableAnnotation movable &&
                movable.HitTest(point, tolerance))
            {
                matches.Add(movable);
            }
        }

        return matches;
    }

    public IReadOnlyList<MovableAnnotation> GetMovableAnnotations() =>
        _annotations.OfType<MovableAnnotation>().ToArray();

    public IReadOnlyList<string> GetVisibleTextContents(Rectangle visibleBounds) =>
        _annotations
            .Where(annotation => Rectangle.Intersect(annotation.VisualBounds, visibleBounds) is { IsEmpty: false })
            .Select(annotation => annotation switch
            {
                TextAnnotation text => text.Text,
                _ => null
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Cast<string>()
            .ToArray();

    public void Clear()
    {
        foreach (var annotation in _annotations)
        {
            annotation.Dispose();
        }
        _annotations.Clear();
    }

    public void Offset(Point delta, AnnotationCategory categories = AnnotationCategory.All)
    {
        if (delta.IsEmpty)
        {
            return;
        }

        foreach (var annotation in _annotations)
        {
            if ((annotation.Category & categories) != AnnotationCategory.None)
            {
                annotation.Offset(delta);
            }
        }
    }

    public IReadOnlyList<Rectangle> GetVisualAreas(
        AnnotationCategory categories = AnnotationCategory.All)
    {
        var areas = new List<Rectangle>();
        foreach (var annotation in _annotations)
        {
            if ((annotation.Category & categories) == AnnotationCategory.None)
            {
                continue;
            }

            var area = annotation.VisualBounds;
            if (area.IsEmpty)
            {
                continue;
            }

            if (annotation.RenderMargin > 0)
            {
                area.Inflate(annotation.RenderMargin, annotation.RenderMargin);
            }
            areas.Add(area);
        }
        return areas;
    }

    public void Render(Graphics graphics, Bitmap source, Annotation? excluded = null)
    {
        foreach (var annotation in _annotations)
        {
            if (ReferenceEquals(annotation, excluded))
            {
                continue;
            }

            annotation.Render(graphics, source);
        }
    }

    public void Dispose() => Clear();
}
