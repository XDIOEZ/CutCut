namespace ScreenshotTool.Core;

internal enum StartupWorkspaceReason
{
    None,
    FirstRun,
    VersionChanged
}

internal static class StartupWorkspacePolicy
{
    public static StartupWorkspaceReason DetermineReason(
        string? lastLaunchedVersion,
        Version currentVersion)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        if (string.IsNullOrWhiteSpace(lastLaunchedVersion))
        {
            return StartupWorkspaceReason.FirstRun;
        }

        if (!Version.TryParse(lastLaunchedVersion.Trim(), out var previousVersion))
        {
            return StartupWorkspaceReason.VersionChanged;
        }

        return GetProductVersion(previousVersion) == GetProductVersion(currentVersion)
            ? StartupWorkspaceReason.None
            : StartupWorkspaceReason.VersionChanged;
    }

    public static string CreateVersionMarker(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var productVersion = GetProductVersion(version);
        return $"{productVersion.Major}.{productVersion.Minor}.{productVersion.Build}";
    }

    public static bool ShouldStartMinimized(
        bool startMinimized,
        StartupWorkspaceReason reason,
        bool startInBackground = false) =>
        (startMinimized || startInBackground) && reason == StartupWorkspaceReason.None;

    private static (int Major, int Minor, int Build) GetProductVersion(Version version) =>
        (version.Major, version.Minor, Math.Max(0, version.Build));
}
