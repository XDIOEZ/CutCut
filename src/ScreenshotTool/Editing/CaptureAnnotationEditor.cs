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

    public CaptureAnnotationEditor(DrawingToolCoefficients? coefficients = null)
    {
        _coefficients = coefficients ?? new DrawingToolCoefficients();
    }

    public AnnotationDocument Document { get; } = new();

    public AnnotationSelection Selection { get; } = new();

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

        Document.Add(annotation);
        return true;
    }

    public void Render(Graphics graphics, Bitmap source) => Document.Render(graphics, source);

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

    public StickerAnnotation AddSticker(Bitmap image, Rectangle bounds)
    {
        var sticker = new StickerAnnotation(image, bounds);
        Document.Add(sticker);
        Selection.SelectOnly(sticker);
        return sticker;
    }

    public T AddAndSelect<T>(T annotation) where T : MovableAnnotation
    {
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
                graphics.DrawRectangle(border, annotation.Bounds);
            }
        }

        if (Selection.Count != 1 || Selection.Primary is not { SupportsResize: true } primary)
        {
            return;
        }

        using var fill = new SolidBrush(Color.White);
        using var outline = new Pen(Color.FromArgb(14, 165, 233), Math.Max(1F, 1.5F / scale));
        foreach (var (_, handle) in StickerLayout.GetHandles(primary.Bounds, handleSize))
        {
            graphics.FillRectangle(fill, handle);
            graphics.DrawRectangle(outline, handle);
        }
    }

    public void Clear()
    {
        Selection.Clear();
        Document.Clear();
    }

    public void Dispose() => Document.Dispose();
}
