using ScreenshotTool.Contracts;

namespace ScreenshotTool.Favorites;

public sealed class FavoritesModule : ScreenshotToolModuleBase, IModuleSettingsPageProvider
{
    public static Version MinimumHostVersion { get; } = new(1, 11, 8);

    private IModuleImageStorageHost? _imageStorageHost;

    public override string Id => "screenshot-tool.favorites";

    public override string DisplayName => "截图收藏夹";

    public override Version Version => new(1, 0, 0);

    // Captures the generic image-storage capability supplied by the host.
    public override void Initialize(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HostVersion < MinimumHostVersion)
        {
            throw new NotSupportedException(
                $"收藏夹模块需要轻截 {MinimumHostVersion} 或更高版本，当前主程序版本为 {context.HostVersion}。请同时更新轻截基础程序。");
        }

        _imageStorageHost = context.ImageHost as IModuleImageStorageHost ??
            throw new NotSupportedException(
                "当前主程序未提供模块自定义图片文件夹能力，请同时更新轻截基础程序。");
    }

    // Creates one independent favorite command for each new screenshot session.
    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures()
    {
        var imageStorageHost = _imageStorageHost ??
            throw new InvalidOperationException("收藏夹模块尚未初始化。");
        return [new FavoritesCaptureFeature(imageStorageHost)];
    }

    // Creates the module-owned page hosted by the floating configuration window.
    public IEnumerable<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host) =>
        [new FavoritesSettingsPage(host)];
}

internal sealed class FavoritesCaptureFeature(IModuleImageStorageHost imageStorageHost) :
    CaptureFeatureBase,
    ICaptureToolbarCommandProvider
{
    internal const string CommandId = "screenshot-tool.favorites.save";

    private static readonly IReadOnlyList<CaptureToolbarCommand> Commands =
        Array.AsReadOnly(
        [
            new CaptureToolbarCommand(
                CommandId,
                "收藏",
                "将当前最终截图直接保存到收藏文件夹",
                50)
        ]);

    public override string Id => "screenshot-tool.favorites.feature";

    public override int Order => 450;

    // Exposes the stable toolbar command rendered by the screenshot host.
    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() => Commands;

    // Renders the final annotated selection and saves it through the host storage boundary.
    public async Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, CommandId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"未知收藏命令：{commandId}", nameof(commandId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!Host.HasSelection || Host is not ICaptureArtifactHost artifactHost)
        {
            throw new InvalidOperationException("请先选择要收藏的截图区域。");
        }

        var configuredFolder = Host.GetStringPreference(
            FavoritesPreferences.FolderId,
            FavoritesPreferences.GetDefaultFolder());
        var outputFolder = FavoritesPreferences.ResolveStoredFolder(configuredFolder);
        using var image = artifactHost.RenderSelection();
        var path = await imageStorageHost.SaveImageAsync(
            image,
            outputFolder,
            artifactHost.GetSelectionTextContents(),
            cancellationToken);
        artifactHost.NotifyArtifactSaved(path);
        artifactHost.CompleteCaptureSession();
    }
}
