using ScreenshotTool.Core;

namespace ScreenshotTool.Abstractions;

internal interface IImageSaveService
{
    string SavePng(
        Bitmap image,
        string outputFolder,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null);
}
