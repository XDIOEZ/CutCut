using System.Globalization;
using System.Text;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.PinnedImage;

// Owns reading-order selection and source-pixel geometry independently of the floating window.
internal sealed class ImageTextSelection
{
    private readonly List<TextCell> _cells = [];
    private readonly string _text;
    private readonly TextCell[][] _lines;
    private int _anchor;
    private int _caret;

    // Builds grapheme cells once; measured advances approximate positions within OCR word boxes.
    public ImageTextSelection(ImageTextRecognitionResult result)
    {
        var text = new StringBuilder();
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(FontFamily.GenericSansSerif, 100, GraphicsUnit.Pixel);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        ImageTextRegion? previous = null;
        foreach (var region in result.Regions.OrderBy(region => region.LineIndex))
        {
            if (string.IsNullOrWhiteSpace(region.Text))
            {
                continue;
            }
            if (previous is not null)
            {
                if (previous.LineIndex != region.LineIndex)
                {
                    text.Append(Environment.NewLine);
                }
                else if (region.LeadingText is not null)
                {
                    text.Append(region.LeadingText);
                }
            }
            var elements = GetTextElements(region.Text);
            var widths = elements.Select(element => Math.Max(1F,
                graphics.MeasureString(element, font, PointF.Empty, format).Width)).ToArray();
            var totalWidth = widths.Sum();
            var advance = 0F;
            for (var index = 0; index < elements.Count; index++)
            {
                var start = advance / totalWidth;
                advance += widths[index];
                var end = advance / totalWidth;
                var offset = text.Length;
                text.Append(elements[index]);
                _cells.Add(new TextCell(offset, text.Length, region.LineIndex,
                    Lerp(region.TopLeft, region.TopRight, start),
                    Lerp(region.TopLeft, region.TopRight, end),
                    Lerp(region.BottomLeft, region.BottomRight, end),
                    Lerp(region.BottomLeft, region.BottomRight, start)));
            }
            previous = region;
        }
        _text = text.ToString();
        _lines = _cells.GroupBy(cell => cell.LineIndex).Select(line => line.ToArray()).ToArray();
    }

    public bool HasText => _cells.Count > 0;
    public bool HasSelection => _anchor != _caret;
    public string SelectedText => _text[Math.Min(_anchor, _caret)..Math.Max(_anchor, _caret)];

    // Starts a selection or extends the previous anchor with Shift.
    public void Begin(PointF point, bool extend)
    {
        _caret = HitTest(point);
        if (!extend)
        {
            _anchor = _caret;
        }
    }

    // Extends the selected reading-order range while the pointer is captured.
    public void Extend(PointF point) => _caret = HitTest(point);

    // Selects all text while preserving recognized line breaks.
    public void SelectAll()
    {
        _anchor = 0;
        _caret = _text.Length;
    }

    // Removes only the active selection, keeping recognized positions available.
    public void Clear() => _anchor = _caret;

    // Paints translucent blue directly over selected source glyphs at the current window scale.
    public void Draw(Graphics graphics, Size sourceSize, Size clientSize)
    {
        var start = Math.Min(_anchor, _caret);
        var end = Math.Max(_anchor, _caret);
        var scaleX = clientSize.Width / (float)sourceSize.Width;
        var scaleY = clientSize.Height / (float)sourceSize.Height;
        using var brush = new SolidBrush(Color.FromArgb(110, 0, 120, 215));
        foreach (var cell in _cells)
        {
            if (cell.Start >= start && cell.End <= end)
            {
                graphics.FillPolygon(brush, new PointF[]
                {
                    new(cell.TopLeft.X * scaleX, cell.TopLeft.Y * scaleY),
                    new(cell.TopRight.X * scaleX, cell.TopRight.Y * scaleY),
                    new(cell.BottomRight.X * scaleX, cell.BottomRight.Y * scaleY),
                    new(cell.BottomLeft.X * scaleX, cell.BottomLeft.Y * scaleY)
                });
            }
        }
    }

    // Finds the nearest glyph and chooses its leading or trailing insertion position.
    private int HitTest(PointF point)
    {
        var line = _lines.MinBy(cells => GetLineDistance(cells, point));
        if (line is null)
        {
            return 0;
        }
        var bestDistance = double.MaxValue;
        var position = 0;
        foreach (var cell in line)
        {
            var left = Lerp(cell.TopLeft, cell.BottomLeft, 0.5F);
            var right = Lerp(cell.TopRight, cell.BottomRight, 0.5F);
            var dx = right.X - left.X;
            var dy = right.Y - left.Y;
            var lengthSquared = dx * dx + dy * dy;
            var fraction = lengthSquared > 0
                ? Math.Clamp(((point.X - left.X) * dx + (point.Y - left.Y) * dy) / lengthSquared, 0, 1)
                : 0;
            var nearest = Lerp(left, right, fraction);
            var distance = Math.Pow(point.X - nearest.X, 2) + Math.Pow(point.Y - nearest.Y, 2);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                position = fraction < 0.5F ? cell.Start : cell.End;
            }
        }
        return position;
    }

    // Chooses the nearest reading line before columns, so dragging past its end stays on that line.
    private static double GetLineDistance(TextCell[] cells, PointF point)
    {
        var start = Lerp(cells[0].TopLeft, cells[0].BottomLeft, 0.5F);
        var end = Lerp(cells[^1].TopRight, cells[^1].BottomRight, 0.5F);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        return length > 0
            ? Math.Abs(dx * (point.Y - start.Y) - dy * (point.X - start.X)) / length
            : Math.Abs(point.Y - start.Y);
    }

    // Keeps surrogate pairs and combining marks together during selection and copying.
    private static List<string> GetTextElements(string text)
    {
        var result = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            result.Add(enumerator.GetTextElement());
        }
        return result;
    }

    // Interpolates along a detected text edge, including slanted quadrilaterals.
    private static PointF Lerp(PointF start, PointF end, float fraction) =>
        new(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction);

    private sealed record TextCell(int Start, int End, int LineIndex,
        PointF TopLeft, PointF TopRight, PointF BottomRight, PointF BottomLeft);
}
