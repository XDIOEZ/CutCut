using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ScreenshotTool.LongCapture;

internal static class BidirectionalLongCaptureTests
{
    private const int ViewportWidth = 360;
    private const int ViewportHeight = 320;

    public static void Run()
    {
        FirstFramePreviewHonorsBoundsWithoutUpscaling();
        StickyBidirectionalPreviewPreservesContentOrder();
        PreviewSupportsOnePixelBounds();
        BuildingPreviewDoesNotMutateFinalResult();
        MiddleStartExtendsDownThenUpToTheWholePage();
        StickyHeaderAndFooterAppearOnceAcrossBidirectionalScroll();
        StickyHeaderRepaintDoesNotStopReliableStitching();
        UnsafeModeAcceptsMostLikelyBidirectionalSeam();
        RevisitingCapturedRangesDoesNotDuplicatePixels();
        MultipleUpwardStepsProduceTheWholePage();
        DuplicateFramesDoNotStopLaterProgress();
        MatcherReportsBothScrollDirections();
        GlobalKeyboardMonitorOnlyHandlesEscape();
        CommandArbitrationPrioritizesCancellation();
        ProvisionalRejectionDoesNotStopManualSession();
        UnsafeModeNeverStopsForRejectedMatches();
        ManualSessionStopsSafelyForFrameBacklog();
        ManualSessionStopsAtTheFrameLimit();
        ManualSessionDiscardsFramesArrivingAfterCancellation();
    }

    private static void FirstFramePreviewHonorsBoundsWithoutUpscaling()
    {
        using var source = CreatePatternBitmap(ViewportWidth, ViewportHeight, seed: 111);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        session.AddFrame((Bitmap)source.Clone());

        using var bounded = session.BuildPreview(maximumWidth: 180, maximumHeight: 100);
        AssertEqual(112, bounded.Width, "首帧缩略图按高度边界等比缩放后的宽度");
        AssertEqual(100, bounded.Height, "首帧缩略图不超过高度边界");
        AssertTrue(bounded.Width <= 180, "首帧缩略图不超过宽度边界");

        using var notUpscaled = session.BuildPreview(
            maximumWidth: ViewportWidth * 2,
            maximumHeight: ViewportHeight * 2);
        AssertEqual(source.Size, notUpscaled.Size, "首帧预览不会放大原图");
        AssertBitmapEqual(source, notUpscaled, "未缩放的首帧预览保持原始像素");
    }

