using System.IO.Pipes;
using System.Text;
using ScreenRecorderLib;

namespace ScreenshotTool.ScreenRecording.Recorder;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        RecorderArguments? options = null;
        NamedPipeClientStream? pipe = null;
        StreamWriter? writer = null;
        try
        {
            options = RecorderArguments.Parse(args);
            pipe = new NamedPipeClientStream(
                ".",
                options.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await pipe.ConnectAsync(connectionTimeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true
            };
            using var recorder = CreateRecorder(options);
            var completion = new TaskCompletionSource<RecorderCompletion>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            recorder.OnRecordingComplete += (_, eventArgs) => completion.TrySetResult(
                new RecorderCompletion(eventArgs.FilePath, null));
            recorder.OnRecordingFailed += (_, eventArgs) => completion.TrySetResult(
                new RecorderCompletion(null, eventArgs.Error));

            recorder.Record(options.OutputPath);
            await writer.WriteLineAsync("started");

            var discard = false;
            var stopRequested = false;
            while (!completion.Task.IsCompleted)
            {
                var commandTask = reader.ReadLineAsync();
                var completed = await Task.WhenAny(commandTask, completion.Task);
                if (completed == completion.Task)
                {
                    break;
                }

                var command = await commandTask;
                if (command is null)
                {
                    discard = true;
                    if (!stopRequested)
                    {
                        recorder.Stop();
                        stopRequested = true;
                    }
                    break;
                }

                switch (command)
                {
                    case "pause":
                        recorder.Pause();
                        break;
                    case "resume":
                        recorder.Resume();
                        break;
                    case "stop" when !stopRequested:
                        recorder.Stop();
                        stopRequested = true;
                        break;
                    case "cancel" when !stopRequested:
                        discard = true;
                        recorder.Stop();
                        stopRequested = true;
                        break;
                }
            }

            var result = await completion.Task;
            if (discard)
            {
                TryDelete(result.FilePath ?? options.OutputPath);
                return 0;
            }

            if (result.Error is not null)
            {
                await writer.WriteLineAsync($"failed\t{Encode(result.Error)}");
                return 2;
            }

            await writer.WriteLineAsync($"completed\t{Encode(result.FilePath ?? options.OutputPath)}");
            return 0;
        }
        catch (Exception exception)
        {
            if (writer is not null)
            {
                try
                {
                    await writer.WriteLineAsync($"failed\t{Encode(exception.Message)}");
                }
                catch (IOException)
                {
                    // The owning module is no longer listening.
                }
            }
            if (options is not null)
            {
                TryDelete(options.OutputPath);
            }
            return 1;
        }
        finally
        {
            writer?.Dispose();
            pipe?.Dispose();
        }
    }

    private static global::ScreenRecorderLib.Recorder CreateRecorder(RecorderArguments options)
    {
        var displaySource = new DisplayRecordingSource(options.DisplayDeviceName)
        {
            SourceRect = new ScreenRect(options.X, options.Y, options.Width, options.Height)
        };
        var microphoneEnabled = options.CaptureMicrophone && HasAudioInputDevice();
        var bothAudioSources = options.CaptureSystemAudio && microphoneEnabled;
        var recorderOptions = new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = [displaySource]
            },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = new ScreenSize(options.Width, options.Height),
                Stretch = StretchMode.Fill
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = options.CaptureSystemAudio || microphoneEnabled,
                IsOutputDeviceEnabled = options.CaptureSystemAudio,
                IsInputDeviceEnabled = microphoneEnabled,
                InputVolume = bothAudioSources ? 0.5F : 1F,
                OutputVolume = bothAudioSources ? 0.5F : 1F,
                Bitrate = AudioBitrate.bitrate_128kbps,
                Channels = AudioChannels.Stereo
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Bitrate = options.VideoBitrate,
                Framerate = options.FramesPerSecond,
                IsFixedFramerate = true,
                IsHardwareEncodingEnabled = true,
                IsLowLatencyEnabled = false,
                IsThrottlingDisabled = false,
                IsMp4FastStartEnabled = true,
                Encoder = new H264VideoEncoder
                {
                    BitrateMode = H264BitrateControlMode.UnconstrainedVBR,
                    EncoderProfile = H264Profile.Main
                }
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = true,
                IsMouseClicksDetected = options.ShowMouseClickIndicator,
                MouseClickDetectionMode = MouseDetectionMode.Polling,
                MouseLeftClickDetectionColor = "#FFF59E0B",
                MouseRightClickDetectionColor = "#FF38BDF8",
                MouseClickDetectionRadius = 18,
                MouseClickDetectionDuration = 120
            },
            LogOptions = new LogOptions
            {
                IsLogEnabled = false
            }
        };
        return global::ScreenRecorderLib.Recorder.CreateRecorder(recorderOptions);
    }

    private static bool HasAudioInputDevice()
    {
        try
        {
            return global::ScreenRecorderLib.Recorder.GetSystemAudioDevices(
                AudioDeviceSource.InputDevices).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record RecorderCompletion(string? FilePath, string? Error);
}

internal sealed record RecorderArguments(
    string PipeName,
    string OutputPath,
    string DisplayDeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    int FramesPerSecond,
    int VideoBitrate,
    bool CaptureSystemAudio,
    bool CaptureMicrophone,
    bool ShowMouseClickIndicator)
{
    public static RecorderArguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("录屏编码器参数不完整。");
            }
            values[args[index]] = args[index + 1];
        }

        return new RecorderArguments(
            Required(values, "--pipe"),
            Path.GetFullPath(Required(values, "--output")),
            Required(values, "--display"),
            ParseInt(values, "--x", 0, int.MaxValue),
            ParseInt(values, "--y", 0, int.MaxValue),
            ParseInt(values, "--width", 2, 32768),
            ParseInt(values, "--height", 2, 32768),
            ParseInt(values, "--fps", 1, 120),
            ParseInt(values, "--bitrate", 100_000, 100_000_000),
            bool.Parse(Required(values, "--system-audio")),
            bool.Parse(Required(values, "--microphone")),
            bool.Parse(Required(values, "--mouse-click-indicator")));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"缺少参数 {key}。");

    private static int ParseInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(Required(values, key), out var value) ||
            value < minimum ||
            value > maximum)
        {
            throw new ArgumentOutOfRangeException(key);
        }
        return value;
    }
}
