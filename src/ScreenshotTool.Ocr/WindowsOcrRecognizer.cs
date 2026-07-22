using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;

namespace ScreenshotTool.Ocr;

internal sealed class WindowsOcrRecognizer : IOcrRecognizer
{
    private const string WorkerResourceName = "ScreenshotTool.Ocr.WindowsOcrWorker.ps1";
    private static readonly Lazy<string> WorkerScript = new(ReadWorkerScript);

    public async Task<string> RecognizeAsync(
        Bitmap image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        var tempDirectory = Path.Combine(Path.GetTempPath(), "LightShotCN", "Ocr");
        Directory.CreateDirectory(tempDirectory);
        var imagePath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.png");

        try
        {
            await Task.Run(
                () => image.Save(imagePath, ImageFormat.Png),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await RunWorkerAsync(imagePath, cancellationToken);
        }
        finally
        {
            TryDelete(imagePath);
        }
    }

    private static async Task<string> RunWorkerAsync(
        string imagePath,
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
        startInfo.Environment["LIGHTSHOT_OCR_INPUT"] = imagePath;

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

        if (!TryDecodeWorkerMessage(output, "OK:", out var result))
        {
            throw new InvalidOperationException("Windows OCR 返回了无法读取的结果。");
        }

        return OcrTextNormalizer.Normalize(result);
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
