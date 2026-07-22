using System.Drawing.Imaging;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Core;

namespace ScreenshotTool.Infrastructure;

internal sealed class PngImageSaveService : IImageSaveService
{
    public string SavePng(
        Bitmap image,
        string outputFolder,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentException("截图内容为空。", nameof(image));
        }

        Directory.CreateDirectory(outputFolder);
        var fileName = ScreenshotFileNamePolicy.CreateFileName(
            fileNameMode,
            DateTime.Now,
            Directory.EnumerateFiles(outputFolder, "*.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>(),
            imageTexts);
        var path = Path.Combine(outputFolder, fileName);

        image.Save(path, ImageFormat.Png);
        return path;
    }
}
