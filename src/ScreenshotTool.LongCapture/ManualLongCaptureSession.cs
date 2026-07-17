namespace ScreenshotTool.LongCapture;

internal enum ManualLongCaptureSessionState
{
    Created,
    Capturing,
    SafetyStopped,
    Completed,
    Cancelled
}

internal enum ManualLongCaptureStopReason
{
    None,
    MatchRejected,
    SizeLimit,
    FrameLimit,
    FrameBacklog
}

internal sealed record ManualLongCaptureSubmission(
    LongCaptureAppendDecision Decision,
    VerticalScrollDirection Direction,
    int AcceptedFrameCount,
    int EstimatedHeight,
    bool PreviewChanged,
    ManualLongCaptureStopReason StopReason,
    FrameMatchDecision? MatchDecision,
    string Diagnostic);

/// <summary>
/// Owns the deterministic stitching state for a user-driven long capture.
/// Input observation, frame stabilization and preview UI stay outside this class.
/// </summary>
internal sealed class ManualLongCaptureSession : IDisposable
{
    private readonly LongCaptureOptions _options;
    private readonly BidirectionalLongCaptureStitchSession _stitchSession;
    private int _consecutiveStableRejections;
    private bool _disposed;

    public ManualLongCaptureSession(LongCaptureOptions? options = null)
    {
        _options = options ?? new LongCaptureOptions();
        _stitchSession = new BidirectionalLongCaptureStitchSession(_options);
    }

    public ManualLongCaptureSessionState State { get; private set; } =
        ManualLongCaptureSessionState.Created;

    public ManualLongCaptureStopReason StopReason { get; private set; }

    public int AcceptedFrameCount => _stitchSession.AcceptedFrameCount;

    public int EstimatedHeight => _stitchSession.EstimatedHeight;

    public Size EstimatedSize => new(
        _stitchSession.EstimatedWidth,
        _stitchSession.EstimatedHeight);

    public string Diagnostic { get; private set; } = string.Empty;

    /// <summary>
    /// Starts the session and takes ownership of <paramref name="firstFrame"/>.
    /// </summary>
    public ManualLongCaptureSubmission Start(Bitmap firstFrame)
    {
        ArgumentNullException.ThrowIfNull(firstFrame);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ManualLongCaptureSessionState.Created)
        {
            firstFrame.Dispose();
            throw new InvalidOperationException("手动长截图会话已经开始。");
        }

        var append = _stitchSession.AddFrame(firstFrame);
        State = ManualLongCaptureSessionState.Capturing;
        Diagnostic = append.Diagnostic;
        return CreateSubmission(append, previewChanged: true);
    }

    /// <summary>
    /// Submits a frame that the capture loop has already confirmed as stable and takes ownership
    /// of it. Frames arriving after completion or cancellation are discarded safely.
    /// </summary>
    public ManualLongCaptureSubmission SubmitStableFrame(
        Bitmap frame,
        bool stopOnRejected = true)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ManualLongCaptureSessionState.Capturing)
        {
            frame.Dispose();
            return new ManualLongCaptureSubmission(
                LongCaptureAppendDecision.NoMotion,
                VerticalScrollDirection.None,
                AcceptedFrameCount,
                EstimatedHeight,
                false,
                StopReason,
                null,
                "会话已经停止，迟到帧已丢弃。");
        }

        var append = _stitchSession.AddFrame(frame);
        Diagnostic = append.Diagnostic;
        switch (append.Decision)
        {
            case LongCaptureAppendDecision.Accepted:
                _consecutiveStableRejections = 0;
                if (append.ContentAppended &&
                    append.AcceptedFrameCount >= _options.MaximumFrames)
                {
                    StopReason = ManualLongCaptureStopReason.FrameLimit;
                    State = ManualLongCaptureSessionState.SafetyStopped;
                    Diagnostic = "长截图已达到最大可信帧数。";
                }
                break;
            case LongCaptureAppendDecision.NoMotion:
                _consecutiveStableRejections = 0;
                break;
            case LongCaptureAppendDecision.Rejected:
                if (!_options.SafetyChecksEnabled)
                {
                    Diagnostic = $"{append.Diagnostic} 宽松模式已跳过本帧，可继续滚动。";
                }
                else if (stopOnRejected)
                {
                    _consecutiveStableRejections++;
                    if (_consecutiveStableRejections >=
                        _options.ConsecutiveStableRejectionLimit)
                    {
                        StopReason = ManualLongCaptureStopReason.MatchRejected;
                        State = ManualLongCaptureSessionState.SafetyStopped;
                    }
                    else
                    {
                        Diagnostic = $"{append.Diagnostic} 已忽略本次异常画面，可继续滚动重试。";
                    }
                }
                break;
            case LongCaptureAppendDecision.LimitReached:
                StopReason = ManualLongCaptureStopReason.SizeLimit;
                State = ManualLongCaptureSessionState.SafetyStopped;
                break;
        }

        return CreateSubmission(
            append,
            previewChanged: append.ContentAppended);
    }

    /// <summary>
    /// Builds an independent preview source image. The caller owns the returned bitmap.
    /// </summary>
    public Bitmap CreatePreviewImage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureStarted();
        return _stitchSession.BuildPreview(
            _options.MaximumPreviewWidth,
            _options.MaximumPreviewHeight);
    }

    /// <summary>
    /// Completes the capture and returns an independent final bitmap owned by the caller.
    /// </summary>
    public Bitmap Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State is not (
            ManualLongCaptureSessionState.Capturing or
            ManualLongCaptureSessionState.SafetyStopped))
        {
            throw new InvalidOperationException("当前手动长截图会话无法完成。");
        }

        var result = _stitchSession.BuildResult();
        State = ManualLongCaptureSessionState.Completed;
        return result;
    }

    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State is ManualLongCaptureSessionState.Completed or
            ManualLongCaptureSessionState.Cancelled)
        {
            return;
        }

        State = ManualLongCaptureSessionState.Cancelled;
        Diagnostic = "用户取消了手动长截图。";
    }

    public void StopForFrameBacklog()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ManualLongCaptureSessionState.Capturing)
        {
            return;
        }

        StopReason = ManualLongCaptureStopReason.FrameBacklog;
        State = ManualLongCaptureSessionState.SafetyStopped;
        Diagnostic = "用户滚动速度超过了可信中间帧的处理速度，已保留此前验证通过的内容。";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stitchSession.Dispose();
    }

    private ManualLongCaptureSubmission CreateSubmission(
        BidirectionalLongCaptureAppendResult append,
        bool previewChanged) => new(
        append.Decision,
        append.Direction,
        append.AcceptedFrameCount,
        append.EstimatedHeight,
        previewChanged,
        StopReason,
        append.Match?.Decision,
        Diagnostic);

    private void EnsureStarted()
    {
        if (State == ManualLongCaptureSessionState.Created)
        {
            throw new InvalidOperationException("手动长截图会话尚未开始。");
        }

        if (State == ManualLongCaptureSessionState.Cancelled)
        {
            throw new InvalidOperationException("手动长截图会话已经取消。");
        }
    }
}
