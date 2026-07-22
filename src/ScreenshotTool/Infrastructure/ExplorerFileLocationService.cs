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

    public void OpenWebPage(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("只允许打开 HTTP 或 HTTPS 网页。", nameof(uri));
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }

    internal static string BuildSelectArguments(string filePath) => $"/select,\"{filePath}\"";
}
