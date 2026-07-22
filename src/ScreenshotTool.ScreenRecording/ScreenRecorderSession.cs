using System.Diagnostics;
using System.ComponentModel;
using System.IO.Pipes;
using System.Text;

namespace ScreenshotTool.ScreenRecording;

internal sealed record ScreenRecorderResult(string? FilePath, string? Error)
{
    public bool Succeeded => Error is null && !string.IsNullOrWhiteSpace(FilePath);
}

internal sealed class ScreenRecorderSession : IDisposable
{
    private const string HelperFileName = "ScreenshotTool.ScreenRecording.Recorder.exe";

    private readonly RecordingTarget _target;
    private readonly RecordingOptions _options;
    private readonly string _helperDirectory;
    private readonly TaskCompletionSource<ScreenRecorderResult> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _commandLock = new();
    private NamedPipeServerStream? _pipe;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private Process? _process;
    private bool _started;
    private bool _stopping;
    private bool _disposed;

    public ScreenRecorderSession(
        RecordingTarget target,
        RecordingOptions options,
        string helperDirectory)
    {
        _target = target;
        _options = options;
        _helperDirectory = Path.GetFullPath(helperDirectory);
    }

    public Task<ScreenRecorderResult> Completion => _completion.Task;

    public async Task StartAsync(string outputPath, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            throw new InvalidOperationException("录屏会话已经开始。");
        }

        var helperPath = Path.Combine(_helperDirectory, HelperFileName);
        if (!File.Exists(helperPath))
        {
            throw new FileNotFoundException(
                "录屏编码器未安装完整，请重新安装录屏模块包。",
                helperPath);
        }

        _started = true;
        var pipeName = $"LightShotCN.ScreenRecording.{Guid.NewGuid():N}";
        _pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        _process = StartHelper(helperPath, pipeName, outputPath);
        try
        {
            using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            connectionTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            await _pipe.WaitForConnectionAsync(connectionTimeout.Token);
            _reader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            _writer = new StreamWriter(_pipe, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true
            };

            var firstMessage = await _reader.ReadLineAsync(cancellationToken);
            if (!string.Equals(firstMessage, "started", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(ParseFailure(firstMessage));
            }

            _ = ObserveHelperAsync();
        }
        catch
        {
            TryTerminateHelper();
            throw;
        }
    }

    public void Pause() => SendCommand("pause");

    public void Resume() => SendCommand("resume");

    public void Stop()
    {
        if (_disposed || !_started || _stopping)
        {
            return;
        }

        _stopping = true;
        SendCommand("stop");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_started && !_completion.Task.IsCompleted)
        {
            try
            {
                SendCommand("cancel");
            }
            catch
            {
                // The helper may already have exited.
            }
        }

        _disposed = true;
        _writer?.Dispose();
        _reader?.Dispose();
        _pipe?.Dispose();
        if (_process is { HasExited: false })
        {
            try
            {
                if (!_process.WaitForExit(750))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                // Process exited between the state check and cleanup.
            }
        }
        _process?.Dispose();
    }

    private Process StartHelper(string helperPath, string pipeName, string outputPath)
    {
        var relativeBounds = _target.DisplayRelativeBounds;
        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            WorkingDirectory = _helperDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        AddArgument(startInfo, "--pipe", pipeName);
        AddArgument(startInfo, "--output", Path.GetFullPath(outputPath));
        AddArgument(startInfo, "--display", _target.DisplayDeviceName);
        AddArgument(startInfo, "--x", relativeBounds.X.ToString());
        AddArgument(startInfo, "--y", relativeBounds.Y.ToString());
        AddArgument(startInfo, "--width", relativeBounds.Width.ToString());
        AddArgument(startInfo, "--height", relativeBounds.Height.ToString());
        AddArgument(startInfo, "--fps", _options.FramesPerSecond.ToString());
        AddArgument(startInfo, "--bitrate", _options.VideoBitrate.ToString());
        AddArgument(startInfo, "--system-audio", _options.CaptureSystemAudio.ToString());
        AddArgument(startInfo, "--microphone", _options.CaptureMicrophone.ToString());
        AddArgument(
            startInfo,
            "--mouse-click-indicator",
            _options.ShowMouseClickIndicator.ToString());
        return Process.Start(startInfo) ??
               throw new InvalidOperationException("无法启动录屏编码器进程。");
    }

    private async Task ObserveHelperAsync()
    {
        try
        {
            while (_reader is not null)
            {
                var message = await _reader.ReadLineAsync();
                if (message is null)
                {
                    break;
                }

                if (message.StartsWith("completed\t", StringComparison.Ordinal))
                {
                    _completion.TrySetResult(new ScreenRecorderResult(
                        Decode(message[10..]),
                        null));
                    return;
                }
                if (message.StartsWith("failed\t", StringComparison.Ordinal))
                {
                    _completion.TrySetResult(new ScreenRecorderResult(
                        null,
                        Decode(message[7..])));
                    return;
                }
            }

            var exitCode = _process is null
                ? -1
                : await WaitForExitAsync(_process);
            _completion.TrySetResult(new ScreenRecorderResult(
                null,
                $"录屏编码器意外退出（代码 {exitCode}）。"));
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            _completion.TrySetResult(new ScreenRecorderResult(null, exception.Message));
        }
    }

    private void SendCommand(string command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_commandLock)
        {
            if (_writer is null)
            {
                return;
            }
            _writer.WriteLine(command);
        }
    }

    private void TryTerminateHelper()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // Process already exited.
        }
    }

    private static void AddArgument(ProcessStartInfo info, string name, string value)
    {
        info.ArgumentList.Add(name);
        info.ArgumentList.Add(value);
    }

    private static string ParseFailure(string? message) =>
        message?.StartsWith("failed\t", StringComparison.Ordinal) == true
            ? Decode(message[7..])
            : "录屏编码器没有成功初始化。";

    private static string Decode(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private static async Task<int> WaitForExitAsync(Process process)
    {
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
