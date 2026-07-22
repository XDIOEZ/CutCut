using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ScreenshotTool.Contracts;
using ScreenshotTool.Infrastructure;
using ScreenshotTool.Infrastructure.Modules;
using ScreenshotTool.LongCapture;
using ScreenshotTool.Presentation;

internal static class LongCaptureLogicTests
{
    private const int ViewportWidth = 360;
    private const int ViewportHeight = 420;

    public static void Run()
    {
        LongCaptureModuleExposesStableToolbarCommand();
        LongCaptureOptionsFollowHostPreference();
        LongCaptureModuleLoadsThroughTheCollectibleHost();
        OverlayParkingStaysOutsideTheVirtualDesktop();
        ParkingAVisibleModalFormKeepsItsDialogLoopAlive();
        TargetedWheelMessageReachesAnOffscreenStaWindow();
        ExactVariableShiftsProducePixelPerfectResult();
        DuplicateFrameDoesNotAppendContent();
        StickyHeaderAndFooterAreIncludedOnlyOnce();
        StickyHeaderRepaintDoesNotStopReliableStitching();
        LocalDynamicContentDoesNotChangeTheMeasuredShift();
        InteriorFixedOverlayIsRejectedInsteadOfCorruptingTheResult();
        UnsafeModeAcceptsTheMostLikelyFixedOverlaySeam();
        SmallRightSideFloatingButtonIsRejected();
        WideFixedSidebarCannotMasqueradeAsNoMotion();
        ChangingScrollbarEdgeDoesNotChangeTheMeasuredShift();
        RepetitiveAndTexturelessContentIsNotAcceptedAsAConfidentScroll();
        InvalidDimensionsAndUnrelatedFramesLeaveTheAcceptedResultUntouched();
        SizeLimitStopsBeforeAppendingTheOversizedStrip();
        CaptureEngineDistinguishesNoMotionFromEndReached();
        TargetedWheelRetriesWhenSystemInputHasNoEffect();
        CaptureEngineReportsScrollDispatchFailure();
        CaptureEngineStopsAfterConfirmedEndAndReturnsExactPixels();
        CaptureEngineRejectsContinuouslyChangingTransitionFrames();
        PngRoundTripPreservesTheCompleteLongImage();
    }

    private static void LongCaptureModuleExposesStableToolbarCommand()
    {
        using var module = new LongCaptureModule();
        AssertEqual("screenshot-tool.long-capture", module.Id, "长截图模块 ID 保持稳定");
        AssertEqual("长截图", module.DisplayName, "长截图模块显示名称");
        AssertEqual(new Version(1, 1, 0), module.Version, "长截图模块版本");

        var features = module.CreateCaptureFeatures().ToArray();
        AssertEqual(1, features.Length, "长截图模块按会话创建一个功能实例");
        using var feature = features[0];
        AssertEqual("screenshot-tool.long-capture.feature", feature.Id, "长截图功能 ID 保持稳定");
        AssertTrue(feature is ICaptureToolbarCommandProvider, "长截图功能向宿主提供组合式工具栏命令");
        var commands = ((ICaptureToolbarCommandProvider)feature).GetToolbarCommands();
        AssertEqual(1, commands.Count, "长截图功能只注册一个启动命令");
        AssertEqual("screenshot-tool.long-capture.start", commands[0].Id, "长截图启动命令 ID 保持稳定");
        AssertEqual("长截图", commands[0].Text, "长截图工具栏按钮文字");
        AssertTrue(commands[0].Width >= 58, "长截图工具栏命令预留足够显示宽度");
    }

    private static void LongCaptureOptionsFollowHostPreference()
    {
        var relaxed = LongCaptureFeature.CreateOptions(new TestCaptureFeatureHost());
        var safe = LongCaptureFeature.CreateOptions(
            new TestCaptureFeatureHost(longCaptureSafetyChecksEnabled: true));

        AssertTrue(!relaxed.SafetyChecksEnabled, "宿主未开启安全截图时模块使用宽松模式");
        AssertTrue(safe.SafetyChecksEnabled, "宿主开启安全截图时模块启用严格校验");
    }

    private static void LongCaptureModuleLoadsThroughTheCollectibleHost()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "ScreenshotTool.LongCaptureModuleTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var packageDirectory = Path.Combine(directory, "LongCapture");
            Directory.CreateDirectory(packageDirectory);
            var modulePath = Path.Combine(packageDirectory, "ScreenshotTool.LongCapture.dll");
            File.Copy(typeof(LongCaptureModule).Assembly.Location, modulePath);
            using var moduleHost = new ModuleHost(directory);
            var loaded = moduleHost.Refresh();
            AssertEqual(0, loaded.Errors.Count, "长截图模块热加载无错误");
            AssertEqual(1, loaded.Modules.Count, "长截图模块可由可回收宿主发现");
            AssertEqual(
                "screenshot-tool.long-capture",
                loaded.Modules[0].Id,
                "热加载后长截图模块 ID 保持稳定");

            var features = moduleHost.CreateCaptureFeatures();
            AssertEqual(1, features.Count, "热加载长截图模块创建会话功能");
            using var feature = features[0];
            AssertTrue(
                feature is ICaptureToolbarCommandProvider,
                "模块租约转发可选工具栏命令契约");
            var commands = ((ICaptureToolbarCommandProvider)feature).GetToolbarCommands();
            AssertEqual(1, commands.Count, "模块租约暴露长截图启动命令");
            AssertEqual(
                "screenshot-tool.long-capture.start",
                commands[0].Id,
                "热加载后的长截图命令 ID 保持稳定");

