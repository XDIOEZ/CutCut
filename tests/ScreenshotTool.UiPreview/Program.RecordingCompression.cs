using System.Media;
using System.Text.Json;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;
using ScreenshotTool.Presentation;
using ScreenshotTool.ScreenRecording;

namespace ScreenshotTool.UiPreview;

internal static partial class Program
{
    private static void RunRecordingCompressionInteractionTest(string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        var settings = new PreviewModuleSettingsHost();
        using (var settingsForm = new Form
        {
            ClientSize = new Size(760, 820),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 50),
            ShowInTaskbar = false,
            TopMost = true
        })
        using (var page = new ScreenRecordingSettingsPage(settings) { Dock = DockStyle.Fill })
        {
            settingsForm.Controls.Add(page);
            settingsForm.Show();
            page.SelectSuperCompression();
            RequireToolbar(page.OneMinuteEstimate == "1.2 MB", "超级压缩有声估算错误");
            page.CaptureMicrophone = false;
            PumpToolbarInput();
            CaptureCompressionSettings(settingsForm, Path.Combine(outputFolder, "settings-top.png"));
            var saveButton = page.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<Button>())
                .Single(button => button.Text == "保存录屏设置");
            page.ScrollControlIntoView(saveButton);
            PumpToolbarInput();
            CaptureCompressionSettings(settingsForm, Path.Combine(outputFolder, "settings-bottom.png"));
            ClickToolbarControl(saveButton);
            RequireToolbar(settings.GetInteger(ScreenRecordingPreferences.VideoBitrateId, 0) == 64_000 &&
                settings.GetInteger(ScreenRecordingPreferences.AudioBitrateId, 0) == 96_000,
                "实际点击保存未保存压缩参数");
            settingsForm.Close();
        }

        var normal = new RecordingOptions(true, false, false, 30, 8_000_000, CaptureRegionIndicatorStyle.None);
        var profiles = new List<(string Name, RecordingOptions Options, Screen Screen)>
        {
            ("original", normal, Screen.PrimaryScreen!),
            ("custom", normal with { ResolutionMode = RecordingResolutionMode.Custom, OutputWidth = 320, OutputHeight = 240 }, Screen.PrimaryScreen!),
            ("super", normal.WithSuperCompression(), Screen.PrimaryScreen!),
            ("super-silent", normal.WithSuperCompression() with { CaptureSystemAudio = false }, Screen.PrimaryScreen!)
        };
        var secondary = Screen.AllScreens.FirstOrDefault(screen => !screen.Primary);
        if (secondary is not null)
        {
            profiles.Add(("secondary-super", normal.WithSuperCompression(), secondary));
        }
        var results = new List<object>();
        using var audio = CreateCompressionTestAudio();
        using var player = new SoundPlayer(audio);
        var originalPointer = Cursor.Position;
        try
        {
            foreach (var profile in profiles)
            {
                var workArea = profile.Screen.WorkingArea;
                var bounds = new Rectangle(workArea.Left + 100, workArea.Top + 100, 960, 540);
                RequireToolbar(profile.Screen.Bounds.Contains(bounds), "测试屏幕空间不足");
                using var backdrop = new CompressionRecordingBackdrop(bounds);
                backdrop.Show();
                using var session = new LiveAnnotationSessionForm(
                    bounds, new Bitmap(bounds.Width, bounds.Height), new PreviewClipboardService(),
                    new ToolWidthController(ToolWidthRange.Create(1, 32), 4), new DrawingToolCoefficients(),
                    AnnotationRotationStep.DefaultDegrees, DrawingCursorShape.Circle, Color.Red, _ => { },
                    showMouseClickIndicator: false);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var path = Path.Combine(outputFolder, $"{profile.Name}.mp4");
                RequireToolbar(RecordingTarget.TryCreate(bounds, out var target), "无法创建测试录屏目标");
                player.PlayLooping();
                var completion = RecordingCoordinator.RunAsync(target!, profile.Options, session,
                    AppContext.BaseDirectory, path, cancellation.Token);
                try
                {
                    WaitForToolbarState(() => completion.IsCompleted || session.Toolbar.Controls.OfType<Label>()
                        .Any(label => label.Text.Contains("00:00:01", StringComparison.Ordinal)));
                    RequireToolbar(!completion.IsCompleted, $"录制 {profile.Name} 提前结束");
                    PumpCompressionFor(TimeSpan.FromSeconds(3));
                    if (profile.Name == "super")
                    {
                        ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "暂停"));
                        WaitForToolbarState(() => session.Toolbar.Controls.OfType<Button>().Any(button => button.Text == "继续"));
                        PumpCompressionFor(TimeSpan.FromMilliseconds(500));
                        ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "继续"));
                        WaitForToolbarState(() => session.Toolbar.Controls.OfType<Button>().Any(button => button.Text == "暂停"));
                        PumpCompressionFor(TimeSpan.FromSeconds(1));
                    }
                    ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "停止并保存"));
                    WaitForToolbarState(() => completion.IsCompleted);
                    var result = completion.GetAwaiter().GetResult();
                    RequireToolbar(result.Saved, $"录制 {profile.Name} 保存失败：{result.Error}");
                    var outputSize = RecordingOutputResolution.Resolve(bounds.Size, profile.Options);
                    var bytes = new FileInfo(path).Length;
                    results.Add(new
                    {
                        profile.Name,
                        profile.Screen.DeviceName,
                        Width = outputSize.Width,
                        Height = outputSize.Height,
                        profile.Options.FramesPerSecond,
                        profile.Options.VideoBitrate,
                        profile.Options.AudioBitrate,
                        profile.Options.MonoAudio,
                        profile.Options.CaptureSystemAudio,
                        Bytes = bytes,
                        Path = path
                    });
                    Console.WriteLine($"PASS {profile.Name}: {outputSize.Width}x{outputSize.Height}, {bytes} bytes, {path}");
                }
                finally
                {
                    player.Stop();
                    cancellation.Cancel();
                    session.Close();
                    backdrop.Close();
                    PumpToolbarInput();
                }
            }
            File.WriteAllText(Path.Combine(outputFolder, "recording-results.json"),
                JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            player.Stop();
            Cursor.Position = originalPointer;
        }
    }

    private static void CaptureCompressionSettings(Form form, string path)
    {
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        image.Save(path);
    }

    private static void PumpCompressionFor(TimeSpan duration)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (elapsed.Elapsed < duration)
        {
            PumpToolbarInput();
        }
    }

    private static MemoryStream CreateCompressionTestAudio()
    {
        const int rate = 48000;
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RIFF"u8.ToArray()); writer.Write(36 + rate * 2); writer.Write("WAVEfmt "u8.ToArray());
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
            writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8.ToArray()); writer.Write(rate * 2);
            for (var sample = 0; sample < rate; sample++)
            {
                writer.Write((short)(Math.Sin(sample * 2 * Math.PI * 440 / rate) * 1200));
            }
        }
        stream.Position = 0;
        return stream;
    }

    private sealed class CompressionRecordingBackdrop : Form
    {
        private readonly Bitmap _pattern;
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
        private int _frame;

        public CompressionRecordingBackdrop(Rectangle bounds)
        {
            Bounds = bounds; StartPosition = FormStartPosition.Manual; FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; DoubleBuffered = true;
            _pattern = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(_pattern);
            var random = new Random(42);
            for (var y = 0; y < bounds.Height; y += 8)
            {
                for (var x = 0; x < bounds.Width; x += 8)
                {
                    using var brush = new SolidBrush(Color.FromArgb(random.Next(255), random.Next(255), random.Next(255)));
                    graphics.FillRectangle(brush, x, y, 8, 8);
                }
            }
            _timer.Tick += (_, _) => { _frame++; Invalidate(); };
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(_pattern, Point.Empty);
            e.Graphics.FillRectangle(Brushes.Blue, (_frame * 12) % (Width - 240), 180, 240, 180);
            using var border = new Pen(Color.Lime, 16);
            e.Graphics.DrawRectangle(border, 8, 8, Width - 16, Height - 16);
            using var font = new Font("Microsoft YaHei UI", 26F);
            e.Graphics.FillRectangle(Brushes.Black, 30, 30, 680, 80);
            e.Graphics.DrawString($"录屏压缩验证  {_frame}", font, Brushes.White, 40, 40);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose(); _pattern.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
