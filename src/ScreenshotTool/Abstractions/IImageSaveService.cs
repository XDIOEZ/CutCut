using ScreenshotTool.Core;

namespace ScreenshotTool.Abstractions;

internal interface IImageSaveService
{
    // Saves an image synchronously for callers whose contract requires an immediate path.
    string SaveImage(
        Bitmap image,
        string outputFolder,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null,
        bool organizeByDate = false);

    // Encodes and saves an image without occupying the caller's UI thread.
    Task<string> SaveImageAsync(
        Bitmap image,
        string outputFolder,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null,
        bool organizeByDate = false,
        CancellationToken cancellationToken = default);
}
