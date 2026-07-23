using System.Drawing.Imaging;
using RapidOcrNet;
using SkiaSharp;

namespace ScreenshotTool.PaddleOcr;

internal sealed class PaddleOcrRecognizer(
    string moduleDirectory,
    PaddleOcrVariant variant) : IPaddleOcrRecognizer
{
    private readonly object _sync = new();
    private RapidOcr? _engine;
    private bool _disposed;

    public async Task<string> RecognizeAsync(
        Bitmap image,
        CancellationToken cancellationToken)
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

                string text;
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    _engine ??= CreateEngine();
                    var result = _engine.Detect(bitmap, RapidOcrOptions.PPOCRv6);
                    text = result.StrRes;
                }

                cancellationToken.ThrowIfCancellationRequested();
                return PaddleOcrTextNormalizer.Normalize(text);
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
