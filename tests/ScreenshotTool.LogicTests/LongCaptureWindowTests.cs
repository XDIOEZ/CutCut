using System.Runtime.InteropServices;
using System.Reflection;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;
using ScreenshotTool.Core;
using ScreenshotTool.Editing;
using ScreenshotTool.LongCapture;
using ScreenshotTool.Presentation;

internal static class LongCaptureWindowTests
{
    private const int BorderThickness = 3;
    private const int ExtendedStyleIndex = -20;
    private const long ExtendedStyleNoActivate = 0x08000000L;

    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunOnStaThread();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "ScreenshotTool.LongCaptureWindowTests"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("长截图屏外窗口测试超时。");
        }

        if (failure is not null)
        {
            throw new InvalidOperationException("长截图屏外窗口测试失败。", failure);
        }
    }

    private static void RunOnStaThread()
    {
        var workArea = Screen.PrimaryScreen?.WorkingArea
            ?? throw new InvalidOperationException("没有可用的主屏幕工作区。");
        var selection = CreateRegularSelection(workArea);
        var parkingBounds = CreateOffscreenParkingBounds();

        using var preview = new LongCapturePreviewForm(selection);
        var calculatedPreviewBounds = preview.Bounds;
        var dragHeader = preview.Controls.Find(
            "PreviewDragHeader",
            searchAllChildren: true).Single();
        var dragStatus = preview.Controls.Find(
            "PreviewDragStatus",
            searchAllChildren: true).Single();
        AssertTrue(dragHeader.Cursor == Cursors.SizeAll, "预览顶部状态栏显示可拖动光标");
        AssertTrue(dragStatus.Cursor == Cursors.SizeAll, "预览状态文字区域同样可以拖动");
        AssertTrue(preview.MaximizeBox, "长截图实时预览支持 Windows 最大化");
        AssertEqual(FormBorderStyle.SizableToolWindow, preview.FormBorderStyle, "长截图实时预览支持拖动窗口边框调整大小");
        var previewClientSize = new Size(600, 420);
        var previewImageSize = new Size(320, 960);
        var zoomCursor = new Point(340, 170);
        var beforeZoomBounds = LongCapturePreviewLayout.GetImageBounds(
            previewClientSize,
            previewImageSize,
            1D,
            PointF.Empty);
        var imagePositionAtCursor = new PointF(
            (zoomCursor.X - beforeZoomBounds.Left) / beforeZoomBounds.Width,
            (zoomCursor.Y - beforeZoomBounds.Top) / beforeZoomBounds.Height);
        var zoomedPan = LongCapturePreviewLayout.ZoomAt(
            previewClientSize,
            previewImageSize,
            1D,
            2D,
            PointF.Empty,
            zoomCursor);
        var afterZoomBounds = LongCapturePreviewLayout.GetImageBounds(
            previewClientSize,
            previewImageSize,
            2D,
            zoomedPan);
        AssertNear(
            imagePositionAtCursor.X,
            (zoomCursor.X - afterZoomBounds.Left) / afterZoomBounds.Width,
            0.001F,
            "预览横向缩放以鼠标位置为中心");
        AssertNear(
            imagePositionAtCursor.Y,
            (zoomCursor.Y - afterZoomBounds.Top) / afterZoomBounds.Height,
            0.001F,
            "预览纵向缩放以鼠标位置为中心");
        AssertTrue(afterZoomBounds.Contains(zoomCursor), "缩放后的图片命中区域仍包含鼠标位置");
        var clampedPan = LongCapturePreviewLayout.ClampPan(
            previewClientSize,
            previewImageSize,
            2D,
            new PointF(10000, -10000));
        var clampedBounds = LongCapturePreviewLayout.GetImageBounds(
            previewClientSize,
            previewImageSize,
            2D,
            clampedPan);
        AssertTrue(
            clampedBounds.Top <= 0 && clampedBounds.Bottom >= previewClientSize.Height,
            "预览拖拽不会把长图完全移出可视区域");
        AssertEqual(
            new Point(400, 100),
            LongCapturePreviewForm.CalculateDraggedLocation(
                new Point(420, 110),
                new Point(20, 10),
                new Size(292, 400),
                new Rectangle(0, 0, 800, 600)),
            "预览拖拽按鼠标偏移移动窗口");
        AssertEqual(
            new Point(508, 200),
            LongCapturePreviewForm.CalculateDraggedLocation(
                new Point(900, 700),
                new Point(20, 10),
                new Size(292, 400),
                new Rectangle(0, 0, 800, 600)),
            "预览拖拽不会把整个窗口移出工作区");
        AssertTrue(
            !calculatedPreviewBounds.IntersectsWith(selection),
            "常规选区旁的长截图预览不覆盖截图内容");
        AssertTrue(!preview.OverlapsSelection, "常规选区无需在采集时隐藏预览窗");
        VerifyHeaderDragMovesPreview(preview, dragHeader, workArea);

        AssertTrue(
            typeof(ILiveCaptureFeatureHost).GetMethod("EditCaptureResult") is null,
            "长截图宿主不再请求独立编辑窗口");
        AssertTrue(
            typeof(ILiveCaptureFeatureHost).GetMethod("ReplaceCaptureResult") is not null,
            "长截图完成后直接回填原截图宿主");
        AssertEqual(
            typeof(CaptureAnnotationEditor),
            typeof(CaptureOverlayForm).GetField(
                "_annotationEditor",
                BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType
                ?? throw new InvalidOperationException("没有找到普通截图共享编辑核心。"),
            "长截图回填后继续使用普通截图编辑核心");
        AssertNear(
            1F,
            (float)CaptureEditorViewportLayout.CalculateWidthFitZoom(
                new Size(320, 500),
                new Size(320, 960)),
            0.001F,
            "内嵌长图默认按原截图选区宽度显示");
        var zoomScroll = CaptureEditorViewportLayout.CalculateZoomScroll(
            1D,
            2D,
            new Point(300, 600),
            new Size(800, 600),
            new Point(200, 150));
        AssertEqual(new Point(800, 1350), zoomScroll, "共用编辑视口缩放保持鼠标锚点");
        AssertEqual(
            new Point(340, 680),
            CaptureEditorViewportLayout.CalculatePanScroll(
                new Point(300, 600),
                new Point(500, 400),
                new Point(460, 320)),
            "右键向左上拖动时长截图画面同步向左上移动");
        using var editorSource = new Bitmap(320, 960);
        using var sharedEditor = new CaptureAnnotationEditor();
        var annotation = sharedEditor.BuildDraft(
            EditorTool.Rectangle,
            new Point(20, 30),
            new Point(100, 90),
            [],
            Color.Red,
            5F) ?? throw new InvalidOperationException("共享编辑核心未创建矩形标注。");
        sharedEditor.Document.Add(annotation);
        sharedEditor.SelectIntersecting(new Rectangle(10, 20, 100, 80));
        AssertEqual(1, sharedEditor.Selection.Count, "共享编辑核心支持框选标注");
        using var renderedEditor = sharedEditor.RenderResult(editorSource);
        AssertEqual(editorSource.Size, renderedEditor.Size, "长截图编辑结果保持完整长图尺寸");
        AssertTrue(renderedEditor.GetPixel(20, 30).R > 180, "长截图最终输出包含编辑窗口添加的标注");

        using var frame = new LongCaptureSelectionFrameForm(selection);
        VerifySelectionFrameRegion(frame, selection);

        // The layout assertions above use the real screen geometry. Move both windows completely
        // outside the virtual desktop before showing them so this test never flashes over the
        // user's desktop or intercepts pointer input.
        frame.Bounds = new Rectangle(parkingBounds.Location, frame.Size);
        preview.Bounds = new Rectangle(
            parkingBounds.Left - preview.Width - 32,
            parkingBounds.Top,
            preview.Width,
            preview.Height);

        var foregroundBeforeShow = GetForegroundWindow();

        frame.ShowFrame();
        Application.DoEvents();
        AssertTrue(frame.Visible, "选区边框屏外显示成功");
        AssertNoActivation(frame, "选区边框");

        preview.ShowPreview();
        Application.DoEvents();
        AssertTrue(preview.Visible, "长截图预览屏外显示成功");
        AssertNoActivation(preview, "长截图预览");

        var foregroundAfterShow = GetForegroundWindow();
        if (foregroundBeforeShow != IntPtr.Zero)
        {
            AssertEqual(
                foregroundBeforeShow,
                foregroundAfterShow,
                "显示长截图辅助窗口不会改变前台窗口");
        }

        AssertTrue(
            Rectangle.Intersect(frame.Bounds, SystemInformation.VirtualScreen).IsEmpty,
            "选区边框测试窗口完全位于虚拟桌面之外");
        AssertTrue(
            Rectangle.Intersect(preview.Bounds, SystemInformation.VirtualScreen).IsEmpty,
            "预览测试窗口完全位于虚拟桌面之外");

        preview.Close();
        frame.Close();
        Application.DoEvents();
    }

    private static void VerifyHeaderDragMovesPreview(
        LongCapturePreviewForm preview,
        Control dragHeader,
        Rectangle workArea)
    {
        var originalLocation = preview.Location;
        var moveDelta = new Point(
            originalLocation.X + preview.Width / 2 < workArea.Left + workArea.Width / 2
                ? 20
                : -20,
            originalLocation.Y + preview.Height / 2 < workArea.Top + workArea.Height / 2
                ? 10
                : -10);
        var down = new Point(10, 10);
        var move = new Point(down.X + moveDelta.X, down.Y + moveDelta.Y);
        var downScreen = dragHeader.PointToScreen(down);
        var moveScreen = dragHeader.PointToScreen(move);
        var expected = LongCapturePreviewForm.CalculateDraggedLocation(
            moveScreen,
            new Point(
                downScreen.X - originalLocation.X,
                downScreen.Y - originalLocation.Y),
            preview.Size,
            Screen.FromPoint(moveScreen).WorkingArea);

        RaiseMouseEvent(
            dragHeader,
            "OnMouseDown",
            new MouseEventArgs(MouseButtons.Left, 1, down.X, down.Y, 0));
        RaiseMouseEvent(
            dragHeader,
            "OnMouseMove",
            new MouseEventArgs(MouseButtons.Left, 0, move.X, move.Y, 0));
        RaiseMouseEvent(
            dragHeader,
            "OnMouseUp",
            new MouseEventArgs(MouseButtons.Left, 1, move.X, move.Y, 0));

        AssertEqual(expected, preview.Location, "拖动预览顶部状态栏会实际移动窗口");
        AssertTrue(preview.Location != originalLocation, "拖拽后预览窗口位置发生变化");
    }

    private static void RaiseMouseEvent(
        Control control,
        string methodName,
        MouseEventArgs eventArgs)
    {
        var method = typeof(Control).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"没有找到控件鼠标方法 {methodName}。");
        method.Invoke(control, [eventArgs]);
    }

    private static Rectangle CreateRegularSelection(Rectangle workArea)
    {
        const int margin = 24;
        const int previewGap = 12;
        const int preferredPreviewWidth = 292;
        var maximumSelectionWidth =
            workArea.Width - margin * 2 - previewGap - preferredPreviewWidth;
        if (maximumSelectionWidth < 80 || workArea.Height < 120)
        {
            throw new InvalidOperationException(
                $"主屏幕工作区 {workArea} 太小，无法建立常规长截图窗口测试布局。");
        }

        var width = Math.Min(480, maximumSelectionWidth);
        var height = Math.Min(420, workArea.Height - margin * 2);
        return new Rectangle(
            workArea.Left + margin,
            workArea.Top + margin,
            width,
            height);
    }

    private static Rectangle CreateOffscreenParkingBounds()
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        const int gap = 2048;
        return new Rectangle(
            virtualScreen.Left - gap - 640,
            virtualScreen.Top - gap - 480,
            640,
            480);
    }

    private static void VerifySelectionFrameRegion(
        LongCaptureSelectionFrameForm frame,
        Rectangle selection)
    {
        var expectedBounds = selection;
        expectedBounds.Inflate(BorderThickness, BorderThickness);
        AssertEqual(expectedBounds, frame.Bounds, "选区边框位于截图范围之外");

        var region = frame.Region
            ?? throw new InvalidOperationException("选区边框没有设置窗口 Region。");
        var clientCenter = new Point(
            frame.ClientSize.Width / 2,
            frame.ClientSize.Height / 2);
        AssertTrue(!region.IsVisible(clientCenter), "选区边框 Region 中心为空");

        AssertTrue(
            region.IsVisible(frame.ClientSize.Width / 2, 1),
            "选区上边框保留在 Region 中");
        AssertTrue(
            region.IsVisible(1, frame.ClientSize.Height / 2),
            "选区左边框保留在 Region 中");

        for (var y = 0; y < frame.ClientSize.Height; y++)
        {
            for (var x = 0; x < frame.ClientSize.Width; x++)
            {
                if (!region.IsVisible(x, y))
                {
                    continue;
                }

                var screenPoint = new Point(frame.Left + x, frame.Top + y);
                AssertTrue(
                    !selection.Contains(screenPoint),
                    $"边框 Region 像素 {screenPoint} 不进入截图选区");
            }
        }
    }

    private sealed class EmptyClipboardService : IClipboardService
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

    private static void AssertNoActivation(Form form, string name)
    {
        var extendedStyle = GetExtendedWindowStyle(form.Handle);
        AssertTrue(
            (extendedStyle & ExtendedStyleNoActivate) != 0,
            $"{name}包含 WS_EX_NOACTIVATE 样式");
        AssertTrue(GetForegroundWindow() != form.Handle, $"{name}显示时不成为前台窗口");
    }

    private static long GetExtendedWindowStyle(IntPtr windowHandle) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, ExtendedStyleIndex).ToInt64()
            : GetWindowLong32(windowHandle, ExtendedStyleIndex);

    private static void AssertEqual<T>(T expected, T actual, string name)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{name}失败：期望 {expected}，实际 {actual}。");
        }
    }

    private static void AssertTrue(bool value, string name)
    {
        if (!value)
        {
            throw new InvalidOperationException($"{name}失败。");
        }
    }

    private static void AssertNear(float expected, float actual, float tolerance, string name)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{name}失败：期望 {expected}，实际 {actual}，容差 {tolerance}。");
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);
}
