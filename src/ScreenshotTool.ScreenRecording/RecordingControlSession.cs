using System.Diagnostics;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal sealed record RecordingControlResult(
    bool Saved,
    bool Discarded,
    string? FilePath,
    string? Error);

internal sealed class RecordingControlSession : IDisposable
{
    private const string PauseCommandId = "screenshot-tool.screen-recording.pause";
    private const string StopCommandId = "screenshot-tool.screen-recording.stop";
    private const string CancelCommandId = "screenshot-tool.screen-recording.cancel";
    private static readonly IReadOnlyList<CaptureAnnotationToolbarCommand> Commands =
    [
        new(PauseCommandId, "暂停", "暂停或继续录屏", 52),
        new(
            StopCommandId,
            "停止并保存",
            "停止录屏并保存 MP4",
            82,
            CaptureAnnotationToolbarCommandStyle.Danger),
        new(CancelCommandId, "取消", "取消录屏并删除本次文件", 48)
    ];

    private readonly object _stateLock = new();
    private readonly ICaptureAnnotationToolbarSession _annotations;
    private readonly ScreenRecorderSession _session;
    private readonly TaskCompletionSource<RecordingControlResult> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stopwatch _elapsed = new();
    private readonly System.Threading.Timer _timer;
    private CancellationTokenRegistration _cancellationRegistration;
    private bool _paused;
    private bool _stopping;
    private bool _discardOnComplete;
    private bool _disposed;

    public RecordingControlSession(
        ICaptureAnnotationToolbarSession annotations,
        ScreenRecorderSession session,
        CancellationToken cancellationToken)
    {
        _annotations = annotations;
        _session = session;
        _annotations.ActiveTool = CaptureAnnotationTool.Operation;
        _annotations.SetToolVisible(CaptureAnnotationTool.Select, visible: true);
        _annotations.ConfigureToolbar("● 00:00:00", Commands);
        _annotations.ToolbarCommandInvoked += HandleToolbarCommandInvoked;
        _timer = new System.Threading.Timer(
            _ => UpdateElapsedTime(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
        _cancellationRegistration = cancellationToken.Register(() => RequestStop(discard: true));
        _ = ObserveRecorderCompletionAsync();
    }

    public Task<RecordingControlResult> Completion => _completion.Task;

    public void MarkStarted()
    {
        lock (_stateLock)
        {
            if (_disposed || _stopping)
            {
                return;
            }
        }
        _elapsed.Start();
        _timer.Change(0, 250);
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
        _annotations.ToolbarCommandInvoked -= HandleToolbarCommandInvoked;
        _cancellationRegistration.Dispose();
        _timer.Dispose();
    }

    private void HandleToolbarCommandInvoked(
        object? sender,
        CaptureAnnotationToolbarCommandEventArgs e)
    {
        switch (e.CommandId)
        {
            case PauseCommandId:
                TogglePause();
                break;
            case StopCommandId:
                RequestStop(discard: false);
                break;
            case CancelCommandId:
                RequestStop(discard: true);
                break;
        }
    }

    private void TogglePause()
    {
        bool resume;
        lock (_stateLock)
        {
            if (_stopping || _disposed)
            {
                return;
            }
            resume = _paused;
        }

        try
        {
            if (resume)
            {
                _session.Resume();
                _elapsed.Start();
            }
            else
            {
                _session.Pause();
                _elapsed.Stop();
            }
            lock (_stateLock)
            {
                _paused = !resume;
            }
            _annotations.UpdateToolbarCommand(
                PauseCommandId,
                resume ? "暂停" : "继续",
                enabled: true);
            UpdateElapsedTime();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"无法切换录制状态：{exception.Message}",
                "录屏",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RequestStop(bool discard)
    {
        lock (_stateLock)
        {
            _discardOnComplete |= discard;
            if (_stopping || _disposed)
            {
                return;
            }
            _stopping = true;
        }

        _elapsed.Stop();
        TryStopTimer();
        _annotations.UpdateToolbarStatus(discard ? "正在取消…" : "正在保存…");
        _annotations.UpdateToolbarCommand(PauseCommandId, _paused ? "继续" : "暂停", false);
        _annotations.UpdateToolbarCommand(StopCommandId, "停止并保存", false);
        _annotations.UpdateToolbarCommand(CancelCommandId, "取消", false);
        _session.Stop();
    }

    private async Task ObserveRecorderCompletionAsync()
    {
        var result = await _session.Completion.ConfigureAwait(false);
        Complete(result);
    }

    private void Complete(ScreenRecorderResult result)
    {
        TryStopTimer();
        _elapsed.Stop();
        bool discard;
        lock (_stateLock)
        {
            discard = _discardOnComplete;
        }
        if (discard && !string.IsNullOrWhiteSpace(result.FilePath))
        {
            TryDelete(result.FilePath);
        }

        _completion.TrySetResult(new RecordingControlResult(
            result.Succeeded && !discard,
            discard,
            result.FilePath,
            result.Error));
    }

    private void UpdateElapsedTime()
    {
        bool paused;
        lock (_stateLock)
        {
            if (_stopping || _disposed)
            {
                return;
            }
            paused = _paused;
        }
        var elapsed = _elapsed.Elapsed;
        var indicator = paused ? "Ⅱ" : "●";
        _annotations.UpdateToolbarStatus(
            $"{indicator} {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The recorder may still be releasing the output handle.
        }
        catch (UnauthorizedAccessException)
        {
            // Cancellation must still release the recording session.
        }
    }

    private void TryStopTimer()
    {
        try
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // A failed recorder can complete while its coordinator is unwinding.
        }
    }
}
