namespace ScreenshotTool.Editing;

internal sealed record TextAnnotationEditDescriptor(
    Rectangle Bounds,
    string Text,
    Color ForegroundColor,
    float FontSize);

internal sealed class TextAnnotationEditSession
{
    public MovableAnnotation? ActiveAnnotation { get; private set; }

    public bool TryBegin(
        AnnotationDocument document,
        MovableAnnotation annotation,
        out TextAnnotationEditDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(annotation);

        descriptor = CreateDescriptor(annotation);
        if (descriptor is null || !document.Contains(annotation))
        {
            return false;
        }

        ActiveAnnotation = annotation;
        return true;
    }

    public MovableAnnotation? End(
        bool commit,
        Rectangle editorContentBounds,
        string text,
        float fontSize)
    {
        var annotation = ActiveAnnotation;
        ActiveAnnotation = null;
        if (annotation is null || !commit || string.IsNullOrWhiteSpace(text))
        {
            return annotation;
        }

        if (annotation is TextAnnotation textAnnotation)
        {
            textAnnotation.UpdateText(
                editorContentBounds,
                text.TrimEnd(),
                fontSize);
        }

        return annotation;
    }

    public void Cancel() => ActiveAnnotation = null;

    public static bool CanEdit(MovableAnnotation annotation) =>
        annotation is TextAnnotation;

    private static TextAnnotationEditDescriptor? CreateDescriptor(MovableAnnotation annotation) =>
        annotation switch
        {
            TextAnnotation text => new TextAnnotationEditDescriptor(
                text.Bounds,
                text.Text,
                text.Color,
                text.FontSize),
            _ => null
        };
}
