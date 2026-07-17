using System.Drawing.Imaging;
using ScreenshotTool.LongCapture;

internal static class LongCapturePreparationTests
{
    public static void Run()
    {
        PreparationCompletesBeforeTheBaselineAndControlsTheFirstScroll();
        PreparationFailurePreservesTheReasonWithoutScrolling();
    }

    private static void PreparationCompletesBeforeTheBaselineAndControlsTheFirstScroll()
    {
        var events = new List<string>();
        using var scrollDriver = new GatedPreparationScrollDriver(events);
        var frameSource = new RecordingFrameSource(events);
        var options = CreateFastOptions();

        var captureTask = Task.Run(() => new LongCaptureEngine(options).CaptureAsync(
            frameSource,
            scrollDriver,
            CancellationToken.None));

        AssertTrue(
            scrollDriver.WaitUntilPreparationStarts(TimeSpan.FromSeconds(2)),
            "引擎进入目标准备阶段");
        AssertEqual(1, scrollDriver.PrepareCount, "长截图只开始一次目标准备");
        AssertEqual(0, frameSource.CaptureCount, "目标准备完成前不会采集首帧");
        AssertEqual(0, scrollDriver.ScrollCount, "目标准备完成前不会发送滚轮");
        AssertTrue(!captureTask.IsCompleted, "引擎等待目标准备完成");

        scrollDriver.CompletePreparation(new ScrollTargetPreparationResult(
            true,
            ScrollInputMode.TargetedWindowMessage,
            false,
            "测试目标已准备，首选定向滚轮。"));

        using var result = captureTask.GetAwaiter().GetResult().Image;

        AssertEqual(1, scrollDriver.PrepareCount, "滚动循环不会重复准备目标");
        AssertEqual(1, frameSource.CaptureCount, "准备完成后才采集首帧基线");
        AssertEqual(1, scrollDriver.ScrollCount, "首帧建立后才尝试第一次滚动");
        AssertEqual(
            ScrollInputMode.TargetedWindowMessage,
            scrollDriver.RequestedModes.Single(),
            "准备结果的首选方式用于第一次滚动");
        AssertSequence(
            events,
            ["prepare-start", "prepare-complete", "capture", "scroll:TargetedWindowMessage"],
            "目标准备、首帧与第一次滚动顺序");
    }

    private static void PreparationFailurePreservesTheReasonWithoutScrolling()
    {
        const string diagnostic = "选区中心没有可接收滚轮的窗口。";
        var events = new List<string>();
        using var scrollDriver = new ImmediatePreparationScrollDriver(
            events,
            new ScrollTargetPreparationResult(
                false,
                ScrollInputMode.SystemInput,
                false,
                diagnostic));
        var frameSource = new RecordingFrameSource(events);

        var result = Task.Run(() => new LongCaptureEngine(CreateFastOptions())
                .CaptureAsync(frameSource, scrollDriver, CancellationToken.None))
            .GetAwaiter()
            .GetResult();
        using (result.Image)
        {
            AssertEqual(
                LongCaptureStopReason.ScrollTargetUnavailable,
                result.StopReason,
                "目标准备失败使用独立停止原因");
            AssertEqual(diagnostic, result.Diagnostic, "目标准备失败保留原始诊断");
            AssertEqual(1, scrollDriver.PrepareCount, "目标准备失败不会重复准备");
            AssertEqual(0, scrollDriver.ScrollCount, "目标准备失败不会发送滚轮");

            var message = LongCaptureFeature.CreateInitialFailureMessage(result);
            AssertEqual("没有找到滚动目标", message.Title, "目标准备失败提示标题准确");
            AssertEqual(
                "框选范围里可以包含可滚动内容，但长截图仍需通过选区中心定位真正接收滚轮的窗口或控件。" +
                "请让选区中心落在可滚动正文、列表或网页内容上后重试。" +
                "\n\n诊断：" + diagnostic,
                message.Text,
                "目标准备失败提示正文准确且包含原始诊断");
            AssertEqual(
                MessageBoxIcon.Warning,
                message.Icon,
                "目标准备失败使用警告图标");
        }
    }

    private static LongCaptureOptions CreateFastOptions() => new()
    {
        SafetyChecksEnabled = true,
        InitialTargetSettleMilliseconds = 1,
        StabilizeIntervalMilliseconds = 1,
        StabilizeTimeoutMilliseconds = 30,
        MaximumFrames = 2,
        ConsecutiveNoMotionLimit = 1
    };

    private static void AssertSequence(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string name)
    {
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{name}失败：期望 [{string.Join(", ", expected)}]，" +
                $"实际 [{string.Join(", ", actual)}]。");
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

    private sealed class RecordingFrameSource(List<string> events) : ILongCaptureFrameSource
    {
        public int CaptureCount { get; private set; }

        public Bitmap CaptureFrame()
        {
            events.Add("capture");
            CaptureCount++;
            var bitmap = new Bitmap(80, 60, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.CornflowerBlue);
            return bitmap;
        }
    }

    private sealed class GatedPreparationScrollDriver(List<string> events) :
        ILongCaptureScrollDriver
    {
        private readonly TaskCompletionSource<ScrollTargetPreparationResult> _preparation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _preparationStarted = new();

        public int PrepareCount { get; private set; }

        public int ScrollCount { get; private set; }

        public List<ScrollInputMode> RequestedModes { get; } = [];

        public bool IsUserCancellationRequested => false;

        public ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
            CancellationToken cancellationToken)
        {
            events.Add("prepare-start");
            PrepareCount++;
            _preparationStarted.Set();
            return new ValueTask<ScrollTargetPreparationResult>(
                _preparation.Task.WaitAsync(cancellationToken));
        }

        public ValueTask<ScrollInputResult> ScrollDownAsync(
            ScrollInputMode mode,
            CancellationToken cancellationToken)
        {
            events.Add($"scroll:{mode}");
            ScrollCount++;
            RequestedModes.Add(mode);
            return ValueTask.FromResult(new ScrollInputResult(
                false,
                mode,
                "测试在第一次滚动后停止。"));
        }

        public void CompletePreparation(ScrollTargetPreparationResult result)
        {
            events.Add("prepare-complete");
            if (!_preparation.TrySetResult(result))
            {
                throw new InvalidOperationException("测试准备结果只能完成一次。");
            }
        }

        public bool WaitUntilPreparationStarts(TimeSpan timeout) =>
            _preparationStarted.Wait(timeout);

        public void Dispose()
        {
            _preparationStarted.Dispose();
        }
    }

    private sealed class ImmediatePreparationScrollDriver(
        List<string> events,
        ScrollTargetPreparationResult preparationResult) : ILongCaptureScrollDriver
    {
        public int PrepareCount { get; private set; }

        public int ScrollCount { get; private set; }

        public bool IsUserCancellationRequested => false;

        public ValueTask<ScrollTargetPreparationResult> PrepareTargetAsync(
            CancellationToken cancellationToken)
        {
            events.Add("prepare");
            PrepareCount++;
            return ValueTask.FromResult(preparationResult);
        }

        public ValueTask<ScrollInputResult> ScrollDownAsync(
            ScrollInputMode mode,
            CancellationToken cancellationToken)
        {
            events.Add($"unexpected-scroll:{mode}");
            ScrollCount++;
            return ValueTask.FromResult(new ScrollInputResult(
                true,
                mode,
                "不应发送的测试滚轮。"));
        }

        public void Dispose()
        {
        }
    }
}
