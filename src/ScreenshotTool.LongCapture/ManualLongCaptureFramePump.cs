namespace ScreenshotTool.LongCapture;

/// <summary>
/// Captures intermediate viewport frames on the WinForms UI thread while the user's physical
/// wheel continues to scroll the target application. It never blocks or consumes the wheel.
/// </summary>
internal sealed class ManualLongCaptureFramePump : IDisposable
{
    private readonly ILongCaptureFrameSource _frameSource;
    private readonly Action<Bitmap, long> _onFrameCaptured;
    private readonly Action<long> _onBurstStarted;
    private readonly Action<long> _onBurstQuiet;
    private readonly Action<Exception> _onCaptureFailed;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly int _quietMilliseconds;
    private readonly int _uiThreadId;
    private long _lastWheelAt;
    private long _wheelVersion;
    private bool _active;
    private bool _disposed;

    public ManualLongCaptureFramePump(
        ILongCaptureFrameSource frameSource,
        LongCaptureOptions options,
        Action<Bitmap, long> onFrameCaptured,
        Action<long> onBurstStarted,
        Action<long> onBurstQuiet,
        Action<Exception> onCaptureFailed)
    {
        ArgumentNullException.ThrowIfNull(frameSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(onFrameCaptured);
        ArgumentNullException.ThrowIfNull(onBurstStarted);
        ArgumentNullException.ThrowIfNull(onBurstQuiet);
        ArgumentNullException.ThrowIfNull(onCaptureFailed);
        if (options.IntermediateCaptureIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The intermediate capture interval must be positive.");
        }
        if (options.WheelQuietMilliseconds <=
            options.IntermediateCaptureIntervalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The wheel quiet interval must exceed the capture interval.");
        }

        _frameSource = frameSource;
        _onFrameCaptured = onFrameCaptured;
        _onBurstStarted = onBurstStarted;
        _onBurstQuiet = onBurstQuiet;
        _onCaptureFailed = onCaptureFailed;
        _quietMilliseconds = options.WheelQuietMilliseconds;
        _uiThreadId = Environment.CurrentManagedThreadId;
        _timer = new System.Windows.Forms.Timer
        {
            Interval = options.IntermediateCaptureIntervalMilliseconds
        };
        _timer.Tick += HandleTimerTick;
    }

    public long WheelVersion => Volatile.Read(ref _wheelVersion);

    /// <summary>
    /// Records a wheel message after the low-level hook has observed it. The original message is
    /// still forwarded to the target application by <see cref="LongCaptureInputMonitor"/>.
    /// </summary>
    public void NotifyWheel()
    {
        VerifyUiThread();
        if (_disposed)
        {
            return;
        }

        var version = Interlocked.Increment(ref _wheelVersion);
        _lastWheelAt = Environment.TickCount64;
        if (_active)
        {
            return;
        }

        _active = true;
        _timer.Start();
        InvokeSafely(() => _onBurstStarted(version));
    }

    public void Stop()
    {
        VerifyUiThread();
        StopCore();
    }

    public void Dispose()
    {
        VerifyUiThread();
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopCore();
        _timer.Tick -= HandleTimerTick;
        _timer.Dispose();
    }

    private void HandleTimerTick(object? sender, EventArgs eventArgs)
    {
        if (_disposed || !_active)
        {
            return;
        }

        var version = WheelVersion;
        if (Environment.TickCount64 - _lastWheelAt >= _quietMilliseconds)
        {
            StopCore();
            InvokeSafely(() => _onBurstQuiet(version));
            return;
        }

        Bitmap? frame = null;
        try
        {
            frame = _frameSource.CaptureFrame();
            _onFrameCaptured(frame, version);
            frame = null;
        }
        catch (Exception exception)
        {
            frame?.Dispose();
            StopCore();
            InvokeSafely(() => _onCaptureFailed(exception));
        }
    }

    private void StopCore()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        _timer.Stop();
    }

    private void VerifyUiThread()
    {
        if (_uiThreadId != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException(
                "ManualLongCaptureFramePump must be used on its creating UI thread.");
        }
    }

    private void InvokeSafely(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception exception)
        {
            try
            {
                _onCaptureFailed(exception);
            }
            catch
            {
                // Timer callbacks must not tear down the application's UI message loop.
            }
        }
    }
}
