namespace ScreenshotTool.PaddleOcr;

internal sealed class PaddleOcrModelWorkspace : IDisposable
{
    private readonly string _workspaceDirectory;
    private bool _disposed;

    private PaddleOcrModelWorkspace(string workspaceDirectory)
    {
        _workspaceDirectory = workspaceDirectory;
    }

    public string ModuleDirectory => _workspaceDirectory;

    public static PaddleOcrModelWorkspace Create(
        string sourceModuleDirectory,
        PaddleOcrVariant variant)
    {
        var sourceFiles = PaddleOcrModelFiles.Resolve(sourceModuleDirectory, variant);
        var missingFiles = sourceFiles.GetMissingFiles();
        if (missingFiles.Count > 0)
        {
            throw new InvalidOperationException(
                "PP-OCR 模块文件不完整，缺少：" +
                string.Join("、", missingFiles.Select(Path.GetFileName)) +
                "。请重新下载并完整解压对应模块。");
        }

        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "LightShotCN",
            "PaddleOcrModels");
        Directory.CreateDirectory(workspaceRoot);
        CleanupStaleWorkspaces(workspaceRoot);

        var workspaceDirectory = Path.Combine(
            workspaceRoot,
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        var targetModelDirectory = Path.Combine(workspaceDirectory, "Models");
        Directory.CreateDirectory(targetModelDirectory);

        try
        {
            foreach (var sourcePath in new[]
                     {
                         sourceFiles.DetectorPath,
                         sourceFiles.ClassifierPath,
                         sourceFiles.RecognizerPath,
                         sourceFiles.DictionaryPath
                     })
            {
                CopyShared(
                    sourcePath,
                    Path.Combine(targetModelDirectory, Path.GetFileName(sourcePath)));
            }

            return new PaddleOcrModelWorkspace(workspaceDirectory);
        }
        catch
        {
            TryDeleteDirectory(workspaceDirectory);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TryDeleteDirectory(_workspaceDirectory);
    }

    private static void CopyShared(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read | FileShare.Delete);
        source.CopyTo(destination);
    }

    private static void CleanupStaleWorkspaces(string workspaceRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(workspaceRoot))
        {
            if (IsOwnerProcessRunning(Path.GetFileName(directory)))
            {
                continue;
            }

            TryDeleteDirectory(directory);
        }
    }

    private static bool IsOwnerProcessRunning(string directoryName)
    {
        var separatorIndex = directoryName.IndexOf('-');
        if (separatorIndex <= 0 ||
            !int.TryParse(directoryName[..separatorIndex], out var processId))
        {
            return false;
        }
        if (processId == Environment.ProcessId)
        {
            return true;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // An active ONNX session can retain the snapshot until process exit.
        }
        catch (UnauthorizedAccessException)
        {
            // Stale snapshot cleanup is best effort.
        }
    }
}
