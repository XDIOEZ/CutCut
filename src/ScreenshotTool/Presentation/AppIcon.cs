namespace ScreenshotTool.Presentation;

internal static class AppIcon
{
    private const string ResourceName = "ScreenshotTool.Assets.LightShotIcon.ico";

    public static Icon Shared { get; } = Load();

    private static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException($"找不到应用图标资源：{ResourceName}");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
