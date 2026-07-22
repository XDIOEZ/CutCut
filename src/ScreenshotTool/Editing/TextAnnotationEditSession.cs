namespace ScreenshotTool.Editing;

internal enum TextAnnotationEditorBoundsMode
{
    Content,
    Outer
}

internal sealed record TextAnnotationEditDescriptor(
    MovableAnnotation Annotation,
    Rectangle Bounds,
    string Text,
    Color ForegroundColor,
    float FontSize,
    Size TextPadding,
    FontStyle FontStyle,
    TextAnnotationEditorBoundsMode BoundsMode);

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
        Rectangle editorOuterBounds,
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

        var normalizedText = text.TrimEnd();
        switch (annotation)
        {
            case TextAnnotation transparentText:
                transparentText.UpdateText(editorContentBounds, normalizedText, fontSize);
                break;
            case PastedTextAnnotation pastedText:
                pastedText.UpdateText(editorOuterBounds, normalizedText, fontSize);
                break;
        }

        return annotation;
    }

    public void Cancel() => ActiveAnnotation = null;

    public static bool CanEdit(MovableAnnotation annotation) =>
        annotation is TextAnnotation or PastedTextAnnotation;

    private static TextAnnotationEditDescriptor? CreateDescriptor(MovableAnnotation annotation) =>
        annotation switch
        {
            TextAnnotation text => new TextAnnotationEditDescriptor(
                text,
                text.Bounds,
                text.Text,
                text.Color,
                text.FontSize,
                new Size(4, 3),
                FontStyle.Bold,
                TextAnnotationEditorBoundsMode.Content),
            PastedTextAnnotation text => new TextAnnotationEditDescriptor(
                text,
                text.Bounds,
                text.Text,
                Color.White,
                text.FontSize,
                new Size(8, 6),
                FontStyle.Regular,
                TextAnnotationEditorBoundsMode.Outer),
            _ => null
        };
}
