namespace ScreenshotTool.Abstractions;

internal interface IApplicationUpdateService : IDisposable
{
    Version CurrentVersion { get; }

    Task<ApplicationUpdateCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken);

    Task<PreparedApplicationUpdate> DownloadAndPrepareAsync(
        ApplicationUpdateInfo update,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken);

    void StartApplying(PreparedApplicationUpdate update, int processId);

    ApplicationUpdateApplyResult? TakePendingApplyResult();
}

internal sealed record ApplicationUpdateCheckResult(
    Version LatestVersion,
    string ReleaseName,
    DateTimeOffset PublishedAt,
    Uri ReleasePageUri,
    ApplicationUpdateInfo? AvailableUpdate);

internal sealed record ApplicationUpdateInfo(
    Version Version,
    string ReleaseName,
    DateTimeOffset PublishedAt,
    Uri ReleasePageUri,
    Uri PackageDownloadUri,
    long PackageSize,
    string PackageSha256,
    ApplicationUpdatePackageKind PackageKind);

internal enum ApplicationUpdatePackageKind
{
    Lightweight,
    Portable
}

internal readonly record struct ApplicationUpdateProgress(
    long BytesReceived,
    long TotalBytes);

internal sealed record PreparedApplicationUpdate(
    ApplicationUpdateInfo Update,
    string UpdateRoot,
    string PayloadDirectory,
    string ApplyScriptPath);

internal sealed record ApplicationUpdateApplyResult(
    bool Succeeded,
    string Version,
    string Message,
    DateTimeOffset CompletedAt);

internal sealed class ApplicationUpdateException(string message, Exception? innerException = null)
    : Exception(message, innerException);
