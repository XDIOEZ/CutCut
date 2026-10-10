using ScreenshotTool.Infrastructure.DragDrop;

internal static class ArtifactDragDropTests
{
    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"CutCut-DragTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var imagePath = Path.Combine(folder, "中文 截图.PNG");
        var videoPath = Path.Combine(folder, "录像.mp4");
        var textPath = Path.Combine(folder, "不支持.txt");
        string? temporaryImage = null;
        try
        {
            using var image = new Bitmap(32, 24);
            image.SetPixel(3, 4, Color.FromArgb(128, 30, 80, 160));
            image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            File.WriteAllText(videoPath, "video placeholder");
            File.WriteAllText(textPath, "text");
            var data = WindowsArtifactDragDrop.CreateFileData([imagePath, videoPath, textPath, imagePath]);
            Require(WindowsArtifactDragDrop.GetFiles(data, true).SequenceEqual([imagePath, videoPath]),
                "图库应接收图片与视频、过滤其他文件并去重");
            Require(WindowsArtifactDragDrop.GetFiles(data, false).SequenceEqual([imagePath]),
                "贴图应只接收图片");
            var moveOnly = new DragEventArgs(data, 0, 0, 0, DragDropEffects.Move, DragDropEffects.None);
            Require(WindowsArtifactDragDrop.GetDropEffect(moveOnly, true) == DragDropEffects.None,
                "不得接受只能移动的拖放");
            var copy = new DragEventArgs(data, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
            Require(WindowsArtifactDragDrop.GetDropEffect(copy, true) == DragDropEffects.Copy,
                "标准 Windows 文件拖放应接受复制");
            Require(!WindowsArtifactDragDrop.CanReceive(new DataObject(DataFormats.Text, "text"), true),
                "普通文字不可作为图片或文件拖入");
            var bitmapData = new DataObject(DataFormats.Bitmap, image);
            Require(WindowsArtifactDragDrop.CanReceive(bitmapData, false), "应接受 Windows 位图数据");
            temporaryImage = WindowsArtifactDragDrop.SaveTemporaryImage(image);
            using (var exported = WindowsArtifactDragDrop.LoadImage(temporaryImage))
            {
                Require(exported.Size == image.Size && exported.GetPixel(3, 4) == image.GetPixel(3, 4),
                    "贴图拖出的 PNG 必须保留原始尺寸、颜色与透明度");
            }
            using (var exclusive = new FileStream(temporaryImage, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Require(exclusive.Length > 0, "拖出 PNG 不得留下源文件锁");
            }
            File.Delete(imagePath);
            Require(WindowsArtifactDragDrop.GetFiles(data, false).Length == 0, "已删除的图片不可拖入");
        }
        finally
        {
            if (temporaryImage is not null)
            {
                File.Delete(temporaryImage);
            }
            Directory.Delete(folder, true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
