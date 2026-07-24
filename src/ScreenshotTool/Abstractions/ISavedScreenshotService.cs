namespace ScreenshotTool.Abstractions;

internal interface ISavedScreenshotService
{
    bool IsSupportedImage(string path);

    bool IsSupportedVideo(string path);

    Bitmap LoadForEditing(string folderPath, string filePath);

    void MoveToRecycleBin(string folderPath, string filePath);
}
