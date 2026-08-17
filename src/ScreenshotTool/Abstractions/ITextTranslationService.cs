namespace ScreenshotTool.Abstractions;

internal interface ITextTranslationService
{
    Task<string> TranslateToSimplifiedChineseAsync(
        string text,
        CancellationToken cancellationToken);
}
