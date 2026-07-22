namespace ScreenshotTool.Core;

internal readonly record struct ApplicationLaunchOptions(bool StartInBackground)
{
    private const string BackgroundArgument = "--background";

    public static ApplicationLaunchOptions Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new ApplicationLaunchOptions(arguments.Any(argument => string.Equals(
            argument,
            BackgroundArgument,
            StringComparison.OrdinalIgnoreCase)));
    }
}

internal static class StartupCommandBuilder
{
    public const string BackgroundArgument = "--background";

    public static string Build(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var fullPath = Path.GetFullPath(executablePath.Trim());
        if (fullPath.Contains('"'))
        {
            throw new ArgumentException("程序路径不能包含双引号。", nameof(executablePath));
        }

        return $"\"{fullPath}\" {BackgroundArgument}";
    }
}
