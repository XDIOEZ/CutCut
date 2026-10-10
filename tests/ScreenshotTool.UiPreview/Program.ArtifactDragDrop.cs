using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Infrastructure;
using ScreenshotTool.Infrastructure.DragDrop;
using ScreenshotTool.PinnedImage;
using ScreenshotTool.Presentation.Pages;

namespace ScreenshotTool.UiPreview;

internal static partial class Program
{
    private static void RunPinnedImageCleanPreview(string output)
    {
        using var image = new Bitmap(240, 160);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.CornflowerBlue);
        }
        using var pin = new PinnedImageForm((Bitmap)image.Clone(),
            new Rectangle(120, 120, 240, 160), new DragTestImageHost());
        pin.Show();
        PumpToolbarInput();
        RequireToolbar(pin.Controls.Count == 0, "贴图应保持无覆盖控件");
        using var rendered = new Bitmap(pin.ClientSize.Width, pin.ClientSize.Height);
        pin.DrawToBitmap(rendered, pin.ClientRectangle);
        for (var y = 8; y < 32; y++)
        {
            for (var x = rendered.Width - 48; x < rendered.Width - 8; x++)
            {
                RequireToolbar(rendered.GetPixel(x, y).ToArgb() == Color.CornflowerBlue.ToArgb(),
                    "原拖出按钮区域未显示原图");
            }
        }
        var fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        rendered.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
        pin.Close();
        Console.WriteLine("PASS: rendered pin has no child controls; former drag button area matches the original image.");
    }

    private static void RunArtifactDragDropInteraction()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"CutCut-OleTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var originalPointer = Cursor.Position;
        var area = Screen.PrimaryScreen!.WorkingArea;
        var savedImage = Path.Combine(folder, "中文截图.png");
        var savedVideo = Path.Combine(folder, "录屏.mp4");
        var externalImage = Path.Combine(folder, "外部图片.png");
        try
        {
            using var pixels = new Bitmap(240, 160);
            using (var graphics = Graphics.FromImage(pixels))
            {
                graphics.Clear(Color.CornflowerBlue);
            }
            pixels.SetPixel(35, 45, Color.FromArgb(128, 10, 20, 30));
            pixels.Save(savedImage);
            pixels.Save(externalImage);
            File.WriteAllText(savedVideo, "video placeholder");
            var locationService = new DragTestLocations();
            using var page = new ScreenshotGalleryPage(folder, locationService,
                new SavedScreenshotService(), new PreviewClipboardService())
            { Dock = DockStyle.Fill };
            using var gallery = new Form
            {
                Text = "轻截拖拽验证 - 图库",
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(area.X + 20, area.Y + 20, 700, 460)
            };
            gallery.Controls.Add(page);
            using var target = new Form
            {
                Text = "Windows OLE 接收目标",
                TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(area.X + 750, area.Y + 20, 260, 300),
                AllowDrop = true
            };
            string[] received = [];
            target.DragEnter += (_, e) => e.Effect = DragDropEffects.Copy;
            target.DragOver += (_, e) => e.Effect = DragDropEffects.Copy;
            target.DragDrop += (_, e) => received = (string[])e.Data!.GetData(DataFormats.FileDrop)!;
            gallery.Show();
            target.Show();
            page.RefreshScreenshots();
            var list = page.Controls.OfType<ListView>().Single();
            WaitArtifact(() => list.Items.Count == 3);
            var targetPoint = target.PointToScreen(new Point(130, 150));
            foreach (var path in new[] { savedImage, savedVideo })
            {
                received = [];
                gallery.Activate();
                var item = list.Items.Cast<ListViewItem>().Single(item => Equals(item.Tag, path));
                var bounds = item.GetBounds(ItemBoundsPortion.Icon);
                NativeArtifactDrag(list.PointToScreen(new Point(bounds.X + bounds.Width / 2,
                    bounds.Y + bounds.Height / 2)), targetPoint);
                RequireToolbar(received.SequenceEqual([path]) && File.Exists(path),
                    $"图库图片/视频未通过原生 OLE 复制拖出，或原文件丢失：Source={path}, Received={string.Join(',', received)}, Gallery={gallery.Bounds}, Target={target.Bounds}");
            }

            var source = new Label { Text = "外部图片源", Dock = DockStyle.Bottom, Height = 60, BackColor = Color.LightGreen };
            target.Controls.Add(source);
            source.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    source.DoDragDrop(WindowsArtifactDragDrop.CreateFileData([externalImage]), DragDropEffects.Copy);
                }
            };
            NativeArtifactDrag(source.PointToScreen(new Point(100, 30)), list.PointToScreen(new Point(60, 200)));
            RequireToolbar(locationService.Opened.SequenceEqual([externalImage]), "图库拖入未打开外部图片");

            using var pin = new PinnedImageForm((Bitmap)pixels.Clone(),
                new Rectangle(area.X + 300, area.Y + 510, 240, 160), new DragTestImageHost(),
                (image, _) =>
                {
                    RequireToolbar(image.Size == pixels.Size && image.GetPixel(35, 45) == pixels.GetPixel(35, 45),
                        "拖入贴图丢失原图像素");
                    locationService.PinCount++;
                });
            pin.Show();
            PumpToolbarInput();
            RequireToolbar(pin.Controls.Count == 0, "贴图上不应出现拖出按钮或其他覆盖控件");
            var pinBounds = pin.Bounds;
            using (var preview = new Bitmap(pin.ClientSize.Width, pin.ClientSize.Height))
            {
                pin.DrawToBitmap(preview, pin.ClientRectangle);
                RequireToolbar(preview.GetPixel(preview.Width - 20, 15).ToArgb() == Color.CornflowerBlue.ToArgb(),
                    "贴图原先把手所在位置应完整显示原图");
            }
            NativeArtifactDrag(source.PointToScreen(new Point(100, 30)), pin.PointToScreen(new Point(80, 80)));
            WaitArtifact(() => locationService.PinCount == 1);
            RequireToolbar(pin.Bounds == pinBounds, "拖入图片改变了原贴图窗口");

            var beforeMove = pin.Location;
            MovePointer(pin.PointToScreen(new Point(80, 80)));
            MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            PumpToolbarInput();
            MovePointer(Cursor.Position + new Size(25, 25));
            PumpToolbarInput();
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            PumpToolbarInput();
            RequireToolbar(pin.Location == beforeMove + new Size(25, 25), "原有贴图左键移动失效");

            // Escape cancels gallery drag-out without removing a saved source.
            received = [];
            gallery.Activate();
            var cancelItem = list.Items.Cast<ListViewItem>().Single(item => Equals(item.Tag, savedImage));
            var cancelBounds = cancelItem.GetBounds(ItemBoundsPortion.Icon);
            NativeArtifactDrag(list.PointToScreen(new Point(cancelBounds.X + cancelBounds.Width / 2,
                cancelBounds.Y + cancelBounds.Height / 2)), targetPoint, cancel: true);
            RequireToolbar(received.Length == 0 && File.Exists(savedImage), "Esc 未取消图库拖拽或删除了原文件");
            pin.Close();
            gallery.Close();
            target.Close();
            Console.WriteLine("PASS: native Windows OLE gallery image/video drag-out, external image drag-in, clean pin rendering without controls, pin drag-in, original movement and gallery Escape cancellation; no Ctrl.");
        }
        finally
        {
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            KeybdEvent(0x1B, 0, KeyEventKeyUp, UIntPtr.Zero);
            Cursor.Position = originalPointer;
            Directory.Delete(folder, true);
        }
    }

    private static void NativeArtifactDrag(Point start, Point end, bool cancel = false)
    {
        MovePointer(start);
        PumpToolbarInput();
        var step = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 150 };
        timer.Tick += (_, _) =>
        {
            var current = step++;
            switch (current)
            {
                case 0:
                    MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                    break;
                case 1:
                    MovePointer(start + new Size(18, 18));
                    break;
                case 2:
                case 3:
                    MovePointer(end);
                    break;
                case 4:
                    if (cancel)
                    {
                        KeybdEvent(0x1B, 0, 0, UIntPtr.Zero);
                        KeybdEvent(0x1B, 0, KeyEventKeyUp, UIntPtr.Zero);
                    }
                    MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                    break;
                default:
                    timer.Stop();
                    break;
            }
        };
        timer.Start();
        WaitArtifact(() => !timer.Enabled);
        PumpToolbarInput();
    }

    private static void WaitArtifact(Func<bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!completed() && DateTime.UtcNow < deadline)
        {
            PumpToolbarInput();
        }
        RequireToolbar(completed(), "等待原生拖拽操作超时");
    }

    private sealed class DragTestLocations : IFileLocationService
    {
        public List<string> Opened { get; } = [];
        public int PinCount { get; set; }
        public void OpenFile(string filePath) => Opened.Add(filePath);
        public void OpenFolder(string folderPath) { }
        public void ShowFileInFolder(string filePath) { }
        public void OpenWebPage(Uri uri) { }
    }

    private sealed class DragTestImageHost : IModuleImageHost
    {
        public void CopyImage(Bitmap image) { }
        public string SaveImage(Bitmap image) => throw new NotSupportedException();
        public void EditImage(Bitmap image) { }
    }
}
