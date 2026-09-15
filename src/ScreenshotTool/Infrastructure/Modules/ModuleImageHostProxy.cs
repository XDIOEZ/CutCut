using ScreenshotTool.Contracts;

namespace ScreenshotTool.Infrastructure.Modules;

internal sealed class ModuleImageHostProxy : IModuleImageStorageHost, IModuleImageTextHost
{
    private IModuleImageHost? _target;
    private IImageTextRecognitionService? _textRecognition;
    private Action<string>? _copyText;

    // Connects optional recognition and the existing clipboard service without coupling modules.
    public void AttachTextRecognition(IImageTextRecognitionService service, Action<string> copyText)
    {
        _textRecognition = service;
        _copyText = copyText;
    }

    // Queries current capabilities whenever a consumer opens its menu.
    public IReadOnlyList<ImageTextRecognizerInfo> GetTextRecognizers() =>
        _textRecognition?.GetTextRecognizers() ?? [];

    // Forwards recognition while leaving the module lifetime with the module host.
    public Task<ImageTextRecognitionResult> RecognizeImageTextAsync(
        string providerId, Bitmap image, CancellationToken cancellationToken) =>
        (_textRecognition ?? throw new InvalidOperationException("文字识别服务尚未就绪。"))
        .RecognizeImageTextAsync(providerId, image, cancellationToken);

    // Reuses the application clipboard behavior for selected image text.
    public void CopyText(string text) =>
        (_copyText ?? throw new InvalidOperationException("剪贴板服务尚未就绪。"))(text);

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
