using System.Drawing.Imaging;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure;

internal sealed class ImageSaveService : IImageSaveService
{
    private const long JpegQuality = 92L;

    // Saves an image using the shared artifact folder, naming, and format policies.
    public string SaveImage(
        Bitmap image,
        string outputFolder,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null,
        bool organizeByDate = false)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentException("截图内容为空。", nameof(image));
        }

        imageFormat = ScreenshotImageFormatPolicy.Normalize(imageFormat);
        outputFolder = ArtifactOutputFolderPolicy.Resolve(
            outputFolder,
            organizeByDate,
            DateTime.Now);
        Directory.CreateDirectory(outputFolder);
        var existingImageNames = Directory
            .EnumerateFiles(outputFolder, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(ScreenshotImageFormatPolicy.IsSupportedFileName)
            .OfType<string>();
        var fileName = ScreenshotFileNamePolicy.CreateFileName(
            fileNameMode,
            DateTime.Now,
            existingImageNames,
            imageTexts,
            imageFormat);
        var path = Path.Combine(outputFolder, fileName);

        SaveEncodedImage(image, path, imageFormat);
        return path;
    }

    // Runs image encoding and file IO on a worker while the caller retains bitmap ownership.
    public Task<string> SaveImageAsync(
        Bitmap image,
        string outputFolder,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null,
        bool organizeByDate = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(
            () => SaveImage(
                image,
                outputFolder,
                imageFormat,
                fileNameMode,
                imageTexts,
                organizeByDate),
            cancellationToken);
    }

    // Dispatches lossless PNG and high-quality JPEG encoding without leaking codec details to callers.
    private static void SaveEncodedImage(
        Bitmap image,
        string path,
        ScreenshotImageFormat imageFormat)
    {
        if (imageFormat == ScreenshotImageFormat.Jpeg)
        {
            SaveJpeg(image, path);
            return;
        }

        image.Save(path, ImageFormat.Png);
    }

    // Encodes JPEG at a text-friendly quality and replaces unsupported transparency with white.
    private static void SaveJpeg(Bitmap image, string path)
    {
        var encoder = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(candidate => candidate.FormatID == ImageFormat.Jpeg.Guid) ??
            throw new NotSupportedException("当前系统没有可用的 JPEG 编码器。");
        using var opaqueImage = CreateOpaqueJpegSource(image);
        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality,
            JpegQuality);
        opaqueImage.Save(path, encoder, encoderParameters);
    }

    // Flattens alpha against white so transparent pixels never become unexpected black areas in JPEG.
    private static Bitmap CreateOpaqueJpegSource(Bitmap image)
    {
        var opaqueImage = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        try
        {
            using var graphics = Graphics.FromImage(opaqueImage);
            graphics.Clear(Color.White);
            graphics.DrawImageUnscaled(image, Point.Empty);
            return opaqueImage;
        }
        catch
        {
            opaqueImage.Dispose();
            throw;
        }
    }
}
