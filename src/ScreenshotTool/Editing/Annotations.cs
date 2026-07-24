using System.Drawing.Drawing2D;

namespace ScreenshotTool.Editing;

[Flags]
internal enum AnnotationCategory
{
    None = 0,
    Drawing = 1,
    Sticker = 2,
    All = Drawing | Sticker
}

internal abstract class Annotation : IDisposable
{
    public virtual AnnotationCategory Category => AnnotationCategory.Drawing;

    public virtual Rectangle VisualBounds => Rectangle.Empty;

    public virtual int RenderMargin => 0;

    public abstract void Render(Graphics graphics, Bitmap source);

    public abstract void Offset(Point delta);

    public virtual void Dispose()
    {
    }

    protected static Pen CreatePen(Color color, float width)
    {
        return new Pen(color, width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
    }
}

internal abstract class MovableAnnotation : Annotation
{
    public abstract Rectangle Bounds { get; protected set; }

    public float RotationDegrees { get; private set; }

    public override Rectangle VisualBounds => AnnotationRotation.GetRotatedBounds(
        Bounds,
        RotationDegrees);

    public virtual bool SupportsResize => false;

    public virtual bool PreserveAspectRatioWhenResizing => false;

    public virtual bool RequiresAltToMove => true;

    public override int RenderMargin => 2;

    public abstract void SetBounds(Rectangle bounds);

    public sealed override void Render(Graphics graphics, Bitmap source)
    {
        var state = graphics.Save();
        try
        {
            ApplyRotationTransform(graphics);
            RenderUnrotated(graphics, source);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    protected abstract void RenderUnrotated(Graphics graphics, Bitmap source);

    public virtual bool HitTest(Point point, int tolerance) =>
        HitTestUnrotated(ToUnrotatedPoint(point), tolerance);

    protected virtual bool HitTestUnrotated(Point point, int tolerance) => Bounds.Contains(point);

    public void RotateBy(float degrees) =>
        RotationDegrees = AnnotationRotation.NormalizeDegrees(RotationDegrees + degrees);

    internal Point ToUnrotatedPoint(Point point) =>
        AnnotationRotation.ToUnrotatedPoint(point, Bounds, RotationDegrees);

    internal void ApplyRotationTransform(Graphics graphics) =>
        AnnotationRotation.ApplyTransform(graphics, Bounds, RotationDegrees);

    public bool CanMove(bool altPressed) => !RequiresAltToMove || altPressed;

    public override void Offset(Point delta) => SetBounds(new Rectangle(
        Bounds.X + delta.X,
        Bounds.Y + delta.Y,
        Bounds.Width,
        Bounds.Height));
}

internal sealed class ShapeAnnotation : MovableAnnotation
{
    public ShapeAnnotation(EditorTool tool, Rectangle bounds, Color color, float width)
    {
        Tool = tool;
        Bounds = bounds;
        Color = color;
        Width = width;
    }

    public EditorTool Tool { get; }
    public override Rectangle Bounds { get; protected set; }
    public Color Color { get; }
    public float Width { get; }

    public override bool SupportsResize => true;

    public override int RenderMargin => (int)Math.Ceiling(Width / 2F) + 3;

    protected override void RenderUnrotated(Graphics graphics, Bitmap source)
    {
        using var pen = CreatePen(Color, Width);
        if (Tool == EditorTool.Ellipse)
        {
            graphics.DrawEllipse(pen, Bounds);
        }
        else
        {
            graphics.DrawRectangle(pen, Bounds);
        }
    }

    public override void SetBounds(Rectangle bounds) => Bounds = bounds;
}

internal sealed class ArrowAnnotation : MovableAnnotation
{
    public ArrowAnnotation(
        Point start,
        Point end,
        Color color,
        float width,
        float? headWidth = null,
        float? headLength = null)
    {
        Start = start;
        End = end;
        Color = color;
        Width = width;
        HeadWidth = headWidth ?? width * 3.2F;
        HeadLength = headLength ?? width * 3.8F;
        Bounds = CalculateBounds(start, end);
    }

    public Point Start { get; private set; }
    public Point End { get; private set; }
    public override Rectangle Bounds { get; protected set; }
    public Color Color { get; }
    public float Width { get; }
    public float HeadWidth { get; }
    public float HeadLength { get; }

    public override bool SupportsResize => true;

    public override int RenderMargin =>
        (int)Math.Ceiling(Math.Max(Width, Math.Max(HeadWidth, HeadLength))) + 3;

    protected override bool HitTestUnrotated(Point point, int tolerance) => AnnotationHitTesting.IsNearSegment(
        point,
        Start,
        End,
        Math.Max(tolerance, Width / 2F + 3F));

    public override void SetBounds(Rectangle bounds)
    {
        var scaled = AnnotationGeometry.ScalePoints([Start, End], Bounds, bounds);
        Start = scaled[0];
        End = scaled[1];
        Bounds = CalculateBounds(Start, End);
    }

    protected override void RenderUnrotated(Graphics graphics, Bitmap source)
    {
        using var pen = CreatePen(Color, Width);
        pen.CustomEndCap = new AdjustableArrowCap(
            HeadWidth / Math.Max(0.1F, Width),
            HeadLength / Math.Max(0.1F, Width),
            true);
        graphics.DrawLine(pen, Start, End);
    }

    private static Rectangle CalculateBounds(Point start, Point end)
    {
        return Rectangle.FromLTRB(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X) + 1,
            Math.Max(start.Y, end.Y) + 1);
    }
}

internal sealed class FreehandAnnotation : MovableAnnotation
{
    public FreehandAnnotation(IEnumerable<Point> points, Color color, float width)
    {
        Points = points.ToArray();
        Color = color;
        Width = width;
        Bounds = CalculateBounds(Points);
    }