            Directory.Delete(packageDirectory, recursive: true);
            var removed = moduleHost.Refresh();
            AssertEqual(0, removed.Modules.Count, "删除模块文件夹后长截图模块退出目录快照");
            AssertEqual(
                1,
                ((ICaptureToolbarCommandProvider)feature).GetToolbarCommands().Count,
                "活动截图会话通过租约延迟释放已删除模块");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void OverlayParkingStaysOutsideTheVirtualDesktop()
    {
        var virtualDesktop = new Rectangle(-1920, -240, 5760, 2400);
        var parked = CaptureOverlayForm.GetLiveCaptureParkingBounds(virtualDesktop);

        AssertEqual(virtualDesktop.Size, parked.Size, "长截图期间遮罩移出屏幕但保持模态窗体尺寸");
        AssertTrue(
            parked.Left > virtualDesktop.Right && parked.Top > virtualDesktop.Bottom,
            "长截图遮罩停放位置完全位于虚拟桌面之外");
    }

    private static void ParkingAVisibleModalFormKeepsItsDialogLoopAlive()
    {
        Exception? failure = null;
        var verifiedWhileModal = false;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form
                {
                    Text = "Long capture modal parking probe",
                    StartPosition = FormStartPosition.Manual,
                    Bounds = new Rectangle(-30000, -30000, 24, 24),
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    Opacity = 0
                };
                using var timer = new System.Windows.Forms.Timer { Interval = 140 };
                form.Shown += (_, _) =>
                {
                    form.Bounds = CaptureOverlayForm.GetLiveCaptureParkingBounds(
                        SystemInformation.VirtualScreen);
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        try
                        {
                            AssertTrue(form.Visible, "停放后的模态遮罩仍保持 Visible");
                            AssertTrue(form.Modal, "停放后的截图遮罩仍处于模态循环");
                            AssertTrue(
                                Rectangle.Intersect(
                                    form.Bounds,
                                    SystemInformation.VirtualScreen).IsEmpty,
                                "真实 WinForms 窗口停放后不覆盖虚拟桌面");
                            verifiedWhileModal = true;
                            form.DialogResult = DialogResult.OK;
                        }
                        catch (Exception exception)
                        {
                            failure = exception;
                            form.DialogResult = DialogResult.Abort;
                        }
                    };
                    timer.Start();
                };

