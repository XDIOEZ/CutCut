using System.Drawing.Imaging;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class PngImageSaveService : IImageSaveService
{
    public string SavePng(Bitmap image, string outputFolder)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new ArgumentException("截图内容为空。", nameof(image));
        }

        Directory.CreateDirectory(outputFolder);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        var path = Path.Combine(outputFolder, $"截图_{timestamp}.png");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(outputFolder, $"截图_{timestamp}_{suffix++}.png");
        }

        image.Save(path, ImageFormat.Png);
        return path;
    }
}
