using System.Reflection;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;
using ScreenshotTool.Presentation;
using ScreenshotTool.ScreenRecording;

namespace ScreenshotTool.UiPreview;

internal static partial class Program
{
    private static void RunToolbarDragInteractionTest()
    {
        var bounds = Screen.PrimaryScreen!.Bounds;
        var originalPointer = Cursor.Position;
        try
        {
            using (var session = new LiveAnnotationSessionForm(
                       bounds,
                       new Bitmap(bounds.Width, bounds.Height),
                       new PreviewClipboardService(),
                       new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
                       new DrawingToolCoefficients(),
                       AnnotationRotationStep.DefaultDegrees,
                       DrawingCursorShape.Circle,
                       Color.Red,
                       _ => { },
                       showMouseClickIndicator: false))
            {
                session.ConfigureToolbar("● 00:00:05", [new("pause", "暂停", "暂停", 52)]);
                session.Show();
                PumpToolbarInput();
                foreach (var tool in new[]
                         {
                             CaptureAnnotationTool.Operation,
                             CaptureAnnotationTool.Pen,
                             CaptureAnnotationTool.Select
                         })
                {
                    session.ActiveTool = tool;
                    var before = session.ToolbarWindow.Location;
                    DragToolbar(session.Toolbar, new Size(-60, 70));
                    RequireToolbar(session.ToolbarWindow.Location == before + new Size(-60, 70),
                        $"录屏 {tool} 模式拖动位置不正确：Before={before}, After={session.ToolbarWindow.Location}, Size={session.ToolbarWindow.Size}, WorkingArea={Screen.PrimaryScreen!.WorkingArea}");
                    RequireToolbar(session.AnnotationCount == 0, "拖动工具栏生成了批注");
                }
                var beforeStatusDrag = session.ToolbarWindow.Location;
                DragToolbar(session.Toolbar, new Size(0, 40),
                    session.Toolbar.Controls.OfType<Label>().Single(label => label.Text == "● 00:00:05"));
                RequireToolbar(session.ToolbarWindow.Location == beforeStatusDrag + new Size(0, 40),
                    "录屏计时文字无法拖动工具栏");
                var manualPosition = session.ToolbarWindow.Location;
                session.UpdateToolbarStatus("暂停 00:00:05");
                session.UpdateToolbarCommand("pause", "继续", true);
                session.SetToolVisible(CaptureAnnotationTool.Select, true);
                session.Toolbar.SnappingButton.PerformClick();
                RequireToolbar(session.ToolbarWindow.Location == manualPosition, "录屏工具栏刷新后跳回原位");
                var command = string.Empty;
                session.ToolbarCommandInvoked += (_, e) => command = e.CommandId;
                ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "继续"));
                RequireToolbar(command == "pause", "拖动后录屏按钮无法点击");
                VerifyToolbarCaptureLoss(session.Toolbar);
                session.Close();
            }