                var result = form.ShowDialog();
                if (failure is null)
                {
                    AssertTrue(verifiedWhileModal, "停放不会让 ShowDialog 提前返回");
                    AssertEqual(DialogResult.OK, result, "模态探针只在显式结束后返回");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "ScreenshotTool.ModalParkingTest"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("模态遮罩停放回归测试超时。");
        }
        if (failure is not null)
        {
            throw new InvalidOperationException("模态遮罩停放回归测试失败。", failure);
        }
    }

    private static void TargetedWheelMessageReachesAnOffscreenStaWindow()
    {
        Exception? failure = null;
        var wheelDelivered = false;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new OffscreenWheelProbeForm
                {
                    Text = "Long capture targeted wheel probe",
                    StartPosition = FormStartPosition.Manual,
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    AutoScaleMode = AutoScaleMode.None,
                    TopMost = true
                };
                var parkingBounds = CaptureOverlayForm.GetLiveCaptureParkingBounds(
                    SystemInformation.VirtualScreen);
                form.Bounds = new Rectangle(parkingBounds.Location, new Size(160, 96));

                using var timeout = new System.Windows.Forms.Timer { Interval = 2000 };
                Point expectedTarget = Point.Empty;
                form.WheelMessageReceived += (_, _) =>
                {
                    try
                    {
                        AssertEqual(1, form.WheelMessageCount, "定向滚轮只投递一条 WM_MOUSEWHEEL");
                        AssertEqual(-120, form.WheelDelta, "定向滚轮使用一格向下滚动量");
                        AssertEqual(expectedTarget, form.WheelScreenPosition,
                            "定向滚轮保留带符号的屏幕坐标");
                        wheelDelivered = true;
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    finally
                    {
                        timeout.Stop();
                        form.BeginInvoke(form.Close);
                    }
                };
                timeout.Tick += (_, _) =>
                {
                    timeout.Stop();
                    failure ??= new TimeoutException(
                        "定向滚轮消息未在 2 秒内到达屏外 WinForms 窗口。");
                    form.Close();
                };
                form.Shown += async (_, _) =>
                {
                    try
                    {
                        AssertTrue(
                            Rectangle.Intersect(form.Bounds, SystemInformation.VirtualScreen).IsEmpty,
                            "滚轮探针窗口完全位于用户虚拟桌面之外");
                        expectedTarget = new Point(
                            form.Bounds.Left + form.Bounds.Width / 2,
                            form.Bounds.Top + form.Bounds.Height / 2);
                        AssertTrue(
                            expectedTarget.X is >= short.MinValue and <= short.MaxValue &&
                            expectedTarget.Y is >= short.MinValue and <= short.MaxValue,
                            "WM_MOUSEWHEEL 探针坐标可用有符号 16 位坐标精确表示");

                        timeout.Start();
                        using var driver = new WindowsScrollDriver(
                            form.Bounds,
                            moveCursor: false,
                            activateTarget: false);
                        var preparation = await driver.PrepareTargetAsync(
                            CancellationToken.None);
                        AssertTrue(preparation.Succeeded,
                            "定向滚轮准备能通过 WindowFromPoint 定位屏外探针窗口");
                        AssertEqual(
                            ScrollInputMode.TargetedWindowMessage,
                            preparation.PreferredInputMode,
                            "禁用前台激活时选择定向滚轮通道");

                        var result = await driver.ScrollDownAsync(
                            ScrollInputMode.TargetedWindowMessage,
                            CancellationToken.None);
                        AssertTrue(result.Succeeded, "PostMessage 成功写入探针窗口消息队列");
                        AssertEqual(
                            ScrollInputMode.TargetedWindowMessage,
                            result.Mode,
                            "真实滚轮投递保持定向消息模式");
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                        timeout.Stop();
                        form.Close();
                    }
                };

                System.Windows.Forms.Application.Run(form);
                if (failure is null)
                {
                    AssertTrue(wheelDelivered,
                        "屏外 STA WinForms 窗口真实处理定向滚轮消息");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "ScreenshotTool.TargetedWheelTest"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(6)))
        {
            throw new TimeoutException("定向滚轮 STA 回归测试超时。");
        }
        if (failure is not null)
        {
            throw new InvalidOperationException("定向滚轮 STA 回归测试失败。", failure);
        }
    }

    private static void ExactVariableShiftsProducePixelPerfectResult()
    {
        const int sourceWidth = 520;
        const int sourceHeight = 1200;
        const int sourceX = 73;
        int[] offsets = [0, 173, 391, 610, 780];
        using var source = CreatePatternBitmap(sourceWidth, sourceHeight, seed: 11);
        using var expected = Crop(source, new Rectangle(
            sourceX,
            0,
            ViewportWidth,
            sourceHeight));
        using var session = CreateSession();

        for (var index = 0; index < offsets.Length; index++)
        {
            var frame = Crop(source, new Rectangle(
                sourceX,
                offsets[index],
                ViewportWidth,
                ViewportHeight));
            var append = session.AddFrame(frame);
            AssertEqual(
                index == 0 ? LongCaptureAppendDecision.Started : LongCaptureAppendDecision.Accepted,
                append.Decision,
                $"长截图第 {index + 1} 帧被接受");
            if (index > 0)
            {
                AssertEqual(
                    offsets[index] - offsets[index - 1],
                    append.Match!.ShiftY,
                    $"长截图第 {index + 1} 帧精确识别滚动距离");
            }
        }

        AssertEqual(offsets.Length, session.AcceptedFrameCount, "变步长长截图记录全部有效帧");
        using var actual = session.BuildResult();
        AssertBitmapEqual(expected, actual, "变步长和末尾小步滚动无缺行或重行");
    }

    private static void DuplicateFrameDoesNotAppendContent()
    {
        using var source = CreatePatternBitmap(ViewportWidth, ViewportHeight, seed: 21);
        using var expected = Clone(source);
        using var session = CreateSession();

        AssertEqual(
            LongCaptureAppendDecision.Started,
            session.AddFrame(Clone(source)).Decision,
            "重复帧测试记录首帧");
        var duplicate = session.AddFrame(Clone(source));
        AssertEqual(LongCaptureAppendDecision.NoMotion, duplicate.Decision, "完全重复帧识别为未滚动");
        AssertEqual(1, session.AcceptedFrameCount, "未滚动帧不增加有效帧计数");
        AssertEqual(ViewportHeight, session.EstimatedHeight, "未滚动帧不增加长图高度");

        using var actual = session.BuildResult();
        AssertBitmapEqual(expected, actual, "未滚动帧不会在结果中重复内容");
    }

    private static void StickyHeaderAndFooterAreIncludedOnlyOnce()
    {
        const int headerHeight = 40;
        const int footerHeight = 24;
        const int dynamicViewportHeight = ViewportHeight - headerHeight - footerHeight;
        int[] offsets = [0, 146, 327, 516];
        var bodyHeight = dynamicViewportHeight + offsets[^1];
        using var header = CreatePatternBitmap(ViewportWidth, headerHeight, seed: 31);
        using var body = CreatePatternBitmap(ViewportWidth, bodyHeight, seed: 32);
        using var footer = CreatePatternBitmap(ViewportWidth, footerHeight, seed: 33);
        using var expected = ComposeHeaderBodyFooter(header, body, footer);
        using var session = CreateSession();

        for (var index = 0; index < offsets.Length; index++)
        {
            var frame = CreateStickyViewport(
                header,
                body,
                footer,
                offsets[index],
                dynamicViewportHeight);
            var append = session.AddFrame(frame);
            AssertEqual(
                index == 0 ? LongCaptureAppendDecision.Started : LongCaptureAppendDecision.Accepted,
                append.Decision,
                $"固定页头页脚第 {index + 1} 帧被接受");
            if (index > 0)
            {
                AssertEqual(headerHeight, append.Match!.FixedTopHeight, "精确识别固定页头高度");
                AssertEqual(footerHeight, append.Match.FixedBottomHeight, "精确识别固定页脚高度");
                AssertEqual(
                    offsets[index] - offsets[index - 1],
                    append.Match.ShiftY,
                    "固定页头页脚场景仍精确识别正文滚动距离");
            }
        }

        using var actual = session.BuildResult();
        AssertBitmapEqual(expected, actual, "固定页头正文页脚只各保留正确次数");
    }

    private static void StickyHeaderRepaintDoesNotStopReliableStitching()
    {
        const int headerHeight = 40;
        const int footerHeight = 24;
        const int dynamicViewportHeight = ViewportHeight - headerHeight - footerHeight;
        const int finalOffset = 292;
        using var header = CreatePatternBitmap(ViewportWidth, headerHeight, seed: 34);
        using var repaintedHeader = Clone(header);
        using (var graphics = Graphics.FromImage(repaintedHeader))
        {
            graphics.DrawLine(Pens.Magenta, 0, 32, repaintedHeader.Width - 1, 32);
        }
        using var body = CreatePatternBitmap(
            ViewportWidth,
            dynamicViewportHeight + finalOffset,
            seed: 35);
        using var footer = CreatePatternBitmap(ViewportWidth, footerHeight, seed: 36);
        using var session = CreateSession();

        session.AddFrame(CreateStickyViewport(
            header,
            body,
            footer,
            bodyOffset: 0,
            dynamicViewportHeight));
        AssertEqual(
            LongCaptureAppendDecision.Accepted,
            session.AddFrame(CreateStickyViewport(
                header,
                body,
                footer,
                bodyOffset: 146,
                dynamicViewportHeight)).Decision,
            "固定页头首次匹配");
        var afterRepaint = session.AddFrame(CreateStickyViewport(
            repaintedHeader,
            body,
            footer,
            bodyOffset: finalOffset,
            dynamicViewportHeight));

        AssertEqual(
            LongCaptureAppendDecision.Accepted,
            afterRepaint.Decision,
            "固定页头局部重绘后仍按既有正文边界拼接");
        AssertEqual(headerHeight, afterRepaint.Match!.FixedTopHeight, "复用首个可信固定页头边界");
        using var expected = ComposeHeaderBodyFooter(header, body, footer);
        using var actual = session.BuildResult();
        AssertBitmapEqual(expected, actual, "固定页头局部重绘后的最终导出位图连续");
    }

    private static void LocalDynamicContentDoesNotChangeTheMeasuredShift()
    {
        const int shift = 181;
        using var page = CreatePatternBitmap(ViewportWidth, ViewportHeight + shift, seed: 41);
        using var previous = new LongCaptureFrame(Crop(page, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight)));
        var currentBitmap = Crop(page, new Rectangle(
            0,
            shift,
            ViewportWidth,
            ViewportHeight));
        using (var graphics = Graphics.FromImage(currentBitmap))
        {
            graphics.FillRectangle(Brushes.Magenta, 18, 92, 54, 76);
        }
        using var current = new LongCaptureFrame(currentBitmap);

        var match = CreateMatcher().Match(previous, current);
        AssertEqual(FrameMatchDecision.Accepted, match.Decision, "局部动态内容不会破坏可靠重叠匹配");
        AssertEqual(shift, match.ShiftY, "局部动态内容下滚动距离仍精确到一个像素");
        AssertTrue(match.Confidence >= 0.90, "局部动态内容匹配保持严格置信度");
    }

    private static void InteriorFixedOverlayIsRejectedInsteadOfCorruptingTheResult()
    {
        const int shift = 180;
        using var page = CreatePatternBitmap(ViewportWidth, ViewportHeight + shift, seed: 51);
        var previousBitmap = Crop(page, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight));
        var currentBitmap = Crop(page, new Rectangle(
            0,
            shift,
            ViewportWidth,
            ViewportHeight));
        var overlayBounds = new Rectangle(0, 106, 86, 116);
        using (var previousGraphics = Graphics.FromImage(previousBitmap))
        using (var currentGraphics = Graphics.FromImage(currentBitmap))
        {
            previousGraphics.FillRectangle(Brushes.Cyan, overlayBounds);
            currentGraphics.FillRectangle(Brushes.Cyan, overlayBounds);
        }
        using var previous = new LongCaptureFrame(previousBitmap);
        using var current = new LongCaptureFrame(currentBitmap);

        var match = CreateMatcher().Match(previous, current);
        AssertEqual(
            FrameMatchDecision.UnsupportedFixedRegion,
            match.Decision,
            "正文内部固定悬浮层会停止严格拼接而不是生成重复内容");
    }

    private static void UnsafeModeAcceptsTheMostLikelyFixedOverlaySeam()
    {
        const int shift = 180;
        using var page = CreatePatternBitmap(ViewportWidth, ViewportHeight + shift, seed: 512);
        var previous = Crop(page, new Rectangle(0, 0, ViewportWidth, ViewportHeight));
        var current = Crop(page, new Rectangle(0, shift, ViewportWidth, ViewportHeight));
        var overlayBounds = new Rectangle(0, 106, 86, 116);
        using (var previousGraphics = Graphics.FromImage(previous))
        using (var currentGraphics = Graphics.FromImage(current))
        {
            previousGraphics.FillRectangle(Brushes.Cyan, overlayBounds);
            currentGraphics.FillRectangle(Brushes.Cyan, overlayBounds);
        }
        using var session = new LongCaptureStitchSession(
            DefaultOptions() with { SafetyChecksEnabled = false });

        session.AddFrame(previous);
        var appended = session.AddFrame(current);

        AssertEqual(
            LongCaptureAppendDecision.Accepted,
            appended.Decision,
            "宽松模式选择固定悬浮区域下最可能的接缝");
        AssertEqual(shift, appended.Match!.ShiftY, "宽松模式仍优先采用最高分滚动距离");
        using var result = session.BuildResult();
        AssertEqual(ViewportHeight + shift, result.Height, "宽松模式最终导出包含追加内容");
    }

    private static void WideFixedSidebarCannotMasqueradeAsNoMotion()
    {
        const int sidebarWidth = ViewportWidth * 7 / 10;
        const int contentWidth = ViewportWidth - sidebarWidth;
        const int shift = 180;
        using var sidebar = CreatePatternBitmap(sidebarWidth, ViewportHeight, seed: 52);
        using var content = CreatePatternBitmap(
            contentWidth,
            ViewportHeight + shift,
            seed: 53);
        using var previous = new LongCaptureFrame(CreateFixedSidebarViewport(
            sidebar,
            content,
            contentOffset: 0));
        using var current = new LongCaptureFrame(CreateFixedSidebarViewport(
            sidebar,
            content,
            contentOffset: shift));

        var match = CreateMatcher().Match(previous, current);
        AssertTrue(
            match.Decision is
                FrameMatchDecision.UnsupportedFixedRegion or
                FrameMatchDecision.InsufficientTexture or
                FrameMatchDecision.Ambiguous,
            "七成宽固定侧栏且主内容实际滚动时会安全拒绝而不是误判未滚动");
        AssertTrue(
            match.Decision != FrameMatchDecision.NoMotion,
            "全 tile 同位置检查不会让宽固定侧栏掩盖主内容滚动");
    }

    private static void SmallRightSideFloatingButtonIsRejected()
    {
        const int shift = 177;
        using var page = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight + shift,
            seed: 511);
        var previousBitmap = Crop(page, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight));
        var currentBitmap = Crop(page, new Rectangle(
            0,
            shift,
            ViewportWidth,
            ViewportHeight));
        var buttonBounds = new Rectangle(ViewportWidth - 52, 146, 32, 32);
        using (var previousGraphics = Graphics.FromImage(previousBitmap))
        using (var currentGraphics = Graphics.FromImage(currentBitmap))
        {
            previousGraphics.FillRectangle(Brushes.OrangeRed, buttonBounds);
            currentGraphics.FillRectangle(Brushes.OrangeRed, buttonBounds);
        }
        using var previous = new LongCaptureFrame(previousBitmap);
        using var current = new LongCaptureFrame(currentBitmap);

        var match = CreateMatcher().Match(previous, current);
        AssertEqual(
            FrameMatchDecision.UnsupportedFixedRegion,
            match.Decision,
            "右侧小型固定悬浮按钮也会被严格模式识别并停止");
    }

    private static void ChangingScrollbarEdgeDoesNotChangeTheMeasuredShift()
    {
        const int shift = 183;
        const int scrollbarWidth = 12;
        using var page = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight + shift,
            seed: 54);
        var previousBitmap = Crop(page, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight));
        var currentBitmap = Crop(page, new Rectangle(
            0,
            shift,
            ViewportWidth,
            ViewportHeight));
        using (var previousGraphics = Graphics.FromImage(previousBitmap))
        using (var currentGraphics = Graphics.FromImage(currentBitmap))
        {
            previousGraphics.FillRectangle(
                Brushes.DarkGray,
                ViewportWidth - scrollbarWidth,
                0,
                scrollbarWidth,
                ViewportHeight);
            currentGraphics.FillRectangle(
                Brushes.LightGray,
                ViewportWidth - scrollbarWidth,
                0,
                scrollbarWidth,
                ViewportHeight);
            previousGraphics.FillRectangle(
                Brushes.White,
                ViewportWidth - scrollbarWidth,
                30,
                scrollbarWidth,
                80);
            currentGraphics.FillRectangle(
                Brushes.Black,
                ViewportWidth - scrollbarWidth,
                240,
                scrollbarWidth,
                80);
        }
        using var previous = new LongCaptureFrame(previousBitmap);
        using var current = new LongCaptureFrame(currentBitmap);

        var match = CreateMatcher().Match(previous, current);
        AssertEqual(FrameMatchDecision.Accepted, match.Decision, "右侧滚动条变化不会破坏正文匹配");
        AssertEqual(shift, match.ShiftY, "排除右侧滚动条后仍精确识别滚动距离");
    }

    private static void RepetitiveAndTexturelessContentIsNotAcceptedAsAConfidentScroll()
    {
        const int periodicShift = 37;
        using var periodic = CreatePeriodicBitmap(
            ViewportWidth,
            ViewportHeight + periodicShift,
            period: 24);
        using var periodicPrevious = new LongCaptureFrame(Crop(periodic, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight)));
        using var periodicCurrent = new LongCaptureFrame(Crop(periodic, new Rectangle(
            0,
            periodicShift,
            ViewportWidth,
            ViewportHeight)));
        var periodicMatch = CreateMatcher().Match(periodicPrevious, periodicCurrent);
        AssertTrue(
            periodicMatch.Decision is
                FrameMatchDecision.Ambiguous or
                FrameMatchDecision.InsufficientTexture,
            "实际滚动但存在多个等价候选的周期内容会判为歧义或纹理不足");

        using var solidPrevious = new LongCaptureFrame(CreateSolidBitmap(
            ViewportWidth,
            ViewportHeight,
            Color.White));
        using var solidCurrent = new LongCaptureFrame(CreateSolidBitmap(
            ViewportWidth,
            ViewportHeight,
            Color.Gainsboro));
        var texturelessMatch = CreateMatcher().Match(solidPrevious, solidCurrent);
        AssertEqual(
            FrameMatchDecision.InsufficientTexture,
            texturelessMatch.Decision,
            "无纹理且发生变化的页面会明确拒绝拼接");
    }

    private static void InvalidDimensionsAndUnrelatedFramesLeaveTheAcceptedResultUntouched()
    {
        using var first = CreatePatternBitmap(ViewportWidth, ViewportHeight, seed: 61);
        using var expected = Clone(first);
        using var session = CreateSession();
        session.AddFrame(Clone(first));

        var invalidDimensions = session.AddFrame(CreatePatternBitmap(
            ViewportWidth + 1,
            ViewportHeight,
            seed: 62));
        AssertEqual(LongCaptureAppendDecision.Rejected, invalidDimensions.Decision, "相邻帧尺寸变化时拒绝拼接");
        AssertEqual(
            FrameMatchDecision.InvalidDimensions,
            invalidDimensions.Match!.Decision,
            "尺寸变化给出明确匹配原因");

        var unrelated = session.AddFrame(CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight,
            seed: 63));
        AssertEqual(LongCaptureAppendDecision.Rejected, unrelated.Decision, "完全不相关帧拒绝拼接");
        AssertEqual(1, session.AcceptedFrameCount, "拒绝帧不会污染有效帧计数");

        using var actual = session.BuildResult();
        AssertBitmapEqual(expected, actual, "拒绝错误帧后保留最后一个可信结果");
    }

    private static void SizeLimitStopsBeforeAppendingTheOversizedStrip()
    {
        const int shift = 180;
        var options = DefaultOptions() with
        {
            MaximumHeight = 500,
            MaximumPixels = ViewportWidth * 500L
        };
        using var page = CreatePatternBitmap(ViewportWidth, ViewportHeight + shift, seed: 71);
        using var expected = Crop(page, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight));
        using var session = new LongCaptureStitchSession(options);
        session.AddFrame(Crop(page, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight)));

        var limited = session.AddFrame(Crop(page, new Rectangle(
            0,
            shift,
            ViewportWidth,
            ViewportHeight)));
        AssertEqual(LongCaptureAppendDecision.LimitReached, limited.Decision, "超过安全高度前停止追加内容");
        AssertEqual(1, session.AcceptedFrameCount, "达到尺寸上限的帧不计入有效帧");
        using var actual = session.BuildResult();
        AssertBitmapEqual(expected, actual, "达到尺寸上限后仍可导出此前可信首帧");
    }

    private static void CaptureEngineStopsAfterConfirmedEndAndReturnsExactPixels()
    {
        int[] offsets = [0, 180, 360];
        using var page = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight + offsets[^1],
            seed: 81);
        using var expected = Clone(page);
        var viewport = new FakeScrollableViewport(page, offsets, ViewportHeight);
        using var scrollDriver = viewport.CreateScrollDriver();
        var options = DefaultOptions() with
        {
            StabilizeIntervalMilliseconds = 1,
            StabilizeTimeoutMilliseconds = 30,
            ConsecutiveNoMotionLimit = 2,
            MaximumFrames = 10
        };

        var result = Task.Run(() => new LongCaptureEngine(options)
                .CaptureAsync(viewport, scrollDriver, CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        using var resultImage = result.Image;

        AssertEqual(LongCaptureStopReason.EndReached, result.StopReason, "状态机连续确认无位移后识别页面底部");
        AssertTrue(result.IsComplete, "到达页面底部的长截图标记为完整");
        AssertEqual(offsets.Length, result.AcceptedFrameCount, "状态机只统计实际滚动后的有效帧");
        AssertEqual(4, viewport.ScrollCount, "状态机使用两次无位移确认而非无限滚动");
        AssertBitmapEqual(expected, result.Image, "状态机最终返回逐像素连续的完整长图");
    }

    private static void CaptureEngineDistinguishesNoMotionFromEndReached()
    {
        using var frame = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight,
            seed: 78);
        var viewport = new FakeScrollableViewport(frame, [0], ViewportHeight);
        using var scrollDriver = viewport.CreateScrollDriver();
        var options = DefaultOptions() with
        {
            StabilizeIntervalMilliseconds = 1,
            StabilizeTimeoutMilliseconds = 30,
            ConsecutiveNoMotionLimit = 2,
            MaximumFrames = 5
        };

        var result = Task.Run(() => new LongCaptureEngine(options)
                .CaptureAsync(viewport, scrollDriver, CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        using var resultImage = result.Image;

        AssertEqual(
            LongCaptureStopReason.NoScrollableMotion,
            result.StopReason,
            "从未验证过滚动时不会把静止画面误报为页面底部");
        AssertTrue(!result.IsComplete, "没有发生视觉滚动的首帧结果不标记为完整");
        AssertEqual(1, result.AcceptedFrameCount, "无视觉滚动时只保留首帧");
        AssertEqual(2, viewport.ScrollCount, "无视觉滚动分别尝试两种滚轮投递方式");
        AssertEqual(
            ScrollInputMode.SystemInput,
            viewport.ScrollModes[0],
            "首轮使用系统滚轮输入");
        AssertEqual(
            ScrollInputMode.TargetedWindowMessage,
            viewport.ScrollModes[1],
            "系统滚轮无效后改用定向窗口消息");

        var message = LongCaptureFeature.CreateInitialFailureMessage(result);
        AssertEqual("滚轮未使选区发生变化", message.Title, "首帧无运动显示真实停止原因");
        AssertTrue(
            !message.Text.Contains("未检测到可滚动内容", StringComparison.Ordinal),
            "首帧无运动不再误报选区没有可滚动内容");
    }

    private static void TargetedWheelRetriesWhenSystemInputHasNoEffect()
    {
        const int shift = 176;
        using var page = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight + shift,
            seed: 79);
        var viewport = new ModeSensitiveScrollableViewport(page, shift, ViewportHeight);
        using var scrollDriver = viewport.CreateScrollDriver();
        var options = DefaultOptions() with
        {
            StabilizeIntervalMilliseconds = 1,
            StabilizeTimeoutMilliseconds = 30,
            ConsecutiveNoMotionLimit = 2,
            MaximumFrames = 8
        };

        var result = Task.Run(() => new LongCaptureEngine(options)
                .CaptureAsync(viewport, scrollDriver, CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        using var resultImage = result.Image;

        AssertEqual(LongCaptureStopReason.EndReached, result.StopReason, "定向滚轮补救后正常识别底部");
        AssertTrue(result.IsComplete, "定向滚轮产生可信位移后可完成长截图");
        AssertEqual(2, result.AcceptedFrameCount, "系统滚轮无效不妨碍定向滚轮追加第二帧");
        AssertEqual(
            ScrollInputMode.TargetedWindowMessage,
            viewport.ScrollModes[1],
            "系统输入无视觉效果后确实执行定向滚轮补救");
        AssertBitmapEqual(page, result.Image, "定向滚轮补救后的长图仍逐像素准确");
    }

    private static void CaptureEngineReportsScrollDispatchFailure()
    {
        using var frame = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight,
            seed: 80);
        using var frameSource = new StaticFrameSource(frame);
        using var scrollDriver = new FailingScrollDriver();
        var options = DefaultOptions() with
        {
            StabilizeIntervalMilliseconds = 1,
            MaximumFrames = 3
        };

        var result = Task.Run(() => new LongCaptureEngine(options)
                .CaptureAsync(frameSource, scrollDriver, CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        using var resultImage = result.Image;

        AssertEqual(LongCaptureStopReason.ScrollFailed, result.StopReason, "滚轮分发失败保留独立原因");
        AssertTrue(!result.IsComplete, "滚轮分发失败不标记为完成");
        AssertTrue(
            result.Diagnostic.Contains("测试输入失败", StringComparison.Ordinal),
            "滚轮分发失败保留底层诊断");
        var message = LongCaptureFeature.CreateInitialFailureMessage(result);
        AssertEqual("滚动输入发送失败", message.Title, "滚轮分发失败显示准确标题");
    }

    private static void CaptureEngineRejectsContinuouslyChangingTransitionFrames()
    {
        using var firstFrame = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight,
            seed: 86);
        using var expected = Clone(firstFrame);
        var frameSource = new UnstableFrameSource(firstFrame);
        using var scrollDriver = new AlwaysScrollDriver();
        var options = DefaultOptions() with
        {
            StabilizeIntervalMilliseconds = 1,
            StabilizeTimeoutMilliseconds = 8,
            MaximumFrames = 3
        };

        var result = Task.Run(() => new LongCaptureEngine(options)
                .CaptureAsync(frameSource, scrollDriver, CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        using var resultImage = result.Image;

        AssertEqual(
            LongCaptureStopReason.UnstableContent,
            result.StopReason,
            "持续变化的滚动过渡画面会按不稳定内容安全停止");
        AssertTrue(!result.IsComplete, "不稳定内容不会伪装成长截图完整结束");
        AssertEqual(1, result.AcceptedFrameCount, "不稳定过渡帧不会进入可信拼接结果");
        AssertBitmapEqual(expected, result.Image, "不稳定停止时只返回此前验证过的首帧");
    }

    private static void PngRoundTripPreservesTheCompleteLongImage()
    {
        const int shift = 197;
        using var source = CreatePatternBitmap(
            ViewportWidth,
            ViewportHeight + shift,
            seed: 91);
        using var session = CreateSession();
        session.AddFrame(Crop(source, new Rectangle(
            0,
            0,
            ViewportWidth,
            ViewportHeight)));
        session.AddFrame(Crop(source, new Rectangle(
            0,
            shift,
            ViewportWidth,
            ViewportHeight)));
        using var stitched = session.BuildResult();
        var directory = Path.Combine(
            Path.GetTempPath(),
            "ScreenshotTool.LongCaptureTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var path = new PngImageSaveService().SavePng(stitched, directory);
            using var reloaded = new Bitmap(path);
            AssertBitmapEqual(source, reloaded, "长截图保存 PNG 后重新读取仍保持全部像素和尺寸");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static LongCaptureStitchSession CreateSession() =>
        new(DefaultOptions());

    private static VerticalFrameMatcher CreateMatcher() =>
        new(DefaultOptions());

    private static LongCaptureOptions DefaultOptions() => new()
    {
        InitialTargetSettleMilliseconds = 1,
        MinimumShift = 4,
        MinimumOverlapPixels = 80,
        MinimumOverlapRatio = 0.25,
        MinimumMatchConfidence = 0.90,
        SafetyChecksEnabled = true,
        MaximumFrames = 20,
        MaximumHeight = 5000,
        MaximumPixels = 10_000_000
    };

    private static ValueTask<ScrollTargetPreparationResult> PrepareTestTargetAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ScrollTargetPreparationResult(
            true,
            ScrollInputMode.SystemInput,
            true,
            "测试滚动目标已准备。"));
    }

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

    private static Bitmap CreatePeriodicBitmap(int width, int height, int period)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var pixels = new int[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var band = (y % period) < period / 2 ? 0x20 : 0xD0;
                var column = (x / 8) % 2 == 0 ? 0x18 : 0x00;
                var shade = Math.Clamp(band + column, 0, 255);
                pixels[y * width + x] = Color.FromArgb(255, shade, shade, shade).ToArgb();
            }
        }
        WritePixels(bitmap, pixels);
        return bitmap;
    }

    private static Bitmap CreateSolidBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static Bitmap CreateStickyViewport(
        Bitmap header,
        Bitmap body,
        Bitmap footer,
        int bodyOffset,
        int dynamicViewportHeight)
    {
        var viewport = new Bitmap(
            header.Width,
            header.Height + dynamicViewportHeight + footer.Height,
            PixelFormat.Format32bppPArgb);
        using var bodySlice = Crop(body, new Rectangle(
            0,
            bodyOffset,
            body.Width,
            dynamicViewportHeight));
        using var graphics = Graphics.FromImage(viewport);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(header, 0, 0);
        graphics.DrawImageUnscaled(bodySlice, 0, header.Height);
        graphics.DrawImageUnscaled(footer, 0, header.Height + dynamicViewportHeight);
        return viewport;
    }

    private static Bitmap ComposeHeaderBodyFooter(Bitmap header, Bitmap body, Bitmap footer)
    {
        var result = new Bitmap(
            header.Width,
            header.Height + body.Height + footer.Height,
            PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(header, 0, 0);
        graphics.DrawImageUnscaled(body, 0, header.Height);
        graphics.DrawImageUnscaled(footer, 0, header.Height + body.Height);
        return result;
    }

    private static Bitmap CreateFixedSidebarViewport(
        Bitmap sidebar,
        Bitmap content,
        int contentOffset)
    {
        var viewport = new Bitmap(
            sidebar.Width + content.Width,
            sidebar.Height,
            PixelFormat.Format32bppPArgb);
        using var contentSlice = Crop(content, new Rectangle(
            0,
            contentOffset,
            content.Width,
            sidebar.Height));
        using var graphics = Graphics.FromImage(viewport);
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(sidebar, 0, 0);
        graphics.DrawImageUnscaled(contentSlice, sidebar.Width, 0);
        return viewport;
    }

    private static Bitmap Crop(Bitmap source, Rectangle bounds) =>
        source.Clone(bounds, PixelFormat.Format32bppPArgb);

    private static Bitmap Clone(Bitmap source) =>
        Crop(source, new Rectangle(Point.Empty, source.Size));

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

    private sealed class OffscreenWheelProbeForm : Form
    {
        private const int WindowMessageMouseWheel = 0x020A;
        private const int WindowStyleExtendedNoActivate = 0x08000000;

        public event EventHandler? WheelMessageReceived;

        public int WheelMessageCount { get; private set; }

        public int WheelDelta { get; private set; }

        public Point WheelScreenPosition { get; private set; }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WindowStyleExtendedNoActivate;
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            var isWheelMessage = message.Msg == WindowMessageMouseWheel;
            if (isWheelMessage)
            {
                var wheelParameter = message.WParam.ToInt64();
                var positionParameter = message.LParam.ToInt64();
                WheelMessageCount++;
                WheelDelta = unchecked((short)((wheelParameter >> 16) & 0xFFFF));
                WheelScreenPosition = new Point(
                    unchecked((short)(positionParameter & 0xFFFF)),
                    unchecked((short)((positionParameter >> 16) & 0xFFFF)));
            }

            base.WndProc(ref message);
            if (isWheelMessage)
            {
                WheelMessageReceived?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private sealed class FakeScrollableViewport(
        Bitmap page,
        IReadOnlyList<int> offsets,
        int viewportHeight) : ILongCaptureFrameSource
    {
        private readonly IReadOnlyList<int> _offsets = offsets;
        private readonly List<ScrollInputMode> _scrollModes = [];
        private int _offsetIndex;

        public int ScrollCount { get; private set; }

        public IReadOnlyList<ScrollInputMode> ScrollModes => _scrollModes;

        public Bitmap CaptureFrame() => Crop(page, new Rectangle(
            0,
            _offsets[_offsetIndex],
            page.Width,
            viewportHeight));

        public ILongCaptureScrollDriver CreateScrollDriver() => new FakeScrollDriver(this);

        private sealed class FakeScrollDriver(FakeScrollableViewport owner) : ILongCaptureScrollDriver
        {
            public bool IsUserCancellationRequested => false;

            public ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
                CancellationToken cancellationToken) =>
                PrepareTestTargetAsync(cancellationToken);

            public ValueTask<ScrollInputResult> ScrollDownAsync(
                ScrollInputMode mode,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                owner.ScrollCount++;
                owner._scrollModes.Add(mode);
                if (owner._offsetIndex < owner._offsets.Count - 1)
                {
                    owner._offsetIndex++;
                }
                return ValueTask.FromResult(new ScrollInputResult(
                    true,
                    mode,
                    "测试滚轮输入已发送。"));
            }

            public void Dispose()
            {
            }
        }
    }

    private sealed class ModeSensitiveScrollableViewport(
        Bitmap page,
        int finalOffset,
        int viewportHeight) : ILongCaptureFrameSource
    {
        private readonly List<ScrollInputMode> _scrollModes = [];
        private readonly int _finalOffset = finalOffset;
        private int _offset;

        public IReadOnlyList<ScrollInputMode> ScrollModes => _scrollModes;

        public Bitmap CaptureFrame() => Crop(page, new Rectangle(
            0,
            _offset,
            page.Width,
            viewportHeight));

        public ILongCaptureScrollDriver CreateScrollDriver() =>
            new ModeSensitiveScrollDriver(this);

        private sealed class ModeSensitiveScrollDriver(
            ModeSensitiveScrollableViewport owner) : ILongCaptureScrollDriver
        {
            public bool IsUserCancellationRequested => false;

            public ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
                CancellationToken cancellationToken) =>
                PrepareTestTargetAsync(cancellationToken);

            public ValueTask<ScrollInputResult> ScrollDownAsync(
                ScrollInputMode mode,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                owner._scrollModes.Add(mode);
                if (mode == ScrollInputMode.TargetedWindowMessage &&
                    owner._offset < owner._finalOffset)
                {
                    owner._offset = owner._finalOffset;
                }

                return ValueTask.FromResult(new ScrollInputResult(
                    true,
                    mode,
                    "测试滚轮输入已发送。"));
            }

            public void Dispose()
            {
            }
        }
    }

    private sealed class StaticFrameSource : ILongCaptureFrameSource, IDisposable
    {
        private readonly Bitmap _frame;

        public StaticFrameSource(Bitmap frame)
        {
            _frame = Clone(frame);
        }

        public Bitmap CaptureFrame() => Clone(_frame);

        public void Dispose() => _frame.Dispose();
    }

    private sealed class FailingScrollDriver : ILongCaptureScrollDriver
    {
        public bool IsUserCancellationRequested => false;

        public ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
            CancellationToken cancellationToken) =>
            PrepareTestTargetAsync(cancellationToken);

        public ValueTask<ScrollInputResult> ScrollDownAsync(
            ScrollInputMode mode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new ScrollInputResult(
                false,
                mode,
                "测试输入失败。"));
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnstableFrameSource(Bitmap firstFrame) : ILongCaptureFrameSource
    {
        private int _captureCount;

        public Bitmap CaptureFrame()
        {
            if (_captureCount++ == 0)
            {
                return Clone(firstFrame);
            }

            return CreatePatternBitmap(
                firstFrame.Width,
                firstFrame.Height,
                seed: 900 + _captureCount);
        }
    }

    private sealed class AlwaysScrollDriver : ILongCaptureScrollDriver
    {
        public bool IsUserCancellationRequested => false;

        public ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
            CancellationToken cancellationToken) =>
            PrepareTestTargetAsync(cancellationToken);

        public ValueTask<ScrollInputResult> ScrollDownAsync(
            ScrollInputMode mode,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScrollInputResult(
                true,
                mode,
                "测试滚轮输入已发送。"));

        public void Dispose()
        {
        }
    }
}
