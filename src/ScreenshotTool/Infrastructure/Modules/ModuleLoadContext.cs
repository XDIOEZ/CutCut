using System.Reflection;
using System.Runtime.Loader;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    private static readonly string ContractsAssemblyName =
        typeof(IScreenshotToolModule).Assembly.GetName().Name!;
    private readonly string _moduleDirectory;

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

        var dependencyPath = Path.Combine(_moduleDirectory, $"{assemblyName.Name}.dll");
        if (!File.Exists(dependencyPath))
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
        var dependencyPath = Path.Combine(_moduleDirectory, fileName);
        return File.Exists(dependencyPath)
            ? LoadUnmanagedDllFromPath(dependencyPath)
            : nint.Zero;
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

    private static FileStream OpenReadShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
