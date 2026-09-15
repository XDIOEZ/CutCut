using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Ocr;

internal sealed class WindowsOcrRecognizer : IOcrRecognizer
{
    private const string WorkerResourceName = "ScreenshotTool.Ocr.WindowsOcrWorker.ps1";
    private static readonly Lazy<string> WorkerScript = new(ReadWorkerScript);

    // Preserves the ordinary OCR result window's normalized text contract.
    public async Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken) =>
        OcrTextNormalizer.Normalize((await RecognizeCandidateAsync(image, cancellationToken)).Text);

    // Converts word geometry into source-pixel regions without creating a result window.
    public async Task<ImageTextRecognitionResult> RecognizeImageTextAsync(
        Bitmap image, CancellationToken cancellationToken)
    {
        var result = await RecognizeCandidateAsync(image, cancellationToken);
        return new ImageTextRecognitionResult((result.Words ?? []).Select(word => new ImageTextRegion(
            word.Text, word.LineIndex,
            new PointF(word.X, word.Y), new PointF(word.X + word.Width, word.Y),
            new PointF(word.X + word.Width, word.Y + word.Height),
            new PointF(word.X, word.Y + word.Height), word.LeadingText)).ToArray());
    }

    // Shares preprocessing and candidate ranking between ordinary OCR and spatial OCR.
    private async Task<OcrWorkerResult> RecognizeCandidateAsync(
        Bitmap image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        var tempDirectory = Path.Combine(Path.GetTempPath(), "LightShotCN", "Ocr");
        Directory.CreateDirectory(tempDirectory);
        var candidates = await Task.Run(() => OcrImagePreprocessor.CreateCandidates(image), cancellationToken);
        var imagePaths = candidates
            .Select(candidate => Path.Combine(
                tempDirectory,
                $"{Guid.NewGuid():N}-{candidate.Name}.png"))
            .ToArray();

        try
        {
            await Task.Run(
                () =>
                {
                    for (var index = 0; index < candidates.Count; index++)
                    {
                        candidates[index].Image.Save(imagePaths[index], ImageFormat.Png);
                    }
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var results = await RunWorkerAsync(imagePaths, cancellationToken);
            var best = OcrCandidateSelector.SelectBestResult(results);
            if (best is null)
            {
                return new OcrWorkerResult(string.Empty, string.Empty, 0, 0, []);
            }
            var candidateIndex = Array.FindIndex(imagePaths,
                path => Path.GetFileNameWithoutExtension(path) == best.Name);
            var candidate = candidates[candidateIndex];
            var padding = candidate.Padding;
            var scaleX = image.Width / (float)(candidate.Image.Width - padding * 2);
            var scaleY = image.Height / (float)(candidate.Image.Height - padding * 2);
            return best with
            {
                Words = (best.Words ?? []).Select(word => word with
                {
                    X = (word.X * candidate.Image.Width - padding) * scaleX,
                    Y = (word.Y * candidate.Image.Height - padding) * scaleY,
                    Width = word.Width * candidate.Image.Width * scaleX,
                    Height = word.Height * candidate.Image.Height * scaleY
                }).ToArray()
            };
        }
        finally
        {
            foreach (var candidate in candidates)
            {
                candidate.Dispose();
            }
            foreach (var imagePath in imagePaths)
            {
                TryDelete(imagePath);
            }
        }
    }

    private static async Task<IReadOnlyList<OcrWorkerResult>> RunWorkerAsync(
        IReadOnlyList<string> imagePaths,
        CancellationToken cancellationToken)
    {
        var powerShellPath = GetWindowsPowerShellPath();
        if (!File.Exists(powerShellPath))
        {
            throw new InvalidOperationException(
                "找不到 Windows PowerShell，无法调用系统离线 OCR 服务。");
        }

        var encodedWorker = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(WorkerScript.Value));
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedWorker}",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        var inputJson = JsonSerializer.Serialize(imagePaths);
        startInfo.Environment["LIGHTSHOT_OCR_INPUTS"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(inputJson));

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动 Windows OCR 辅助进程。");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                TryDecodeWorkerMessage(output, "ERROR:", out var workerError)
                    ? workerError
                    : string.IsNullOrWhiteSpace(error)
                        ? "Windows 离线 OCR 执行失败。"
                        : error);
        }

        if (!output.StartsWith("OK:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Windows OCR 返回了无法读取的结果。");
        }

        if (!TryDecodeWorkerMessage(output, "OK:", out var resultJson))
        {
            throw new InvalidOperationException("Windows OCR 返回了无法读取的结果。");
        }

        try
        {
            return JsonSerializer.Deserialize<OcrWorkerResult[]>(resultJson) ??
                   throw new JsonException("OCR 结果为空。");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Windows OCR 返回了无法读取的结果。", exception);
        }
    }

    private static bool TryDecodeWorkerMessage(
        string output,
        string prefix,
        out string message)
    {
        message = string.Empty;
        if (!output.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            message = Encoding.UTF8.GetString(
                Convert.FromBase64String(output[prefix.Length..]));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GetWindowsPowerShellPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private static string ReadWorkerScript()
    {
        using var stream = typeof(WindowsOcrRecognizer).Assembly.GetManifestResourceStream(
            WorkerResourceName) ?? throw new InvalidOperationException("OCR 辅助脚本资源缺失。");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The worker exited between the state check and termination.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Cancellation remains the primary outcome when Windows denies termination.
        }
    }

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
            // Temporary OCR input can be reclaimed by the operating system later.
        }
        catch (UnauthorizedAccessException)
        {
            // Recognition already completed; cleanup failure must not hide the result.
        }
    }
}
