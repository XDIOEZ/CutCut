using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenshotTool.Ocr;

internal static class OcrImagePreprocessor
{
    private const int MaximumPreparedDimension = 2400;
    private const int EnhancementPadding = 12;
    private const double MaximumScale = 3D;

    public static IReadOnlyList<OcrImageCandidate> CreateCandidates(Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentException("OCR 输入图片尺寸无效。", nameof(source));
        }

        var candidates = new List<OcrImageCandidate>(4);
        try
        {
            candidates.Add(new OcrImageCandidate("original", new Bitmap(source)));

            var scale = Math.Min(
                MaximumScale,
                MaximumPreparedDimension / (double)Math.Max(source.Width, source.Height));
            var scaled = Resize(source, scale);
            candidates.Add(new OcrImageCandidate("scaled-color", scaled));

            using (var enhanced = CreateEnhancedGrayscale(scaled, binary: false))
            {
                candidates.Add(new OcrImageCandidate(
                    "scaled-contrast",
                    AddPadding(enhanced, EnhancementPadding)));
            }

            using (var binary = CreateEnhancedGrayscale(scaled, binary: true))
            {
                candidates.Add(new OcrImageCandidate(
                    "scaled-binary",
                    AddPadding(binary, EnhancementPadding)));
            }

            return candidates;
        }
        catch
        {
            foreach (var candidate in candidates)
            {
                candidate.Dispose();
            }

            throw;
        }
    }

    private static Bitmap Resize(Bitmap source, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        result.SetResolution(
            Math.Max(1F, source.HorizontalResolution),
            Math.Max(1F, source.VerticalResolution));

        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        using var imageAttributes = new ImageAttributes();
        imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, width, height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            imageAttributes);
        return result;
    }

    private static Bitmap CreateEnhancedGrayscale(Bitmap source, bool binary)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.DrawImageUnscaled(source, Point.Empty);
        }

        var bounds = new Rectangle(Point.Empty, result.Size);
        var data = result.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var pixels = new byte[stride * result.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            var histogram = new int[256];
            long luminanceTotal = 0;
            for (var y = 0; y < result.Height; y++)
            {
                var row = GetRowOffset(data.Stride, stride, result.Height, y);
                for (var x = 0; x < result.Width; x++)
                {
                    var offset = row + (x * 4);
                    var luminance = GetLuminance(
                        pixels[offset + 2],
                        pixels[offset + 1],
                        pixels[offset]);
                    histogram[luminance]++;
                    luminanceTotal += luminance;
                }
            }

            var pixelCount = checked(result.Width * result.Height);
            var low = FindPercentile(histogram, pixelCount, 0.01D);
            var high = FindPercentile(histogram, pixelCount, 0.99D);
            if (high - low < 32)
            {
                low = Math.Max(0, low - 16);
                high = Math.Min(255, high + 16);
            }

            var invert = luminanceTotal < (long)pixelCount * 128L;
            var enhancedHistogram = new int[256];
            for (var y = 0; y < result.Height; y++)
            {
                var row = GetRowOffset(data.Stride, stride, result.Height, y);
                for (var x = 0; x < result.Width; x++)
                {
                    var offset = row + (x * 4);
                    var luminance = GetLuminance(
                        pixels[offset + 2],
                        pixels[offset + 1],
                        pixels[offset]);
                    var value = Stretch(luminance, low, high);
                    if (invert)
                    {
                        value = (byte)(255 - value);
                    }

                    pixels[offset] = value;
                    pixels[offset + 1] = value;
                    pixels[offset + 2] = value;
                    pixels[offset + 3] = 255;
                    enhancedHistogram[value]++;
                }
            }

            if (binary)
            {
                var threshold = FindOtsuThreshold(enhancedHistogram, pixelCount);
                for (var y = 0; y < result.Height; y++)
                {
                    var row = GetRowOffset(data.Stride, stride, result.Height, y);
                    for (var x = 0; x < result.Width; x++)
                    {
                        var offset = row + (x * 4);
                        var value = pixels[offset] >= threshold ? (byte)255 : (byte)0;
                        pixels[offset] = value;
                        pixels[offset + 1] = value;
                        pixels[offset + 2] = value;
                    }
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            result.UnlockBits(data);
        }

        return result;
    }

    private static Bitmap AddPadding(Bitmap source, int padding)
    {
        var result = new Bitmap(
            checked(source.Width + (padding * 2)),
            checked(source.Height + (padding * 2)),
            PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.White);
        graphics.DrawImageUnscaled(source, padding, padding);
        return result;
    }

    private static int GetRowOffset(int signedStride, int stride, int height, int y) =>
        signedStride >= 0 ? y * stride : (height - 1 - y) * stride;

    private static int GetLuminance(byte red, byte green, byte blue) =>
        ((red * 77) + (green * 150) + (blue * 29)) >> 8;

    private static byte Stretch(int value, int low, int high)
    {
        if (high <= low)
        {
            return (byte)value;
        }

        return (byte)Math.Clamp(
            ((value - low) * 255) / (high - low),
            0,
            255);
    }

    private static int FindPercentile(int[] histogram, int total, double percentile)
    {
        var target = Math.Clamp((int)Math.Round(total * percentile), 0, total - 1);
        var seen = 0;
        for (var value = 0; value < histogram.Length; value++)
        {
            seen += histogram[value];
            if (seen > target)
            {
                return value;
            }
        }

        return 255;
    }

    private static int FindOtsuThreshold(int[] histogram, int total)
    {
        long weightedTotal = 0;
        for (var value = 0; value < histogram.Length; value++)
        {
            weightedTotal += (long)value * histogram[value];
        }

        long backgroundWeight = 0;
        long backgroundTotal = 0;
        var maximumVariance = double.MinValue;
        var threshold = 128;
        for (var value = 0; value < histogram.Length; value++)
        {
            backgroundWeight += histogram[value];
            if (backgroundWeight == 0)
            {
                continue;
            }

            var foregroundWeight = total - backgroundWeight;
            if (foregroundWeight == 0)
            {
                break;
            }

            backgroundTotal += (long)value * histogram[value];
            var backgroundMean = backgroundTotal / (double)backgroundWeight;
            var foregroundMean = (weightedTotal - backgroundTotal) / (double)foregroundWeight;
            var difference = backgroundMean - foregroundMean;
            var variance = backgroundWeight * (double)foregroundWeight * difference * difference;
            if (variance > maximumVariance)
            {
                maximumVariance = variance;
                threshold = value;
            }
        }

        return threshold;
    }
}

internal sealed record OcrImageCandidate(string Name, Bitmap Image) : IDisposable
{
    public void Dispose() => Image.Dispose();
}