    private static void StickyBidirectionalPreviewPreservesContentOrder()
    {
        const int headerHeight = 36;
        const int footerHeight = 28;
        const int bodyViewportHeight = ViewportHeight - headerHeight - footerHeight;
        const int bodyHeight = bodyViewportHeight * 3;
        var headerColor = Color.FromArgb(230, 32, 32);
        var firstBodyColor = Color.FromArgb(30, 210, 50);
        var secondBodyColor = Color.FromArgb(30, 80, 220);
        var thirdBodyColor = Color.FromArgb(190, 35, 210);
        var footerColor = Color.FromArgb(230, 200, 25);
        using var header = CreateTintedPatternBitmap(
            ViewportWidth,
            headerHeight,
            seed: 112,
            headerColor);
        using var body = CreateTintedPatternBitmap(
            ViewportWidth,
            bodyHeight,
            seed: 113,
            firstBodyColor,
            secondBodyColor,
            thirdBodyColor);
        using var footer = CreateTintedPatternBitmap(
            ViewportWidth,
            footerHeight,
            seed: 114,
            footerColor);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 256));
        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 384));
        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 512));
        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 384));
        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 256));
        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 128));
        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 0));

        using var preview = session.BuildPreview(maximumWidth: 180, maximumHeight: 300);
        AssertEqual(130, preview.Width, "双向固定头尾预览按长边约束后的宽度");
        AssertEqual(300, preview.Height, "双向固定头尾预览按长边约束后的高度");

        var sourceHeight = headerHeight + bodyHeight + footerHeight;
        AssertColorClose(
            headerColor,
            AveragePreviewRow(preview, ScaleY(headerHeight / 2, sourceHeight, preview.Height)),
            "预览顶部保留固定页头");
        AssertColorClose(
            firstBodyColor,
            AveragePreviewRow(preview, ScaleY(headerHeight + bodyHeight / 6, sourceHeight, preview.Height)),
            "预览正文第一段位于页头之后");
        AssertColorClose(
            secondBodyColor,
            AveragePreviewRow(preview, ScaleY(headerHeight + bodyHeight / 2, sourceHeight, preview.Height)),
            "预览正文第二段保持在中部");
        AssertColorClose(
            thirdBodyColor,
            AveragePreviewRow(preview, ScaleY(headerHeight + bodyHeight * 5 / 6, sourceHeight, preview.Height)),
            "预览正文第三段保持在尾部之前");
        AssertColorClose(
            footerColor,
            AveragePreviewRow(
                preview,
                ScaleY(headerHeight + bodyHeight + footerHeight / 2, sourceHeight, preview.Height)),
            "预览底部只保留固定页脚");
    }

    private static void PreviewSupportsOnePixelBounds()
    {
        using var source = CreatePatternBitmap(ViewportWidth, ViewportHeight, seed: 115);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());
        session.AddFrame((Bitmap)source.Clone());

        using var square = session.BuildPreview(maximumWidth: 1, maximumHeight: 1);
        AssertEqual(new Size(1, 1), square.Size, "预览支持 1x1 的极小边界");

        using var widthLimited = session.BuildPreview(maximumWidth: 1, maximumHeight: 7);
        AssertEqual(new Size(1, 1), widthLimited.Size, "极小宽度仍保持至少一个像素");

        using var heightLimited = session.BuildPreview(maximumWidth: 7, maximumHeight: 1);
        AssertEqual(new Size(1, 1), heightLimited.Size, "极小高度仍保持至少一个像素");
    }

    private static void BuildingPreviewDoesNotMutateFinalResult()
    {
        const int pageHeight = 480;
        using var source = CreatePatternBitmap(ViewportWidth, pageHeight, seed: 116);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        AddViewport(session, source, 0);
        AssertAccepted(
            AddViewport(session, source, 160),
            VerticalScrollDirection.Down,
            contentAppended: true,
            "预览只读测试的第二帧");
        var acceptedFrameCount = session.AcceptedFrameCount;
        var estimatedHeight = session.EstimatedHeight;
        using var beforePreview = session.BuildResult();

        using var preview = session.BuildPreview(maximumWidth: 80, maximumHeight: 80);

        AssertEqual(acceptedFrameCount, session.AcceptedFrameCount, "生成预览不改变可信帧数");
        AssertEqual(estimatedHeight, session.EstimatedHeight, "生成预览不改变最终高度");
        using var afterPreview = session.BuildResult();
        AssertBitmapEqual(beforePreview, afterPreview, "生成预览不改变最终 BuildResult");
        AssertBitmapEqual(source, afterPreview, "生成预览后仍导出完整原页");
    }

    private static void MiddleStartExtendsDownThenUpToTheWholePage()
    {
        const int pageHeight = 960;
        using var source = CreatePatternBitmap(ViewportWidth, pageHeight, seed: 101);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        var started = AddViewport(session, source, 320);
        AssertEqual(LongCaptureAppendDecision.Started, started.Decision, "中部首帧开始会话");

        var firstDown = AddViewport(session, source, 480);
        AssertAccepted(firstDown, VerticalScrollDirection.Down, contentAppended: true, "中部起点首次向下");
        using (var downwardResult = session.BuildResult())
        using (var downwardExpected = Crop(source, new Rectangle(0, 320, ViewportWidth, 480)))
        {
            AssertBitmapEqual(downwardExpected, downwardResult, "中部起点向下扩展结果");
        }

        var secondDown = AddViewport(session, source, 640);
        AssertAccepted(secondDown, VerticalScrollDirection.Down, contentAppended: true, "向下扩展到页尾");
        using (var downwardResult = session.BuildResult())
        using (var downwardExpected = Crop(source, new Rectangle(0, 320, ViewportWidth, 640)))
        {
            AssertBitmapEqual(downwardExpected, downwardResult, "向下扩展到页尾结果");
        }

        AssertAccepted(
            AddViewport(session, source, 480),
            VerticalScrollDirection.Up,
            contentAppended: false,
            "向上回到已捕获区域");
        AssertAccepted(
            AddViewport(session, source, 320),
            VerticalScrollDirection.Up,
            contentAppended: false,
            "向上回到初始区域");
        AssertAccepted(
            AddViewport(session, source, 160),
            VerticalScrollDirection.Up,
            contentAppended: true,
            "向上扩展新区域");
        AssertAccepted(
            AddViewport(session, source, 0),
            VerticalScrollDirection.Up,
            contentAppended: true,
            "向上扩展到页首");

        AssertEqual(pageHeight, session.EstimatedHeight, "双向扩展后的完整高度");
        using var result = session.BuildResult();
        AssertBitmapEqual(source, result, "中部起点先下后上得到完整源页");
    }

    private static void StickyHeaderAndFooterAppearOnceAcrossBidirectionalScroll()
    {
        const int headerHeight = 36;
        const int footerHeight = 28;
        const int bodyViewportHeight = ViewportHeight - headerHeight - footerHeight;
        const int bodyHeight = bodyViewportHeight * 3;
        using var header = CreatePatternBitmap(ViewportWidth, headerHeight, seed: 108);
        using var body = CreatePatternBitmap(ViewportWidth, bodyHeight, seed: 109);
        using var footer = CreatePatternBitmap(ViewportWidth, footerHeight, seed: 110);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 256));
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 384)),
            VerticalScrollDirection.Down,
            contentAppended: true,
            "固定页头页脚首次向下");
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 512)),
            VerticalScrollDirection.Down,
            contentAppended: true,
            "固定页头页脚向下到正文末尾");
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 384)),
            VerticalScrollDirection.Up,
            contentAppended: false,
            "固定页头页脚向上回访");
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 256)),
            VerticalScrollDirection.Up,
            contentAppended: false,
            "固定页头页脚向上回到起点");
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 128)),
            VerticalScrollDirection.Up,
            contentAppended: true,
            "固定页头页脚向上扩展");
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 0)),
            VerticalScrollDirection.Up,
            contentAppended: true,
            "固定页头页脚向上到正文起点");

        using var expected = ComposeHeaderBodyFooter(header, body, footer);
        using var result = session.BuildResult();
        AssertEqual(expected.Height, session.EstimatedHeight, "固定页头正文页脚双向拼接高度");
        AssertBitmapEqual(expected, result, "固定页头正文页脚双向逐像素连续");
        AssertEqual(1, CountExactVerticalOccurrences(result, header), "固定页头在长图中只出现一次");
        AssertEqual(1, CountExactVerticalOccurrences(result, footer), "固定页脚在长图中只出现一次");
    }

    private static void StickyHeaderRepaintDoesNotStopReliableStitching()
    {
        const int headerHeight = 40;
        const int footerHeight = 24;
        const int bodyViewportHeight = ViewportHeight - headerHeight - footerHeight;
        const int finalOffset = 256;
        using var header = CreatePatternBitmap(ViewportWidth, headerHeight, seed: 121);
        using var repaintedHeader = (Bitmap)header.Clone();
        using (var graphics = Graphics.FromImage(repaintedHeader))
        {
            graphics.DrawLine(Pens.Magenta, 0, 32, repaintedHeader.Width - 1, 32);
        }
        using var body = CreatePatternBitmap(
            ViewportWidth,
            bodyViewportHeight + finalOffset,
            seed: 122);
        using var footer = CreatePatternBitmap(ViewportWidth, footerHeight, seed: 123);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 0));
        AssertAccepted(
            session.AddFrame(CreateStickyViewport(header, body, footer, bodyOffset: 128)),
            VerticalScrollDirection.Down,
            contentAppended: true,
            "固定页头首次匹配");
        var afterRepaint = session.AddFrame(
            CreateStickyViewport(repaintedHeader, body, footer, bodyOffset: finalOffset));

        AssertAccepted(
            afterRepaint,
            VerticalScrollDirection.Down,
            contentAppended: true,
            "固定页头局部重绘后仍按既有正文边界拼接");
        AssertEqual(headerHeight, afterRepaint.Match!.FixedTopHeight, "复用首个可信固定页头边界");
        using var expected = ComposeHeaderBodyFooter(header, body, footer);
        using var result = session.BuildResult();
        AssertBitmapEqual(expected, result, "固定页头局部重绘后的最终导出位图连续");
    }

    private static void UnsafeModeAcceptsMostLikelyBidirectionalSeam()
    {
        const int shift = 160;
        using var source = CreatePatternBitmap(ViewportWidth, ViewportHeight + shift, seed: 130);
        var previous = CreateViewport(source, 0);
        var current = CreateViewport(source, shift);
        var overlayBounds = new Rectangle(0, 104, 84, 116);
        using (var previousGraphics = Graphics.FromImage(previous))
        using (var currentGraphics = Graphics.FromImage(current))
        {
            previousGraphics.FillRectangle(Brushes.Cyan, overlayBounds);
            currentGraphics.FillRectangle(Brushes.Cyan, overlayBounds);
        }
        using var session = new BidirectionalLongCaptureStitchSession(
            CreateOptions() with { SafetyChecksEnabled = false });

        session.AddFrame(previous);
        var appended = session.AddFrame(current);

        AssertAccepted(
            appended,
            VerticalScrollDirection.Down,
            contentAppended: true,
            "双向宽松模式选择固定悬浮区域下最可能的接缝");
        AssertEqual(shift, appended.Match!.ShiftY, "双向宽松模式仍采用最高分滚动距离");
        using var result = session.BuildResult();
        AssertEqual(ViewportHeight + shift, result.Height, "双向宽松模式最终导出包含追加内容");
    }

    private static void RevisitingCapturedRangesDoesNotDuplicatePixels()
    {
        const int pageHeight = 800;
        using var source = CreatePatternBitmap(ViewportWidth, pageHeight, seed: 102);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        AddViewport(session, source, 0);
        var firstDown = AddViewport(session, source, 160);
        AssertAccepted(firstDown, VerticalScrollDirection.Down, contentAppended: true, "首次向下追加");
        AssertEqual(2, session.AcceptedFrameCount, "首次向下后的可信帧数");
        AssertEqual(480, session.EstimatedHeight, "首次向下后的拼接高度");

        var upToCaptured = AddViewport(session, source, 0);
        AssertAccepted(upToCaptured, VerticalScrollDirection.Up, contentAppended: false, "向上回访已捕获范围");
        AssertEqual(2, session.AcceptedFrameCount, "向上回访不增加可信帧数");
        AssertEqual(480, session.EstimatedHeight, "向上回访不增加高度");

        var downToCaptured = AddViewport(session, source, 160);
        AssertAccepted(downToCaptured, VerticalScrollDirection.Down, contentAppended: false, "再次向下回访已捕获范围");
        AssertEqual(2, session.AcceptedFrameCount, "再次向下回访不增加可信帧数");
        AssertEqual(480, session.EstimatedHeight, "再次向下回访不重复追加像素");

        AssertAccepted(
            AddViewport(session, source, 320),
            VerticalScrollDirection.Down,
            contentAppended: true,
            "越过已捕获下边界继续追加");
        AssertAccepted(
            AddViewport(session, source, 480),
            VerticalScrollDirection.Down,
            contentAppended: true,
            "继续向下扩展到页尾");

        AssertEqual(4, session.AcceptedFrameCount, "仅新覆盖区域增加可信帧数");
        AssertEqual(pageHeight, session.EstimatedHeight, "回访后继续向下的完整高度");
        using var result = session.BuildResult();
        AssertBitmapEqual(source, result, "先下后上再下不产生重复行");
    }

    private static void MultipleUpwardStepsProduceTheWholePage()
    {
        const int pageHeight = 800;
        using var source = CreatePatternBitmap(ViewportWidth, pageHeight, seed: 103);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        AddViewport(session, source, 480);
        foreach (var offset in new[] { 320, 160, 0 })
        {
            var append = AddViewport(session, source, offset);
            AssertAccepted(
                append,
                VerticalScrollDirection.Up,
                contentAppended: true,
                $"纯向上滚动到 {offset}px");
        }

        AssertEqual(4, session.AcceptedFrameCount, "纯向上多步可信帧数");
        AssertEqual(pageHeight, session.EstimatedHeight, "纯向上多步完整高度");
        using var result = session.BuildResult();
        AssertBitmapEqual(source, result, "纯向上多步得到完整源页");
    }

    private static void DuplicateFramesDoNotStopLaterProgress()
    {
        const int pageHeight = 480;
        using var source = CreatePatternBitmap(ViewportWidth, pageHeight, seed: 104);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        AddViewport(session, source, 0);
        var duplicate = AddViewport(session, source, 0);
        AssertEqual(LongCaptureAppendDecision.NoMotion, duplicate.Decision, "重复帧只报告未移动");
        AssertEqual(VerticalScrollDirection.None, duplicate.Direction, "重复帧没有滚动方向");
        AssertTrue(!duplicate.ContentAppended, "重复帧不追加内容");
        AssertEqual(1, session.AcceptedFrameCount, "重复帧不增加可信帧数");

        var moved = AddViewport(session, source, 160);
        AssertAccepted(moved, VerticalScrollDirection.Down, contentAppended: true, "重复帧后仍可继续滚动");
        AssertEqual(2, session.AcceptedFrameCount, "重复帧后新区域仍被接纳");
        using var result = session.BuildResult();
        AssertBitmapEqual(source, result, "重复帧不会终止后续完整拼接");
    }

    private static void MatcherReportsBothScrollDirections()
    {
        using var source = CreatePatternBitmap(ViewportWidth, 640, seed: 105);
        using var session = new BidirectionalLongCaptureStitchSession(CreateOptions());

        AddViewport(session, source, 160);
        var downward = AddViewport(session, source, 320);
        AssertAccepted(downward, VerticalScrollDirection.Down, contentAppended: true, "方向判定向下");
        AssertEqual(160, downward.Match!.ShiftY, "向下位移量");

        var upward = AddViewport(session, source, 160);
        AssertAccepted(upward, VerticalScrollDirection.Up, contentAppended: false, "方向判定向上");
        AssertEqual(160, upward.Match!.ShiftY, "向上位移量");
    }

    private static void ProvisionalRejectionDoesNotStopManualSession()
    {
        using var source = CreatePatternBitmap(ViewportWidth, 480, seed: 117);
        using var session = new ManualLongCaptureSession(CreateOptions());

        session.Start(CreateViewport(source, 0));
        var provisional = session.SubmitStableFrame(
            CreatePatternBitmap(ViewportWidth - 1, ViewportHeight, seed: 118),
            stopOnRejected: false);

        AssertEqual(LongCaptureAppendDecision.Rejected, provisional.Decision, "临时帧尺寸不一致会被拒绝");
        AssertEqual(FrameMatchDecision.InvalidDimensions, provisional.MatchDecision!.Value, "临时拒绝保留真实匹配原因");
        AssertEqual(ManualLongCaptureStopReason.None, provisional.StopReason, "临时拒绝不会设置停止原因");
        AssertEqual(ManualLongCaptureSessionState.Capturing, session.State, "临时拒绝后会话继续捕获");
        AssertEqual(1, session.AcceptedFrameCount, "临时拒绝不改变可信帧数");
        AssertTrue(!provisional.PreviewChanged, "临时拒绝不触发预览刷新");

        var recovered = session.SubmitStableFrame(CreateViewport(source, 160));
        AssertEqual(LongCaptureAppendDecision.Accepted, recovered.Decision, "临时拒绝后仍可接纳后续可信帧");
        AssertEqual(ManualLongCaptureSessionState.Capturing, session.State, "接纳恢复帧后继续捕获");

        var firstStableRejection = session.SubmitStableFrame(
            CreatePatternBitmap(ViewportWidth - 1, ViewportHeight, seed: 119),
            stopOnRejected: true);
        AssertEqual(LongCaptureAppendDecision.Rejected, firstStableRejection.Decision, "首次最终稳定帧验证失败会被拒绝");
        AssertEqual(ManualLongCaptureStopReason.None, firstStableRejection.StopReason, "首次最终稳定拒绝仍允许重试");
        AssertEqual(ManualLongCaptureSessionState.Capturing, session.State, "首次最终稳定拒绝不会立即停止");

        var confirmedStableRejection = session.SubmitStableFrame(
            CreatePatternBitmap(ViewportWidth - 1, ViewportHeight, seed: 124),
            stopOnRejected: true);
        AssertEqual(LongCaptureAppendDecision.Rejected, confirmedStableRejection.Decision, "连续最终稳定帧验证失败会被拒绝");
        AssertEqual(ManualLongCaptureStopReason.MatchRejected, confirmedStableRejection.StopReason, "连续最终稳定拒绝报告匹配失败");
        AssertEqual(ManualLongCaptureStopReason.MatchRejected, session.StopReason, "会话记录确认后的稳定拒绝原因");
        AssertEqual(ManualLongCaptureSessionState.SafetyStopped, session.State, "连续最终稳定拒绝才触发安全停止");
        AssertTrue(
            ManualLongCaptureController.ShouldWaitForUserCompletion(session.State),
            "匹配失败后暂停等待用户点击完成");
    }

    private static void UnsafeModeNeverStopsForRejectedMatches()
    {
        using var source = CreatePatternBitmap(ViewportWidth, ViewportHeight, seed: 125);
        using var session = new ManualLongCaptureSession(
            CreateOptions() with { SafetyChecksEnabled = false });
        session.Start((Bitmap)source.Clone());

        for (var index = 0; index < 3; index++)
        {
            var rejected = session.SubmitStableFrame(
                CreatePatternBitmap(ViewportWidth - 1, ViewportHeight, seed: 126 + index),
                stopOnRejected: true);
            AssertEqual(LongCaptureAppendDecision.Rejected, rejected.Decision, "宽松模式仍识别无法拼接的异常帧");
            AssertEqual(ManualLongCaptureStopReason.None, rejected.StopReason, "宽松模式拒绝帧不设置停止原因");
            AssertEqual(ManualLongCaptureSessionState.Capturing, session.State, "宽松模式拒绝帧后继续捕获");
        }

        using var result = session.Complete();
        AssertBitmapEqual(source, result, "宽松模式跳过异常帧后仍可导出已有内容");
    }

    private static void CommandArbitrationPrioritizesCancellation()
    {
        var commands = new ConcurrentQueue<ManualLongCaptureInput>();
        commands.Enqueue(new ManualLongCaptureInput(ManualLongCaptureInputKind.Finish));
        commands.Enqueue(new ManualLongCaptureInput(ManualLongCaptureInputKind.Cancel));
        commands.Enqueue(new ManualLongCaptureInput(ManualLongCaptureInputKind.Finish));

        AssertEqual(
            ManualLongCaptureInputKind.Cancel,
            ManualLongCaptureController.ConsumePendingCommand(commands)!.Value,
            "Esc 在待处理完成命令中拥有最高优先级");
        AssertTrue(commands.IsEmpty, "命令仲裁会清空同批重复命令");

        commands.Enqueue(new ManualLongCaptureInput(ManualLongCaptureInputKind.Finish));
        AssertEqual(
            ManualLongCaptureInputKind.Finish,
            ManualLongCaptureController.ConsumePendingCommand(commands)!.Value,
            "没有 Esc 时完成按钮命令正常完成");
        AssertTrue(
            ManualLongCaptureController.ConsumePendingCommand(commands) is null,
            "没有命令时仲裁结果为空");
    }

    private static void GlobalKeyboardMonitorOnlyHandlesEscape()
    {
        AssertEqual(
            ManualLongCaptureInputKind.Cancel,
            LongCaptureInputMonitor.GetKeyboardCommand(0x1B)!.Value,
            "全局 Esc 仍会取消长截图");
        AssertTrue(
            LongCaptureInputMonitor.GetKeyboardCommand(0x0D) is null,
            "全局 Enter 不会完成长截图");
    }

    private static void ManualSessionStopsSafelyForFrameBacklog()
    {
        using var source = CreatePatternBitmap(ViewportWidth, ViewportHeight, seed: 120);
        using var session = new ManualLongCaptureSession(CreateOptions());
        session.Start((Bitmap)source.Clone());

        session.StopForFrameBacklog();

        AssertEqual(
            ManualLongCaptureSessionState.SafetyStopped,
            session.State,
            "中间帧积压时安全停止会话");
        AssertEqual(
            ManualLongCaptureStopReason.FrameBacklog,
            session.StopReason,
            "中间帧积压报告独立停止原因");
        AssertTrue(
            ManualLongCaptureController.ShouldWaitForUserCompletion(session.State),
            "中间帧积压后暂停等待用户点击完成");
        using var result = session.Complete();
        AssertBitmapEqual(source, result, "积压停止仍保留全部已验证像素");
    }

    private static void ManualSessionStopsAtTheFrameLimit()
    {
        var options = CreateOptions() with { MaximumFrames = 3 };
        using var source = CreatePatternBitmap(ViewportWidth, 640, seed: 106);
        using var session = new ManualLongCaptureSession(options);

        var started = session.Start(CreateViewport(source, 0));
        AssertEqual(LongCaptureAppendDecision.Started, started.Decision, "手动会话首帧");
        AssertEqual(ManualLongCaptureSessionState.Capturing, session.State, "手动会话开始捕获");

        var second = session.SubmitStableFrame(CreateViewport(source, 120));
        AssertEqual(LongCaptureAppendDecision.Accepted, second.Decision, "帧限制前接纳第二帧");
        AssertEqual(ManualLongCaptureSessionState.Capturing, session.State, "第二帧后继续捕获");

        var limited = session.SubmitStableFrame(CreateViewport(source, 240));
        AssertEqual(LongCaptureAppendDecision.Accepted, limited.Decision, "达到限制的帧仍纳入结果");
        AssertEqual(3, limited.AcceptedFrameCount, "达到最大可信帧数");
        AssertEqual(ManualLongCaptureStopReason.FrameLimit, limited.StopReason, "提交结果报告帧数限制");
        AssertEqual(ManualLongCaptureStopReason.FrameLimit, session.StopReason, "会话记录帧数限制");
        AssertEqual(ManualLongCaptureSessionState.SafetyStopped, session.State, "达到帧限制后安全停止");
        AssertTrue(
            ManualLongCaptureController.ShouldWaitForUserCompletion(session.State),
            "达到帧限制后暂停等待用户点击完成");

        using var result = session.Complete();
        using var expected = Crop(source, new Rectangle(0, 0, ViewportWidth, 560));
        AssertBitmapEqual(expected, result, "帧限制停止时保留全部可信像素");
        AssertEqual(ManualLongCaptureSessionState.Completed, session.State, "帧限制结果仍可完成导出");
    }

    private static void ManualSessionDiscardsFramesArrivingAfterCancellation()
    {
        using var source = CreatePatternBitmap(ViewportWidth, 480, seed: 107);
        using var session = new ManualLongCaptureSession(CreateOptions());

        session.Start(CreateViewport(source, 0));
        session.Cancel();
        AssertEqual(ManualLongCaptureSessionState.Cancelled, session.State, "手动会话取消状态");

        var lateFrame = CreateViewport(source, 160);
        var late = session.SubmitStableFrame(lateFrame);
        AssertEqual(LongCaptureAppendDecision.NoMotion, late.Decision, "取消后的迟到帧不参与匹配");
        AssertEqual(VerticalScrollDirection.None, late.Direction, "取消后的迟到帧没有方向");
        AssertTrue(!late.PreviewChanged, "取消后的迟到帧不刷新预览");
        AssertEqual(1, late.AcceptedFrameCount, "取消后的迟到帧不改变可信帧数");
        AssertEqual(ViewportHeight, late.EstimatedHeight, "取消后的迟到帧不改变结果高度");
        AssertEqual(ManualLongCaptureSessionState.Cancelled, session.State, "迟到帧不恢复已取消会话");
        AssertBitmapDisposed(lateFrame, "取消后的迟到帧由会话释放");
    }

    private static BidirectionalLongCaptureAppendResult AddViewport(
        BidirectionalLongCaptureStitchSession session,
        Bitmap source,
        int offset) => session.AddFrame(CreateViewport(source, offset));

    private static Bitmap CreateViewport(Bitmap source, int offset) =>
        Crop(source, new Rectangle(0, offset, ViewportWidth, ViewportHeight));

    private static LongCaptureOptions CreateOptions() => new()
    {
        MinimumShift = 4,
        MinimumOverlapPixels = 80,
        MinimumOverlapRatio = 0.25,
        MinimumMatchConfidence = 0.90,
        SafetyChecksEnabled = true,
        MaximumFrames = 20,
        MaximumHeight = 5000,
        MaximumPixels = 10_000_000
    };

    private static Bitmap CreatePatternBitmap(int width, int height, int seed)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var pixels = new int[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = unchecked(
                    (uint)(x * 0x45D9F3B) ^
                    (uint)(y * 0x119DE1F3) ^
                    (uint)(seed * 0x27D4EB2D));
                value ^= value >> 16;
                value *= 0x7FEB352D;
                value ^= value >> 15;
                var red = 24 + (int)(value & 0xCF);
                var green = 24 + (int)((value >> 8) & 0xCF);
                var blue = 24 + (int)((value >> 16) & 0xCF);
                pixels[y * width + x] = unchecked(
                    (int)(0xFF000000U |
                          (uint)(red << 16) |
                          (uint)(green << 8) |
                          (uint)blue));
            }
        }

        WritePixels(bitmap, pixels);
        return bitmap;
    }

    private static Bitmap CreateTintedPatternBitmap(
        int width,
        int height,
        int seed,
        params Color[] verticalBands)
    {
        if (verticalBands.Length == 0)
        {
            throw new ArgumentException("至少需要一个纵向色带。", nameof(verticalBands));
        }

        var bitmap = CreatePatternBitmap(width, height, seed);
        var pixels = ReadPixels(bitmap);
        for (var y = 0; y < height; y++)
        {
            var tint = verticalBands[Math.Min(
                verticalBands.Length - 1,
                y * verticalBands.Length / height)];
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var original = unchecked((uint)pixels[index]);
                var red = (tint.R * 7 + (int)((original >> 16) & 0xFF)) / 8;
                var green = (tint.G * 7 + (int)((original >> 8) & 0xFF)) / 8;
                var blue = (tint.B * 7 + (int)(original & 0xFF)) / 8;
                pixels[index] = unchecked(
                    (int)(0xFF000000U |
                          (uint)(red << 16) |
                          (uint)(green << 8) |
                          (uint)blue));
            }
        }

        WritePixels(bitmap, pixels);
        return bitmap;
    }

    private static int ScaleY(int sourceY, int sourceHeight, int targetHeight) =>
        Math.Clamp(
            (int)Math.Round((double)sourceY * targetHeight / sourceHeight),
            0,
            targetHeight - 1);

    private static Color AveragePreviewRow(Bitmap bitmap, int y)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            red += pixel.R;
            green += pixel.G;
            blue += pixel.B;
        }

        return Color.FromArgb(
            (int)(red / bitmap.Width),
            (int)(green / bitmap.Width),
            (int)(blue / bitmap.Width));
    }

    private static Bitmap CreateStickyViewport(
        Bitmap header,
        Bitmap body,
        Bitmap footer,
        int bodyOffset)
    {
        var bodyViewportHeight = ViewportHeight - header.Height - footer.Height;
        var viewport = new Bitmap(
            ViewportWidth,
            ViewportHeight,
            PixelFormat.Format32bppPArgb);
        using var bodySlice = Crop(body, new Rectangle(
            0,
            bodyOffset,
            ViewportWidth,
            bodyViewportHeight));
        using var graphics = Graphics.FromImage(viewport);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(header, 0, 0);
        graphics.DrawImageUnscaled(bodySlice, 0, header.Height);
        graphics.DrawImageUnscaled(footer, 0, header.Height + bodyViewportHeight);
        return viewport;
    }

    private static Bitmap ComposeHeaderBodyFooter(
        Bitmap header,
        Bitmap body,
        Bitmap footer)
    {
        var result = new Bitmap(
            ViewportWidth,
            header.Height + body.Height + footer.Height,
            PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(header, 0, 0);
        graphics.DrawImageUnscaled(body, 0, header.Height);
        graphics.DrawImageUnscaled(footer, 0, header.Height + body.Height);
        return result;
    }

    private static int CountExactVerticalOccurrences(Bitmap image, Bitmap fragment)
    {
        AssertEqual(image.Width, fragment.Width, "固定区域计数宽度");
        var imagePixels = ReadPixels(image);
        var fragmentPixels = ReadPixels(fragment);
        var count = 0;
        for (var candidateY = 0; candidateY <= image.Height - fragment.Height; candidateY++)
        {
            var matches = true;
            for (var fragmentY = 0; fragmentY < fragment.Height && matches; fragmentY++)
            {
                var imageRow = (candidateY + fragmentY) * image.Width;
                var fragmentRow = fragmentY * fragment.Width;
                for (var x = 0; x < fragment.Width; x++)
                {
                    if (imagePixels[imageRow + x] == fragmentPixels[fragmentRow + x])
                    {
                        continue;
                    }

                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                count++;
            }
        }
        return count;
    }

    private static Bitmap Crop(Bitmap source, Rectangle bounds) =>
        source.Clone(bounds, PixelFormat.Format32bppPArgb);

    private static void AssertAccepted(
        BidirectionalLongCaptureAppendResult append,
        VerticalScrollDirection expectedDirection,
        bool contentAppended,
        string name)
    {
        AssertEqual(LongCaptureAppendDecision.Accepted, append.Decision, $"{name}接纳状态");
        AssertEqual(expectedDirection, append.Direction, $"{name}方向");
        AssertEqual(contentAppended, append.ContentAppended, $"{name}内容追加状态");
        AssertEqual(FrameMatchDecision.Accepted, append.Match!.Decision, $"{name}匹配状态");
    }

    private static void AssertBitmapEqual(Bitmap expected, Bitmap actual, string name)
    {
        AssertEqual(expected.Width, actual.Width, $"{name}宽度");
        AssertEqual(expected.Height, actual.Height, $"{name}高度");
        var expectedPixels = ReadPixels(expected);
        var actualPixels = ReadPixels(actual);
        for (var index = 0; index < expectedPixels.Length; index++)
        {
            if (expectedPixels[index] == actualPixels[index])
            {
                continue;
            }

            var x = index % expected.Width;
            var y = index / expected.Width;
            throw new InvalidOperationException(
                $"{name}失败：首个差异位于 ({x}, {y})，" +
                $"期望 0x{expectedPixels[index]:X8}，实际 0x{actualPixels[index]:X8}。");
        }
    }

    private static void AssertColorClose(Color expected, Color actual, string name)
    {
        const int channelTolerance = 50;
        if (Math.Abs(expected.R - actual.R) <= channelTolerance &&
            Math.Abs(expected.G - actual.G) <= channelTolerance &&
            Math.Abs(expected.B - actual.B) <= channelTolerance)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{name}失败：期望接近 ({expected.R}, {expected.G}, {expected.B})，" +
            $"实际为 ({actual.R}, {actual.G}, {actual.B})。");
    }

    private static int[] ReadPixels(Bitmap bitmap)
    {
        using var normalized = bitmap.Clone(
            new Rectangle(Point.Empty, bitmap.Size),
            PixelFormat.Format32bppPArgb);
        var bounds = new Rectangle(Point.Empty, normalized.Size);
        var data = normalized.LockBits(
            bounds,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var pixels = new int[checked(normalized.Width * normalized.Height)];
            for (var y = 0; y < normalized.Height; y++)
            {
                var row = IntPtr.Add(data.Scan0, y * data.Stride);
                Marshal.Copy(row, pixels, y * normalized.Width, normalized.Width);
            }
            return pixels;
        }
        finally
        {
            normalized.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap bitmap, int[] pixels)
    {
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        var data = bitmap.LockBits(
            bounds,
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var row = IntPtr.Add(data.Scan0, y * data.Stride);
                Marshal.Copy(pixels, y * bitmap.Width, row, bitmap.Width);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void AssertBitmapDisposed(Bitmap bitmap, string name)
    {
        try
        {
            _ = bitmap.GetPixel(0, 0);
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException($"{name}失败：位图仍可读取。");
    }

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
}