            using var snapshot = new DesktopSnapshot(new Bitmap(bounds.Width, bounds.Height), bounds);
            using var overlay = new CaptureOverlayForm(
                snapshot,
                new PreviewImageSaveService(),
                new PreviewClipboardService(),
                new PreviewWindowLocator(),
                new PreviewModuleManager(),
                SelectionMoveAnnotationStrategyFactory.Create(StickerSelectionMoveMode.FollowSelection),
                new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
                new LiveAnnotationSessionFactory(
                    new PreviewClipboardService(),
                    new DrawingToolCoefficients(),
                    AnnotationRotationStep.DefaultDegrees,
                    DrawingCursorShape.Circle),
                new Dictionary<string, bool>(),
                new Dictionary<string, int>(),
                Path.GetTempPath(),
                ScreenshotFileNameMode.DateTime);
            overlay.Show();
            PumpToolbarInput();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CaptureOverlayForm).GetMethod("BeginManualSelection", flags)!
                .Invoke(overlay, [new Point(300, 200), Rectangle.Empty]);
            typeof(CaptureOverlayForm).GetMethod("HandleMouseMove", flags)!
                .Invoke(overlay, [overlay, new MouseEventArgs(MouseButtons.Left, 0, 900, 500, 0)]);
            typeof(CaptureOverlayForm).GetMethod("HandleMouseUp", flags)!
                .Invoke(overlay, [overlay, new MouseEventArgs(MouseButtons.Left, 1, 900, 500, 0)]);
            PumpToolbarInput();
            var toolbar = overlay.Controls.OfType<CaptureEditorToolbar>().Single();
            var initialLocation = toolbar.Location;
            var selection = ((ICaptureFeatureHost)overlay).Selection;
            DragToolbar(toolbar, new Size(0, 140));
            RequireToolbar(toolbar.Location == initialLocation + new Size(0, 140), "截图工具栏拖动位置不正确");
            RequireToolbar(((ICaptureFeatureHost)overlay).Selection == selection, "工具栏拖动改变了截图选区");
            var manualLocation = toolbar.Location;
            typeof(CaptureOverlayForm).GetMethod("PositionToolbar", flags)!.Invoke(overlay, null);
            RequireToolbar(toolbar.Location == manualLocation, "截图工具栏布局后跳回原位");
            var toolbarCenter = new Point(toolbar.Left + toolbar.Width / 2, toolbar.Top + toolbar.Height / 2);
            RequireToolbar(overlay.Region!.IsVisible(toolbarCenter), "工具栏新位置未加入可交互区域");
            RequireToolbar(!overlay.Region.IsVisible(new Point(initialLocation.X + 12, initialLocation.Y + 12)),
                "工具栏旧位置仍阻挡桌面");
            ClickToolbarControl(toolbar.SnappingButton);
            RequireToolbar(toolbar.SnappingButton.Text == "吸附 关", "拖动后截图工具栏按钮无法点击");
            VerifyToolbarCaptureLoss(toolbar);
            overlay.Close();
            PumpToolbarInput();
            Console.WriteLine("PASS: native mouse toolbar dragging in recording operation/pen/select and screenshot; position retention, buttons, region and capture loss.");
        }
        finally
        {
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            Cursor.Position = originalPointer;
        }
    }

    private static void DragToolbar(CaptureEditorToolbar toolbar, Size delta, Control? dragSurface = null)
    {
        toolbar.FindForm()!.Activate();
        PumpToolbarInput();
        var handle = dragSurface ?? toolbar.Controls.Find("ToolbarDragHandle", false).Single();
        var start = handle.PointToScreen(new Point(handle.Width / 2, handle.Height / 2));
        MovePointer(start);
        PumpToolbarInput();
        RequireToolbar(ReferenceEquals(Control.FromHandle(WindowFromPoint(start)), handle), "工具栏拖动把手被遮挡");
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        PumpToolbarInput();
        RequireToolbar(toolbar.IsDragging,
            $"鼠标按下未开始工具栏拖动：Pointer={Cursor.Position}, Start={start}, Capture={handle.Capture}, Active={Form.ActiveForm?.Text}");
        MovePointer(start + delta);
        PumpToolbarInput();
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        PumpToolbarInput();
        RequireToolbar(!toolbar.IsDragging, "松开鼠标后仍处于工具栏拖动状态");
    }

    private static void VerifyToolbarCaptureLoss(CaptureEditorToolbar toolbar)
    {
        var handle = toolbar.Controls.Find("ToolbarDragHandle", false).Single();
        MovePointer(handle.PointToScreen(new Point(12, 15)));
        PumpToolbarInput();
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        PumpToolbarInput();
        RequireToolbar(toolbar.IsDragging, "捕获丢失测试未开始拖动");
        handle.Capture = false;
        RequireToolbar(!toolbar.IsDragging, "丢失鼠标捕获后未结束拖动");
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        PumpToolbarInput();
    }

    private static void ClickToolbarControl(Control control)
    {
        MovePointer(control.PointToScreen(new Point(control.Width / 2, control.Height / 2)));
        PumpToolbarInput();
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        PumpToolbarInput();
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        PumpToolbarInput();
    }

    private static void PumpToolbarInput()
    {
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(60);
        System.Windows.Forms.Application.DoEvents();
    }

    private static void RequireToolbar(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RunToolbarDragRecordingTest(string outputPath)
    {
        var workArea = Screen.PrimaryScreen!.WorkingArea;
        var bounds = new Rectangle(workArea.Left + 320, workArea.Top + 160, 640, 360);
        RequireToolbar(RecordingTarget.TryCreate(bounds, out var target), "无法创建真实录屏目标");
        using var backdrop = new Form
        {
            Bounds = bounds,
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(30, 64, 175),
            TopMost = true,
            ShowInTaskbar = false
        };
        backdrop.Show();
        using var session = new LiveAnnotationSessionForm(
            bounds,
            new Bitmap(bounds.Width, bounds.Height),
            new PreviewClipboardService(),
            new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle,
            Color.Red,
            _ => { },
            showMouseClickIndicator: false);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var originalPointer = Cursor.Position;
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var completion = RecordingCoordinator.RunAsync(
            target!,
            new RecordingOptions(false, false, false, 30, 2_000_000, CaptureRegionIndicatorStyle.Dashed),
            session,
            AppContext.BaseDirectory,
            fullPath,
            cancellation.Token);
        try
        {
            WaitForToolbarState(() => completion.IsCompleted || session.Toolbar.Controls.OfType<Label>()
                .Any(label => label.Text.Contains("00:00:01", StringComparison.Ordinal)));
            RequireToolbar(!completion.IsCompleted,
                $"真实录屏在拖动测试前已结束：{(completion.IsCompletedSuccessfully ? completion.Result.Error : completion.Exception?.ToString())}");
            var initial = session.ToolbarWindow.Location;
            DragToolbar(session.Toolbar, new Size(0, 70));
            RequireToolbar(session.ToolbarWindow.Location == initial + new Size(0, 70),
                $"录制中拖动失败：Before={initial}, After={session.ToolbarWindow.Location}, Size={session.ToolbarWindow.Size}");
            ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "暂停"));
            WaitForToolbarState(() => session.Toolbar.Controls.OfType<Button>().Any(button => button.Text == "继续"));
            var pausedPosition = session.ToolbarWindow.Location;
            DragToolbar(session.Toolbar, new Size(0, -50));
            RequireToolbar(session.ToolbarWindow.Location == pausedPosition + new Size(0, -50), "暂停时拖动失败");
            ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "继续"));
            WaitForToolbarState(() => session.Toolbar.Controls.OfType<Button>().Any(button => button.Text == "暂停"));
            session.ActiveTool = CaptureAnnotationTool.Pen;
            var drawingPosition = session.ToolbarWindow.Location;
            DragToolbar(session.Toolbar, new Size(0, -50));
            RequireToolbar(session.ToolbarWindow.Location == drawingPosition + new Size(0, -50), "录制画笔模式拖动失败");
            RequireToolbar(session.AnnotationCount == 0, "录制拖动生成了意外批注");
            ClickToolbarControl(session.Toolbar.Controls.OfType<Button>().Single(button => button.Text == "停止并保存"));
            WaitForToolbarState(() => completion.IsCompleted);
            var result = completion.GetAwaiter().GetResult();
            RequireToolbar(result.Saved && File.Exists(fullPath) && new FileInfo(fullPath).Length > 0,
                $"实际录屏未保存成功：{result.Error}");
            Console.WriteLine($"PASS: real recording toolbar drag, pause, resume, pen-mode drag and stop/save: {fullPath}");
        }
        finally
        {
            cancellation.Cancel();
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            session.Close();
            backdrop.Close();
            Cursor.Position = originalPointer;
            PumpToolbarInput();
        }
    }

    private static void WaitForToolbarState(Func<bool> predicate)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate() && timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            PumpToolbarInput();
        }
        RequireToolbar(predicate(), "等待录屏工具栏状态超时");
    }
}
