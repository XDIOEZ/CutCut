using System.Reflection;
using System.Diagnostics;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleHost : IModuleManager
{
    private readonly Dictionary<string, LoadedModuleAssembly> _packages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackageStamp> _failedPackages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackageStamp> _nonModulePackages =
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
        var packages = Directory
            .EnumerateDirectories(ModulesDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .ToDictionary(
                path => path,
                PackageStamp.FromDirectory,
                StringComparer.OrdinalIgnoreCase);

        foreach (var current in _packages.ToArray())
        {
            if (!packages.TryGetValue(current.Key, out var stamp) ||
                force ||
                current.Value.Stamp != stamp)
            {
                _packages.Remove(current.Key);
                current.Value.Retire();
                changed = true;
            }
        }

        if (changed)
        {
            // A duplicate-ID or dependency failure may become valid after another module is removed.
            _failedPackages.Clear();
        }

        foreach (var failed in _failedPackages.Keys
                     .Except(packages.Keys, StringComparer.OrdinalIgnoreCase)
                     .ToArray())
        {
            _failedPackages.Remove(failed);
        }
        foreach (var ignored in _nonModulePackages.Keys
                     .Except(packages.Keys, StringComparer.OrdinalIgnoreCase)
                     .ToArray())
        {
            _nonModulePackages.Remove(ignored);
        }

        foreach (var package in packages)
        {
            if (_packages.ContainsKey(package.Key))
            {
                continue;
            }
            if (!force &&
                _failedPackages.TryGetValue(package.Key, out var failedStamp) &&
                failedStamp == package.Value)
            {
                continue;
            }
            if (!force &&
                _nonModulePackages.TryGetValue(package.Key, out var ignoredStamp) &&
                ignoredStamp == package.Value)
            {
                continue;
            }

            try
            {
                var loaded = LoadedModuleAssembly.LoadPackage(package.Key, package.Value);
                if (loaded is null)
                {
                    _nonModulePackages[package.Key] = package.Value;
                    continue;
                }
                var existingIds = _packages.Values
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
                _packages.Add(package.Key, loaded);
                _failedPackages.Remove(package.Key);
                _nonModulePackages.Remove(package.Key);
                changed = true;
            }
            catch (Exception exception)
            {
                _failedPackages[package.Key] = package.Value;
                errors.Add($"{Path.GetFileName(package.Key)}：{GetLoadError(exception)}");
                changed = true;
            }
        }

        return new ModuleRefreshResult(GetModules(), errors, changed);
    }

    public IReadOnlyList<ModuleInfo> GetModules() => _packages.Values
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
        return _packages.Values
            .SelectMany(assembly => assembly.CreateFeatureLeases())
            .OrderBy(feature => feature.Order)
            .ThenBy(feature => feature.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(host);
        return _packages.Values
            .SelectMany(assembly => assembly.CreateSettingsPageLeases(host))
            .ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var assembly in _packages.Values)
        {
            assembly.Retire();
        }
        _packages.Clear();
        _failedPackages.Clear();
        _nonModulePackages.Clear();
    }

    private static string GetLoadError(Exception exception)
    {
        if (exception is ReflectionTypeLoadException reflectionException)
        {
            return reflectionException.LoaderExceptions.FirstOrDefault()?.Message ?? reflectionException.Message;
        }
        return exception.InnerException?.Message ?? exception.Message;
    }

    private readonly record struct PackageStamp(string Fingerprint)
    {
        public static PackageStamp FromDirectory(string path)
        {
            var fingerprint = string.Join(
                '\n',
                Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Select(file => new FileInfo(file))
                    .OrderBy(
                        file => Path.GetRelativePath(path, file.FullName),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(file =>
                        $"{Path.GetRelativePath(path, file.FullName)}|" +
                        $"{file.Length}|{file.LastWriteTimeUtc.Ticks}"));
            return new PackageStamp(fingerprint);
        }
    }

    private sealed class LoadedModuleAssembly
    {
        private readonly ModuleLoadContext _loadContext;
        private int _activeLeases;
        private bool _retired;
        private bool _unloaded;

        private LoadedModuleAssembly(
            string assemblyPath,
            PackageStamp stamp,
            ModuleLoadContext loadContext,
            IReadOnlyList<IScreenshotToolModule> modules)
        {
            AssemblyPath = assemblyPath;
            Stamp = stamp;
            _loadContext = loadContext;
            Modules = modules;
        }

        public string AssemblyPath { get; }
        public PackageStamp Stamp { get; }
        public IReadOnlyList<IScreenshotToolModule> Modules { get; }

        public static LoadedModuleAssembly? LoadPackage(
            string packageDirectory,
            PackageStamp stamp)
        {
            LoadedModuleAssembly? package = null;
            var loadErrors = new List<Exception>();
            foreach (var assemblyPath in Directory
                         .EnumerateFiles(packageDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                LoadedModuleAssembly candidate;
                try
                {
                    candidate = LoadAssembly(assemblyPath, stamp);
                }
                catch (BadImageFormatException)
                {
                    // Native DLLs may live beside the module entry assembly.
                    continue;
                }
                catch (Exception exception)
                {
                    loadErrors.Add(exception);
                    continue;
                }

                if (candidate.Modules.Count == 0)
                {
                    candidate.Retire();
                    continue;
                }
                if (candidate.Modules.Count > 1 || package is not null)
                {
                    candidate.Retire();
                    package?.Retire();
                    throw new InvalidDataException(
                        $"每个模块文件夹只能包含一个模块：{packageDirectory}");
                }
                package = candidate;
            }

            if (package is not null)
            {
                return package;
            }
            if (loadErrors.Count > 0)
            {
                throw new InvalidDataException(
                    $"模块文件夹中没有可加载的入口程序集：{packageDirectory}",
                    loadErrors[0]);
            }
            return null;
        }

        private static LoadedModuleAssembly LoadAssembly(
            string assemblyPath,
            PackageStamp stamp)
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
                    _activeLeases++;
                    yield return new CaptureFeatureLease(feature, ReleaseLease);
                }
            }
        }

        public IEnumerable<IModuleSettingsPage> CreateSettingsPageLeases(
            IModuleSettingsHost host)
        {
            foreach (var module in Modules.OfType<IModuleSettingsPageProvider>())
            {
                IModuleSettingsPage[] pages;
                try
                {
                    pages = module.CreateSettingsPages(host).ToArray();
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"模块设置页创建失败：{exception}");
                    continue;
                }

                foreach (var page in pages)
                {
                    _activeLeases++;
                    yield return new ModuleSettingsPageLease(page, ReleaseLease);
                }
            }
        }

        public void Retire()
        {
            _retired = true;
            TryUnload();
        }

        private void ReleaseLease()
        {
            _activeLeases = Math.Max(0, _activeLeases - 1);
            TryUnload();
        }

        private void TryUnload()
        {
            if (!_retired || _activeLeases != 0 || _unloaded)
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

    private sealed class ModuleSettingsPageLease(
        IModuleSettingsPage inner,
        Action release) : IModuleSettingsPage
    {
        private bool _disposed;

        public string Id => inner.Id;
        public string Title => inner.Title;
        public string Description => inner.Description;
        public int Order => inner.Order;
        public Control Content => inner.Content;

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
