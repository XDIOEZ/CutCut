using System.Reflection;
using System.Runtime.Loader;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    private static readonly string ContractsAssemblyName =
        typeof(IScreenshotToolModule).Assembly.GetName().Name!;
    private readonly string _moduleDirectory;
    private readonly object _nativeLoadLock = new();
    private readonly Dictionary<string, nint> _nativeHandles =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _nativeShadowDirectory;

    public ModuleLoadContext(string moduleDirectory)
        : base(isCollectible: true)
    {
        _moduleDirectory = moduleDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, ContractsAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dependencyPath = ResolveManagedDependencyPath($"{assemblyName.Name}.dll");
        if (dependencyPath is null)
        {
            return null;
        }

        using var stream = OpenReadShared(dependencyPath);
        return LoadFromStream(stream);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var fileName = unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? unmanagedDllName
            : $"{unmanagedDllName}.dll";

        lock (_nativeLoadLock)
        {
            if (_nativeHandles.TryGetValue(fileName, out var loaded))
            {
                return loaded;
            }

            var shadowPath = PrepareDependencyShadow(fileName);
            if (shadowPath is null)
            {
                return nint.Zero;
            }
            var handle = LoadUnmanagedDllFromPath(shadowPath);
            _nativeHandles[fileName] = handle;
            return handle;
        }
    }

    public Assembly LoadModule(string assemblyPath)
    {
        using var assemblyStream = OpenReadShared(assemblyPath);
        var symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (!File.Exists(symbolsPath))
        {
            return LoadFromStream(assemblyStream);
        }

        using var symbolsStream = OpenReadShared(symbolsPath);
        return LoadFromStream(assemblyStream, symbolsStream);
    }

    public void PrepareForActiveLeases()
    {
        lock (_nativeLoadLock)
        {
            PrepareDependencyShadow();
        }
    }

    public void PrepareForUnload()
    {
        lock (_nativeLoadLock)
        {
            _nativeHandles.Clear();
            TryDeleteDirectory(_nativeShadowDirectory);
        }
    }

    private string? ResolveManagedDependencyPath(string fileName)
    {
        var modulePath = Path.Combine(_moduleDirectory, fileName);
        if (File.Exists(modulePath))
        {
            return modulePath;
        }

        lock (_nativeLoadLock)
        {
            if (_nativeShadowDirectory is null)
            {
                return null;
            }

            var shadowPath = Path.Combine(_nativeShadowDirectory, fileName);
            return File.Exists(shadowPath) ? shadowPath : null;
        }
    }

    private string? PrepareDependencyShadow(string? requestedFileName = null)
    {
        if (_nativeShadowDirectory is null)
        {
            var shadowRoot = Path.Combine(
                Path.GetTempPath(),
                "LightShotCN",
                "ModuleNative");
            Directory.CreateDirectory(shadowRoot);
            CleanupStaleNativeShadows(shadowRoot);
            _nativeShadowDirectory = Path.Combine(
                shadowRoot,
                $"{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_nativeShadowDirectory);

            foreach (var sourcePath in Directory.EnumerateFiles(
                         _moduleDirectory,
                         "*.dll",
                         SearchOption.TopDirectoryOnly))
            {
                var destinationPath = Path.Combine(
                    _nativeShadowDirectory,
                    Path.GetFileName(sourcePath));
                using var source = OpenReadShared(sourcePath);
                using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read | FileShare.Delete);
                source.CopyTo(destination);
            }
        }

        if (requestedFileName is null)
        {
            return _nativeShadowDirectory;
        }

        var shadowPath = Path.Combine(_nativeShadowDirectory, requestedFileName);
        return File.Exists(shadowPath) ? shadowPath : null;
    }

    private static void CleanupStaleNativeShadows(string shadowRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(shadowRoot))
        {
            var directoryName = Path.GetFileName(directory);
            if (IsOwnerProcessRunning(directoryName))
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

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A live collectible context can still own the shadow copy.
        }
        catch (UnauthorizedAccessException)
        {
            // Stale native shadows are best-effort temporary cleanup.
        }
    }

    private static FileStream OpenReadShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
