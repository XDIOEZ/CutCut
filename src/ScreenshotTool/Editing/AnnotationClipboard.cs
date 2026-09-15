using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Editing;

internal sealed class AnnotationClipboard : IDisposable
{
    private const string ClipboardFormat = "CutCut.AnnotationSelection.v1";
    private readonly List<MovableAnnotation> _copies = [];
    private string? _token;
    private Rectangle _bounds;

    // Copies selected objects in document order and publishes their transparent image.
    public void Copy(CaptureAnnotationEditor editor, Bitmap source, IClipboardService clipboard)
    {
        var selected = editor.Document.GetMovableAnnotations()
            .Where(editor.Selection.Contains).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        var copies = new List<MovableAnnotation>();
        try
        {
            foreach (var annotation in selected)
            {
                copies.Add(annotation.Clone());
            }

            var bounds = editor.Selection.Bounds;
            using var image = editor.RenderSelectedImage() ?? Render(copies, bounds, source);
            var token = Guid.NewGuid().ToString("N");
            clipboard.SetImageWithData(image, ClipboardFormat, token);
            Dispose();
            _copies.AddRange(copies);
            copies.Clear();
            _bounds = bounds;
            _token = token;
        }
        finally
        {
            foreach (var copy in copies)
            {
                copy.Dispose();
            }
        }
    }

    // Pastes independent editable copies only while this session's clipboard token is current.
    public bool TryPaste(CaptureAnnotationEditor editor, IClipboardService clipboard, Point anchor)
    {
        if (_token is null || clipboard.GetData(ClipboardFormat) != _token)
        {
            return false;
        }

        var copies = new List<MovableAnnotation>();
        try
        {
            var offset = new Point(anchor.X - _bounds.Left, anchor.Y - _bounds.Top);
            foreach (var original in _copies)
            {
                var copy = original.Clone();
                copies.Add(copy);
                copy.Offset(offset);
            }

            editor.ResetHitCycle();
            editor.Selection.Clear();
            foreach (var copy in copies)
            {
                editor.Document.Add(copy);
                editor.Selection.Add(copy);
            }
            copies.Clear();
            return true;
        }
        finally
        {
            foreach (var copy in copies)
            {
                copy.Dispose();
            }
        }
    }

    // Renders selected annotations without the desktop or selection handles, including stroke margins.
    private static Bitmap Render(IReadOnlyList<MovableAnnotation> annotations, Rectangle bounds, Bitmap source)
    {
        bounds.Inflate(annotations.Max(annotation => annotation.RenderMargin),
            annotations.Max(annotation => annotation.RenderMargin));
        var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(-bounds.Left, -bounds.Top);
            foreach (var annotation in annotations)
            {
                annotation.Render(graphics, source);
            }
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    // Releases the session-owned snapshots without changing the system clipboard image.
    public void Dispose()
    {
        foreach (var copy in _copies)
        {
            copy.Dispose();
        }
        _copies.Clear();
        _token = null;
    }
}