    public IReadOnlyList<Point> Points { get; private set; }
    public override Rectangle Bounds { get; protected set; }
    public Color Color { get; }
    public float Width { get; }

    public override bool SupportsResize => true;

    public override int RenderMargin => (int)Math.Ceiling(Width / 2F) + 3;

    protected override bool HitTestUnrotated(Point point, int tolerance) => AnnotationHitTesting.IsNearPolyline(
        Points,
        point,
        Math.Max(tolerance, Width / 2F + 3F));

    public override void SetBounds(Rectangle bounds)
    {
        Points = AnnotationGeometry.ScalePoints(Points, Bounds, bounds);
        Bounds = CalculateBounds(Points);
    }

    protected override void RenderUnrotated(Graphics graphics, Bitmap source)
    {
        if (Points.Count == 0)
        {
            return;
        }

        using var pen = CreatePen(Color, Width);
        if (Points.Count == 1)
        {
            graphics.DrawEllipse(pen, Points[0].X, Points[0].Y, 1, 1);
            return;
        }

        graphics.DrawLines(pen, Points.ToArray());
    }

    private static Rectangle CalculateBounds(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X) + 1,
            points.Max(point => point.Y) + 1);
    }
}

internal sealed class TextAnnotation : MovableAnnotation
{
    public TextAnnotation(Rectangle bounds, string text, Color color, float fontSize)
    {
        Bounds = bounds;
        Text = text;
        Color = color;
        FontSize = fontSize;
    }

    public override Rectangle Bounds { get; protected set; }
    public string Text { get; private set; }
    public Color Color { get; }
    public float FontSize { get; private set; }

    public override AnnotationCategory Category => AnnotationCategory.Sticker;

    public override void SetBounds(Rectangle bounds)
    {
        FontSize = AnnotationScaling.ScaleFontSize(FontSize, Bounds, bounds);
        Bounds = bounds;
    }

