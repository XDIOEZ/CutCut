namespace ScreenshotTool.Abstractions;

internal interface ISavedScreenshotService
{
    bool IsSupportedImage(string path);

    Bitmap LoadForEditing(string folderPath, string filePath);

    void MoveToRecycleBin(string folderPath, string filePath);
}
