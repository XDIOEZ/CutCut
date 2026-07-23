using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleImageHostProxy : IModuleImageHost
{
    private IModuleImageHost? _target;

    public void Attach(IModuleImageHost target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
    }

    public void CopyImage(Bitmap image) => GetTarget().CopyImage(image);

    public string SaveImage(Bitmap image) => GetTarget().SaveImage(image);

    public void EditImage(Bitmap image) => GetTarget().EditImage(image);

    private IModuleImageHost GetTarget() =>
        _target ?? throw new InvalidOperationException("主程序图片服务尚未就绪。");
}
