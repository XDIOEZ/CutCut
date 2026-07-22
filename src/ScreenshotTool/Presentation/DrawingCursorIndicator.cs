using System.Drawing.Drawing2D;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Presentation;

internal sealed class DrawingCursorIndicator : IDisposable
{
    private const int OutlineMargin = 4;

    private readonly DrawingCursorShape _shape;

    public DrawingCursorIndicator(DrawingCursorShape shape)
    {
        _shape = Enum.IsDefined(shape) ? shape : DrawingCursorShape.Circle;
    }

    public bool Visible { get; private set; }

    public Point Center { get; private set; }

    public int Diameter { get; private set; }

    public bool SystemCursorHidden { get; private set; }

    public Rectangle DirtyBounds => Visible
        ? Inflate(GetOutlineBounds(Center, Diameter), OutlineMargin)
        : Rectangle.Empty;

    public static bool Supports(EditorTool tool) => tool is EditorTool.Pen or EditorTool.Mosaic;

    public static int CalculateClientDiameter(float editingDiameter, double zoom)
    {
        if (editingDiameter <= 0F || zoom <= 0D)
        {
            return 0;
        }

        return Math.Max(3, (int)Math.Round(
            editingDiameter * zoom,
            MidpointRounding.AwayFromZero));
    }

    public static Rectangle GetOutlineBounds(Point center, int diameter)
    {
        var safeDiameter = Math.Max(1, diameter);
        return new Rectangle(
            center.X - safeDiameter / 2,
            center.Y - safeDiameter / 2,
            safeDiameter,
            safeDiameter);
    }

    public Rectangle Update(Point center, float editingDiameter, double zoom)
    {
        var previous = DirtyBounds;
        Center = center;
        Diameter = CalculateClientDiameter(editingDiameter, zoom);
        Visible = Diameter > 0;
        return Union(previous, DirtyBounds);
    }

    public Rectangle Hide()
    {
        var previous = DirtyBounds;
        Visible = false;
        return previous;
    }

    public void HideSystemCursor()
    {
        if (SystemCursorHidden)
        {
            return;
        }

        Cursor.Hide();
        SystemCursorHidden = true;
    }

    public void ShowSystemCursor()
    {
        if (!SystemCursorHidden)
        {
            return;
        }

        Cursor.Show();
        SystemCursorHidden = false;
    }

    public void Draw(Graphics graphics)
    {
        if (!Visible)
        {
            return;
        }

        var bounds = GetOutlineBounds(Center, Diameter);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(Color.FromArgb(24, 255, 255, 255));
            using var darkOutline = new Pen(Color.FromArgb(220, 15, 23, 42), 3F);
            using var lightOutline = new Pen(Color.FromArgb(245, 255, 255, 255), 1F);
            if (_shape == DrawingCursorShape.Square)
            {
                graphics.FillRectangle(fill, bounds);
                graphics.DrawRectangle(darkOutline, bounds);
                graphics.DrawRectangle(lightOutline, bounds);
            }
            else
            {
                graphics.FillEllipse(fill, bounds);
                graphics.DrawEllipse(darkOutline, bounds);
                graphics.DrawEllipse(lightOutline, bounds);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    public void Dispose() => ShowSystemCursor();

    private static Rectangle Inflate(Rectangle bounds, int margin)
    {
        bounds.Inflate(margin, margin);
        return bounds;
    }

    private static Rectangle Union(Rectangle first, Rectangle second)
    {
        if (first.IsEmpty)
        {
            return second;
        }

        return second.IsEmpty ? first : Rectangle.Union(first, second);
    }
}
