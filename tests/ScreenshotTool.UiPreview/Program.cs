using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;
using ScreenshotTool.Presentation;
using ScreenshotTool.Presentation.Pages;
using ScreenshotTool.Presentation.Shell;
using ScreenshotTool.ScreenRecording;
using System.Runtime.InteropServices;

namespace ScreenshotTool.UiPreview;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args is ["--annotation-smoke", var outputPath])
        {
            RunAnnotationSmoke(outputPath);
            return 0;
        }
        if (args is ["--recording-options-smoke", var optionsOutputPath])
        {
            RunScreenRecordingSettingsSmoke(optionsOutputPath);
            return 0;
        }
        if (args is ["--recording-toolbar-smoke", var toolbarOutputPath])
        {
            RunRecordingToolbarSmoke(toolbarOutputPath);
            return 0;
        }
        if (args is ["--recording-drawing-input-smoke", var drawingOutputPath])
        {
            RunRecordingDrawingInputSmoke(drawingOutputPath);
            return 0;
        }
        if (args is ["--recording-fullscreen-edit-smoke"])
        {
            RunRecordingFullScreenEditSmoke();
            return 0;
        }
        if (args is ["--saved-artifact-notification-smoke", var notificationOutputPath])
        {
            RunSavedArtifactNotificationSmoke(notificationOutputPath);
            return 0;
        }
        if (args is ["--ocr-text-result-smoke", var ocrResultOutputPath])
        {
            RunOcrTextResultSmoke(ocrResultOutputPath);
            return 0;
        }
        if (args is ["--qr-code-result-smoke", var qrCodeResultOutputPath])
        {
            RunQrCodeResultSmoke(qrCodeResultOutputPath);
            return 0;
        }
        if (args is ["--select-all-displays-smoke"])
        {
            RunSelectAllDisplaysSmoke();
            return 0;
        }
        if (args is ["--save-naming-page-smoke", var saveNamingOutputPath])
        {
            RunSaveNamingPageSmoke(saveNamingOutputPath);
            return 0;
        }
        if (args is ["--screenshot-settings-page-smoke", var screenshotSettingsOutputPath])
        {
            RunScreenshotSettingsPageSmoke(screenshotSettingsOutputPath);
            return 0;
        }
        if (args is ["--notification-capture-policy-smoke"])
        {
            RunNotificationCapturePolicySmoke();
            return 0;
        }
        if (args is ["--editor-alignment-page-smoke", var alignmentOutputPath])
        {
            RunEditorAlignmentPageSmoke(alignmentOutputPath);
            return 0;
        }
        if (args is ["--drawing-coefficients-page-smoke", var coefficientsOutputPath])
        {
            RunDrawingCoefficientsPageSmoke(coefficientsOutputPath);
            return 0;
        }
        if (args is ["--main-window-capture-visibility-smoke"])
        {
            RunMainWindowCaptureVisibilitySmoke();
            return 0;
        }
        if (args is ["--main-navigation-smoke", var navigationOutputPath])
        {
            RunMainNavigationSmoke(navigationOutputPath);
            return 0;
        }
        if (args is ["--module-management-page-smoke", var moduleManagementOutputPath])
        {
            RunModuleManagementPageSmoke(moduleManagementOutputPath);
            return 0;
        }

        var previewFolder = Path.Combine(Path.GetTempPath(), "LightShotUiPreview");
        var form = new MainForm(
            new PreviewSettingsStore(previewFolder),
            new PreviewHotkeyService(),
            new PreviewCaptureService(),
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(),
            new PreviewStartupRegistrationService(),
            enableBackgroundIntegration: false)
        {
            Text = "轻截 - 界面预览"
        };

        System.Windows.Forms.Application.Run(form);
        return 0;
    }

    private static void RunAnnotationSmoke(string outputPath)
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("找不到主显示器。");
        var bounds = new Rectangle(
            screen.WorkingArea.Left + 24,
            screen.WorkingArea.Top + 24,
            Math.Min(260, screen.WorkingArea.Width - 48),
            Math.Min(180, screen.WorkingArea.Height - 48));
        if (bounds.Width < 180 || bounds.Height < 120)
        {
            throw new InvalidOperationException("主显示器空间不足，无法验证核心批注层。");
        }

        using var source = new Bitmap(bounds.Width, bounds.Height);
        using (var sourceGraphics = Graphics.FromImage(source))
        {
            sourceGraphics.Clear(Color.Black);
        }
        var width = new ToolWidthController(ToolWidthRange.Create(1, 32), 4);
        using var session = new LiveAnnotationSessionForm(
            bounds,
            (Bitmap)source.Clone(),
            new PreviewClipboardService(),
            width,
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle,
            Color.Magenta,
            _ => { });
        session.Editor.AddDraft(
            EditorTool.Rectangle,
            new Point(20, 20),
            new Point(bounds.Width - 30, bounds.Height - 35),
            [],
            Color.Magenta,
            session.ToolWidth);
        session.Editor.AddDraft(
            EditorTool.Arrow,
            new Point(30, bounds.Height - 30),
            new Point(bounds.Width - 35, 28),
            [],
            Color.Cyan,
            session.ToolWidth);
        session.Editor.Selection.SelectOnly(session.Editor.Document.GetMovableAnnotations()[0]);
        session.ActiveTool = CaptureAnnotationTool.Select;

        session.Show();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(180);
        System.Windows.Forms.Application.DoEvents();

        using var captured = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(captured))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        session.Close();
        System.Windows.Forms.Application.DoEvents();

        var annotationPixels = CountAnnotationPixels(captured);
        if (annotationPixels < 150)
        {
            throw new InvalidOperationException(
                $"共享批注内容层没有出现在屏幕采集中，只检测到 {annotationPixels} 个批注像素。");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static int CountAnnotationPixels(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if ((pixel.R > 220 && pixel.G < 70 && pixel.B > 220) ||
                    (pixel.R < 70 && pixel.G > 220 && pixel.B > 220))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static void RunScreenRecordingSettingsSmoke(string outputPath)
    {
        var settings = new PreviewModuleSettingsHost();
        settings.SetBoolean(ScreenRecordingPreferences.CaptureSystemAudioId, true);
        settings.SetBoolean(ScreenRecordingPreferences.CaptureMicrophoneId, false);
        settings.SetBoolean(ScreenRecordingPreferences.ShowMouseClickIndicatorId, true);
        settings.SetInteger(ScreenRecordingPreferences.FramesPerSecondId, 60);
        settings.SetInteger(ScreenRecordingPreferences.VideoBitrateId, 12_000_000);
        settings.SetInteger(
            ScreenRecordingPreferences.RegionIndicatorStyleId,
            (int)CaptureRegionIndicatorStyle.Dashed);
        using var form = new Form
        {
            Text = "轻截 - 录屏设置",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 680),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new ScreenRecordingSettingsPage(settings)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 644)
        };
        form.Controls.Add(page);
        form.Show();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(100);
        System.Windows.Forms.Application.DoEvents();

        if (!page.CaptureSystemAudio ||
            page.CaptureMicrophone ||
            !page.ShowMouseClickIndicator ||
            page.FramesPerSecond != 60 ||
            page.VideoBitrate != 12_000_000 ||
            page.RegionIndicatorStyle != CaptureRegionIndicatorStyle.Dashed)
        {
            throw new InvalidOperationException("录屏设置页没有恢复已保存的参数。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        form.Close();

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunRecordingToolbarSmoke(string outputPath)
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("找不到主显示器。");
        var bounds = screen.WorkingArea;
        if (bounds.Width < 760 || bounds.Height < 240)
        {
            throw new InvalidOperationException("主显示器空间不足，无法验证录屏共享工具栏。");
        }

        using var session = new LiveAnnotationSessionForm(
            bounds,
            new Bitmap(bounds.Width, bounds.Height),
            new PreviewClipboardService(),
            new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle,
            Color.FromArgb(239, 68, 68),
            _ => { });
        var recordingToolbarSession = (ICaptureAnnotationToolbarSession)session;
        recordingToolbarSession.SetToolVisible(CaptureAnnotationTool.Select, visible: true);
        recordingToolbarSession.ConfigureToolbar(
            "● 00:00:05",
            [
                new("pause", "暂停", "暂停录屏", 52),
                new(
                    "stop",
                    "停止并保存",
                    "停止录屏并保存 MP4",
                    82,
                    CaptureAnnotationToolbarCommandStyle.Danger),
                new("cancel", "取消", "取消本次录屏", 48)
            ]);
        var invokedCommand = string.Empty;
        recordingToolbarSession.ToolbarCommandInvoked +=
            (_, e) => invokedCommand = e.CommandId;
        session.Show();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(100);
        System.Windows.Forms.Application.DoEvents();

        var rectangleButton = session.Toolbar.Controls.OfType<Button>()
            .Single(button => button.Text == "矩形");
        var selectButton = session.Toolbar.Controls.OfType<Button>()
            .Single(button => button.Text == "选择");
        if (!selectButton.Visible ||
            session.Toolbar.Controls.GetChildIndex(selectButton) >=
            session.Toolbar.Controls.GetChildIndex(rectangleButton) ||
            selectButton.BackColor.ToArgb() == rectangleButton.BackColor.ToArgb())
        {
            throw new InvalidOperationException(
                "录屏专属选择按钮没有显示在矩形左侧或缺少区别色。");
        }
        var selectInactiveColor = selectButton.BackColor;
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(selectButton, [EventArgs.Empty]);
        if (session.ActiveTool != CaptureAnnotationTool.Select ||
            selectButton.Text != "✓ 选择中" ||
            selectButton.FlatAppearance.BorderSize != 2 ||
            selectButton.BackColor.ToArgb() == selectInactiveColor.ToArgb())
        {
            throw new InvalidOperationException(
                "录屏选择按钮没有用文字、边框和高亮色明确显示开启状态。");
        }
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(rectangleButton, [EventArgs.Empty]);
        if (selectButton.Text != "选择" ||
            selectButton.FlatAppearance.BorderSize != 1 ||
            selectButton.BackColor.ToArgb() != selectInactiveColor.ToArgb())
        {
            throw new InvalidOperationException("切换到绘图工具后选择按钮没有恢复关闭状态。");
        }
        var stopButton = session.Toolbar.Controls.OfType<Button>()
            .Single(button => button.Text == "停止并保存");
        var stopButtonCenter = stopButton.PointToScreen(new Point(
            stopButton.Width / 2,
            stopButton.Height / 2));
        var hitWindow = WindowFromPoint(stopButtonCenter);
        var hitControl = Control.FromHandle(hitWindow);
        if (!ReferenceEquals(hitControl, stopButton))
        {
            throw new InvalidOperationException(
                $"录屏工具栏没有位于实时输入层上方。Hit={hitControl?.GetType().Name}:{hitControl?.Text}");
        }
        if (session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
                LiveAnnotationPointerEventKind.LeftDown,
                stopButtonCenter)))
        {
            throw new InvalidOperationException("实时绘图输入钩子吞掉了录屏工具栏点击。");
        }
        stopButton.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        if (!string.Equals(invokedCommand, "stop", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "实时绘图开启后，选区内的录屏停止按钮无法点击。" +
                $" Hit={hitControl?.GetType().Name}:{hitControl?.Text}, " +
                $"HitHandle={hitWindow}, StopHandle={stopButton.Handle}, " +
                $"ToolbarHandle={session.ToolbarWindow.Handle}, SessionHandle={session.Handle}");
        }

        selectButton.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        if (session.ActiveTool != CaptureAnnotationTool.Select ||
            selectButton.Text != "✓ 选择中")
        {
            throw new InvalidOperationException("录屏工具栏截图前没有恢复选择开启状态。");
        }

        session.Toolbar.PerformLayout();
        var preferred = session.Toolbar.GetPreferredSize(Size.Empty);
        using var captured = new Bitmap(preferred.Width, preferred.Height);
        session.Toolbar.DrawToBitmap(captured, new Rectangle(Point.Empty, preferred));
        session.Close();

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunRecordingDrawingInputSmoke(string outputPath)
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("找不到主显示器。");
        var bounds = new Rectangle(
            screen.WorkingArea.Left + 80,
            screen.WorkingArea.Top + 80,
            360,
            240);
        using var backingForm = new Form
        {
            Bounds = bounds,
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(30, 41, 59),
            TopMost = true,
            ShowInTaskbar = false
        };
        var backingClickCount = 0;
        backingForm.MouseDown += (_, _) => backingClickCount++;
        backingForm.Show();

        using var source = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.Clear(backingForm.BackColor);
        }
        using var session = new LiveAnnotationSessionForm(
            bounds,
            (Bitmap)source.Clone(),
            new PreviewClipboardService(),
            new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle,
            Color.Red,
            _ => { });
        ((ICaptureAnnotationToolbarSession)session).SetToolVisible(
            CaptureAnnotationTool.Select,
            visible: true);
        session.Show();
        System.Windows.Forms.Application.DoEvents();

        if (!session.ClickPreviewHookStarted || session.PointerHookStarted)
        {
            throw new InvalidOperationException(
                "鼠标穿透状态没有启动只观察左键提示的输入钩子。");
        }
        var previewClickPoint = new Point(bounds.Left + 180, bounds.Top + 120);
        MovePointer(previewClickPoint);
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(240);
        System.Windows.Forms.Application.DoEvents();
        if (!session.MouseClickIndicatorVisible || !session.MouseClickIndicatorPressed)
        {
            throw new InvalidOperationException("长按左键时黄色圆圈没有持续显示。");
        }
        var heldMovePoint = new Point(previewClickPoint.X + 46, previewClickPoint.Y + 28);
        session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.Move,
            heldMovePoint));
        System.Windows.Forms.Application.DoEvents();
        if (session.MouseClickIndicatorCenter != heldMovePoint)
        {
            throw new InvalidOperationException(
                $"长按左键移动时黄色圆圈没有跟随鼠标。" +
                $" Expected={heldMovePoint}, Actual={session.MouseClickIndicatorCenter}");
        }
        if (session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
                LiveAnnotationPointerEventKind.LeftDown,
                previewClickPoint)))
        {
            throw new InvalidOperationException("现场左键提示吞掉了鼠标点击。");
        }
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(180);
        System.Windows.Forms.Application.DoEvents();
        if (session.MouseClickIndicatorVisible || session.MouseClickIndicatorPressed)
        {
            throw new InvalidOperationException("松开左键后黄色圆圈没有按时隐藏。");
        }
        backingClickCount = 0;

        var rectangleButton = session.Toolbar.Controls.OfType<Button>()
            .Single(button => button.Text == "矩形");
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(rectangleButton, [EventArgs.Empty]);
        System.Windows.Forms.Application.DoEvents();

        var start = new Point(bounds.Left + 40, bounds.Top + 40);
        var end = new Point(bounds.Right - 50, bounds.Bottom - 50);
        MovePointer(start);
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        for (var step = 1; step <= 8; step++)
        {
            MovePointer(new Point(
                start.X + ((end.X - start.X) * step / 8),
                start.Y + ((end.Y - start.Y) * step / 8)));
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        System.Windows.Forms.Application.DoEvents();

        var penButton = session.Toolbar.Controls.OfType<Button>()
            .Single(button => button.Text == "画笔");
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(penButton, [EventArgs.Empty]);
        var penStart = new Point(bounds.Left + 55, bounds.Bottom - 35);
        var penEnd = new Point(bounds.Right - 55, bounds.Top + 35);
        MovePointer(penStart);
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        for (var step = 1; step <= 12; step++)
        {
            var next = new Point(
                penStart.X + ((penEnd.X - penStart.X) * step / 12),
                penStart.Y + ((penEnd.Y - penStart.Y) * step / 12));
            var current = Cursor.Position;
            MouseEvent(
                MouseEventMove | MouseEventMoveNoCoalesce,
                unchecked((uint)(next.X - current.X)),
                unchecked((uint)(next.Y - current.Y)),
                0,
                UIntPtr.Zero);
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        System.Windows.Forms.Application.DoEvents();

        using var rendered = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(rendered))
        {
            session.RenderContent(graphics);
        }
        Thread.Sleep(80);
        System.Windows.Forms.Application.DoEvents();
        using var screenCapture = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(screenCapture))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        var annotationCount = session.AnnotationCount;
        var redPixels = CountRedPixels(rendered);
        var visibleRedPixels = CountRedPixels(screenCapture);
        var penBounds = session.Editor.Document.GetMovableAnnotations().Last().Bounds;

        if (backingClickCount != 0)
        {
            throw new InvalidOperationException("批注绘制时鼠标点击泄漏给了被录制程序。");
        }
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(penButton, [EventArgs.Empty]);
        System.Windows.Forms.Application.DoEvents();
        if (session.ActiveTool != CaptureAnnotationTool.Operation || session.PointerHookStarted)
        {
            throw new InvalidOperationException("退出批注工具后没有释放鼠标输入捕获。");
        }
        if (!session.ClickPreviewHookStarted)
        {
            throw new InvalidOperationException("退出批注工具后没有恢复左键提示观察。");
        }
        var selectButton = session.Toolbar.Controls.OfType<Button>()
            .Single(button => button.Text == "选择");
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(selectButton, [EventArgs.Empty]);
        var marqueeStart = new Point(bounds.Left + 8, bounds.Top + 8);
        var marqueeEnd = new Point(bounds.Left + 28, bounds.Top + 28);
        session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.LeftDown,
            marqueeStart));
        session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.Move,
            marqueeEnd));
        System.Windows.Forms.Application.DoEvents();
        if (!session.MarqueeFillVisible ||
            session.MarqueeFillOpacity <= 0D ||
            session.MarqueeFillOpacity >= 1D)
        {
            throw new InvalidOperationException(
                $"录屏框选没有使用半透明填充。" +
                $" Visible={session.MarqueeFillVisible}, Opacity={session.MarqueeFillOpacity}, " +
                $"Selecting={session.IsSelectingMarquee}, Bounds={session.MarqueeBounds}, " +
                $"Tool={session.ActiveTool}");
        }
        session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.LeftUp,
            marqueeEnd));
        System.Windows.Forms.Application.DoEvents();
        if (session.MarqueeFillVisible)
        {
            throw new InvalidOperationException("结束框选后半透明填充仍未隐藏。");
        }
        var rectangleBorderPoint = new Point(bounds.Left + 40, bounds.Top + 100);
        MovePointer(rectangleBorderPoint);
        MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        System.Windows.Forms.Application.DoEvents();
        if (session.ActiveTool != CaptureAnnotationTool.Select ||
            session.Editor.Selection.Count != 1 ||
            backingClickCount != 0)
        {
            throw new InvalidOperationException(
                "录屏选择模式没有用左键选中编辑元素，或点击泄漏给了被录制程序。");
        }
        typeof(Button).GetMethod(
                "OnClick",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(selectButton, [EventArgs.Empty]);
        System.Windows.Forms.Application.DoEvents();
        if (session.ActiveTool != CaptureAnnotationTool.Operation || session.PointerHookStarted)
        {
            throw new InvalidOperationException("关闭录屏选择模式后没有恢复鼠标穿透。");
        }

        session.Close();
        System.Windows.Forms.Application.DoEvents();

        using (var disabledIndicatorSession = new LiveAnnotationSessionForm(
                   bounds,
                   (Bitmap)source.Clone(),
                   new PreviewClipboardService(),
                   new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
                   new DrawingToolCoefficients(),
                   AnnotationRotationStep.DefaultDegrees,
                   DrawingCursorShape.Circle,
                   Color.Red,
                   _ => { },
                   showMouseClickIndicator: false))
        {
            disabledIndicatorSession.Show();
            System.Windows.Forms.Application.DoEvents();
            if (disabledIndicatorSession.ClickPreviewHookStarted ||
                disabledIndicatorSession.MouseClickIndicatorVisible)
            {
                throw new InvalidOperationException(
                    "关闭左键圆圈后仍然启动了现场点击提示。");
            }
            disabledIndicatorSession.Close();
        }
        backingForm.Close();

        if (annotationCount < 2 ||
            redPixels < 100 ||
            visibleRedPixels < 100 ||
            penBounds.Width < 120 ||
            penBounds.Height < 80)
        {
            throw new InvalidOperationException(
                $"录屏共享工具栏已切换绘图工具，但实时批注输入不完整。" +
                $" Count={annotationCount}, RenderedRed={redPixels}, " +
                $"VisibleRed={visibleRedPixels}, PenBounds={penBounds}");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        screenCapture.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunRecordingFullScreenEditSmoke()
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("找不到主显示器。");
        var bounds = screen.Bounds;
        if (bounds.Width < 800 || bounds.Height < 600)
        {
            throw new InvalidOperationException("主显示器空间不足，无法验证全屏录屏编辑性能。");
        }

        using var session = new LiveAnnotationSessionForm(
            bounds,
            new Bitmap(bounds.Width, bounds.Height),
            new PreviewClipboardService(),
            new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle,
            Color.Red,
            _ => { });
        ((ICaptureAnnotationToolbarSession)session).SetToolVisible(
            CaptureAnnotationTool.Select,
            visible: true);
        var initialBounds = new Rectangle(
            bounds.Width / 2 - 120,
            bounds.Height / 2 - 80,
            240,
            160);
        session.Editor.AddDraft(
            EditorTool.Rectangle,
            initialBounds.Location,
            new Point(initialBounds.Right, initialBounds.Bottom),
            [],
            Color.Red,
            session.ToolWidth);
        session.ActiveTool = CaptureAnnotationTool.Select;
        session.Show();
        System.Windows.Forms.Application.DoEvents();
        var pointerHook = (LiveAnnotationPointerHook?)typeof(LiveAnnotationSessionForm)
            .GetField(
                "_pointerHook",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)?
            .GetValue(session) ??
            throw new InvalidOperationException("无法取得全屏录屏测试输入钩子。");
        pointerHook.Stop();

        var dragStart = new Point(
            bounds.Left + initialBounds.Left,
            bounds.Top + initialBounds.Top + initialBounds.Height / 2);
        if (!session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
                LiveAnnotationPointerEventKind.LeftDown,
                dragStart)))
        {
            throw new InvalidOperationException("全屏录屏测试没有进入元素拖动状态。");
        }
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var step = 1; step <= 30; step++)
        {
            session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
                LiveAnnotationPointerEventKind.Move,
                new Point(dragStart.X + step * 4, dragStart.Y + step)));
            System.Windows.Forms.Application.DoEvents();
        }
        session.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.LeftUp,
            new Point(dragStart.X + 120, dragStart.Y + 30)));
        System.Windows.Forms.Application.DoEvents();
        stopwatch.Stop();

        var movedBounds = session.Editor.Document.GetMovableAnnotations().Single().Bounds;
        var dirtyBounds = session.LastContentInvalidationBounds;
        session.Close();
        System.Windows.Forms.Application.DoEvents();

        if (movedBounds.X <= initialBounds.X ||
            dirtyBounds.IsEmpty ||
            dirtyBounds.Width >= bounds.Width / 2 ||
            dirtyBounds.Height >= bounds.Height / 2 ||
            stopwatch.ElapsedMilliseconds > 3000)
        {
            throw new InvalidOperationException(
                $"全屏录屏元素编辑仍未使用流畅的局部重绘。" +
                $" Elapsed={stopwatch.ElapsedMilliseconds}ms, Dirty={dirtyBounds}, " +
                $"Initial={initialBounds}, Moved={movedBounds}");
        }
    }

    private static int CountRedPixels(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 180 && pixel.G < 100 && pixel.B < 100)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static void RunSavedArtifactNotificationSmoke(string outputPath)
    {
        var expectedPath = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "LightShotNotificationSmoke",
            "录屏_20260721_120000.mp4"));
        string? openedPath = null;
        using var notification = new SavedArtifactNotificationForm(expectedPath);
        notification.OpenRequested += (_, path) => openedPath = path;
        notification.Show();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(100);
        System.Windows.Forms.Application.DoEvents();

        var workingArea = Screen.FromPoint(notification.Bounds.Location).WorkingArea;
        if (Math.Abs(notification.Right - (workingArea.Right - 18)) > 2 ||
            Math.Abs(notification.Bottom - (workingArea.Bottom - 18)) > 2)
        {
            throw new InvalidOperationException(
                $"保存成功提示没有显示在屏幕右下角。Bounds={notification.Bounds}, WorkingArea={workingArea}");
        }

        using var captured = new Bitmap(notification.Width, notification.Height);
        using (var graphics = Graphics.FromImage(captured))
        {
            graphics.CopyFromScreen(notification.Location, Point.Empty, notification.Size);
        }

        var clickX = notification.Width / 2;
        var clickY = notification.Height / 2;
        var clickPosition = (IntPtr)((clickY << 16) | (clickX & 0xffff));
        SendMessage(notification.Handle, WindowMessageLeftButtonDown, (IntPtr)1, clickPosition);
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(30);
        SendMessage(notification.Handle, WindowMessageLeftButtonUp, IntPtr.Zero, clickPosition);
        System.Windows.Forms.Application.DoEvents();

        if (!string.Equals(openedPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"点击保存成功提示没有传递对应视频路径。Expected={expectedPath}, Actual={openedPath}");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunOcrTextResultSmoke(string outputPath)
    {
        RunTextResultSmoke(
            outputPath,
            "OCR 识别结果",
            "轻截文字识别\r\n\r\nThe quick brown fox jumps over the lazy dog.\r\n2026-07-22",
            "OCR");
    }

    private static void RunQrCodeResultSmoke(string outputPath)
    {
        RunTextResultSmoke(
            outputPath,
            "二维码扫描结果",
            "https://example.com/cutcut?source=qr\r\n\r\nWIFI:T:WPA;S:LightShot;P:12345678;;",
            "二维码");
    }

    private static void RunTextResultSmoke(
        string outputPath,
        string title,
        string text,
        string featureName)
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("找不到主显示器。");
        var anchor = new Rectangle(
            screen.WorkingArea.Left + 40,
            screen.WorkingArea.Top + 80,
            Math.Min(720, screen.WorkingArea.Width / 2),
            360);
        using var resultWindow = new CaptureTextResultForm(
            title,
            text,
            anchor,
            new PreviewClipboardService());
        resultWindow.Show();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        if (!string.Equals(
                resultWindow.ResultText,
                text,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{featureName} 结果窗口没有完整保留多行内容。");
        }

        using var captured = new Bitmap(resultWindow.Width, resultWindow.Height);
        using (var graphics = Graphics.FromImage(captured))
        {
            graphics.CopyFromScreen(resultWindow.Location, Point.Empty, resultWindow.Size);
        }
        resultWindow.Close();
        System.Windows.Forms.Application.DoEvents();

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunSelectAllDisplaysSmoke()
    {
        var virtualDesktop = SystemInformation.VirtualScreen;
        var targetDisplay = Screen.AllScreens.FirstOrDefault(screen => !screen.Primary) ??
                            Screen.PrimaryScreen ??
                            throw new InvalidOperationException("找不到可用显示器。");
        var originalPointer = Cursor.Position;
        using var snapshotImage = new Bitmap(virtualDesktop.Width, virtualDesktop.Height);
        using var snapshot = new DesktopSnapshot((Bitmap)snapshotImage.Clone(), virtualDesktop);
        var width = new ToolWidthController(ToolWidthRange.Create(1, 32), 4);
        var annotations = new LiveAnnotationSessionFactory(
            new PreviewClipboardService(),
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle);
        var overlay = new CaptureOverlayForm(
            snapshot,
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewModuleManager(),
            SelectionMoveAnnotationStrategyFactory.Create(StickerSelectionMoveMode.FollowSelection),
            width,
            annotations,
            new Dictionary<string, bool>(),
            new Dictionary<string, int>(),
            Path.GetTempPath(),
            ScreenshotFileNameMode.DateTime);

        try
        {
            Cursor.Position = new Point(
                targetDisplay.Bounds.Left + targetDisplay.Bounds.Width / 2,
                targetDisplay.Bounds.Top + targetDisplay.Bounds.Height / 2);
            overlay.Show();
            System.Windows.Forms.Application.DoEvents();

            RaiseSelectAllKey(overlay);
            System.Windows.Forms.Application.DoEvents();
            var firstSelection = ((ICaptureFeatureHost)overlay).Selection;
            var expectedDisplaySelection = CaptureSelectAllPolicy.ResolveSelectionTarget(
                Rectangle.Empty,
                virtualDesktop,
                targetDisplay.Bounds);
            if (firstSelection != expectedDisplaySelection)
            {
                throw new InvalidOperationException(
                    $"第一次 Ctrl+A 没有选择鼠标所在显示器。Expected={expectedDisplaySelection}, Actual={firstSelection}");
            }

            RaiseSelectAllKey(overlay);
            System.Windows.Forms.Application.DoEvents();
            var secondSelection = ((ICaptureFeatureHost)overlay).Selection;
            var expectedVirtualDesktop = new Rectangle(Point.Empty, virtualDesktop.Size);
            if (secondSelection != expectedVirtualDesktop)
            {
                throw new InvalidOperationException(
                    $"第二次 Ctrl+A 没有选择全部显示器。Expected={expectedVirtualDesktop}, Actual={secondSelection}");
            }
        }
        finally
        {
            if (!overlay.IsDisposed)
            {
                overlay.Hide();
                overlay.Dispose();
            }
            Cursor.Position = originalPointer;
            System.Windows.Forms.Application.DoEvents();
        }
    }

    private static void RaiseSelectAllKey(Control control) =>
        typeof(Control).GetMethod(
                "OnKeyDown",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(control, [new KeyEventArgs(Keys.Control | Keys.A)]);

    private static void RunSaveNamingPageSmoke(string outputPath)
    {
        using var form = new Form
        {
            Text = "轻截 - 保存路径与命名规则",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 590),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new SavePathSettingsPage(
            @"C:\Users\User\Pictures\轻截",
            ScreenshotFileNameMode.ImageText)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 554)
        };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        form.BringToFront();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        if (page.FileNameMode != ScreenshotFileNameMode.ImageText)
        {
            throw new InvalidOperationException("保存设置页面没有恢复图片文字命名规则。");
        }
        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();

    }

    private static void RunScreenshotSettingsPageSmoke(string outputPath)
    {
        using var form = new Form
        {
            Text = "轻截 - 截图设置",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 660),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new ScreenshotSettingsPage(
            new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, (int)Keys.Q),
            startMinimized: true,
            startWithWindows: true,
            dismissSaveNotificationBeforeCapture: false,
            hideMainWindowDuringCapture: true)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 624)
        };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        form.BringToFront();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        if (page.Hotkey !=
                new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, (int)Keys.Q) ||
            !page.StartMinimized ||
            !page.StartWithWindows ||
            page.DismissSaveNotificationBeforeCapture ||
            !page.HideMainWindowDuringCapture)
        {
            throw new InvalidOperationException("截图设置页面没有恢复快捷键、启动方式或截图行为选项。");
        }
        var settingRows = page.Controls
            .OfType<Panel>()
            .SelectMany(panel => panel.Controls.OfType<Panel>())
            .Where(panel => string.Equals(
                panel.Tag as string,
                "SettingRow",
                StringComparison.Ordinal))
            .ToArray();
        if (settingRows.Length != 5 || settingRows.Any(row =>
                row.Controls.Cast<Control>().Count(control =>
                    control is CheckBox or TextBox) != 1))
        {
            throw new InvalidOperationException("截图设置页面没有使用五行单列设置项。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void RunNotificationCapturePolicySmoke()
    {
        VerifyNotificationCapturePolicy(dismissBeforeCapture: true);
        VerifyNotificationCapturePolicy(dismissBeforeCapture: false);
    }

    private static void RunMainWindowCaptureVisibilitySmoke()
    {
        VerifyMainWindowCaptureVisibility(hideMainWindowDuringCapture: false);
        VerifyMainWindowCaptureVisibility(hideMainWindowDuringCapture: true);
    }

    private static void RunMainNavigationSmoke(string outputPath)
    {
        using var settings = new PreviewSettingsStore(Path.GetTempPath());
        using var form = new MainForm(
            settings,
            new PreviewHotkeyService(),
            new PreviewCaptureService(),
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(),
            new PreviewStartupRegistrationService(),
            enableBackgroundIntegration: false)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(60, 60)
        };
        var shell = (AppShellControl)typeof(MainForm).GetField(
                "_shell",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(form)!;
        shell.SelectPage("screenshot-tool.screen-recording.settings");
        form.Show();
        form.Activate();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        if (form.Icon is null || form.Icon.Width <= 0 || form.Icon.Height <= 0)
        {
            throw new InvalidOperationException("主窗口没有加载轻截专属应用图标。");
        }
        var trayIcon = (NotifyIcon)typeof(MainForm).GetField(
                "_trayIcon",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(form)!;
        if (trayIcon.Icon is null)
        {
            throw new InvalidOperationException("系统托盘没有加载轻截专属应用图标。");
        }

        var navigation = (FlowLayoutPanel)typeof(AppShellControl).GetField(
                "_navigation",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(shell)!;
        if (navigation.HorizontalScroll.Visible)
        {
            throw new InvalidOperationException("主界面左侧导航仍然显示横向滚动条。");
        }
        var navigationTexts = navigation.Controls
            .OfType<Button>()
            .Select(button => button.Text)
            .ToArray();
        if (navigationTexts.Contains("长截图", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("未安装长截图模块时仍显示了长截图设置入口。");
        }
        if (!navigationTexts.Contains("截图设置", StringComparer.Ordinal) ||
            !navigationTexts.Contains("插件模块", StringComparer.Ordinal) ||
            navigationTexts.Contains("快捷键设置", StringComparer.Ordinal) ||
            navigationTexts.Contains("图片复制", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("主界面没有把快捷键整合进截图设置分页。");
        }
        if (!navigationTexts.Contains("录屏设置", StringComparer.Ordinal) ||
            shell.SelectedPageId != "screenshot-tool.screen-recording.settings" ||
            !shell.VersionText.Equals("v1.10.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"主界面没有正确显示录屏分页或版本号，当前页面 {shell.SelectedPageId}，版本 {shell.VersionText}。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();

        using var emptyModuleSettings = new PreviewSettingsStore(Path.GetTempPath());
        using var emptyModuleForm = new MainForm(
            emptyModuleSettings,
            new PreviewHotkeyService(),
            new PreviewCaptureService(),
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(includeScreenRecording: false),
            new PreviewStartupRegistrationService(),
            enableBackgroundIntegration: false);
        var emptyShell = (AppShellControl)typeof(MainForm).GetField(
                "_shell",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(emptyModuleForm)!;
        var emptyNavigation = (FlowLayoutPanel)typeof(AppShellControl).GetField(
                "_navigation",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(emptyShell)!;
        var emptyNavigationTexts = emptyNavigation.Controls
            .OfType<Button>()
            .Select(button => button.Text)
            .ToArray();
        if (emptyNavigationTexts.Contains("长截图", StringComparer.Ordinal) ||
            emptyNavigationTexts.Contains("录屏设置", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("未安装录屏和长截图模块时仍显示了模块设置入口。");
        }
        if (!emptyNavigationTexts.Contains("截图设置", StringComparer.Ordinal) ||
            !emptyNavigationTexts.Contains("插件模块", StringComparer.Ordinal) ||
            emptyNavigationTexts.Contains("快捷键设置", StringComparer.Ordinal) ||
            emptyNavigationTexts.Contains("图片复制", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("无模块时截图设置分页或合并后的导航不正确。");
        }
    }

    private static void RunModuleManagementPageSmoke(string outputPath)
    {
        using var settings = new PreviewSettingsStore(Path.GetTempPath());
        using var form = new MainForm(
            settings,
            new PreviewHotkeyService(),
            new PreviewCaptureService(),
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(),
            new PreviewStartupRegistrationService(),
            enableBackgroundIntegration: false)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80)
        };
        var shell = (AppShellControl)typeof(MainForm).GetField(
                "_shell",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(form)!;
        shell.SelectPage("modules");
        form.Show();
        form.Activate();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        var page = (ModuleManagementPage)typeof(MainForm).GetField(
                "_moduleManagementPage",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(form)!;
        var content = (FlowLayoutPanel)typeof(ModuleManagementPage).GetField(
                "_content",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(page)!;
        if (content.FlowDirection != FlowDirection.TopDown || content.WrapContents)
        {
            throw new InvalidOperationException("插件模块页面没有使用纵向单列布局。");
        }
        var pageText = string.Join(
            '\n',
            page.Controls.Cast<Control>().SelectMany(GetControlTree).Select(control => control.Text));
        foreach (var expectedText in new[] { "录屏", "禁用模块", "永久删除", "前往下载" })
        {
            if (!pageText.Contains(expectedText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"插件模块页面缺少操作：{expectedText}");
            }
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static IEnumerable<Control> GetControlTree(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (var descendant in GetControlTree(child))
            {
                yield return descendant;
            }
        }
    }

    private static void VerifyMainWindowCaptureVisibility(bool hideMainWindowDuringCapture)
    {
        MainForm? form = null;
        var captureService = new VisibilityRecordingCaptureService(() => form);
        using var settings = new PreviewSettingsStore(
            Path.GetTempPath(),
            dismissSaveNotificationBeforeCapture: true,
            hideMainWindowDuringCapture: hideMainWindowDuringCapture);
        form = new MainForm(
            settings,
            new PreviewHotkeyService(),
            captureService,
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(),
            new PreviewStartupRegistrationService(),
            enableBackgroundIntegration: false)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(120, 120)
        };
        using (form)
        using (var closeOverlayTimer = new System.Windows.Forms.Timer { Interval = 25 })
        {
            closeOverlayTimer.Tick += (_, _) =>
            {
                foreach (var overlay in System.Windows.Forms.Application.OpenForms
                             .OfType<CaptureOverlayForm>()
                             .ToArray())
                {
                    overlay.Close();
                }
            };
            form.Show();
            form.Activate();
            System.Windows.Forms.Application.DoEvents();
            closeOverlayTimer.Start();
            typeof(MainForm).GetMethod(
                    "BeginCapture",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, null);

            var capturingField = typeof(MainForm).GetField(
                "_isCapturing",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!;
            var deadline = Environment.TickCount64 + 4000;
            while ((!captureService.Captured ||
                    System.Windows.Forms.Application.OpenForms.OfType<CaptureOverlayForm>().Any() ||
                    (bool)capturingField.GetValue(form)!) &&
                   Environment.TickCount64 < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
            System.Windows.Forms.Application.DoEvents();
            closeOverlayTimer.Stop();

            if (!captureService.Captured)
            {
                throw new InvalidOperationException("主界面可见性测试没有进入桌面抓屏阶段。");
            }
            if (hideMainWindowDuringCapture &&
                (captureService.MainWindowVisible || captureService.MainWindowOpacity > 0D))
            {
                throw new InvalidOperationException("开启隐藏开关后，主界面在抓屏时仍然可见。");
            }
            if (!hideMainWindowDuringCapture &&
                (!captureService.MainWindowVisible || captureService.MainWindowOpacity <= 0D))
            {
                throw new InvalidOperationException("默认宣传模式错误隐藏了轻截主界面。");
            }
            if (Math.Abs(form.Opacity - 1D) > 0.001D ||
                (!hideMainWindowDuringCapture && !form.Visible))
            {
                throw new InvalidOperationException("截图结束后轻截主界面没有恢复原始显示状态。");
            }
        }
    }

    private static void RunEditorAlignmentPageSmoke(string outputPath)
    {
        using var form = new Form
        {
            Text = "轻截 - 编辑与对齐设置",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 640),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new EditorSettingsPage(
            ToolWidthRange.Create(2, 8),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle,
            snappingEnabled: true,
            snapThresholdPixels: 8,
            ctrlDragStepPixels: 10)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 604)
        };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        form.BringToFront();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        if (!page.SnappingEnabled ||
            page.SnapThresholdPixels != 8 ||
            page.CtrlDragStepPixels != 10)
        {
            throw new InvalidOperationException("编辑设置页面没有恢复元素吸附与 Ctrl 拖动参数。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void RunDrawingCoefficientsPageSmoke(string outputPath)
    {
        using var form = new Form
        {
            Text = "轻截 - 绘制系数设置",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 640),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new DrawingCoefficientsSettingsPage(new DrawingToolCoefficients())
        {
            Location = new Point(18, 18),
            Size = new Size(724, 604)
        };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        form.BringToFront();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        var settingRows = page.Controls
            .OfType<Panel>()
            .SelectMany(panel => panel.Controls.OfType<Panel>())
            .Where(panel => string.Equals(
                panel.Tag as string,
                "SettingRow",
                StringComparison.Ordinal))
            .ToArray();
        if (settingRows.Length != 7)
        {
            throw new InvalidOperationException("绘制系数页面没有使用七行单列设置项。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void VerifyNotificationCapturePolicy(bool dismissBeforeCapture)
    {
        using var form = new MainForm(
            new PreviewSettingsStore(Path.GetTempPath(), dismissBeforeCapture),
            new PreviewHotkeyService(),
            new PreviewCaptureService(),
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewFileLocationService(),
            new PreviewModuleManager(),
            new PreviewStartupRegistrationService(),
            enableBackgroundIntegration: false);
        var showNotification = typeof(MainForm).GetMethod(
            "ShowSavedArtifactNotification",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!;
        var notificationField = typeof(MainForm).GetField(
            "_savedArtifactNotification",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!;
        showNotification.Invoke(form, [Path.Combine(Path.GetTempPath(), "截图_通知测试.png")]);
        System.Windows.Forms.Application.DoEvents();
        var notification = notificationField.GetValue(form) as SavedArtifactNotificationForm ??
                           throw new InvalidOperationException("测试保存提示没有显示。");

        form.ApplySavedArtifactNotificationCaptureStartPolicy();
        System.Windows.Forms.Application.DoEvents();
        var notificationAfterPolicy = notificationField.GetValue(form);
        if (dismissBeforeCapture && notificationAfterPolicy is not null)
        {
            throw new InvalidOperationException("开启开关后，下次截图前没有关闭保存提示。");
        }
        if (!dismissBeforeCapture && !ReferenceEquals(notificationAfterPolicy, notification))
        {
            throw new InvalidOperationException("关闭开关后，保存提示没有保留用于演示跳转功能。");
        }
    }

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventMoveNoCoalesce = 0x2000;
    private const uint WindowMessageLeftButtonDown = 0x0201;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private static void MovePointer(Point screenPoint) => Cursor.Position = screenPoint;

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(
        uint flags,
        uint dx,
        uint dy,
        uint data,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);
}

internal sealed class PreviewSettingsStore(
    string outputFolder,
    bool dismissSaveNotificationBeforeCapture = true,
    bool hideMainWindowDuringCapture = false) : ISettingsStore, IDisposable
{
    private AppSettings _settings = new()
    {
        OutputFolder = outputFolder,
        Preferences = new UserPreferences
        {
            DismissSaveNotificationBeforeCapture = dismissSaveNotificationBeforeCapture,
            HideMainWindowDuringCapture = hideMainWindowDuringCapture
        }
    };

    public string ProfileId => "preview";

    public AppSettings Load() => _settings;

    public void Save(AppSettings settings) => _settings = settings;

    public void Dispose()
    {
    }
}

internal sealed class PreviewHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? Pressed
    {
        add { }
        remove { }
    }

    public bool TryRegister(HotkeyDefinition hotkey, out string? error)
    {
        error = null;
        return true;
    }

    public void Unregister()
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class PreviewCaptureService : IScreenCaptureService
{
    public DesktopSnapshot CaptureDesktop() => throw new NotSupportedException("界面预览不执行截图。");
}

internal sealed class VisibilityRecordingCaptureService(Func<MainForm?> formProvider)
    : IScreenCaptureService
{
    public bool Captured { get; private set; }

    public bool MainWindowVisible { get; private set; }

    public double MainWindowOpacity { get; private set; }

    public DesktopSnapshot CaptureDesktop()
    {
        var form = formProvider() ?? throw new InvalidOperationException("测试主窗口尚未创建。");
        Captured = true;
        MainWindowVisible = form.Visible;
        MainWindowOpacity = form.Opacity;
        var image = new Bitmap(320, 240);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.FromArgb(22, 28, 36));
        }
        return new DesktopSnapshot(image, new Rectangle(0, 0, image.Width, image.Height));
    }
}

internal sealed class PreviewImageSaveService : IImageSaveService
{
    public string SavePng(
        Bitmap image,
        string outputFolder,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null) =>
        throw new NotSupportedException("界面预览不保存截图。");
}

internal sealed class PreviewClipboardService : IClipboardService
{
    public void SetImage(Image image)
    {
    }

    public Bitmap? GetImage() => null;

    public string? GetText() => null;

    public void SetText(string text)
    {
    }
}

internal sealed class PreviewWindowLocator : IWindowLocator
{
    public Rectangle? FindWindowAt(Point screenPoint) => null;
}

internal sealed class PreviewFileLocationService : IFileLocationService
{
    public void OpenFolder(string folderPath)
    {
    }

    public void ShowFileInFolder(string filePath)
    {
    }

    public void OpenFile(string filePath)
    {
    }

    public void OpenWebPage(Uri uri)
    {
    }
}

internal sealed class PreviewStartupRegistrationService : IStartupRegistrationService
{
    public bool IsEnabled { get; private set; } = true;

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

internal sealed class PreviewModuleManager(bool includeScreenRecording = true) : IModuleManager
{
    private readonly bool _includeScreenRecording = includeScreenRecording;
    private bool _refreshed;
    private bool _packageExists = includeScreenRecording;
    private bool _packageEnabled = true;

    public string ModulesDirectory => Path.Combine(Path.GetTempPath(), "LightShotUiPreviewModules");

    public ModuleRefreshResult Refresh(bool force = false)
    {
        var changed = force || !_refreshed;
        _refreshed = true;
        return new([], [], changed);
    }

    public IReadOnlyList<ModuleInfo> GetModules() => [];

    public IReadOnlyList<ModulePackageInfo> GetInstalledPackages() => _packageExists
        ?
        [
            new ModulePackageInfo(
                "ScreenRecording",
                "lightshot.screen-recording",
                "录屏",
                new Version(1, 0),
                Path.Combine(ModulesDirectory, "ScreenRecording"),
                _packageEnabled ? ModulePackageState.Enabled : ModulePackageState.Disabled)
        ]
        : [];

    public ModuleOperationResult SetPackageEnabled(string packageName, bool enabled)
    {
        _packageEnabled = enabled;
        return new(true, enabled ? "已启用录屏" : "已禁用录屏", new([], [], true));
    }

    public ModuleOperationResult DeletePackage(string packageName)
    {
        _packageExists = false;
        return new(true, "已永久删除录屏", new([], [], true));
    }

    public IReadOnlyList<ICaptureFeature> CreateCaptureFeatures() => [];

    public IReadOnlyList<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host) =>
        _includeScreenRecording
            ? [new ScreenRecordingSettingsPage(host)]
            : [];

    public void Dispose()
    {
    }
}

internal sealed class PreviewModuleSettingsHost : IModuleSettingsHost
{
    private readonly Dictionary<string, bool> _booleans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _integers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public bool GetBoolean(string id, bool defaultValue) =>
        _booleans.TryGetValue(id, out var value) ? value : defaultValue;

    public int GetInteger(string id, int defaultValue) =>
        _integers.TryGetValue(id, out var value) ? value : defaultValue;

    public string GetString(string id, string defaultValue) =>
        _strings.TryGetValue(id, out var value) ? value : defaultValue;

    public void SetBoolean(string id, bool value) => _booleans[id] = value;

    public void SetInteger(string id, int value) => _integers[id] = value;

    public void SetString(string id, string value) => _strings[id] = value;

    public void Save()
    {
    }
}
