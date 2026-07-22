using System.Drawing.Drawing2D;
using ScreenshotTool.Core;

namespace ScreenshotTool.Editing;

internal sealed record AnnotationDeleteResult(
    int RemovedCount,
    Rectangle Bounds,
    int RenderMargin);

internal sealed class CaptureAnnotationEditor : IDisposable
{
    private readonly DrawingToolCoefficients _coefficients;
    private readonly AnnotationHitCycle _hitCycle = new();
    private readonly TextAnnotationEditSession _textEditSession = new();

    public CaptureAnnotationEditor(DrawingToolCoefficients? coefficients = null)
    {
        _coefficients = coefficients ?? new DrawingToolCoefficients();
    }

    public AnnotationDocument Document { get; } = new();

    public AnnotationSelection Selection { get; } = new();

    public float GetDrawingCursorDiameter(EditorTool tool, float width) => tool switch
    {
        EditorTool.Pen => _coefficients.ApplyPen(width),
        EditorTool.Mosaic => MosaicAnnotation.CalculateBrushDiameter(
            _coefficients.ApplyMosaic(width)),
        _ => 0F
    };

    public Annotation? BuildDraft(
        EditorTool tool,
        Point start,
        Point current,
        IReadOnlyList<Point> points,
        Color color,
        float width)
    {
        var bounds = Geometry.Normalize(start, current);
        return tool switch
        {
            EditorTool.Rectangle when bounds.Width > 1 && bounds.Height > 1 =>
                new ShapeAnnotation(
                    EditorTool.Rectangle,
                    bounds,
                    color,
                    _coefficients.ApplyRectangle(width)),
            EditorTool.Ellipse when bounds.Width > 1 && bounds.Height > 1 =>
                new ShapeAnnotation(
                    EditorTool.Ellipse,
                    bounds,
                    color,
                    _coefficients.ApplyEllipse(width)),
            EditorTool.Arrow when start != current =>
                new ArrowAnnotation(
                    start,
                    current,
                    color,
                    _coefficients.ApplyArrowBody(width),
                    (float)_coefficients.ArrowHeadWidth * width,
                    (float)_coefficients.ArrowHeadLength * width),
            EditorTool.Pen when points.Count > 0 =>
                new FreehandAnnotation(points, color, _coefficients.ApplyPen(width)),
            EditorTool.Mosaic when points.Count > 0 =>
                new MosaicAnnotation(points, _coefficients.ApplyMosaic(width)),
            _ => null
        };
    }

    public bool AddDraft(
        EditorTool tool,
        Point start,
        Point current,
        IReadOnlyList<Point> points,
        Color color,
        float width)
    {
        var annotation = BuildDraft(tool, start, current, points, color, width);
        if (annotation is null)
        {
            return false;
        }

        ResetHitCycle();
        Document.Add(annotation);
        return true;
    }

    public MovableAnnotation? ActiveTextEditAnnotation => _textEditSession.ActiveAnnotation;

    public void Render(Graphics graphics, Bitmap source) =>
        Document.Render(graphics, source, ActiveTextEditAnnotation);

    public bool TryBeginTextEdit(
        MovableAnnotation annotation,
        out TextAnnotationEditDescriptor? descriptor)
    {
        ResetHitCycle();
        return _textEditSession.TryBegin(Document, annotation, out descriptor);
    }

    public MovableAnnotation? EndTextEdit(
        bool commit,
        Rectangle editorOuterBounds,
        Rectangle editorContentBounds,
        string text,
        float fontSize) =>
        _textEditSession.End(
            commit,
            editorOuterBounds,
            editorContentBounds,
            text,
            fontSize);

    public Bitmap RenderResult(Bitmap source)
    {
        var result = new Bitmap(
            source.Width,
            source.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(source, Point.Empty);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Render(graphics, source);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public bool Undo()
    {
        if (!Document.Undo())
        {
            return false;
        }

        ResetHitCycle();
        Selection.Prune(Document.Contains);
        return true;
    }

    public AnnotationDeleteResult DeleteSelected()
    {
        if (Selection.Count == 0)
        {
            return new AnnotationDeleteResult(0, Rectangle.Empty, 0);
        }

        var bounds = Selection.Bounds;
        var renderMargin = Selection.RenderMargin;
        var annotations = Selection.Items.Cast<Annotation>().ToArray();
        ResetHitCycle();
        Selection.Clear();
        return new AnnotationDeleteResult(
            Document.Remove(annotations),
            bounds,
            renderMargin);
    }

    public void SelectIntersecting(Rectangle area) =>
        Selection.SelectIntersecting(Document.GetMovableAnnotations(), area);

    public void SelectAll() => Selection.SelectAll(Document.GetMovableAnnotations());

    public MovableAnnotation? FindTopMovableAt(Point point, int tolerance) =>
        Document.FindTopMovableAt(point, tolerance);

    public MovableAnnotation? FindNextMovableAt(
        Point point,
        int hitTolerance,
        int regionTolerance) =>
        _hitCycle.SelectNext(
            point,
            Document.FindMovablesAt(point, hitTolerance),
            regionTolerance);

    public void ResetHitCycle() => _hitCycle.Reset();

    public StickerAnnotation AddSticker(Bitmap image, Rectangle bounds)
    {
        ResetHitCycle();
        var sticker = new StickerAnnotation(image, bounds);
        Document.Add(sticker);
        Selection.SelectOnly(sticker);
        return sticker;
    }

    public T AddAndSelect<T>(T annotation) where T : MovableAnnotation
    {
        ResetHitCycle();
        Document.Add(annotation);
        Selection.SelectOnly(annotation);
        return annotation;
    }

    public void DrawSelection(Graphics graphics, int handleSize, float scale = 1F)
    {
        if (Selection.Count == 0)
        {
            return;
        }

        using var border = new Pen(Color.White, Math.Max(1F, 1.4F / scale))
        {
            DashStyle = DashStyle.Dash
        };
        foreach (var annotation in Selection.Items)
        {
            if (Document.Contains(annotation))
            {
                var state = graphics.Save();
                try
                {
                    annotation.ApplyRotationTransform(graphics);
                    graphics.DrawRectangle(border, annotation.Bounds);
                }
                finally
                {
                    graphics.Restore(state);
                }
            }
        }

        if (Selection.Count != 1 || Selection.Primary is not { SupportsResize: true } primary)
        {
            return;
        }

        using var fill = new SolidBrush(Color.White);
        using var outline = new Pen(Color.FromArgb(14, 165, 233), Math.Max(1F, 1.5F / scale));
        var handleState = graphics.Save();
        try
        {
            primary.ApplyRotationTransform(graphics);
            foreach (var (_, handle) in StickerLayout.GetHandles(primary.Bounds, handleSize))
            {
                graphics.FillRectangle(fill, handle);
                graphics.DrawRectangle(outline, handle);
            }
        }
        finally
        {
            graphics.Restore(handleState);
        }
    }

    public void Clear()
    {
        ResetHitCycle();
        _textEditSession.Cancel();
        Selection.Clear();
        Document.Clear();
    }

    public void Dispose() => Document.Dispose();
}
