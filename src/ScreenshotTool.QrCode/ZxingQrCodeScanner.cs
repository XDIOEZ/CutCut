using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

namespace ScreenshotTool.QrCode;

internal sealed class ZxingQrCodeScanner : IQrCodeScanner
{
    public Task<IReadOnlyList<string>> ScanAsync(
        Bitmap image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run<IReadOnlyList<string>>(
            () => Decode(image, cancellationToken),
            CancellationToken.None);
    }

    private static IReadOnlyList<string> Decode(
        Bitmap image,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var normalizedImage = new Bitmap(
            image.Width,
            image.Height,
            PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalizedImage))
        {
            graphics.Clear(Color.White);
            graphics.DrawImageUnscaled(image, Point.Empty);
        }

        var bounds = new Rectangle(Point.Empty, normalizedImage.Size);
        var imageData = normalizedImage.LockBits(
            bounds,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stride = Math.Abs(imageData.Stride);
            var pixels = new byte[checked(stride * imageData.Height)];
            Marshal.Copy(imageData.Scan0, pixels, 0, pixels.Length);

            var source = new RGBLuminanceSource(
                pixels,
                imageData.Width,
                imageData.Height,
                RGBLuminanceSource.BitmapFormat.BGRA32);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = [BarcodeFormat.QR_CODE],
                    TryHarder = true,
                    TryInverted = true
                }
            };
            var decoded = reader.DecodeMultiple(source) ?? [];
            cancellationToken.ThrowIfCancellationRequested();

            return decoded
                .Select(result => result.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            normalizedImage.UnlockBits(imageData);
        }
    }
}
