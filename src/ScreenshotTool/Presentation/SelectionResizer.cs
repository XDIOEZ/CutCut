namespace ScreenshotTool.Presentation;

[Flags]
internal enum SelectionResizeEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8
}

internal static class SelectionResizer
{
    public static SelectionResizeEdges HitTest(Rectangle selection, Point point, int tolerance)
    {
        if (selection.IsEmpty)
        {
            return SelectionResizeEdges.None;
        }

        var hitBounds = selection;
        hitBounds.Inflate(tolerance, tolerance);
        if (!hitBounds.Contains(point))
        {
            return SelectionResizeEdges.None;
        }

        var right = selection.Right - 1;
        var bottom = selection.Bottom - 1;
        var leftDistance = Math.Abs(point.X - selection.Left);
        var rightDistance = Math.Abs(point.X - right);
        var topDistance = Math.Abs(point.Y - selection.Top);
        var bottomDistance = Math.Abs(point.Y - bottom);

        var edges = SelectionResizeEdges.None;
        if (leftDistance <= tolerance || rightDistance <= tolerance)
        {
            edges |= leftDistance <= rightDistance
                ? SelectionResizeEdges.Left
                : SelectionResizeEdges.Right;
        }
        if (topDistance <= tolerance || bottomDistance <= tolerance)
        {
            edges |= topDistance <= bottomDistance
                ? SelectionResizeEdges.Top
                : SelectionResizeEdges.Bottom;
        }

        return edges;
    }

    public static Rectangle Resize(
        Rectangle original,
        SelectionResizeEdges edges,
        Point pointer,
        Rectangle limits,
        int minimumSize)
    {
        var left = original.Left;
        var top = original.Top;
        var right = original.Right;
        var bottom = original.Bottom;

        if (edges.HasFlag(SelectionResizeEdges.Left))
        {
            left = Math.Clamp(pointer.X, limits.Left, right - minimumSize);
        }
        else if (edges.HasFlag(SelectionResizeEdges.Right))
        {
            right = Math.Clamp(pointer.X + 1, left + minimumSize, limits.Right);
        }

        if (edges.HasFlag(SelectionResizeEdges.Top))
        {
            top = Math.Clamp(pointer.Y, limits.Top, bottom - minimumSize);
        }
        else if (edges.HasFlag(SelectionResizeEdges.Bottom))
        {
            bottom = Math.Clamp(pointer.Y + 1, top + minimumSize, limits.Bottom);
        }

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public static Cursor GetCursor(SelectionResizeEdges edges)
    {
        var horizontal = edges & (SelectionResizeEdges.Left | SelectionResizeEdges.Right);
        var vertical = edges & (SelectionResizeEdges.Top | SelectionResizeEdges.Bottom);

        if (horizontal != 0 && vertical != 0)
        {
            var northWestToSouthEast =
                edges.HasFlag(SelectionResizeEdges.Left) && edges.HasFlag(SelectionResizeEdges.Top) ||
                edges.HasFlag(SelectionResizeEdges.Right) && edges.HasFlag(SelectionResizeEdges.Bottom);
            return northWestToSouthEast ? Cursors.SizeNWSE : Cursors.SizeNESW;
        }

        if (horizontal != 0)
        {
            return Cursors.SizeWE;
        }

        return vertical != 0 ? Cursors.SizeNS : Cursors.Default;
    }
}
