using System.Drawing.Imaging;
using RapidOcrNet;
using SkiaSharp;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.PaddleOcr;

internal sealed class PaddleOcrRecognizer(
    string moduleDirectory,
    PaddleOcrVariant variant) : IPaddleOcrRecognizer
{
    private readonly object _sync = new();
    private RapidOcr? _engine;
    private bool _disposed;

    // Preserves the existing normalized text output for the screenshot OCR command.
    public async Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken) =>
        PaddleOcrTextNormalizer.Normalize((await RecognizeCoreAsync(image, cancellationToken)).StrRes);

    // Retains detected quadrilaterals so selection follows text in the original image.
    public async Task<ImageTextRecognitionResult> RecognizeImageTextAsync(
        Bitmap image, CancellationToken cancellationToken)
    {
        var result = await RecognizeCoreAsync(image, cancellationToken, spatial: true);
        var regions = new List<ImageTextRegion>();
        var lineIndex = 0;
        foreach (var block in result.TextBlocks)
        {
            if (block.WordResults is { Length: > 0 })
            {
                var textOffset = 0;
                foreach (var word in block.WordResults)
                {
                    var wordOffset = block.Text.IndexOf(word.Text, textOffset, StringComparison.Ordinal);
                    var leadingText = wordOffset >= 0 ? block.Text[textOffset..wordOffset] : null;
                    AddRegion(regions, word.Text, word.BoxPoints, lineIndex, leadingText);
                    if (wordOffset >= 0)
                    {
                        textOffset = wordOffset + word.Text.Length;
                    }
                }
            }
            else
            {
                AddRegion(regions, block.Text, block.BoxPoints, lineIndex);
            }
            lineIndex++;
        }
        return new ImageTextRecognitionResult(regions.ToArray());
    }

    // Copies engine-owned geometry into contract-only values before the recognizer is released.
    private static void AddRegion(List<ImageTextRegion> regions, string text, SKPointI[] points,
        int lineIndex, string? leadingText = null)
    {
        if (string.IsNullOrWhiteSpace(text) || points.Length != 4)
        {
            return;
        }
        regions.Add(new ImageTextRegion(text, lineIndex,
            new PointF(points[0].X, points[0].Y), new PointF(points[1].X, points[1].Y),
            new PointF(points[2].X, points[2].Y), new PointF(points[3].X, points[3].Y), leadingText));
    }

    // Shares model initialization and serialized inference across both result formats.
    private async Task<OcrResult> RecognizeCoreAsync(
        Bitmap image,
        CancellationToken cancellationToken,
        bool spatial = false)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var encodedImage = new MemoryStream();
                image.Save(encodedImage, ImageFormat.Png);
                encodedImage.Position = 0;
                using var bitmap = SKBitmap.Decode(encodedImage) ??
                                   throw new InvalidOperationException(
                                       "PP-OCR 无法读取当前截图选区。");

                OcrResult result;
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    _engine ??= CreateEngine();
                    var options = spatial
                        ? RapidOcrOptions.PPOCRv6 with { ReturnWordBox = true, ReturnSingleCharBox = true }
                        : RapidOcrOptions.PPOCRv6;
                    result = _engine.Detect(bitmap, options);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            },
            cancellationToken);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _engine?.Dispose();
            _engine = null;
        }
    }

    private RapidOcr CreateEngine()
    {
        var models = PaddleOcrModelFiles.Resolve(moduleDirectory, variant)
            .CreateModelSet(variant);
        var engine = new RapidOcr();
        try
        {
            engine.InitModels(models);
            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }
}
