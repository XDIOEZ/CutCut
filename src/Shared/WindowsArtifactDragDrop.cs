using System.Collections.Specialized;
using System.Drawing.Imaging;

#if PINNED_IMAGE_MODULE
namespace ScreenshotTool.PinnedImage.DragDrop;
#else
namespace ScreenshotTool.Infrastructure.DragDrop;
#endif

// Linked into the host and pin module without introducing an assembly dependency.
internal static class WindowsArtifactDragDrop
{
    public static bool IsImage(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

    public static string[] GetFiles(IDataObject? data, bool includeVideo)
    {
        if (data?.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return [];
        }
        return paths.Where(path => File.Exists(path) &&
                (IsImage(path) || (includeVideo &&
                    string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool CanReceive(IDataObject? data, bool includeVideo) =>
        GetFiles(data, includeVideo).Length > 0 ||
        data?.GetDataPresent(DataFormats.Bitmap) == true;

    public static DragDropEffects GetDropEffect(DragEventArgs e, bool includeVideo) =>
        (e.AllowedEffect & DragDropEffects.Copy) != 0 && CanReceive(e.Data, includeVideo)
            ? DragDropEffects.Copy : DragDropEffects.None;

    public static DataObject CreateFileData(IEnumerable<string> paths)
    {
        var files = new StringCollection();
        files.AddRange(paths.Select(Path.GetFullPath).ToArray());
        var data = new DataObject();
        data.SetFileDropList(files);
        // CFSTR_PREFERREDDROPEFFECT: ask Explorer to copy, preserving the source.
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)));
        return data;
    }

    public static Bitmap LoadImage(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var image = Image.FromStream(stream, true, true);
        return (Bitmap)image.Clone();
    }

    // Keep PNGs after the OLE loop: some consumers read the file asynchronously.
    public static string SaveTemporaryImage(Bitmap image)
    {
        var folder = Path.Combine(Path.GetTempPath(), "CutCut", "DragDrop");
        Directory.CreateDirectory(folder);
        foreach (var oldFile in Directory.EnumerateFiles(folder, "轻截贴图-*.png").Take(64))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(oldFile) < DateTime.UtcNow.AddDays(-7))
                {
                    File.Delete(oldFile);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var path = Path.Combine(folder, $"轻截贴图-{Guid.NewGuid():N}.png");
        try
        {
            image.Save(path, ImageFormat.Png);
            return path;
        }
        catch
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }
}
