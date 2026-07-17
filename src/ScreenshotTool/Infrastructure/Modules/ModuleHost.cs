using System.Reflection;
using System.Diagnostics;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleHost : IModuleManager
{
    private readonly Dictionary<string, LoadedModuleAssembly> _assemblies =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileStamp> _failedFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileStamp> _nonModuleFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ModuleHost(string modulesDirectory)
    {
        ModulesDirectory = Path.GetFullPath(modulesDirectory);
    }

    public string ModulesDirectory { get; }

    public ModuleRefreshResult Refresh(bool force = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(ModulesDirectory);

        var errors = new List<string>();
        var changed = false;
        var files = Directory.EnumerateFiles(ModulesDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .ToDictionary(path => path, FileStamp.FromFile, StringComparer.OrdinalIgnoreCase);

        foreach (var current in _assemblies.ToArray())
        {
            if (!files.TryGetValue(current.Key, out var stamp) || force || current.Value.Stamp != stamp)
            {
                _assemblies.Remove(current.Key);
                current.Value.Retire();
                changed = true;
            }
        }

        if (changed)
        {
            // A duplicate-ID or dependency failure may become valid after another module is removed.
            _failedFiles.Clear();
        }

        foreach (var failed in _failedFiles.Keys.Except(files.Keys, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            _failedFiles.Remove(failed);
        }
        foreach (var ignored in _nonModuleFiles.Keys.Except(files.Keys, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            _nonModuleFiles.Remove(ignored);
        }

        foreach (var file in files)
        {
            if (_assemblies.ContainsKey(file.Key))
            {
                continue;
            }
            if (!force && _failedFiles.TryGetValue(file.Key, out var failedStamp) && failedStamp == file.Value)
            {
                continue;
            }
            if (!force && _nonModuleFiles.TryGetValue(file.Key, out var ignoredStamp) && ignoredStamp == file.Value)
            {
                continue;
            }

            try
            {
                var loaded = LoadedModuleAssembly.Load(file.Key, file.Value);
                if (loaded.Modules.Count == 0)
                {
                    loaded.Retire();
                    _nonModuleFiles[file.Key] = file.Value;
                    continue;
                }
                var existingIds = _assemblies.Values
                    .SelectMany(assembly => assembly.Modules)
                    .Select(module => module.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var duplicateId = loaded.Modules
                    .GroupBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(group => group.Count() > 1 || existingIds.Contains(group.Key))
                    ?.Key;
                if (duplicateId is not null)
                {
                    loaded.Retire();
                    throw new InvalidDataException($"模块 ID 重复：{duplicateId}");
                }
                _assemblies.Add(file.Key, loaded);
                _failedFiles.Remove(file.Key);
                _nonModuleFiles.Remove(file.Key);
                changed = true;
            }
            catch (Exception exception)
            {
                _failedFiles[file.Key] = file.Value;
                errors.Add($"{Path.GetFileName(file.Key)}：{GetLoadError(exception)}");
                changed = true;
            }
        }

        return new ModuleRefreshResult(GetModules(), errors, changed);
    }

    public IReadOnlyList<ModuleInfo> GetModules() => _assemblies.Values
        .SelectMany(assembly => assembly.Modules.Select(module => new ModuleInfo(
            module.Id,
            module.DisplayName,
            module.Version,
            assembly.AssemblyPath)))
        .OrderBy(module => module.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public IReadOnlyList<ICaptureFeature> CreateCaptureFeatures()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _assemblies.Values
            .SelectMany(assembly => assembly.CreateFeatureLeases())
            .OrderBy(feature => feature.Order)
            .ThenBy(feature => feature.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var assembly in _assemblies.Values)
        {
            assembly.Retire();
        }
        _assemblies.Clear();
        _failedFiles.Clear();
        _nonModuleFiles.Clear();
    }

    private static string GetLoadError(Exception exception)
    {
        if (exception is ReflectionTypeLoadException reflectionException)
        {
            return reflectionException.LoaderExceptions.FirstOrDefault()?.Message ?? reflectionException.Message;
        }
        return exception.InnerException?.Message ?? exception.Message;
    }

    private readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc)
    {
        public static FileStamp FromFile(string path)
        {
            var info = new FileInfo(path);
            return new FileStamp(info.Length, info.LastWriteTimeUtc);
        }
    }

    private sealed class LoadedModuleAssembly
    {
        private readonly ModuleLoadContext _loadContext;
        private int _activeFeatures;
        private bool _retired;
        private bool _unloaded;

        private LoadedModuleAssembly(
            string assemblyPath,
            FileStamp stamp,
            ModuleLoadContext loadContext,
            IReadOnlyList<IScreenshotToolModule> modules)
        {
            AssemblyPath = assemblyPath;
            Stamp = stamp;
            _loadContext = loadContext;
            Modules = modules;
        }

        public string AssemblyPath { get; }
        public FileStamp Stamp { get; }
        public IReadOnlyList<IScreenshotToolModule> Modules { get; }

        public static LoadedModuleAssembly Load(string assemblyPath, FileStamp stamp)
        {
            var loadContext = new ModuleLoadContext(Path.GetDirectoryName(assemblyPath)!);
            var modules = new List<IScreenshotToolModule>();
            try
            {
                var assembly = loadContext.LoadModule(assemblyPath);
                var moduleTypes = assembly.GetTypes()
                    .Where(type => !type.IsAbstract &&
                                   !type.IsInterface &&
                                   typeof(IScreenshotToolModule).IsAssignableFrom(type));
                foreach (var moduleType in moduleTypes)
                {
                    if (Activator.CreateInstance(moduleType) is not IScreenshotToolModule module)
                    {
                        continue;
                    }
                    modules.Add(module);
                    module.Initialize(new ModuleContext(Path.GetDirectoryName(assemblyPath)!));
                }

                return new LoadedModuleAssembly(assemblyPath, stamp, loadContext, modules);
            }
            catch
            {
                foreach (var module in modules)
                {
                    try
                    {
                        module.Dispose();
                    }
                    catch (Exception disposeException)
                    {
                        Debug.WriteLine($"模块初始化失败后的释放也失败：{disposeException}");
                    }
                }
                loadContext.Unload();
                throw;
            }
        }

        public IEnumerable<ICaptureFeature> CreateFeatureLeases()
        {
            foreach (var module in Modules)
            {
                ICaptureFeature[] features;
                try
                {
                    features = module.CreateCaptureFeatures().ToArray();
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"模块 {module.Id} 创建截图功能失败：{exception}");
                    continue;
                }
                foreach (var feature in features)
                {
                    _activeFeatures++;
                    yield return new CaptureFeatureLease(feature, ReleaseFeature);
                }
            }
        }

        public void Retire()
        {
            _retired = true;
            TryUnload();
        }

        private void ReleaseFeature()
        {
            _activeFeatures = Math.Max(0, _activeFeatures - 1);
            TryUnload();
        }

        private void TryUnload()
        {
            if (!_retired || _activeFeatures != 0 || _unloaded)
            {
                return;
            }

            _unloaded = true;
            foreach (var module in Modules)
            {
                try
                {
                    module.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"模块 {module.Id} 释放失败：{exception}");
                }
            }
            _loadContext.Unload();
        }
    }

    private sealed class ModuleContext(string moduleDirectory) : IModuleContext
    {
        public string ModuleDirectory { get; } = moduleDirectory;
        public Version HostVersion { get; } = typeof(ModuleHost).Assembly.GetName().Version ?? new Version(1, 0);
    }

    private sealed class CaptureFeatureLease(ICaptureFeature inner, Action release) :
        ICaptureFeature,
        ICaptureToolbarCommandProvider
    {
        private bool _disposed;

        public string Id => inner.Id;
        public int Order => inner.Order;
        public void Attach(ICaptureFeatureHost host) => inner.Attach(host);
        public bool HandleKeyDown(KeyEventArgs e) => inner.HandleKeyDown(e);
        public bool HandleMouseDown(MouseEventArgs e) => inner.HandleMouseDown(e);
        public bool HandleMouseMove(MouseEventArgs e) => inner.HandleMouseMove(e);
        public bool HandleMouseUp(MouseEventArgs e) => inner.HandleMouseUp(e);
        public void Render(Graphics graphics, CaptureRenderTarget target) => inner.Render(graphics, target);

        public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() =>
            inner is ICaptureToolbarCommandProvider provider
                ? provider.GetToolbarCommands()
                : [];

        public Task ExecuteToolbarCommandAsync(
            string commandId,
            CancellationToken cancellationToken) =>
            inner is ICaptureToolbarCommandProvider provider
                ? provider.ExecuteToolbarCommandAsync(commandId, cancellationToken)
                : Task.CompletedTask;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                inner.Dispose();
            }
            finally
            {
                release();
            }
        }
    }
}