    internal void UpdateText(Rectangle bounds, string text, float fontSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!float.IsFinite(fontSize) || fontSize <= 0F)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        }

        Bounds = bounds;
        Text = text;
        FontSize = fontSize;
    }

    protected override void RenderUnrotated(Graphics graphics, Bitmap source)
    {
        using var font = new Font("Microsoft YaHei UI", FontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
        };
        var state = graphics.Save();
        try
        {
            graphics.SetClip(Bounds, CombineMode.Intersect);
            graphics.DrawString(Text, font, brush, Bounds, format);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

}

internal sealed class MosaicAnnotation : MovableAnnotation
{
    private const int BlockSize = 14;
    private Size _blockSize = new(BlockSize, BlockSize);

    public MosaicAnnotation(IEnumerable<Point> points, float width)
    {
        Width = width;
        Blocks = BuildBlocks(points, width).ToArray();
        Bounds = CalculateBounds(Blocks);
    }

    public float Width { get; }
    public IReadOnlyList<Point> Blocks { get; private set; }
    public override Rectangle Bounds { get; protected set; }

    public override bool SupportsResize => true;

    internal static float CalculateBrushDiameter(float width) =>
        CalculateBrushRadius(width) * 2F;

    protected override bool HitTestUnrotated(Point point, int tolerance)
    {
        var hitBounds = Bounds;
        hitBounds.Inflate(tolerance, tolerance);
        if (!hitBounds.Contains(point))
        {
            return false;
        }

        foreach (var block in Blocks)
        {
            var bounds = new Rectangle(block, _blockSize);
            bounds.Inflate(tolerance, tolerance);
            if (bounds.Contains(point))
            {
                return true;
            }
        }
        return false;
    }

    protected override void RenderUnrotated(Graphics graphics, Bitmap source)
    {
        var oldMode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        foreach (var block in Blocks)
        {
            var sampleX = Math.Clamp(block.X + _blockSize.Width / 2, 0, source.Width - 1);
            var sampleY = Math.Clamp(block.Y + _blockSize.Height / 2, 0, source.Height - 1);
            using var brush = new SolidBrush(source.GetPixel(sampleX, sampleY));
            graphics.FillRectangle(brush, block.X, block.Y, _blockSize.Width, _blockSize.Height);
        }

        graphics.SmoothingMode = oldMode;
    }

    public override void SetBounds(Rectangle bounds)
    {
        if (bounds.IsEmpty || Bounds.IsEmpty)
        {
            return;
        }

        if (bounds.Size == Bounds.Size)
        {
            MoveBy(new Point(bounds.X - Bounds.X, bounds.Y - Bounds.Y));
            return;
        }

        var originalBounds = Bounds;
        var originalBlockSize = _blockSize;
        _blockSize = new Size(
            Math.Max(1, (int)Math.Round(
                originalBlockSize.Width * bounds.Width / (double)originalBounds.Width)),
            Math.Max(1, (int)Math.Round(
                originalBlockSize.Height * bounds.Height / (double)originalBounds.Height)));
        Blocks = Blocks
            .Select(block => ScaleBlockOrigin(
                block,
                originalBounds,
                originalBlockSize,
                bounds,
                _blockSize))
            .Distinct()
            .ToArray();
        Bounds = bounds;
    }

    public override void Offset(Point delta) => MoveBy(delta);

    private void MoveBy(Point delta)
    {
        if (delta.IsEmpty)
        {
            return;
        }

        Blocks = Blocks
            .Select(block => new Point(block.X + delta.X, block.Y + delta.Y))
            .ToArray();
        Bounds = new Rectangle(
            Bounds.X + delta.X,
            Bounds.Y + delta.Y,
            Bounds.Width,
            Bounds.Height);
    }

    private static Rectangle CalculateBounds(IReadOnlyList<Point> blocks) => blocks.Count == 0
        ? Rectangle.Empty
        : Rectangle.FromLTRB(
            blocks.Min(block => block.X),
            blocks.Min(block => block.Y),
            blocks.Max(block => block.X) + BlockSize,
            blocks.Max(block => block.Y) + BlockSize);

    private static Point ScaleBlockOrigin(
        Point block,
        Rectangle originalBounds,
        Size originalBlockSize,
        Rectangle resizedBounds,
        Size resizedBlockSize)
    {
        var originalSpanX = Math.Max(0, originalBounds.Width - originalBlockSize.Width);
        var originalSpanY = Math.Max(0, originalBounds.Height - originalBlockSize.Height);
        var resizedSpanX = Math.Max(0, resizedBounds.Width - resizedBlockSize.Width);
        var resizedSpanY = Math.Max(0, resizedBounds.Height - resizedBlockSize.Height);
        var ratioX = originalSpanX == 0
            ? 0D
            : (block.X - originalBounds.Left) / (double)originalSpanX;
        var ratioY = originalSpanY == 0
            ? 0D
            : (block.Y - originalBounds.Top) / (double)originalSpanY;
        return new Point(
            resizedBounds.Left + (int)Math.Round(
                Math.Clamp(ratioX, 0D, 1D) * resizedSpanX,
                MidpointRounding.AwayFromZero),
            resizedBounds.Top + (int)Math.Round(
                Math.Clamp(ratioY, 0D, 1D) * resizedSpanY,
                MidpointRounding.AwayFromZero));
    }

    private static HashSet<Point> BuildBlocks(IEnumerable<Point> points, float width)
    {
        var blocks = new HashSet<Point>();
        var radius = CalculateBrushRadius(width);

        foreach (var point in points)
        {
            var left = (point.X - radius) / BlockSize * BlockSize;
            var top = (point.Y - radius) / BlockSize * BlockSize;
            var right = (point.X + radius) / BlockSize * BlockSize;
            var bottom = (point.Y + radius) / BlockSize * BlockSize;
            for (var x = left; x <= right; x += BlockSize)
            {
                for (var y = top; y <= bottom; y += BlockSize)
                {
                    var centerX = x + BlockSize / 2;
                    var centerY = y + BlockSize / 2;
                    var dx = centerX - point.X;
                    var dy = centerY - point.Y;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        blocks.Add(new Point(x, y));
                    }
                }
            }
        }

        return blocks;
    }

    private static int CalculateBrushRadius(float width) =>
        Math.Max(BlockSize, (int)width * 3);
}

internal sealed class StickerAnnotation : MovableAnnotation
{
    public StickerAnnotation(Bitmap image, Rectangle bounds)
    {
        Image = image;
        Bounds = bounds;
    }

    public Bitmap Image { get; }
    public override Rectangle Bounds { get; protected set; }

    public override bool SupportsResize => true;

    public override bool PreserveAspectRatioWhenResizing => true;

    public override AnnotationCategory Category => AnnotationCategory.Sticker;

    public override void SetBounds(Rectangle bounds) => Bounds = bounds;

    protected override void RenderUnrotated(Graphics graphics, Bitmap source)
    {
        var interpolationMode = graphics.InterpolationMode;
        var pixelOffsetMode = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(Image, Bounds);
        graphics.InterpolationMode = interpolationMode;
        graphics.PixelOffsetMode = pixelOffsetMode;
    }

    public override void Dispose() => Image.Dispose();
}
