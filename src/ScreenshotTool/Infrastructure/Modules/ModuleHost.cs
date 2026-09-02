using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleHost : IModuleManager
{
    private const string DisabledMarkerFileName = ".lightshot-module-disabled.json";
    private static readonly JsonSerializerOptions MarkerJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Dictionary<string, LoadedModuleAssembly> _packages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackageStamp> _failedPackages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackageStamp> _nonModulePackages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModulePackageInfo> _disabledPackageInfo =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _fileChangesLock = new();
    private readonly HashSet<string> _changedPackageDirectories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IModuleImageHost _imageHost;
    private readonly IModuleActivationPreferenceStore _activationPreferences;
    private readonly FileSystemWatcher _moduleWatcher;
    private bool _rescanAllPackages;
    private string? _packageDirectoryState;
    private bool _disposed;

    public ModuleHost(
        string modulesDirectory,
        IModuleImageHost? imageHost = null,
        IModuleActivationPreferenceStore? activationPreferences = null)
    {
        ModulesDirectory = Path.GetFullPath(modulesDirectory);
        _imageHost = imageHost ?? UnavailableModuleImageHost.Instance;
        _activationPreferences =
            activationPreferences ?? new TransientModuleActivationPreferenceStore();
        Directory.CreateDirectory(ModulesDirectory);
        _moduleWatcher = CreateModuleWatcher();
    }

    public string ModulesDirectory { get; }

    public ModuleRefreshResult Refresh(bool force = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(ModulesDirectory);

        var errors = new List<string>();
        var changed = false;
        var changedPackageDirectories = ConsumeChangedPackageDirectories(
            out var rescanAllPackages);
        var reloadAllPackages = force || rescanAllPackages;
        var packageDirectories = Directory
            .EnumerateDirectories(ModulesDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .ToArray();
        MigrateLegacyDisabledMarkers(packageDirectories, errors);
        var packageDirectoryState = string.Join(
            '\n',
            packageDirectories
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => $"{path}|{IsPackageEnabled(path)}"));
        if (!string.Equals(
                _packageDirectoryState,
                packageDirectoryState,
                StringComparison.Ordinal))
        {
            _packageDirectoryState = packageDirectoryState;
            changed = true;
        }
        var packages = packageDirectories
            .Where(IsPackageEnabled)
            .ToDictionary(
                path => path,
                PackageStamp.FromDirectory,
                StringComparer.OrdinalIgnoreCase);

        foreach (var current in _packages.ToArray())
        {
            if (!packages.TryGetValue(current.Key, out var stamp) ||
                reloadAllPackages ||
                changedPackageDirectories.Contains(current.Key) ||
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
        foreach (var disabled in _disabledPackageInfo.Keys
                     .Except(packageDirectories, StringComparer.OrdinalIgnoreCase)
                     .ToArray())
        {
            _disabledPackageInfo.Remove(disabled);
        }

        foreach (var package in packages)
        {
            if (_packages.ContainsKey(package.Key))
            {
                continue;
            }
            if (!reloadAllPackages &&
                !changedPackageDirectories.Contains(package.Key) &&
                _failedPackages.TryGetValue(package.Key, out var failedStamp) &&
                failedStamp == package.Value)
            {
                continue;
            }
            if (!reloadAllPackages &&
                !changedPackageDirectories.Contains(package.Key) &&
                _nonModulePackages.TryGetValue(package.Key, out var ignoredStamp) &&
                ignoredStamp == package.Value)
            {
                continue;
            }

            try
            {
                var loaded = LoadedModuleAssembly.LoadPackage(
                    package.Key,
                    package.Value,
                    _imageHost);
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

    public IReadOnlyList<ModulePackageInfo> GetInstalledPackages()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Directory.CreateDirectory(ModulesDirectory);

        return Directory
            .EnumerateDirectories(ModulesDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .Select(GetInstalledPackage)
            .Where(package => package is not null)
            .Cast<ModulePackageInfo>()
            .OrderBy(package => package.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(package => package.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ModuleOperationResult SetPackageEnabled(string packageName, bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!TryResolvePackageDirectory(packageName, out var packageDirectory, out var error))
        {
            return new ModuleOperationResult(false, error);
        }
        if (!Directory.Exists(packageDirectory))
        {
            return new ModuleOperationResult(false, $"模块包不存在：{packageName}");
        }

        try
        {
            var currentPackage = GetInstalledPackage(packageDirectory);
            if (!enabled && currentPackage is not null)
            {
                _disabledPackageInfo[packageDirectory] = currentPackage with
                {
                    State = ModulePackageState.Disabled,
                    ErrorMessage = null
                };
            }

            _activationPreferences.TryGet(packageName, out var previousPreference);
            _activationPreferences.Set(
                packageName,
                new ModuleActivationPreference(
                    enabled,
                    currentPackage?.ModuleId ?? previousPreference?.ModuleId,
                    currentPackage?.DisplayName ?? previousPreference?.DisplayName,
                    currentPackage?.Version?.ToString() ?? previousPreference?.Version));
            if (enabled)
            {
                _failedPackages.Remove(packageDirectory);
                _nonModulePackages.Remove(packageDirectory);
            }

            var refresh = Refresh();
            var current = GetInstalledPackages().FirstOrDefault(package =>
                string.Equals(package.PackageName, packageName, StringComparison.OrdinalIgnoreCase));
            var expectedState = enabled
                ? ModulePackageState.Enabled
                : ModulePackageState.Disabled;
            if (current?.State != expectedState)
            {
                var failure = current?.ErrorMessage ?? refresh.Errors.FirstOrDefault();
                return new ModuleOperationResult(
                    false,
                    enabled
                        ? $"模块未能启用：{failure ?? "没有找到可加载的模块入口"}"
                        : $"模块未能禁用：{failure ?? "状态更新失败"}",
                    refresh);
            }

            return new ModuleOperationResult(
                true,
                enabled ? $"已启用“{current.DisplayName}”" : $"已禁用“{current.DisplayName}”",
                refresh);
        }
        catch (Exception exception) when (IsPackageOperationException(exception))
        {
            return new ModuleOperationResult(
                false,
                $"{(enabled ? "启用" : "禁用")}模块失败：{exception.Message}");
        }
    }

    public ModuleOperationResult DeletePackage(string packageName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!TryResolvePackageDirectory(packageName, out var packageDirectory, out var error))
        {
            return new ModuleOperationResult(false, error);
        }
        if (!Directory.Exists(packageDirectory))
        {
            return new ModuleOperationResult(false, $"模块包不存在：{packageName}");
        }

        try
        {
            var currentPackage = GetInstalledPackage(packageDirectory);
            var displayName = currentPackage?.DisplayName ?? packageName;
            _activationPreferences.TryGet(packageName, out var previousPreference);
            _activationPreferences.Set(
                packageName,
                new ModuleActivationPreference(
                    Enabled: false,
                    currentPackage?.ModuleId ?? previousPreference?.ModuleId,
                    currentPackage?.DisplayName ?? previousPreference?.DisplayName,
                    currentPackage?.Version?.ToString() ?? previousPreference?.Version));
            Refresh();
            Directory.Delete(packageDirectory, recursive: true);
            _activationPreferences.Remove(packageName);
            _disabledPackageInfo.Remove(packageDirectory);
            var refresh = Refresh();
            return new ModuleOperationResult(true, $"已永久删除“{displayName}”", refresh);
        }
        catch (Exception exception) when (IsPackageOperationException(exception))
        {
            ModuleRefreshResult? refresh = null;
            try
            {
                refresh = Refresh();
            }
            catch (Exception refreshException) when (IsPackageOperationException(refreshException))
            {
                Debug.WriteLine($"模块删除失败后的刷新也失败：{refreshException}");
            }

            return new ModuleOperationResult(
                false,
                $"永久删除模块失败：{exception.Message}。模块已保持禁用，可关闭占用它的程序后重试。",
                refresh);
        }
    }

    public IReadOnlyList<ICaptureFeature> CreateCaptureFeatures()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _packages.Values
            .SelectMany(assembly => assembly.CreateFeatureLeases())
            .OrderBy(feature => feature.Order)
            .ThenBy(feature => feature.Id, StringComparer.Ordinal)
            .ToArray();
    }

    // Creates settings-page leases only for the requested enabled module package.
    public IReadOnlyList<IModuleSettingsPage> CreateSettingsPages(
        string packageName,
        IModuleSettingsHost host)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentNullException.ThrowIfNull(host);

        if (!TryResolvePackageDirectory(
                packageName,
                out var packageDirectory,
                out var error))
        {
            throw new ArgumentException(error, nameof(packageName));
        }

        return _packages.TryGetValue(packageDirectory, out var package)
            ? package.CreateSettingsPageLeases(host)
                .ToArray()
            : [];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _moduleWatcher.Dispose();
        foreach (var assembly in _packages.Values)
        {
            assembly.Retire();
        }
        _packages.Clear();
        _failedPackages.Clear();
        _nonModulePackages.Clear();
        _disabledPackageInfo.Clear();
        _packageDirectoryState = null;
    }

    private FileSystemWatcher CreateModuleWatcher()
    {
        var watcher = new FileSystemWatcher(ModulesDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size |
                           NotifyFilters.CreationTime
        };
        watcher.Changed += HandleModulePathChanged;
        watcher.Created += HandleModulePathChanged;
        watcher.Deleted += HandleModulePathChanged;
        watcher.Renamed += HandleModulePathRenamed;
        watcher.Error += HandleModuleWatcherError;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void HandleModulePathChanged(object sender, FileSystemEventArgs e) =>
        TrackChangedPackageDirectory(e.FullPath);

    private void HandleModulePathRenamed(object sender, RenamedEventArgs e)
    {
        TrackChangedPackageDirectory(e.OldFullPath);
        TrackChangedPackageDirectory(e.FullPath);
    }

    private void HandleModuleWatcherError(object sender, ErrorEventArgs e)
    {
        lock (_fileChangesLock)
        {
            _rescanAllPackages = true;
        }
        Debug.WriteLine($"模块目录监听失败，下次刷新将重新扫描全部模块：{e.GetException()}");
    }

    private void TrackChangedPackageDirectory(string path)
    {
        var relativePath = Path.GetRelativePath(ModulesDirectory, path);
        if (relativePath == "." ||
            relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            lock (_fileChangesLock)
            {
                _rescanAllPackages = true;
            }
            return;
        }

        var separatorIndex = relativePath.IndexOfAny(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        var packageName = separatorIndex < 0
            ? relativePath
            : relativePath[..separatorIndex];
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return;
        }

        lock (_fileChangesLock)
        {
            _changedPackageDirectories.Add(
                Path.GetFullPath(Path.Combine(ModulesDirectory, packageName)));
        }
    }

    private HashSet<string> ConsumeChangedPackageDirectories(out bool rescanAllPackages)
    {
        lock (_fileChangesLock)
        {
            var changedPackageDirectories = new HashSet<string>(
                _changedPackageDirectories,
                StringComparer.OrdinalIgnoreCase);
            _changedPackageDirectories.Clear();
            rescanAllPackages = _rescanAllPackages;
            _rescanAllPackages = false;
            return changedPackageDirectories;
        }
    }

    private ModulePackageInfo? GetInstalledPackage(string packageDirectory)
    {
        var packageName = Path.GetFileName(packageDirectory);
        if (_packages.TryGetValue(packageDirectory, out var loaded))
        {
            var module = loaded.Modules.Single();
            return new ModulePackageInfo(
                packageName,
                module.Id,
                module.DisplayName,
                module.Version,
                packageDirectory,
                ModulePackageState.Enabled);
        }

        if (!IsPackageEnabled(packageDirectory))
        {
            if (_disabledPackageInfo.TryGetValue(packageDirectory, out var disabledPackage))
            {
                return disabledPackage;
            }
            if (_activationPreferences.TryGet(packageName, out var preference))
            {
                return new ModulePackageInfo(
                    packageName,
                    preference.ModuleId ?? packageName,
                    preference.DisplayName ?? packageName,
                    Version.TryParse(preference.Version, out var version) ? version : null,
                    packageDirectory,
                    ModulePackageState.Disabled);
            }

            return new ModulePackageInfo(
                packageName,
                packageName,
                packageName,
                null,
                packageDirectory,
                ModulePackageState.Disabled);
        }

        if (_failedPackages.ContainsKey(packageDirectory))
        {
            return new ModulePackageInfo(
                packageName,
                packageName,
                packageName,
                null,
                packageDirectory,
                ModulePackageState.LoadFailed,
                "模块入口加载失败，请重新加载或重新安装模块");
        }

        return null;
    }

    private bool IsPackageEnabled(string packageDirectory)
    {
        var packageName = Path.GetFileName(packageDirectory);
        if (_activationPreferences.TryGet(packageName, out var preference))
        {
            return preference.Enabled;
        }

        return !File.Exists(GetDisabledMarkerPath(packageDirectory));
    }

    private void MigrateLegacyDisabledMarkers(
        IReadOnlyList<string> packageDirectories,
        ICollection<string> errors)
    {
        foreach (var packageDirectory in packageDirectories)
        {
            var markerPath = GetDisabledMarkerPath(packageDirectory);
            if (!File.Exists(markerPath))
            {
                continue;
            }

            var packageName = Path.GetFileName(packageDirectory);
            var marker = TryReadDisabledMarker(markerPath);
            if (marker is not null)
            {
                _disabledPackageInfo[packageDirectory] = new ModulePackageInfo(
                    packageName,
                    marker.ModuleId,
                    marker.DisplayName,
                    Version.TryParse(marker.Version, out var version) ? version : null,
                    packageDirectory,
                    ModulePackageState.Disabled);
            }

            try
            {
                if (!_activationPreferences.TryGet(packageName, out _))
                {
                    _activationPreferences.Set(
                        packageName,
                        new ModuleActivationPreference(
                            Enabled: false,
                            marker?.ModuleId,
                            marker?.DisplayName,
                            marker?.Version));
                }
                File.Delete(markerPath);
            }
            catch (Exception exception) when (IsPackageOperationException(exception))
            {
                errors.Add($"{packageName}：迁移旧版插件启用状态失败：{exception.Message}");
            }
        }
    }

    private static DisabledPackageMarker? TryReadDisabledMarker(string markerPath)
    {
        try
        {
            return JsonSerializer.Deserialize<DisabledPackageMarker>(
                File.ReadAllText(markerPath),
                MarkerJsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"读取模块禁用标记失败：{exception}");
            return null;
        }
    }

    private bool TryResolvePackageDirectory(
        string packageName,
        out string packageDirectory,
        out string error)
    {
        packageDirectory = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packageName) ||
            !string.Equals(packageName, Path.GetFileName(packageName), StringComparison.Ordinal) ||
            packageName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "模块包名称无效。";
            return false;
        }

        packageDirectory = Path.GetFullPath(Path.Combine(ModulesDirectory, packageName));
        var rootPrefix = Path.TrimEndingDirectorySeparator(ModulesDirectory) +
                         Path.DirectorySeparatorChar;
        if (!packageDirectory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            packageDirectory = string.Empty;
            error = "模块包路径超出模块目录。";
            return false;
        }
        return true;
    }

    private static string GetDisabledMarkerPath(string packageDirectory) =>
        Path.Combine(packageDirectory, DisabledMarkerFileName);

    private static bool IsPackageOperationException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException;

    private static string GetLoadError(Exception exception)
    {
        if (exception is ReflectionTypeLoadException reflectionException)
        {
            return reflectionException.LoaderExceptions.FirstOrDefault()?.Message ?? reflectionException.Message;
        }
        return exception.InnerException?.Message ?? exception.Message;
    }

    private sealed record DisabledPackageMarker(
        string ModuleId,
        string DisplayName,
        string? Version);

    private sealed class TransientModuleActivationPreferenceStore :
        IModuleActivationPreferenceStore
    {
        private readonly Dictionary<string, ModuleActivationPreference> _preferences =
            new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string packageName, out ModuleActivationPreference preference) =>
            _preferences.TryGetValue(packageName, out preference!);

        public void Set(string packageName, ModuleActivationPreference preference) =>
            _preferences[packageName] = preference;

        public void Remove(string packageName) =>
            _preferences.Remove(packageName);
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
            PackageStamp stamp,
            IModuleImageHost imageHost)
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
                    candidate = LoadAssembly(assemblyPath, stamp, imageHost);
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
            PackageStamp stamp,
            IModuleImageHost imageHost)
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
                    if (modules.Count == 0)
                    {
                        loadContext.PrepareForActiveLeases();
                    }
                    modules.Add(module);
                    module.Initialize(new ModuleContext(
                        Path.GetDirectoryName(assemblyPath)!,
                        imageHost));
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
                loadContext.PrepareForUnload();
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
            _loadContext.PrepareForUnload();
            _loadContext.Unload();
        }
    }

    private sealed class ModuleContext(
        string moduleDirectory,
        IModuleImageHost imageHost) : IModuleContext
    {
        public string ModuleDirectory { get; } = moduleDirectory;
        public Version HostVersion { get; } = typeof(ModuleHost).Assembly.GetName().Version ?? new Version(1, 0);
        public IModuleImageHost ImageHost { get; } = imageHost;
    }

    private sealed class UnavailableModuleImageHost : IModuleImageHost
    {
        public static UnavailableModuleImageHost Instance { get; } = new();

        public void CopyImage(Bitmap image) => ThrowUnavailable();

        public string SaveImage(Bitmap image)
        {
            ThrowUnavailable();
            return string.Empty;
        }

        public void EditImage(Bitmap image) => ThrowUnavailable();

        private static void ThrowUnavailable() =>
            throw new InvalidOperationException("当前宿主没有提供模块图片服务。");
    }

    private sealed class CaptureFeatureLease(ICaptureFeature inner, Action release) :
        ICaptureFeature,
        ICaptureToolbarCommandProvider,
        ICaptureToolbarCommandProgressProvider
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

        public bool UsesIndeterminateProgress(string commandId) =>
            inner is ICaptureToolbarCommandProgressProvider progressProvider &&
            progressProvider.UsesIndeterminateProgress(commandId);

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
