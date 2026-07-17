namespace ScreenshotTool.Abstractions;

internal interface IFileLocationService
{
    void OpenFolder(string folderPath);

    void ShowFileInFolder(string filePath);

    void OpenFile(string filePath);
}
