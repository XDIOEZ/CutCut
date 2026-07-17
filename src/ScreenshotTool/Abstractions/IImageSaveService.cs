namespace ScreenshotTool.Abstractions;

internal interface IImageSaveService
{
    string SavePng(Bitmap image, string outputFolder);
}
