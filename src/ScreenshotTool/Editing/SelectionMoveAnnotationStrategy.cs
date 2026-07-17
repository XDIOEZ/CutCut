using ScreenshotTool.Core;

namespace ScreenshotTool.Editing;

internal interface ISelectionMoveAnnotationStrategy
{
    AnnotationCategory MovedCategories { get; }

    void Apply(AnnotationDocument document, Point delta);
}

internal static class SelectionMoveAnnotationStrategyFactory
{
    public static ISelectionMoveAnnotationStrategy Create(StickerSelectionMoveMode mode) => mode switch
    {
        StickerSelectionMoveMode.KeepScreenPosition => KeepStickersAtScreenPositionStrategy.Instance,
        _ => FollowSelectionStrategy.Instance
    };
}

internal sealed class FollowSelectionStrategy : ISelectionMoveAnnotationStrategy
{
    public static FollowSelectionStrategy Instance { get; } = new();

    private FollowSelectionStrategy()
    {
    }

    public AnnotationCategory MovedCategories => AnnotationCategory.All;

    public void Apply(AnnotationDocument document, Point delta) =>
        document.Offset(delta, MovedCategories);
}

internal sealed class KeepStickersAtScreenPositionStrategy : ISelectionMoveAnnotationStrategy
{
    public static KeepStickersAtScreenPositionStrategy Instance { get; } = new();

    private KeepStickersAtScreenPositionStrategy()
    {
    }

    public AnnotationCategory MovedCategories => AnnotationCategory.Drawing;

    public void Apply(AnnotationDocument document, Point delta) =>
        document.Offset(delta, MovedCategories);
}
