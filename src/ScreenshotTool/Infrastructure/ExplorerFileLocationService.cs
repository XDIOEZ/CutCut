using System.Diagnostics;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class ExplorerFileLocationService : IFileLocationService
{
    public void OpenFolder(string folderPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{Path.GetFullPath(folderPath)}\"",
            UseShellExecute = true
        });
    }

    public void ShowFileInFolder(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            var folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                OpenFolder(folder);
            }
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = BuildSelectArguments(fullPath),
            UseShellExecute = true
        });
    }

    public void OpenFile(string filePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(filePath),
            UseShellExecute = true
        });
    }

    internal static string BuildSelectArguments(string filePath) => $"/select,\"{filePath}\"";
}
