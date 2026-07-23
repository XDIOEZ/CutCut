using System.Drawing.Drawing2D;
using Microsoft.VisualBasic.FileIO;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class SavedScreenshotService(Action<string>? recycleFile = null)
    : ISavedScreenshotService
{
    private readonly Action<string> _recycleFile = recycleFile ?? RecycleFile;

    public bool IsSupportedImage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

    public Bitmap LoadForEditing(string folderPath, string filePath)
    {
        var validatedPath = ValidateScreenshotPath(folderPath, filePath);
        using var stream = new FileStream(
            validatedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var source = Image.FromStream(
            stream,
            useEmbeddedColorManagement: true,
            validateImageData: true);
        var editable = new Bitmap(
            source.Width,
            source.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(editable);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(
                source,
                new Rectangle(Point.Empty, editable.Size),
                0,
                0,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);
            return editable;
        }
        catch
        {
            editable.Dispose();
            throw;
        }
    }

    public void MoveToRecycleBin(string folderPath, string filePath)
    {
        var validatedPath = ValidateScreenshotPath(folderPath, filePath);
        _recycleFile(validatedPath);
    }

    private string ValidateScreenshotPath(string folderPath, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullFolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        var fullFilePath = Path.GetFullPath(filePath);
        var parentPath = Path.GetDirectoryName(fullFilePath);
        if (string.IsNullOrEmpty(parentPath) ||
            !string.Equals(
                Path.TrimEndingDirectorySeparator(parentPath),
                fullFolderPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只能管理当前截图保存目录中的图片。");
        }
        if (!IsSupportedImage(fullFilePath))
        {
            throw new InvalidOperationException("选中的文件不是受支持的截图格式。");
        }
        if (!File.Exists(fullFilePath))
        {
            throw new FileNotFoundException("选中的截图文件已经不存在。", fullFilePath);
        }

        return fullFilePath;
    }

    private static void RecycleFile(string path)
    {
        FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);
    }
}
