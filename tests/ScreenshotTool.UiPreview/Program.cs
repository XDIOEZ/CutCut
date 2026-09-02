using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;
using ScreenshotTool.Presentation;
using ScreenshotTool.Presentation.Pages;
using ScreenshotTool.Presentation.Shell;
using ScreenshotTool.Presentation.Theme;
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
        if (args is ["--general-settings-page-smoke", var generalSettingsOutputPath])
        {
            RunGeneralSettingsPageSmoke(generalSettingsOutputPath);
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
        if (args is ["--application-update-page-smoke", var updatePageOutputPath])
        {
            RunApplicationUpdatePageSmoke(updatePageOutputPath);
            return 0;
        }
        if (args is ["--gallery-context-menu-smoke", var galleryMenuOutputPath])
        {
            RunGalleryContextMenuSmoke(galleryMenuOutputPath);
            return 0;
        }
        if (args is ["--existing-image-edit-smoke", var existingImageOutputPath])
        {
            RunExistingImageEditSmoke(existingImageOutputPath);
            return 0;
        }
        if (args is ["--capture-outside-interaction-smoke", var outsideInteractionOutputPath])
        {
            RunCaptureOutsideInteractionSmoke(outsideInteractionOutputPath);
            return 0;
        }
        if (args is ["--capture-background-refresh-smoke", var backgroundRefreshOutputPath])
        {
            RunCaptureBackgroundRefreshSmoke(backgroundRefreshOutputPath);
            return 0;
        }
        if (args is ["--capture-dispose-smoke"])
        {
            RunCaptureDisposeSmoke();
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
            new PreviewSavedScreenshotService(),
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
            "OCR",
            enableTranslation: true);
    }

    private static void RunQrCodeResultSmoke(string outputPath)
    {
        RunTextResultSmoke(
            outputPath,
            "二维码扫描结果",
            "https://example.com/cutcut?source=qr\r\n\r\nWIFI:T:WPA;S:LightShot;P:12345678;;",
            "二维码",
            enableTranslation: false);
    }

    private static void RunTextResultSmoke(
        string outputPath,
        string title,
        string text,
        string featureName,
        bool enableTranslation)
    {
        var screen = Screen.PrimaryScreen ?? throw new InvalidOperationException("找不到主显示器。");
        var anchor = new Rectangle(
            screen.WorkingArea.Left + 40,
            screen.WorkingArea.Top + 80,
            Math.Min(720, screen.WorkingArea.Width / 2),
            360);
        const string translatedText = "轻截文字识别\r\n\r\n敏捷的棕色狐狸跳过了懒狗。\r\n2026-07-22";
        var translationService = enableTranslation
            ? new PreviewTextTranslationService(translatedText)
            : null;
        using var resultWindow = new CaptureTextResultForm(
            title,
            text,
            anchor,
            new PreviewClipboardService(),
            translationService);
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
        if (resultWindow.TranslationAvailable != enableTranslation)
        {
            throw new InvalidOperationException(
                $"{featureName} 结果窗口的翻译按钮显示状态不正确。");
        }

        Button? translateButton = null;
        if (enableTranslation)
        {
            translateButton = resultWindow.Controls
                .Find("TranslateButton", searchAllChildren: true)
                .OfType<Button>()
                .Single();
            translateButton.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            if (!resultWindow.IsTranslated ||
                !string.Equals(
                    resultWindow.ResultText,
                    translatedText,
                    StringComparison.Ordinal) ||
                !string.Equals(translateButton.Text, "显示原文", StringComparison.Ordinal) ||
                translationService?.CallCount != 1 ||
                !string.Equals(translationService.LastText, text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("OCR 结果窗口没有切换到中文翻译结果。");
            }
        }

        using var captured = new Bitmap(resultWindow.Width, resultWindow.Height);
        resultWindow.DrawToBitmap(
            captured,
            new Rectangle(Point.Empty, captured.Size));
        if (translateButton is not null)
        {
            translateButton.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            if (resultWindow.IsTranslated ||
                !string.Equals(resultWindow.ResultText, text, StringComparison.Ordinal) ||
                translationService?.CallCount != 1)
            {
                throw new InvalidOperationException(
                    "OCR 结果窗口再次点击翻译按钮后没有在本地恢复原文。");
            }

            translateButton.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            if (!resultWindow.IsTranslated ||
                !string.Equals(resultWindow.ResultText, translatedText, StringComparison.Ordinal) ||
                translationService?.CallCount != 1)
            {
                throw new InvalidOperationException(
                    "OCR 结果窗口没有复用已有译文进行双向切换。");
            }

            translateButton.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            var editableResult = resultWindow.Controls.OfType<TextBox>().Single();
            const string editedSource = "Edited source after restoring the original.";
            editableResult.Text = editedSource;
            translateButton.PerformClick();
            System.Windows.Forms.Application.DoEvents();
            if (!resultWindow.IsTranslated ||
                translationService?.CallCount != 2 ||
                !string.Equals(
                    translationService.LastText,
                    editedSource,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "OCR 原文被编辑后没有使用最新文字重新翻译。");
            }
        }
        resultWindow.Close();
        System.Windows.Forms.Application.DoEvents();

        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void RunGalleryContextMenuSmoke(string outputPath)
    {
        var galleryFolder = Path.Combine(
            Path.GetTempPath(),
            $"LightShotGalleryMenuSmoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(galleryFolder);
        try
        {
            var firstGalleryImagePath = Path.Combine(galleryFolder, "第一张截图.png");
            var newestGalleryImagePath = Path.Combine(galleryFolder, "准备编辑的截图.png");
            var galleryVideoPath = Path.Combine(galleryFolder, "录屏示例.mp4");
            CreateGallerySmokeImage(
                firstGalleryImagePath,
                Color.FromArgb(36, 99, 235),
                "第一张截图");
            CreateGallerySmokeImage(
                newestGalleryImagePath,
                Color.FromArgb(22, 163, 74),
                "右键编辑");
            File.WriteAllBytes(galleryVideoPath, [0, 0, 0, 0]);
            var galleryBaseTime = new DateTime(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(firstGalleryImagePath, galleryBaseTime);
            File.SetLastWriteTimeUtc(galleryVideoPath, galleryBaseTime.AddSeconds(30));
            File.SetLastWriteTimeUtc(newestGalleryImagePath, galleryBaseTime.AddMinutes(1));

            using var galleryClipboard = new RecordingPreviewClipboardService();
            using var page = new ScreenshotGalleryPage(
                galleryFolder,
                new PreviewFileLocationService(),
                new PreviewSavedScreenshotService(),
                galleryClipboard)
            {
                Dock = DockStyle.Fill
            };
            using var form = new Form
            {
                Text = "轻截 - 查看截图右键菜单验证",
                StartPosition = FormStartPosition.Manual,
                Location = new Point(80, 80),
                ClientSize = new Size(760, 480),
                BackColor = AppTheme.Canvas
            };
            form.Controls.Add(page);
            form.Show();
            page.RefreshScreenshots();

            var listView = (ListView)typeof(ScreenshotGalleryPage).GetField(
                    "_listView",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(page)!;
            var menu = (ContextMenuStrip)typeof(ScreenshotGalleryPage).GetField(
                    "_itemMenu",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(page)!;
            var searchInput = (TextBox)typeof(ScreenshotGalleryPage).GetField(
                    "_searchInput",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(page)!;
            var sortButton = (Button)typeof(ScreenshotGalleryPage).GetField(
                    "_sortButton",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(page)!;
            var sortMenu = (ContextMenuStrip)typeof(ScreenshotGalleryPage).GetField(
                    "_sortMenu",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(page)!;
            WaitForUiCondition(
                () => listView.Items.Count == 3,
                TimeSpan.FromSeconds(5),
                "截图画廊异步刷新未在限定时间内加载三个文件。");
            if (listView.Items.Count != 3 ||
                listView.Items.Cast<ListViewItem>().All(item => item.Text != "录屏示例.mp4"))
            {
                throw new InvalidOperationException(
                    $"截图画廊没有加载两张图片和一个视频，实际为 {listView.Items.Count} 个文件。");
            }
            if (searchInput.PlaceholderText != "按文件名搜索" ||
                sortButton.Text != "时间：新→旧")
            {
                throw new InvalidOperationException("截图画廊搜索框或默认排序按钮文案不正确。");
            }
            var sortItems = sortMenu.Items.OfType<ToolStripMenuItem>().ToArray();
            if (sortItems.Length != 4 ||
                !sortItems[0].Checked ||
                string.Join(',', sortItems.Select(item => item.Text)) !=
                "保存时间（最新优先）,保存时间（最早优先）,名称（A → Z）,名称（Z → A）")
            {
                throw new InvalidOperationException("截图画廊排序菜单没有提供完整的四种排序模式。");
            }

            searchInput.Text = "第一张";
            WaitForUiCondition(
                () => listView.Items.Count == 1 &&
                      listView.Items[0].Text == "第一张截图.png",
                TimeSpan.FromSeconds(5),
                "截图画廊异步搜索未在限定时间内完成。");
            if (listView.Items.Count != 1 || listView.Items[0].Text != "第一张截图.png")
            {
                throw new InvalidOperationException("截图画廊没有按文件名实时筛选。");
            }
            searchInput.Clear();
            WaitForUiCondition(
                () => listView.Items.Count == 3,
                TimeSpan.FromSeconds(5),
                "截图画廊清空搜索后未在限定时间内恢复全部文件。");

            var oldestFirstItem = sortMenu.Items
                .OfType<ToolStripMenuItem>()
                .Single(item => item.Text == "保存时间（最早优先）");
            oldestFirstItem.PerformClick();
            WaitForUiCondition(
                () => listView.Items.Count == 3 &&
                      listView.Items[0].Text == "第一张截图.png" &&
                      sortButton.Text == "时间：旧→新",
                TimeSpan.FromSeconds(5),
                "截图画廊异步正序排序未在限定时间内完成。");
            if (listView.Items.Count != 3 ||
                listView.Items[0].Text != "第一张截图.png" ||
                sortButton.Text != "时间：旧→新")
            {
                throw new InvalidOperationException("截图画廊没有按保存时间正序排列。");
            }
            sortMenu.Items
                .OfType<ToolStripMenuItem>()
                .Single(item => item.Text == "保存时间（最新优先）")
                .PerformClick();
            WaitForUiCondition(
                () => listView.Items.Count == 3 &&
                      listView.Items[0].Text == "准备编辑的截图.png" &&
                      sortButton.Text == "时间：新→旧",
                TimeSpan.FromSeconds(5),
                "截图画廊异步倒序排序未在限定时间内完成。");

            var selectedItem = listView.Items[0];
            selectedItem.Selected = true;
            selectedItem.Focused = true;
            var editItem = menu.Items["EditScreenshotMenuItem"] ??
                           throw new InvalidOperationException("右键菜单缺少“编辑”选项。");
            var copyItem = menu.Items["CopyScreenshotMenuItem"] ??
                           throw new InvalidOperationException("右键菜单缺少“复制”选项。");
            var deleteItem = menu.Items["DeleteScreenshotMenuItem"] ??
                             throw new InvalidOperationException("右键菜单缺少“删除”选项。");
            if (editItem.Text != "编辑" ||
                copyItem.Text != "复制" ||
                deleteItem.Text != "删除" ||
                string.Join(
                    ',',
                    menu.Items.OfType<ToolStripMenuItem>().Select(item => item.Text)) !=
                "编辑,复制,删除")
            {
                throw new InvalidOperationException("截图右键菜单的编辑、复制或删除文字不正确。");
            }

            menu.Show(
                listView,
                new Point(
                    Math.Max(20, selectedItem.Bounds.Left + 84),
                    Math.Max(20, selectedItem.Bounds.Top + 52)));
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(120);
            System.Windows.Forms.Application.DoEvents();

            using (var captured = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
                using var menuBitmap = new Bitmap(menu.Width, menu.Height);
                menu.DrawToBitmap(
                    menuBitmap,
                    new Rectangle(Point.Empty, menu.Size));
                var menuLocation = form.PointToClient(menu.PointToScreen(Point.Empty));
                using var graphics = Graphics.FromImage(captured);
                graphics.DrawImageUnscaled(menuBitmap, menuLocation);
                var fullOutputPath = Path.GetFullPath(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
                captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            string? requestedEditPath = null;
            page.EditRequested += (_, eventArgs) => requestedEditPath = eventArgs.Path;
            menu.Close();
            copyItem.PerformClick();
            if (galleryClipboard.CopiedImage is null ||
                galleryClipboard.CopiedImage.Size != new Size(640, 360) ||
                galleryClipboard.CopiedImage.GetPixel(0, 0).ToArgb() !=
                Color.FromArgb(22, 163, 74).ToArgb())
            {
                throw new InvalidOperationException("复制菜单没有把当前选中的原始截图写入剪贴板。");
            }
            editItem.PerformClick();
            if (!string.Equals(
                    Path.GetFullPath((string)selectedItem.Tag!),
                    Path.GetFullPath(requestedEditPath ?? string.Empty),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("编辑菜单没有回传当前选中的截图路径。");
            }

            foreach (ListViewItem item in listView.Items)
            {
                item.Selected = item.Text == "录屏示例.mp4";
                item.Focused = item.Selected;
            }
            menu.Show(listView, new Point(20, 20));
            System.Windows.Forms.Application.DoEvents();
            if (editItem.Visible || copyItem.Visible || !deleteItem.Visible)
            {
                throw new InvalidOperationException(
                    "视频右键菜单应隐藏图片专用的编辑和复制，只保留删除。");
            }
            menu.Close();

            form.Close();
            System.Windows.Forms.Application.DoEvents();
        }
        finally
        {
            Directory.Delete(galleryFolder, recursive: true);
        }
    }

    private static void CreateGallerySmokeImage(
        string path,
        Color background,
        string title)
    {
        using var image = new Bitmap(640, 360);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(background);
        using var font = new Font("Microsoft YaHei UI", 30F, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        graphics.DrawString(title, font, brush, new PointF(34, 44));
        graphics.FillRectangle(brush, new Rectangle(34, 126, 400, 8));
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    // Pumps WinForms messages until an asynchronous preview condition is satisfied.
    private static void WaitForUiCondition(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed >= timeout)
            {
                throw new InvalidOperationException(failureMessage);
            }

            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static void RunExistingImageEditSmoke(string outputPath)
    {
        var screen = Screen.PrimaryScreen ??
                     throw new InvalidOperationException("找不到主显示器。");
        var bounds = new Rectangle(
            screen.WorkingArea.Left + 40,
            screen.WorkingArea.Top + 40,
            Math.Min(900, screen.WorkingArea.Width - 80),
            Math.Min(620, screen.WorkingArea.Height - 80));
        if (bounds.Width < 640 || bounds.Height < 420)
        {
            throw new InvalidOperationException("主显示器空间不足，无法验证已有截图编辑界面。");
        }

        using var snapshotImage = new Bitmap(bounds.Width, bounds.Height);
        using (var snapshotGraphics = Graphics.FromImage(snapshotImage))
        {
            snapshotGraphics.Clear(Color.FromArgb(31, 41, 55));
        }
        using var snapshot = new DesktopSnapshot((Bitmap)snapshotImage.Clone(), bounds);
        using var existingImage = new Bitmap(640, 360);
        using (var existingGraphics = Graphics.FromImage(existingImage))
        {
            existingGraphics.Clear(Color.FromArgb(15, 23, 42));
            using var accent = new SolidBrush(Color.FromArgb(239, 68, 68));
            existingGraphics.FillRectangle(accent, new Rectangle(40, 48, 240, 124));
            using var font = new Font("Microsoft YaHei UI", 26F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            existingGraphics.DrawString("正在编辑这张截图", font, textBrush, 46, 214);
        }

        var width = new ToolWidthController(ToolWidthRange.Create(1, 32), 4);
        var annotations = new LiveAnnotationSessionFactory(
            new PreviewClipboardService(),
            new DrawingToolCoefficients(),
            AnnotationRotationStep.DefaultDegrees,
            DrawingCursorShape.Circle);
        using var existingImageClipboard = new RecordingPreviewClipboardService
        {
            Text = "粘贴文字继续编辑"
        };
        var overlay = new CaptureOverlayForm(
            snapshot,
            new PreviewImageSaveService(),
            existingImageClipboard,
            new PreviewWindowLocator(),
            new PreviewModuleManager(),
            SelectionMoveAnnotationStrategyFactory.Create(StickerSelectionMoveMode.FollowSelection),
            width,
            annotations,
            new Dictionary<string, bool>(),
            new Dictionary<string, int>(),
            Path.GetTempPath(),
            ScreenshotFileNameMode.DateTime,
            initialEditImage: existingImage);
        overlay.Show();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        var featureHost = (ICaptureFeatureHost)overlay;
        var liveFeatureHost = (ILiveCaptureFeatureHost)overlay;
        if (!featureHost.HasSelection || !liveFeatureHost.HasEdits)
        {
            throw new InvalidOperationException("已有截图没有作为当前截图选区进入编辑模式。");
        }
        if (!overlay.Text.Contains("编辑已有截图", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("已有截图编辑窗口没有显示正确的编辑状态。");
        }

        typeof(CaptureOverlayForm)
            .GetMethod(
                "PasteClipboardContent",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, null);
        var pastedInputEditor = (TransparentTextEditorControl?)typeof(CaptureOverlayForm)
            .GetField(
                "_textEditor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(overlay);
        if (pastedInputEditor is null ||
            pastedInputEditor.Text != "粘贴文字继续编辑")
        {
            throw new InvalidOperationException(
                "已有截图粘贴文字没有进入统一文字输入框。");
        }
        typeof(CaptureOverlayForm)
            .GetMethod(
                "CancelTextEditor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [true]);
        var annotationEditor = (CaptureAnnotationEditor)typeof(CaptureOverlayForm)
            .GetField(
                "_annotationEditor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(overlay)!;
        var pastedAnnotation = annotationEditor.Document
            .GetMovableAnnotations()
            .Single();
        if (pastedAnnotation is not TextAnnotation textAnnotation ||
            textAnnotation.Text != "粘贴文字继续编辑")
        {
            throw new InvalidOperationException(
                "已有截图粘贴文字提交后没有生成普通文字元素。");
        }
        if (!textAnnotation.SupportsResize ||
            !ReferenceEquals(annotationEditor.Selection.Primary, textAnnotation))
        {
            throw new InvalidOperationException(
                "文字框提交并选中后没有启用缩放交互。");
        }
        typeof(CaptureOverlayForm)
            .GetMethod(
                "BeginExistingTextEditor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [textAnnotation]);
        var reeditInput = (TransparentTextEditorControl?)typeof(CaptureOverlayForm)
            .GetField(
                "_textEditor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(overlay);
        if (reeditInput is null || annotationEditor.Selection.Count != 0)
        {
            throw new InvalidOperationException(
                "文字框进入编辑模式后没有关闭缩放交互。");
        }
        typeof(CaptureOverlayForm)
            .GetMethod(
                "CancelTextEditor",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [true]);
        if (!annotationEditor.Undo())
        {
            throw new InvalidOperationException("已有截图无法撤销粘贴文字验证元素。");
        }
        existingImageClipboard.Text = null;

        using (var pastedImage = new Bitmap(24, 12))
        {
            using var pastedImageGraphics = Graphics.FromImage(pastedImage);
            pastedImageGraphics.Clear(Color.LimeGreen);
            existingImageClipboard.SetImage(pastedImage);
        }
        typeof(CaptureOverlayForm)
            .GetMethod(
                "PasteClipboardContent",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, null);
        var selectedImage = annotationEditor.Selection.Primary as StickerAnnotation;
        if (selectedImage is null)
        {
            throw new InvalidOperationException("已有截图没有选中刚粘贴的图片元素。");
        }
        selectedImage.SetBounds(new Rectangle(
            selectedImage.Bounds.X,
            selectedImage.Bounds.Y,
            72,
            36));
        var copyImageShortcut = new KeyEventArgs(Keys.Control | Keys.C);
        typeof(CaptureOverlayForm)
            .GetMethod(
                "HandleKeyDown",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [overlay, copyImageShortcut]);
        if (!copyImageShortcut.SuppressKeyPress ||
            existingImageClipboard.CopiedImage?.Size != selectedImage.Bounds.Size ||
            overlay.IsDisposed)
        {
            throw new InvalidOperationException(
                "选中图片时 Ctrl+C 没有只复制当前图片并保持编辑窗口打开。");
        }
        if (!annotationEditor.Undo())
        {
            throw new InvalidOperationException("已有截图无法撤销粘贴图片验证元素。");
        }

        using (var recognitionInput = featureHost.CopyDesktopSelection())
        {
            if (recognitionInput.Size != existingImage.Size ||
                recognitionInput.GetPixel(80, 90).ToArgb() !=
                Color.FromArgb(239, 68, 68).ToArgb())
            {
                throw new InvalidOperationException(
                    "再次编辑时 OCR 取到的不是当前已有截图原始像素。");
            }
        }

        using (var rendered = (Bitmap)typeof(CaptureOverlayForm).GetMethod(
                       "RenderSelection",
                       System.Reflection.BindingFlags.Instance |
                       System.Reflection.BindingFlags.NonPublic)!
                   .Invoke(overlay, null)!)
        {
            if (rendered.Size != existingImage.Size)
            {
                throw new InvalidOperationException(
                    $"已有截图导出尺寸发生变化：{rendered.Size}。");
            }
            if (rendered.GetPixel(80, 90).ToArgb() != Color.FromArgb(239, 68, 68).ToArgb())
            {
                throw new InvalidOperationException("已有截图的原始像素没有进入最终编辑结果。");
            }
        }

        using (var captured = new Bitmap(overlay.Width, overlay.Height))
        {
            overlay.DrawToBitmap(captured, new Rectangle(Point.Empty, overlay.Size));
            var fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        }

        overlay.Close();
        System.Windows.Forms.Application.DoEvents();
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
            ClientSize = new Size(760, 820),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new SavePathSettingsPage(
            @"C:\Users\User\Pictures\轻截",
            ScreenshotFileNameMode.ImageText,
            organizeByDate: true,
            dateParentFolder: @"D:\截图归档",
            imageFormat: ScreenshotImageFormat.Jpeg)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 784)
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
        if (page.ImageFormat != ScreenshotImageFormat.Jpeg)
        {
            throw new InvalidOperationException("保存设置页面没有恢复 JPEG 图片格式。");
        }
        if (!page.OrganizeByDate)
        {
            throw new InvalidOperationException("保存设置页面没有恢复按日期分类开关。");
        }
        if (!string.Equals(page.DateParentFolder, @"D:\截图归档", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("保存设置页面没有恢复用户绑定的日期父文件夹。");
        }
        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();

    }

    private static void RunGeneralSettingsPageSmoke(string outputPath)
    {
        using var form = new Form
        {
            Text = "轻截 - 通用设置",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 480),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var page = new GeneralSettingsPage(
            startMinimized: true,
            startWithWindows: true)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 444)
        };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        form.BringToFront();
        System.Windows.Forms.Application.DoEvents();
        Thread.Sleep(120);
        System.Windows.Forms.Application.DoEvents();

        if (!page.StartMinimized || !page.StartWithWindows)
        {
            throw new InvalidOperationException(
                "通用设置页面没有恢复开机启动与启动后最小化选项。");
        }
        var settingRows = page.Controls
            .OfType<Panel>()
            .SelectMany(panel => panel.Controls.OfType<Panel>())
            .Where(panel => string.Equals(
                panel.Tag as string,
                "SettingRow",
                StringComparison.Ordinal))
            .ToArray();
        if (settingRows.Length != 2 || settingRows.Any(row =>
                row.Controls.Cast<Control>().Count(control =>
                    control is CheckBox or TextBox) != 1))
        {
            throw new InvalidOperationException(
                "通用设置页面没有使用两行单列设置项。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void RunCaptureOutsideInteractionSmoke(string outputPath)
    {
        var screen = Screen.PrimaryScreen ??
                     throw new InvalidOperationException("找不到主显示器。");
        var bounds = new Rectangle(
            screen.WorkingArea.Left + 40,
            screen.WorkingArea.Top + 40,
            Math.Min(1100, screen.WorkingArea.Width - 80),
            Math.Min(620, screen.WorkingArea.Height - 80));
        if (bounds.Width < 900 || bounds.Height < 420)
        {
            throw new InvalidOperationException("主显示器空间不足，无法验证截图框外鼠标穿透。");
        }

        var originalPointer = Cursor.Position;
        using var backingForm = new Form
        {
            Bounds = bounds,
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(15, 23, 42),
            TopMost = true,
            ShowInTaskbar = false
        };
        backingForm.Show();

        var pinnedBounds = new Rectangle(
            bounds.Left + 20,
            bounds.Top + 20,
            140,
            96);
        var pinnedLeftDownCount = 0;
        var pinnedCopyMenuOpened = false;
        var pinnedCopyInvoked = false;
        using var pinnedMenu = new ContextMenuStrip();
        var pinnedCopy = pinnedMenu.Items.Add(
            "复制",
            null,
            (_, _) => pinnedCopyInvoked = true);
        pinnedCopy.Name = "CopyPinnedImageMenuItem";
        pinnedMenu.Opened += (_, _) => pinnedCopyMenuOpened = true;
        using var pinnedForm = new Form
        {
            Bounds = pinnedBounds,
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(30, 64, 175),
            ContextMenuStrip = pinnedMenu,
            TopMost = true,
            ShowInTaskbar = false
        };
        pinnedForm.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                pinnedLeftDownCount++;
            }
        };
        pinnedForm.Show();

        using var snapshotImage = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(snapshotImage))
        {
            graphics.Clear(backingForm.BackColor);
            using var brush = new SolidBrush(Color.FromArgb(30, 41, 59));
            graphics.FillRectangle(brush, new Rectangle(0, 0, bounds.Width, 84));
        }
        using var snapshot = new DesktopSnapshot((Bitmap)snapshotImage.Clone(), bounds);
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

        Task? overlaySession = null;
        try
        {
            overlaySession = CaptureOverlayPresenter.ShowAsync(overlay);
            System.Windows.Forms.Application.DoEvents();

            typeof(CaptureOverlayForm)
                .GetMethod(
                    "BeginManualSelection",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay, [new Point(190, 130), Rectangle.Empty]);
            typeof(CaptureOverlayForm)
                .GetMethod(
                    "HandleMouseMove",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay,
                [
                    overlay,
                    new MouseEventArgs(MouseButtons.Left, 0, 520, 320, 0)
                ]);
            typeof(CaptureOverlayForm)
                .GetMethod(
                    "HandleMouseUp",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay,
                [
                    overlay,
                    new MouseEventArgs(MouseButtons.Left, 1, 520, 320, 0)
                ]);
            System.Windows.Forms.Application.DoEvents();

            var expectedInitialSelection = Rectangle.FromLTRB(190, 130, 520, 320);
            var actualInitialSelection = ((ICaptureFeatureHost)overlay).Selection;
            if (actualInitialSelection != expectedInitialSelection ||
                overlay.Region is null)
            {
                throw new InvalidOperationException(
                    $"初始截图完成后覆盖层没有收缩到编辑框和工具栏。" +
                    $"Expected={expectedInitialSelection}, Actual={actualInitialSelection}, " +
                    $"Region={overlay.Region is not null}");
            }

            var selectionMoveOrigin = new Point(
                expectedInitialSelection.Left + expectedInitialSelection.Width / 2,
                expectedInitialSelection.Top + expectedInitialSelection.Height / 2);
            typeof(CaptureOverlayForm)
                .GetMethod(
                    "BeginSelectionMove",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay, [selectionMoveOrigin]);
            System.Windows.Forms.Application.DoEvents();
            if (!overlay.Capture ||
                overlay.Region is null ||
                overlay.Region.IsVisible(new Point(20, 20)))
            {
                throw new InvalidOperationException(
                    "拖动截图框取得鼠标捕获后，覆盖层错误扩张到框外并会显示黑色底面。");
            }

            var selectionMoveCurrent = new Point(
                selectionMoveOrigin.X + 20,
                selectionMoveOrigin.Y + 20);
            typeof(CaptureOverlayForm)
                .GetMethod(
                    "HandleMouseMove",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay,
                [
                    overlay,
                    new MouseEventArgs(
                        MouseButtons.Right,
                        0,
                        selectionMoveCurrent.X,
                        selectionMoveCurrent.Y,
                        0)
                ]);
            System.Windows.Forms.Application.DoEvents();

            var expectedMovedSelection = new Rectangle(
                expectedInitialSelection.X + 20,
                expectedInitialSelection.Y + 20,
                expectedInitialSelection.Width,
                expectedInitialSelection.Height);
            var actualMovedSelection = ((ICaptureFeatureHost)overlay).Selection;
            var movedSelectionCenter = new Point(
                expectedMovedSelection.Left + expectedMovedSelection.Width / 2,
                expectedMovedSelection.Top + expectedMovedSelection.Height / 2);
            if (actualMovedSelection != expectedMovedSelection ||
                overlay.Region is null ||
                !overlay.Region.IsVisible(movedSelectionCenter) ||
                overlay.Region.IsVisible(new Point(20, 20)))
            {
                throw new InvalidOperationException(
                    $"拖动截图框时透明裁剪没有跟随新位置。" +
                    $"Expected={expectedMovedSelection}, Actual={actualMovedSelection}");
            }

            typeof(CaptureOverlayForm)
                .GetMethod(
                    "HandleMouseUp",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay,
                [
                    overlay,
                    new MouseEventArgs(
                        MouseButtons.Right,
                        1,
                        selectionMoveCurrent.X,
                        selectionMoveCurrent.Y,
                        0)
                ]);
            System.Windows.Forms.Application.DoEvents();

            if (!pinnedForm.Enabled)
            {
                throw new InvalidOperationException(
                    "截图浮层显示后禁用了已有贴图窗口。");
            }

            var outsidePoint = new Point(
                pinnedBounds.Left + pinnedBounds.Width / 2,
                pinnedBounds.Top + pinnedBounds.Height / 2);
            MovePointer(outsidePoint);
            System.Windows.Forms.Application.DoEvents();
            var outsideHitWindow = WindowFromPoint(outsidePoint);
            var outsideHitRoot = GetAncestor(outsideHitWindow, GetRootAncestor);
            if (outsideHitRoot != pinnedForm.Handle)
            {
                throw new InvalidOperationException(
                    "截图框外没有命中已有贴图窗口。" +
                    $"Expected={pinnedForm.Handle}, Actual={outsideHitRoot}");
            }

            MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            System.Windows.Forms.Application.DoEvents();
            if (pinnedLeftDownCount != 1)
            {
                throw new InvalidOperationException(
                    "截图框外的已有贴图没有收到左键操作。");
            }

            MouseEvent(MouseEventRightDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventRightUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            System.Windows.Forms.Application.DoEvents();
            pinnedCopy.PerformClick();
            if (!pinnedCopyMenuOpened || !pinnedCopyInvoked)
            {
                throw new InvalidOperationException(
                    "截图框外无法打开已有贴图的右键菜单并执行复制。");
            }

            var redrawStart = new Point(bounds.Left + 650, bounds.Top + 70);
            var redrawEnd = new Point(bounds.Left + 790, bounds.Top + 170);
            KeybdEvent(VirtualKeyControl, 0, 0, UIntPtr.Zero);
            MovePointer(redrawStart);
            MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            MovePointer(redrawEnd);
            MouseEvent(MouseEventMove | MouseEventMoveNoCoalesce, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            KeybdEvent(VirtualKeyControl, 0, KeyEventKeyUp, UIntPtr.Zero);
            System.Windows.Forms.Application.DoEvents();

            var expectedRedrawSelection = Rectangle.FromLTRB(650, 70, 790, 170);
            var actualRedrawSelection = ((ICaptureFeatureHost)overlay).Selection;
            if (actualRedrawSelection != expectedRedrawSelection)
            {
                throw new InvalidOperationException(
                    $"Ctrl 加框外左键拖动没有重新框选。" +
                    $"Expected={expectedRedrawSelection}, Actual={actualRedrawSelection}");
            }

            var toolbar = (CaptureEditorToolbar)typeof(CaptureOverlayForm)
                .GetField(
                    "_toolbar",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(overlay)!;
            var toolbarCenter = new Point(
                toolbar.Left + toolbar.Width / 2,
                toolbar.Top + toolbar.Height / 2);
            if (!toolbar.Visible ||
                overlay.Region is null ||
                !overlay.Region.IsVisible(toolbarCenter))
            {
                throw new InvalidOperationException(
                    "重新框选完成后工具栏没有保留在覆盖层交互区域中。");
            }
            Thread.Sleep(120);
            System.Windows.Forms.Application.DoEvents();

            using var captured = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(captured))
            {
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            var fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            KeybdEvent(VirtualKeyControl, 0, KeyEventKeyUp, UIntPtr.Zero);
            overlay.Close();
            System.Windows.Forms.Application.DoEvents();
            overlaySession?.GetAwaiter().GetResult();
            pinnedForm.Close();
            backingForm.Close();
            Cursor.Position = originalPointer;
            System.Windows.Forms.Application.DoEvents();
        }
    }

    private static void RunCaptureBackgroundRefreshSmoke(string outputPath)
    {
        var screen = Screen.PrimaryScreen ??
                     throw new InvalidOperationException("找不到主显示器。");
        var bounds = new Rectangle(
            screen.WorkingArea.Left + 40,
            screen.WorkingArea.Top + 40,
            Math.Min(1100, screen.WorkingArea.Width - 80),
            Math.Min(620, screen.WorkingArea.Height - 80));
        if (bounds.Width < 900 || bounds.Height < 420)
        {
            throw new InvalidOperationException("主显示器空间不足，无法验证截图背景刷新。");
        }

        var originalPointer = Cursor.Position;
        var initialColor = Color.FromArgb(91, 33, 42);
        var firstRefreshColor = Color.FromArgb(30, 64, 175);
        var blockedRefreshColor = Color.FromArgb(202, 138, 4);
        var secondRefreshColor = Color.FromArgb(5, 150, 105);
        var editingRefreshColor = Color.FromArgb(126, 34, 206);
        var backingShortcutCount = 0;
        using var backingForm = new Form
        {
            Bounds = bounds,
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            BackColor = initialColor,
            TopMost = false,
            ShowInTaskbar = false,
            KeyPreview = true
        };
        backingForm.KeyDown += (_, e) =>
        {
            if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.R)
            {
                backingShortcutCount++;
            }
        };
        backingForm.Show();
        backingForm.Activate();

        using var snapshotImage = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(snapshotImage))
        {
            graphics.Clear(initialColor);
        }
        using var snapshot = new DesktopSnapshot((Bitmap)snapshotImage.Clone(), bounds);
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

        Task? overlaySession = null;
        try
        {
            overlaySession = CaptureOverlayPresenter.ShowAsync(overlay);
            System.Windows.Forms.Application.DoEvents();

            var expectedSelection = Rectangle.FromLTRB(190, 130, 560, 350);
            CompleteOverlaySelection(
                overlay,
                expectedSelection.Location,
                new Point(expectedSelection.Right, expectedSelection.Bottom));
            System.Windows.Forms.Application.DoEvents();

            var editor = (CaptureAnnotationEditor)typeof(CaptureOverlayForm)
                .GetField(
                    "_annotationEditor",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(overlay)!;
            var shape = new ShapeAnnotation(
                EditorTool.Rectangle,
                new Rectangle(
                    expectedSelection.Left + 120,
                    expectedSelection.Top + 70,
                    110,
                    70),
                Color.Magenta,
                5F);
            var text = new TextAnnotation(
                new Rectangle(
                    expectedSelection.Left + 45,
                    expectedSelection.Top + 35,
                    80,
                    32),
                "刷新保留",
                Color.Yellow,
                18F);
            editor.Document.Add(shape);
            editor.Document.Add(text);
            editor.Selection.SelectOnly(shape);
            overlay.Invalidate(expectedSelection);
            System.Windows.Forms.Application.DoEvents();

            var originalShapeBounds = shape.Bounds;
            var originalTextBounds = text.Bounds;
            AssertSelectionPrimary(editor, shape, "添加标注后");
            using (var originalBackground = ((ICaptureFeatureHost)overlay).CopyDesktopSelection())
            {
                AssertBitmapSolidColor(
                    originalBackground,
                    initialColor,
                    "刷新前截图背景不是最初的静态画面");
            }

            var outsidePoint = new Point(bounds.Left + 60, bounds.Top + 60);
            MovePointer(outsidePoint);
            System.Windows.Forms.Application.DoEvents();
            AssertSelectionPrimary(editor, shape, "移动到框外后");
            MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            System.Windows.Forms.Application.DoEvents();
            AssertSelectionPrimary(editor, shape, "点击框外后");
            backingForm.Activate();
            backingForm.Focus();
            System.Windows.Forms.Application.DoEvents();
            AssertSelectionPrimary(editor, shape, "底层窗口激活后");

            SetBackingColor(backingForm, firstRefreshColor);
            PressControlRInside(
                overlay,
                new Point(
                    bounds.Left + expectedSelection.Left + 20,
                    bounds.Top + expectedSelection.Top + 20),
                "首次刷新前");
            AssertRefreshStatePreserved(
                overlay,
                editor,
                expectedSelection,
                shape,
                originalShapeBounds,
                text,
                originalTextBounds);
            using (var firstBackground = ((ICaptureFeatureHost)overlay).CopyDesktopSelection())
            {
                AssertBitmapSolidColor(
                    firstBackground,
                    firstRefreshColor,
                    "框内 Ctrl+R 没有采集到当前桌面，或把边框、工具栏、标注层截入了底图");
            }
            using (var firstComposite = ((ICaptureArtifactHost)overlay).RenderSelection())
            {
                var localShapeBounds = new Rectangle(
                    shape.Bounds.X - expectedSelection.X,
                    shape.Bounds.Y - expectedSelection.Y,
                    shape.Bounds.Width,
                    shape.Bounds.Height);
                if (!ContainsColorNear(firstComposite, localShapeBounds, Color.Magenta, 8))
                {
                    throw new InvalidOperationException("刷新后的最终合成没有保留原矩形标注。");
                }
            }

            SetBackingColor(backingForm, blockedRefreshColor);
            PressControlROutside(overlay, backingForm, outsidePoint);
            if (backingShortcutCount != 1)
            {
                throw new InvalidOperationException(
                    $"鼠标位于截图框外时 Ctrl+R 没有交还底层窗口。Count={backingShortcutCount}");
            }
            using (var unchangedBackground = ((ICaptureFeatureHost)overlay).CopyDesktopSelection())
            {
                AssertBitmapSolidColor(
                    unchangedBackground,
                    firstRefreshColor,
                    "鼠标位于截图框外时仍然错误刷新了截图背景");
            }

            SetBackingColor(backingForm, secondRefreshColor);
            PressControlRInside(
                overlay,
                new Point(
                    bounds.Left + expectedSelection.Left + 30,
                    bounds.Top + expectedSelection.Top + 30),
                "连续刷新前");

            AssertRefreshStatePreserved(
                overlay,
                editor,
                expectedSelection,
                shape,
                originalShapeBounds,
                text,
                originalTextBounds);
            using (var secondBackground = ((ICaptureFeatureHost)overlay).CopyDesktopSelection())
            {
                AssertBitmapSolidColor(
                    secondBackground,
                    secondRefreshColor,
                    "连续第二次刷新没有得到最新桌面画面");
            }

            typeof(CaptureOverlayForm)
                .GetMethod(
                    "BeginExistingTextEditor",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(overlay, [text]);
            var activeTextEditor = (TransparentTextEditorControl?)typeof(CaptureOverlayForm)
                .GetField(
                    "_textEditor",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(overlay) ??
                throw new InvalidOperationException("无法进入文字重新编辑状态。");
            activeTextEditor.Text = "刷新保留-编辑中";
            activeTextEditor.SelectText(2, 4);
            var editorBoundsBeforeRefresh = activeTextEditor.Bounds;

            SetBackingColor(backingForm, editingRefreshColor);
            PressControlRInside(
                overlay,
                new Point(
                    bounds.Left + expectedSelection.Right - 30,
                    bounds.Top + expectedSelection.Bottom - 30),
                "文字编辑刷新前");

            var textEditorAfterRefresh = (TransparentTextEditorControl?)typeof(CaptureOverlayForm)
                .GetField(
                    "_textEditor",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(overlay);
            if (!ReferenceEquals(textEditorAfterRefresh, activeTextEditor) ||
                activeTextEditor.IsDisposed ||
                activeTextEditor.Text != "刷新保留-编辑中" ||
                activeTextEditor.SelectionStart != 2 ||
                activeTextEditor.SelectionLength != 4 ||
                activeTextEditor.Bounds != editorBoundsBeforeRefresh ||
                !ReferenceEquals(editor.ActiveTextEditAnnotation, text))
            {
                throw new InvalidOperationException(
                    "刷新没有保持文字输入框、未提交内容、文本选区或编辑位置。");
            }
            if (((ICaptureFeatureHost)overlay).Selection != expectedSelection)
            {
                throw new InvalidOperationException("文字编辑期间刷新改变了截图框位置或尺寸。");
            }
            using (var editingBackground = ((ICaptureFeatureHost)overlay).CopyDesktopSelection())
            {
                AssertBitmapSolidColor(
                    editingBackground,
                    editingRefreshColor,
                    "文字编辑期间 Ctrl+R 没有刷新纯背景层");
            }

            Thread.Sleep(120);
            System.Windows.Forms.Application.DoEvents();
            using var captured = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(captured))
            {
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            var fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            KeybdEvent(VirtualKeyR, 0, KeyEventKeyUp, UIntPtr.Zero);
            KeybdEvent(VirtualKeyControl, 0, KeyEventKeyUp, UIntPtr.Zero);
            overlay.Close();
            System.Windows.Forms.Application.DoEvents();
            overlaySession?.GetAwaiter().GetResult();
            backingForm.Close();
            Cursor.Position = originalPointer;
            System.Windows.Forms.Application.DoEvents();
        }
    }

    private static void RunCaptureDisposeSmoke()
    {
        var screen = Screen.PrimaryScreen ??
                     throw new InvalidOperationException("找不到主显示器。");
        var bounds = new Rectangle(screen.Bounds.Location, new Size(320, 200));
        using var snapshotImage = new Bitmap(bounds.Width, bounds.Height);
        using var snapshot = new DesktopSnapshot((Bitmap)snapshotImage.Clone(), bounds);
        var overlay = new CaptureOverlayForm(
            snapshot,
            new PreviewImageSaveService(),
            new PreviewClipboardService(),
            new PreviewWindowLocator(),
            new PreviewModuleManager(includeScreenRecording: false),
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

        var overlaySession = CaptureOverlayPresenter.ShowAsync(overlay);
        System.Windows.Forms.Application.DoEvents();
        overlay.Close();
        System.Windows.Forms.Application.DoEvents();
        overlaySession.GetAwaiter().GetResult();

        // A modeless Form disposes itself when closed. MainForm's using scope then
        // disposes it again, so this second call is the production regression case.
        overlay.Dispose();
    }

    private static void CompleteOverlaySelection(
        CaptureOverlayForm overlay,
        Point start,
        Point end)
    {
        typeof(CaptureOverlayForm)
            .GetMethod(
                "BeginManualSelection",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [start, Rectangle.Empty]);
        typeof(CaptureOverlayForm)
            .GetMethod(
                "HandleMouseMove",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay,
            [
                overlay,
                new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0)
            ]);
        typeof(CaptureOverlayForm)
            .GetMethod(
                "HandleMouseUp",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay,
            [
                overlay,
                new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0)
            ]);
    }

    private static void SetBackingColor(Form backingForm, Color color)
    {
        backingForm.BackColor = color;
        backingForm.Refresh();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void PressControlR()
    {
        KeybdEvent(VirtualKeyControl, 0, 0, UIntPtr.Zero);
        KeybdEvent(VirtualKeyR, 0, 0, UIntPtr.Zero);
        KeybdEvent(VirtualKeyR, 0, KeyEventKeyUp, UIntPtr.Zero);
        KeybdEvent(VirtualKeyControl, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    private static void PressControlRInside(
        CaptureOverlayForm overlay,
        Point screenPoint,
        string stage)
    {
        var previousClip = Cursor.Clip;
        try
        {
            Cursor.Clip = new Rectangle(screenPoint, new Size(1, 1));
            MovePointer(screenPoint);
            MouseEvent(MouseEventMove | MouseEventMoveNoCoalesce, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(120);
            System.Windows.Forms.Application.DoEvents();
            AssertBackgroundRefreshHotkeyReady(overlay, stage);
            PressControlR();
            System.Windows.Forms.Application.DoEvents();
        }
        finally
        {
            Cursor.Clip = previousClip;
        }
    }

    private static void PressControlROutside(
        CaptureOverlayForm overlay,
        Form backingForm,
        Point screenPoint)
    {
        var previousClip = Cursor.Clip;
        try
        {
            Cursor.Clip = new Rectangle(screenPoint, new Size(1, 1));
            MovePointer(screenPoint);
            MouseEvent(MouseEventMove | MouseEventMoveNoCoalesce, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(120);
            System.Windows.Forms.Application.DoEvents();
            backingForm.Activate();
            backingForm.Focus();
            System.Windows.Forms.Application.DoEvents();

            var hotkey = (CaptureBackgroundRefreshHotkeyRegistration?)typeof(CaptureOverlayForm)
                .GetField(
                    "_backgroundRefreshHotkey",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(overlay);
            if (hotkey?.IsRegistered == true)
            {
                throw new InvalidOperationException("鼠标位于截图框外时仍然占用了 Ctrl+R。");
            }

            PressControlR();
            System.Windows.Forms.Application.DoEvents();
        }
        finally
        {
            Cursor.Clip = previousClip;
        }
    }

    private static void AssertBackgroundRefreshHotkeyReady(
        CaptureOverlayForm overlay,
        string stage)
    {
        var hotkey = (CaptureBackgroundRefreshHotkeyRegistration?)typeof(CaptureOverlayForm)
            .GetField(
                "_backgroundRefreshHotkey",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(overlay);
        var pointer = overlay.PointToClient(Cursor.Position);
        var canRefresh = (bool)typeof(CaptureOverlayForm)
            .GetMethod(
                "CanRefreshCaptureBackground",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .Invoke(overlay, [pointer])!;
        if (hotkey is null || !hotkey.IsRegistered)
        {
            throw new InvalidOperationException(
                $"{stage}动态 Ctrl+R 没有注册。CanRefresh={canRefresh}, " +
                $"Pointer={pointer}, Selection={((ICaptureFeatureHost)overlay).Selection}, " +
                $"Capture={overlay.Capture}, Visible={overlay.Visible}, " +
                $"Win32Error={hotkey?.LastError}");
        }
    }

    private static void AssertRefreshStatePreserved(
        CaptureOverlayForm overlay,
        CaptureAnnotationEditor editor,
        Rectangle expectedSelection,
        ShapeAnnotation shape,
        Rectangle expectedShapeBounds,
        TextAnnotation text,
        Rectangle expectedTextBounds)
    {
        var actualSelection = ((ICaptureFeatureHost)overlay).Selection;
        if (actualSelection != expectedSelection)
        {
            throw new InvalidOperationException(
                $"刷新改变了截图框位置或尺寸。Expected={expectedSelection}, Actual={actualSelection}");
        }
        if (editor.Document.Count != 2 ||
            !editor.Document.Contains(shape) ||
            !editor.Document.Contains(text))
        {
            throw new InvalidOperationException("刷新清空或替换了原有编辑文档。");
        }
        if (!ReferenceEquals(editor.Selection.Primary, shape))
        {
            throw new InvalidOperationException("刷新改变了编辑元素的选中状态。");
        }
        if (shape.Bounds != expectedShapeBounds ||
            text.Bounds != expectedTextBounds ||
            shape.Color.ToArgb() != Color.Magenta.ToArgb() ||
            text.Color.ToArgb() != Color.Yellow.ToArgb() ||
            text.Text != "刷新保留")
        {
            throw new InvalidOperationException("刷新改变了编辑元素的位置、大小、颜色或内容。");
        }
    }

    private static void AssertSelectionPrimary(
        CaptureAnnotationEditor editor,
        MovableAnnotation expected,
        string stage)
    {
        if (!ReferenceEquals(editor.Selection.Primary, expected))
        {
            throw new InvalidOperationException(
                $"{stage}编辑元素的选中状态已改变。" +
                $"Count={editor.Selection.Count}, Primary={editor.Selection.Primary?.GetType().Name ?? "null"}");
        }
    }

    private static void AssertBitmapSolidColor(Bitmap bitmap, Color expected, string message)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!ColorsAreNear(bitmap.GetPixel(x, y), expected, 2))
                {
                    throw new InvalidOperationException(
                        $"{message} Pixel=({x},{y}), Expected={expected}, Actual={bitmap.GetPixel(x, y)}");
                }
            }
        }
    }

    private static bool ContainsColorNear(
        Bitmap bitmap,
        Rectangle bounds,
        Color expected,
        int tolerance)
    {
        bounds.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                if (ColorsAreNear(bitmap.GetPixel(x, y), expected, tolerance))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ColorsAreNear(Color actual, Color expected, int tolerance) =>
        Math.Abs(actual.R - expected.R) <= tolerance &&
        Math.Abs(actual.G - expected.G) <= tolerance &&
        Math.Abs(actual.B - expected.B) <= tolerance;

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
            [
                new HotkeyDefinition(
                    HotkeyModifiers.Control | HotkeyModifiers.Alt,
                    (int)Keys.Q),
                new HotkeyDefinition(
                    HotkeyModifiers.Control | HotkeyModifiers.Shift,
                    (int)Keys.A)
            ],
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

        var thirdHotkeyInput = page.Controls
            .Find("ScreenshotHotkeyInput3", searchAllChildren: true)
            .OfType<HotkeyInputBox>()
            .Single();
        var processCmdKey = typeof(HotkeyInputBox).GetMethod(
            "ProcessCmdKey",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic,
            binder: null,
            [typeof(Message).MakeByRefType(), typeof(Keys)],
            modifiers: null) ?? throw new InvalidOperationException(
                "快捷键输入框没有提供命令键处理入口。");
        object?[] commandArguments =
        [
            Message.Create(thirdHotkeyInput.Handle, 0, IntPtr.Zero, IntPtr.Zero),
            Keys.Control | Keys.Shift | Keys.X
        ];
        var commandHandled = (bool)(processCmdKey.Invoke(
            thirdHotkeyInput,
            commandArguments) ?? false);

        if (!page.Hotkeys.SequenceEqual(
            [
                new HotkeyDefinition(
                    HotkeyModifiers.Control | HotkeyModifiers.Alt,
                    (int)Keys.Q),
                new HotkeyDefinition(
                    HotkeyModifiers.Control | HotkeyModifiers.Shift,
                    (int)Keys.A),
                HotkeyDefinition.Default
            ]) ||
            !commandHandled ||
            page.DismissSaveNotificationBeforeCapture ||
            !page.HideMainWindowDuringCapture)
        {
            throw new InvalidOperationException(
                $"截图设置页面没有恢复快捷键或截图行为选项。当前绑定：" +
                HotkeyBindings.ToDisplayText(page.Hotkeys) +
                $"；命令键已处理：{commandHandled}");
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
                    control is CheckBox or TextBox ||
                    string.Equals(control.Tag as string, "SettingInput", StringComparison.Ordinal)) != 1))
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
            new PreviewSavedScreenshotService(),
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
        shell.SelectPage("modules");
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
        if (!navigationTexts.Contains("通用设置", StringComparer.Ordinal) ||
            !navigationTexts.Contains("截图设置", StringComparer.Ordinal) ||
            !navigationTexts.Contains("插件模块", StringComparer.Ordinal) ||
            !navigationTexts.Contains("软件更新", StringComparer.Ordinal) ||
            navigationTexts.Contains("快捷键设置", StringComparer.Ordinal) ||
            navigationTexts.Contains("图片复制", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("主界面没有把快捷键整合进截图设置分页。");
        }
        if (navigationTexts.Contains("录屏设置", StringComparer.Ordinal) ||
            shell.SelectedPageId != "modules" ||
            !shell.VersionText.Equals("v1.11.8", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"模块配置仍占用主导航，或版本号不正确；当前页面 {shell.SelectedPageId}，版本 {shell.VersionText}。");
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
            new PreviewSavedScreenshotService(),
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
        if (!emptyNavigationTexts.Contains("通用设置", StringComparer.Ordinal) ||
            !emptyNavigationTexts.Contains("截图设置", StringComparer.Ordinal) ||
            !emptyNavigationTexts.Contains("插件模块", StringComparer.Ordinal) ||
            !emptyNavigationTexts.Contains("软件更新", StringComparer.Ordinal) ||
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
            new PreviewSavedScreenshotService(),
            new PreviewModuleManager(includeDisabledPackage: true),
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
        var enabledPageText = string.Join(
            '\n',
            page.Controls.Cast<Control>().SelectMany(GetControlTree).Select(control => control.Text));
        foreach (var expectedText in new[]
                 {
                     "已启用模块 (1)",
                     "已禁用模块 (1)",
                     "录屏",
                     "管理配置",
                     "禁用模块",
                     "永久删除",
                     "前往下载"
                 })
        {
            if (!enabledPageText.Contains(expectedText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"插件模块页面缺少操作：{expectedText}");
            }
        }
        if (enabledPageText.Contains("PP-OCR Tiny 文字识别", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("已启用模块分页显示了禁用模块。");
        }
        var enabledTitle = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .Single(control => control.Name == "ModuleTitle:ScreenRecording");
        if (enabledTitle.ForeColor != AppTheme.Success)
        {
            throw new InvalidOperationException("已启用模块名称没有使用绿色状态色。");
        }

        var configurationButton = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .OfType<Button>()
            .Single(button => button.Name == "ModuleConfiguration:ScreenRecording");
        configurationButton.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var configurationForm = System.Windows.Forms.Application.OpenForms
            .OfType<ModuleConfigurationForm>()
            .Single(form => form.PackageName == "ScreenRecording");
        var configurationText = string.Join(
            '\n',
            configurationForm.Controls
                .Cast<Control>()
                .SelectMany(GetControlTree)
                .Select(control => control.Text));
        if (!configurationText.Contains("保存录屏设置", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("管理配置没有在独立窗口承载录屏模块设置页。");
        }
        configurationForm.Close();
        System.Windows.Forms.Application.DoEvents();

        var disabledTab = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .OfType<Button>()
            .Single(button => button.Name == "DisabledModulesTab");
        disabledTab.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var disabledPageText = string.Join(
            '\n',
            page.Controls.Cast<Control>().SelectMany(GetControlTree).Select(control => control.Text));
        foreach (var expectedText in new[]
                 {
                     "PP-OCR Tiny 文字识别",
                     "管理配置",
                     "启用模块",
                     "永久删除"
                 })
        {
            if (!disabledPageText.Contains(expectedText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"已禁用模块分页缺少操作：{expectedText}");
            }
        }
        if (disabledPageText.Contains("录屏", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("已禁用模块分页显示了启用模块。");
        }
        var disabledTitle = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .Single(control => control.Name == "ModuleTitle:PaddleOcrTiny");
        if (disabledTitle.ForeColor != AppTheme.Danger)
        {
            throw new InvalidOperationException("已禁用模块名称没有使用红色状态色。");
        }

        using var captured = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(captured, new Rectangle(Point.Empty, form.Size));
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        captured.Save(fullOutputPath, System.Drawing.Imaging.ImageFormat.Png);

        var enableButton = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .OfType<Button>()
            .Single(button => button.Text == "启用模块");
        enableButton.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var movedPageText = string.Join(
            '\n',
            page.Controls.Cast<Control>().SelectMany(GetControlTree).Select(control => control.Text));
        if (!movedPageText.Contains("已启用模块 (2)", StringComparison.Ordinal) ||
            !movedPageText.Contains("已禁用模块 (0)", StringComparison.Ordinal) ||
            !movedPageText.Contains("没有已禁用的模块", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("重新启用后模块没有从禁用分页移出。");
        }

        var enabledTab = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .OfType<Button>()
            .Single(button => button.Name == "EnabledModulesTab");
        enabledTab.PerformClick();
        System.Windows.Forms.Application.DoEvents();
        var enabledTitles = page.Controls
            .Cast<Control>()
            .SelectMany(GetControlTree)
            .Where(control => control.Name.StartsWith("ModuleTitle:", StringComparison.Ordinal))
            .ToArray();
        if (enabledTitles.Length != 2 ||
            enabledTitles.Any(title => title.ForeColor != AppTheme.Success))
        {
            throw new InvalidOperationException("重新启用后的模块没有进入启用分页并显示为绿色。");
        }

        form.Close();
        System.Windows.Forms.Application.DoEvents();
    }

    private static void RunApplicationUpdatePageSmoke(string outputPath)
    {
        using var form = new Form
        {
            Text = "轻截 - 软件更新",
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(760, 620),
            BackColor = Color.FromArgb(244, 247, 252),
            ShowInTaskbar = false,
            TopMost = true
        };
        using var updateService = new PreviewApplicationUpdateService();
        using var page = new ApplicationUpdatePage(
            new Version(1, 11, 0),
            updateService)
        {
            Location = new Point(18, 18),
            Size = new Size(724, 584)
        };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        System.Windows.Forms.Application.DoEvents();

        var checkMethod = typeof(ApplicationUpdatePage).GetMethod(
            "CheckForUpdatesAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!;
        ((Task)checkMethod.Invoke(page, null)!).GetAwaiter().GetResult();
        System.Windows.Forms.Application.DoEvents();

        var rows = GetControlTree(page)
            .OfType<Panel>()
            .Where(panel => string.Equals(
                panel.Tag as string,
                "SettingRow",
                StringComparison.Ordinal))
            .OrderBy(panel => panel.Top)
            .ToArray();
        if (rows.Length != 3 ||
            rows.Any(row => row.Controls.OfType<Button>().Count() > 1))
        {
            throw new InvalidOperationException("软件更新页没有使用三行纵向单列设置项。");
        }
        if (!page.StateText.Contains("v1.12.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("软件更新页没有显示可安装的新版本。");
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
            new PreviewSavedScreenshotService(),
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
            ctrlDragStepPixels: 10,
            annotationMoveActivationMode: AnnotationMoveActivationMode.ToggleOnAltTap)
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
            page.CtrlDragStepPixels != 10 ||
            page.AnnotationMoveActivationMode != AnnotationMoveActivationMode.ToggleOnAltTap)
        {
            throw new InvalidOperationException("编辑设置页面没有恢复移动方式、元素吸附与 Ctrl 拖动参数。");
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
            new PreviewSavedScreenshotService(),
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
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventMoveNoCoalesce = 0x2000;
    private const uint WindowMessageLeftButtonDown = 0x0201;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const byte VirtualKeyControl = 0x11;
    private const byte VirtualKeyR = 0x52;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint GetRootAncestor = 2;
    private static void MovePointer(Point screenPoint) => Cursor.Position = screenPoint;

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(
        uint flags,
        uint dx,
        uint dy,
        uint data,
        UIntPtr extraInfo);

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void KeybdEvent(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

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

    public bool TryRegister(IReadOnlyList<HotkeyDefinition> hotkeys, out string? error)
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
    // Rejects synchronous saves because UI previews never write screenshot artifacts.
    public string SaveImage(
        Bitmap image,
        string outputFolder,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null,
        bool organizeByDate = false) =>
        throw new NotSupportedException("界面预览不保存截图。");

    // Rejects asynchronous saves because UI previews never write screenshot artifacts.
    public Task<string> SaveImageAsync(
        Bitmap image,
        string outputFolder,
        ScreenshotImageFormat imageFormat = ScreenshotImageFormat.Png,
        ScreenshotFileNameMode fileNameMode = ScreenshotFileNameMode.DateTime,
        IReadOnlyList<string>? imageTexts = null,
        bool organizeByDate = false,
        CancellationToken cancellationToken = default) =>
        Task.FromException<string>(new NotSupportedException("界面预览不保存截图。"));
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

internal sealed class PreviewTextTranslationService(string translatedText) :
    ITextTranslationService
{
    public int CallCount { get; private set; }

    public string? LastText { get; private set; }

    public Task<string> TranslateToSimplifiedChineseAsync(
        string text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastText = text;
        return Task.FromResult(translatedText);
    }
}

internal sealed class RecordingPreviewClipboardService : IClipboardService, IDisposable
{
    public Bitmap? CopiedImage { get; private set; }

    public string? Text { get; set; }

    public void SetImage(Image image)
    {
        CopiedImage?.Dispose();
        CopiedImage = new Bitmap(image);
    }

    public Bitmap? GetImage() =>
        CopiedImage is null ? null : new Bitmap(CopiedImage);

    public string? GetText() => Text;

    public void SetText(string text) => Text = text;

    public void Dispose() => CopiedImage?.Dispose();
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

internal sealed class PreviewSavedScreenshotService : ISavedScreenshotService
{
    public bool IsSupportedImage(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

    public bool IsSupportedVideo(string path) =>
        string.Equals(
            Path.GetExtension(path),
            ".mp4",
            StringComparison.OrdinalIgnoreCase);

    public Bitmap LoadForEditing(string folderPath, string filePath)
    {
        using var source = Image.FromFile(filePath);
        return new Bitmap(source);
    }

    public void MoveToRecycleBin(string folderPath, string filePath) => File.Delete(filePath);
}

internal sealed class PreviewStartupRegistrationService : IStartupRegistrationService
{
    public bool HasRegistration => IsEnabled;

    public bool IsEnabled { get; private set; } = true;

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

internal sealed class PreviewApplicationUpdateService : IApplicationUpdateService
{
    public Version CurrentVersion => new(1, 11, 0);

    public Task<ApplicationUpdateCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var update = new ApplicationUpdateInfo(
            new Version(1, 12, 0),
            "轻截 v1.12.0",
            new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.FromHours(8)),
            new Uri("https://github.com/XDIOEZ/CutCut/releases/tag/v1.12.0"),
            new Uri(
                "https://github.com/XDIOEZ/CutCut/releases/download/v1.12.0/" +
                "complete-lightweight-win-x64.zip"),
            1_350_000,
            new string('a', 64),
            ApplicationUpdatePackageKind.Lightweight);
        return Task.FromResult(new ApplicationUpdateCheckResult(
            update.Version,
            update.ReleaseName,
            update.PublishedAt,
            update.ReleasePageUri,
            update));
    }

    public Task<PreparedApplicationUpdate> DownloadAndPrepareAsync(
        ApplicationUpdateInfo update,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("界面预览不下载更新。");

    public void StartApplying(PreparedApplicationUpdate update, int processId) =>
        throw new NotSupportedException("界面预览不安装更新。");

    public ApplicationUpdateApplyResult? TakePendingApplyResult() => null;

    public void Dispose()
    {
    }
}

internal sealed class PreviewModuleManager(
    bool includeScreenRecording = true,
    bool includeDisabledPackage = false) : IModuleManager
{
    private readonly bool _includeScreenRecording = includeScreenRecording;
    private bool _refreshed;
    private bool _packageExists = includeScreenRecording;
    private bool _packageEnabled = true;
    private bool _disabledPackageExists = includeDisabledPackage;
    private bool _disabledPackageEnabled;

    public string ModulesDirectory => Path.Combine(Path.GetTempPath(), "LightShotUiPreviewModules");

    public ModuleRefreshResult Refresh(bool force = false)
    {
        var changed = force || !_refreshed;
        _refreshed = true;
        return new([], [], changed);
    }

    public IReadOnlyList<ModuleInfo> GetModules() => [];

    public IReadOnlyList<ModulePackageInfo> GetInstalledPackages()
    {
        var packages = new List<ModulePackageInfo>();
        if (_packageExists)
        {
            packages.Add(new ModulePackageInfo(
                "ScreenRecording",
                "lightshot.screen-recording",
                "录屏",
                new Version(1, 0),
                Path.Combine(ModulesDirectory, "ScreenRecording"),
                _packageEnabled ? ModulePackageState.Enabled : ModulePackageState.Disabled));
        }
        if (_disabledPackageExists)
        {
            packages.Add(new ModulePackageInfo(
                "PaddleOcrTiny",
                "screenshot-tool.paddle-ocr.tiny",
                "PP-OCR Tiny 文字识别",
                new Version(1, 0),
                Path.Combine(ModulesDirectory, "PaddleOcrTiny"),
                _disabledPackageEnabled ? ModulePackageState.Enabled : ModulePackageState.Disabled));
        }

        return packages;
    }

    public ModuleOperationResult SetPackageEnabled(string packageName, bool enabled)
    {
        if (packageName == "PaddleOcrTiny")
        {
            _disabledPackageEnabled = enabled;
            return new(
                true,
                enabled ? "已启用 PP-OCR Tiny" : "已禁用 PP-OCR Tiny",
                new([], [], true));
        }

        _packageEnabled = enabled;
        return new(true, enabled ? "已启用录屏" : "已禁用录屏", new([], [], true));
    }

    public ModuleOperationResult DeletePackage(string packageName)
    {
        if (packageName == "PaddleOcrTiny")
        {
            _disabledPackageExists = false;
            return new(true, "已永久删除 PP-OCR Tiny", new([], [], true));
        }

        _packageExists = false;
        return new(true, "已永久删除录屏", new([], [], true));
    }

    public IReadOnlyList<ICaptureFeature> CreateCaptureFeatures() => [];

    public IReadOnlyList<IModuleSettingsPage> CreateSettingsPages(
        string packageName,
        IModuleSettingsHost host) =>
        _includeScreenRecording &&
        _packageEnabled &&
        string.Equals(packageName, "ScreenRecording", StringComparison.OrdinalIgnoreCase)
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
