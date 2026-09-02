using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleImageHostProxy : IModuleImageStorageHost
{
    private IModuleImageHost? _target;

    public void Attach(IModuleImageHost target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
    }

    public void CopyImage(Bitmap image) => GetTarget().CopyImage(image);

    public string SaveImage(Bitmap image) => GetTarget().SaveImage(image);

    // Forwards custom-folder image writes only when the attached host supports storage routing.
    public Task<string> SaveImageAsync(
        Bitmap image,
        string outputFolder,
        IReadOnlyList<string>? imageTexts = null,
        CancellationToken cancellationToken = default) =>
        GetTarget() is IModuleImageStorageHost storageHost
            ? storageHost.SaveImageAsync(
                image,
                outputFolder,
                imageTexts,
                cancellationToken)
            : Task.FromException<string>(new NotSupportedException(
                "当前主程序不支持模块自定义图片文件夹。"));

    public void EditImage(Bitmap image) => GetTarget().EditImage(image);

    private IModuleImageHost GetTarget() =>
        _target ?? throw new InvalidOperationException("主程序图片服务尚未就绪。");
}
