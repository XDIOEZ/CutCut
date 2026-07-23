using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class GitHubReleaseApplicationUpdateService : IApplicationUpdateService
{
    private const string LightweightAssetName = "complete-lightweight-win-x64.zip";
    private const string PortableAssetName = "complete-portable-win-x64.zip";
    private const long MaximumPackageBytes = 100L * 1024 * 1024;
    private const long MaximumExtractedBytes = 160L * 1024 * 1024;
    private const int MaximumArchiveEntries = 2000;

    private static readonly Uri LatestReleaseApiUri = new(
        "https://api.github.com/repos/XDIOEZ/CutCut/releases/latest");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _applicationDirectory;
    private readonly string _executablePath;
    private readonly string _updatesDirectory;
    private readonly string _resultPath;
    private readonly Func<bool> _desktopRuntimeAvailable;

    public GitHubReleaseApplicationUpdateService(
        Version currentVersion,
        string applicationDirectory,
        string executablePath)
        : this(
            currentVersion,
            applicationDirectory,
            executablePath,
            CreateHttpClient(),
            () => DesktopRuntimeDetector.IsWindowsDesktopRuntimeAvailable(8),
            ownsHttpClient: true)
    {
    }

    internal GitHubReleaseApplicationUpdateService(
        Version currentVersion,
        string applicationDirectory,
        string executablePath,
        HttpClient httpClient,
        Func<bool> desktopRuntimeAvailable)
        : this(
            currentVersion,
            applicationDirectory,
            executablePath,
            httpClient,
            desktopRuntimeAvailable,
            ownsHttpClient: false)
    {
    }

    private GitHubReleaseApplicationUpdateService(
        Version currentVersion,
        string applicationDirectory,
        string executablePath,
        HttpClient httpClient,
        Func<bool> desktopRuntimeAvailable,
        bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(desktopRuntimeAvailable);

        CurrentVersion = GitHubReleaseVersion.Normalize(currentVersion);
        _applicationDirectory = Path.GetFullPath(applicationDirectory);
        _executablePath = Path.GetFullPath(executablePath);
        _httpClient = httpClient;
        _desktopRuntimeAvailable = desktopRuntimeAvailable;
        _ownsHttpClient = ownsHttpClient;
        _updatesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LightShotCN",
            "Updates");
        _resultPath = Path.Combine(_updatesDirectory, "last-update-result.json");
    }

    public Version CurrentVersion { get; }

    public async Task<ApplicationUpdateCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        GitHubReleaseDocument release;
        try
        {
            using var response = await _httpClient.GetAsync(
                LatestReleaseApiUri,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var content = await response.Content.ReadAsStreamAsync(timeout.Token);
            release = await JsonSerializer.DeserializeAsync<GitHubReleaseDocument>(
                    content,
                    JsonOptions,
                    timeout.Token)
                ?? throw new ApplicationUpdateException("GitHub 返回了空的 Release 信息。");
        }
        catch (ApplicationUpdateException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApplicationUpdateException("连接 GitHub 超时，请稍后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new ApplicationUpdateException(
                $"无法连接 GitHub Releases：{exception.Message}",
                exception);
        }
        catch (JsonException exception)
        {
            throw new ApplicationUpdateException("GitHub Release 信息格式无效。", exception);
        }

        if (!GitHubReleaseVersion.TryParse(release.TagName, out var latestVersion))
        {
            throw new ApplicationUpdateException(
                $"无法识别最新 Release 版本号“{release.TagName}”。");
        }

        var releasePageUri = ParseHttpsUri(release.HtmlUrl, "Release 页面", "github.com");
        var releaseName = string.IsNullOrWhiteSpace(release.Name)
            ? $"轻截 v{latestVersion}"
            : release.Name.Trim();
        if (latestVersion <= CurrentVersion)
        {
            return new ApplicationUpdateCheckResult(
                latestVersion,
                releaseName,
                release.PublishedAt,
                releasePageUri,
                AvailableUpdate: null);
        }

        var packageKind = _desktopRuntimeAvailable()
            ? ApplicationUpdatePackageKind.Lightweight
            : ApplicationUpdatePackageKind.Portable;
        var expectedAssetName = packageKind == ApplicationUpdatePackageKind.Lightweight
            ? LightweightAssetName
            : PortableAssetName;
        var asset = release.Assets.FirstOrDefault(candidate => string.Equals(
            candidate.Name,
            expectedAssetName,
            StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            throw new ApplicationUpdateException(
                $"v{latestVersion} 没有提供更新文件 {expectedAssetName}。");
        }
        if (asset.Size <= 0 || asset.Size > MaximumPackageBytes)
        {
            throw new ApplicationUpdateException(
                $"更新文件大小异常：{FormatBytes(asset.Size)}。");
        }
        if (!GitHubAssetDigest.TryParseSha256(asset.Digest, out var sha256))
        {
            throw new ApplicationUpdateException(
                "GitHub 没有提供可验证的 SHA-256 摘要，已停止更新。");
        }

        var update = new ApplicationUpdateInfo(
            latestVersion,
            releaseName,
            release.PublishedAt,
            releasePageUri,
            ParseHttpsUri(asset.BrowserDownloadUrl, "更新文件", "github.com"),
            asset.Size,
            sha256,
            packageKind);
        return new ApplicationUpdateCheckResult(
            latestVersion,
            releaseName,
            release.PublishedAt,
            releasePageUri,
            update);
    }

    public async Task<PreparedApplicationUpdate> DownloadAndPrepareAsync(
        ApplicationUpdateInfo update,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.Version <= CurrentVersion)
        {
            throw new ApplicationUpdateException("目标版本不高于当前版本，已停止更新。");
        }
        if (!string.Equals(
                update.PackageDownloadUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                update.PackageDownloadUri.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationUpdateException("更新文件不是受信任的 GitHub HTTPS 地址。");
        }
        if (!GitHubAssetDigest.TryParseSha256(
                $"sha256:{update.PackageSha256}",
                out var expectedSha256))
        {
            throw new ApplicationUpdateException("更新文件的 SHA-256 摘要无效。");
        }

        Directory.CreateDirectory(_updatesDirectory);
        TryDeleteStaleUpdateDirectories();
        var updateRoot = Path.Combine(
            _updatesDirectory,
            $"v{update.Version}-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(updateRoot, "package.zip");
        var payloadDirectory = Path.Combine(updateRoot, "payload");
        var applyScriptPath = Path.Combine(updateRoot, "ApplyUpdate.ps1");
        Directory.CreateDirectory(updateRoot);

        try
        {
            await DownloadPackageAsync(update, archivePath, progress, cancellationToken);
            var actualSha256 = await CalculateSha256Async(archivePath, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedSha256),
                    Convert.FromHexString(actualSha256)))
            {
                throw new ApplicationUpdateException(
                    "更新文件 SHA-256 校验失败，文件可能不完整，已停止更新。");
            }

            await UpdateArchiveExtractor.ExtractAsync(
                archivePath,
                payloadDirectory,
                MaximumArchiveEntries,
                MaximumExtractedBytes,
                cancellationToken);
            ValidatePayloadVersion(payloadDirectory, update.Version);
            UpdatePayloadPruner.PreserveMissingModuleChoices(
                payloadDirectory,
                _applicationDirectory);
            await File.WriteAllTextAsync(
                applyScriptPath,
                PowerShellUpdateScript.Content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                cancellationToken);
            return new PreparedApplicationUpdate(
                update,
                updateRoot,
                payloadDirectory,
                applyScriptPath);
        }
        catch
        {
            TryDeleteDirectory(updateRoot);
            throw;
        }
    }

    public void StartApplying(PreparedApplicationUpdate update, int processId)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        EnsureInsideUpdatesDirectory(update.UpdateRoot);
        EnsureInsideUpdatesDirectory(update.PayloadDirectory);
        EnsureInsideUpdatesDirectory(update.ApplyScriptPath);
        if (!Directory.Exists(update.PayloadDirectory) ||
            !File.Exists(update.ApplyScriptPath))
        {
            throw new ApplicationUpdateException("暂存的更新文件已丢失，请重新下载。");
        }

        Directory.CreateDirectory(_updatesDirectory);
        if (File.Exists(_resultPath))
        {
            File.Delete(_resultPath);
        }

        var powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(powershellPath))
        {
            throw new ApplicationUpdateException("找不到 Windows PowerShell，无法启动更新程序。");
        }

        var requiresElevation = !DirectoryWriteProbe.CanWrite(_applicationDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = powershellPath,
            UseShellExecute = requiresElevation,
            CreateNoWindow = !requiresElevation,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        if (requiresElevation)
        {
            startInfo.Verb = "runas";
        }
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(update.ApplyScriptPath);
        startInfo.ArgumentList.Add("-ProcessId");
        startInfo.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-SourceDirectory");
        startInfo.ArgumentList.Add(update.PayloadDirectory);
        startInfo.ArgumentList.Add("-TargetDirectory");
        startInfo.ArgumentList.Add(_applicationDirectory);
        startInfo.ArgumentList.Add("-ExecutablePath");
        startInfo.ArgumentList.Add(_executablePath);
        startInfo.ArgumentList.Add("-ResultPath");
        startInfo.ArgumentList.Add(_resultPath);
        startInfo.ArgumentList.Add("-Version");
        startInfo.ArgumentList.Add(update.Update.Version.ToString(3));

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new ApplicationUpdateException("更新程序未能启动。");
            if (process.WaitForExit(500))
            {
                throw new ApplicationUpdateException(
                    $"更新程序提前退出（代码 {process.ExitCode}），当前版本没有关闭。");
            }
        }
        catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new ApplicationUpdateException("已取消管理员授权，更新没有开始。", exception);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new ApplicationUpdateException($"无法启动更新程序：{exception.Message}", exception);
        }
    }

    public ApplicationUpdateApplyResult? TakePendingApplyResult()
    {
        if (!File.Exists(_resultPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_resultPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<ApplicationUpdateApplyResult>(json, JsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            try
            {
                File.Delete(_resultPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task DownloadPackageAsync(
        ApplicationUpdateInfo update,
        string archivePath,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            using var response = await _httpClient.GetAsync(
                update.PackageDownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            response.EnsureSuccessStatusCode();
            var reportedLength = response.Content.Headers.ContentLength ?? update.PackageSize;
            if (reportedLength <= 0 || reportedLength > MaximumPackageBytes)
            {
                throw new ApplicationUpdateException(
                    $"更新文件大小异常：{FormatBytes(reportedLength)}。");
            }

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var destination = new FileStream(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            var buffer = new byte[81920];
            long received = 0;
            progress?.Report(new ApplicationUpdateProgress(0, reportedLength));
            while (true)
            {
                var read = await source.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                {
                    break;
                }

                received += read;
                if (received > MaximumPackageBytes)
                {
                    throw new ApplicationUpdateException("更新文件超过允许的最大大小。");
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                progress?.Report(new ApplicationUpdateProgress(received, reportedLength));
            }

            if (received != update.PackageSize)
            {
                throw new ApplicationUpdateException(
                    $"更新文件大小不匹配：应为 {FormatBytes(update.PackageSize)}，" +
                    $"实际为 {FormatBytes(received)}。");
            }
        }
        catch (ApplicationUpdateException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApplicationUpdateException("下载更新超时，请稍后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new ApplicationUpdateException(
                $"下载更新失败：{exception.Message}",
                exception);
        }
        catch (IOException exception)
        {
            throw new ApplicationUpdateException(
                $"保存更新文件失败：{exception.Message}",
                exception);
        }
    }

    private static async Task<string> CalculateSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidatePayloadVersion(string payloadDirectory, Version expectedVersion)
    {
        var executablePath = Path.Combine(payloadDirectory, "ScreenshotTool.exe");
        if (!File.Exists(executablePath))
        {
            throw new ApplicationUpdateException("更新包缺少 ScreenshotTool.exe。");
        }

        var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
        var payloadVersion = new Version(
            Math.Max(0, versionInfo.FileMajorPart),
            Math.Max(0, versionInfo.FileMinorPart),
            Math.Max(0, versionInfo.FileBuildPart));
        if (payloadVersion != GitHubReleaseVersion.Normalize(expectedVersion))
        {
            throw new ApplicationUpdateException(
                $"更新包版本为 v{payloadVersion}，与 Release v{expectedVersion} 不一致。");
        }
    }

    private void TryDeleteStaleUpdateDirectories()
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(_updatesDirectory))
            {
                var info = new DirectoryInfo(directory);
                if (DateTime.UtcNow - info.LastWriteTimeUtc > TimeSpan.FromDays(7))
                {
                    TryDeleteDirectory(directory);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void EnsureInsideUpdatesDirectory(string path)
    {
        var root = Path.GetFullPath(_updatesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationUpdateException("更新暂存路径无效。");
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Uri ParseHttpsUri(
        string? value,
        string fieldName,
        string expectedHost)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApplicationUpdateException($"GitHub 返回的{fieldName}地址无效。");
        }
        return uri;
    }

    private static string FormatBytes(long bytes) =>
        bytes < 0 ? "未知" : $"{bytes / 1024D / 1024D:0.##} MiB";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
            "LightShotCN",
            "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(
            "application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private sealed class GitHubReleaseDocument
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; init; }

        [JsonPropertyName("assets")]
        public IReadOnlyList<GitHubReleaseAsset> Assets { get; init; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }
    }
}

internal static class GitHubReleaseVersion
{
    public static bool TryParse(string? tagName, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var value = tagName.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }
        if (!Version.TryParse(value, out var parsed) ||
            parsed.Major < 0 ||
            parsed.Minor < 0)
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }

    public static Version Normalize(Version version) => new(
        version.Major,
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build));
}

internal static class GitHubAssetDigest
{
    public static bool TryParseSha256(string? digest, out string sha256)
    {
        sha256 = string.Empty;
        const string prefix = "sha256:";
        if (string.IsNullOrWhiteSpace(digest) ||
            !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = digest[prefix.Length..].Trim();
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        sha256 = value.ToLowerInvariant();
        return true;
    }
}

internal static class UpdateArchiveExtractor
{
    public static async Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        int maximumEntries,
        long maximumExtractedBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (maximumEntries <= 0 || maximumExtractedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }

        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long extractedBytes = 0;

        try
        {
            await using var archiveStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            if (archive.Entries.Count == 0 || archive.Entries.Count > maximumEntries)
            {
                throw new ApplicationUpdateException("更新压缩包的文件数量异常。");
            }

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsSymbolicLink(entry) ||
                    string.IsNullOrWhiteSpace(entry.FullName) ||
                    entry.FullName.IndexOf('\0') >= 0)
                {
                    throw new ApplicationUpdateException("更新压缩包包含不安全的文件项。");
                }

                var relativePath = entry.FullName
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var destinationPath = Path.GetFullPath(
                    Path.Combine(destinationDirectory, relativePath));
                if (!destinationPath.StartsWith(
                        destinationRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApplicationUpdateException(
                        "更新压缩包包含越出安装目录的文件路径。");
                }
                if (!extractedPaths.Add(destinationPath))
                {
                    throw new ApplicationUpdateException("更新压缩包包含重复的文件路径。");
                }

                var isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                                  entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                if (isDirectory)
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                extractedBytes = checked(extractedBytes + entry.Length);
                if (extractedBytes > maximumExtractedBytes)
                {
                    throw new ApplicationUpdateException("更新压缩包解压后超过允许的最大大小。");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using var source = entry.Open();
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);
                await source.CopyToAsync(destination, 81920, cancellationToken);
            }
        }
        catch (InvalidDataException exception)
        {
            throw new ApplicationUpdateException("更新压缩包已损坏。", exception);
        }
        catch (OverflowException exception)
        {
            throw new ApplicationUpdateException("更新压缩包解压大小异常。", exception);
        }
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int UnixFileTypeMask = 0xF000;
        const int UnixSymbolicLink = 0xA000;
        var unixMode = (entry.ExternalAttributes >> 16) & UnixFileTypeMask;
        return unixMode == UnixSymbolicLink;
    }
}

internal static class UpdatePayloadPruner
{
    public static void PreserveMissingModuleChoices(
        string payloadDirectory,
        string applicationDirectory)
    {
        var payloadModules = Path.Combine(payloadDirectory, "Modules");
        if (!Directory.Exists(payloadModules))
        {
            return;
        }

        var installedModules = Path.Combine(applicationDirectory, "Modules");
        foreach (var packagedModule in Directory.EnumerateDirectories(
                     payloadModules,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            var moduleName = Path.GetFileName(packagedModule);
            if (!Directory.Exists(Path.Combine(installedModules, moduleName)))
            {
                Directory.Delete(packagedModule, recursive: true);
            }
        }
    }
}

internal static class DesktopRuntimeDetector
{
    public static bool IsWindowsDesktopRuntimeAvailable(int requiredMajorVersion)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRoot(roots, Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"));
        AddRoot(roots, Environment.GetEnvironmentVariable("DOTNET_ROOT"));
        AddRoot(
            roots,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet"));

        foreach (var root in roots)
        {
            var sharedFramework = Path.Combine(
                root,
                "shared",
                "Microsoft.WindowsDesktop.App");
            try
            {
                if (Directory.EnumerateDirectories(sharedFramework)
                    .Select(Path.GetFileName)
                    .Any(name => Version.TryParse(name, out var version) &&
                                 version.Major >= requiredMajorVersion))
                {
                    return true;
                }
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    private static void AddRoot(ISet<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            roots.Add(Path.GetFullPath(path));
        }
    }
}

internal static class DirectoryWriteProbe
{
    public static bool CanWrite(string directory)
    {
        var probePath = Path.Combine(directory, $".lightshot-update-{Guid.NewGuid():N}.tmp");
        try
        {
            using var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal static class PowerShellUpdateScript
{
    public const string Content = """
        [CmdletBinding()]
        param(
            [Parameter(Mandatory = $true)]
            [int]$ProcessId,
            [Parameter(Mandatory = $true)]
            [string]$SourceDirectory,
            [Parameter(Mandatory = $true)]
            [string]$TargetDirectory,
            [Parameter(Mandatory = $true)]
            [string]$ExecutablePath,
            [Parameter(Mandatory = $true)]
            [string]$ResultPath,
            [Parameter(Mandatory = $true)]
            [string]$Version
        )

        $ErrorActionPreference = "Stop"
        $backupDirectory = Join-Path (Split-Path -Parent $SourceDirectory) "backup"
        $createdFiles = New-Object "System.Collections.Generic.List[string]"

        function Copy-WithRetry {
            param(
                [Parameter(Mandatory = $true)]
                [string]$Source,
                [Parameter(Mandatory = $true)]
                [string]$Destination
            )

            for ($attempt = 1; $attempt -le 20; $attempt++) {
                try {
                    $parent = Split-Path -Parent $Destination
                    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
                        New-Item -ItemType Directory -Path $parent -Force | Out-Null
                    }
                    Copy-Item -LiteralPath $Source -Destination $Destination -Force
                    return
                } catch {
                    if ($attempt -eq 20) {
                        throw
                    }
                    Start-Sleep -Milliseconds 250
                }
            }
        }

        function Write-Result {
            param(
                [Parameter(Mandatory = $true)]
                [bool]$Succeeded,
                [Parameter(Mandatory = $true)]
                [string]$Message
            )

            $resultParent = Split-Path -Parent $ResultPath
            if (-not (Test-Path -LiteralPath $resultParent -PathType Container)) {
                New-Item -ItemType Directory -Path $resultParent -Force | Out-Null
            }
            [pscustomobject]@{
                succeeded = $Succeeded
                version = $Version
                message = $Message
                completedAt = [DateTimeOffset]::Now
            } | ConvertTo-Json | Set-Content -LiteralPath $ResultPath -Encoding UTF8
        }

        $succeeded = $false
        $message = ""
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(45)
            while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
                if ([DateTime]::UtcNow -ge $deadline) {
                    throw "等待轻截退出超时。"
                }
                Start-Sleep -Milliseconds 250
            }

            New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
            $sourceRoot = $SourceDirectory.TrimEnd("\", "/")
            foreach ($sourceFile in Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse) {
                $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length).TrimStart("\", "/")
                $destinationPath = Join-Path $TargetDirectory $relativePath
                if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
                    $backupPath = Join-Path $backupDirectory $relativePath
                    Copy-WithRetry -Source $destinationPath -Destination $backupPath
                } else {
                    $createdFiles.Add($destinationPath)
                }
                Copy-WithRetry -Source $sourceFile.FullName -Destination $destinationPath
            }

            $succeeded = $true
            $message = "轻截已成功更新到 v$Version。"
        } catch {
            $message = "更新失败，已尝试恢复原版本：$($_.Exception.Message)"
            try {
                if (Test-Path -LiteralPath $backupDirectory -PathType Container) {
                    $backupRoot = $backupDirectory.TrimEnd("\", "/")
                    foreach ($backupFile in Get-ChildItem -LiteralPath $backupDirectory -File -Recurse) {
                        $relativePath = $backupFile.FullName.Substring($backupRoot.Length).TrimStart("\", "/")
                        Copy-WithRetry `
                            -Source $backupFile.FullName `
                            -Destination (Join-Path $TargetDirectory $relativePath)
                    }
                }
                foreach ($createdFile in $createdFiles) {
                    if (Test-Path -LiteralPath $createdFile -PathType Leaf) {
                        Remove-Item -LiteralPath $createdFile -Force
                    }
                }
            } catch {
                $message = "$message 回滚时也遇到问题：$($_.Exception.Message)"
            }
        }

        Write-Result -Succeeded $succeeded -Message $message
        try {
            Start-Process -FilePath $ExecutablePath
        } catch {
            Write-Result `
                -Succeeded $false `
                -Message "$message 自动重启失败，请手动打开 ScreenshotTool.exe：$($_.Exception.Message)"
        }
        """;
}
