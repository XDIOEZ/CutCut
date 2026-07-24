using ScreenshotTool.Contracts;

namespace ScreenshotTool.PaddleOcr;

public abstract class PaddleOcrModuleBase : ScreenshotToolModuleBase
{
    public static Version MinimumHostVersion { get; } = new(1, 11, 6);

    private readonly object _lifecycleLock = new();
    private string? _moduleDirectory;
    private PaddleOcrModelWorkspace? _modelWorkspace;
    private bool _disposed;

    protected abstract PaddleOcrVariant Variant { get; }

    protected abstract string FeatureId { get; }

    protected abstract string CommandId { get; }

    protected abstract string CommandText { get; }

    protected abstract string CommandToolTip { get; }

    protected abstract string ResultTitle { get; }

    protected abstract int FeatureOrder { get; }

    public override Version Version => new(1, 1, 0);

    public override void Initialize(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HostVersion < MinimumHostVersion)
        {
            throw new NotSupportedException(
                $"{DisplayName}需要轻截 {MinimumHostVersion} 或更高版本，" +
                $"当前主程序版本为 {context.HostVersion}。请同时更新轻截基础程序。");
        }

        PaddleOcrModelFiles.Resolve(context.ModuleDirectory, Variant)
            .EnsurePresent();

        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _moduleDirectory = context.ModuleDirectory;
        }
    }

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures()
    {
        string modelModuleDirectory;
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (string.IsNullOrWhiteSpace(_moduleDirectory))
            {
                throw new InvalidOperationException($"{DisplayName}尚未初始化。");
            }

            _modelWorkspace ??= PaddleOcrModelWorkspace.Create(
                _moduleDirectory,
                Variant);
            modelModuleDirectory = _modelWorkspace.ModuleDirectory;
        }

        return
        [
            new PaddleOcrFeature(
                FeatureId,
                FeatureOrder,
                CommandId,
                CommandText,
                CommandToolTip,
                ResultTitle,
                new PaddleOcrRecognizer(modelModuleDirectory, Variant))
        ];
    }

    public override void Dispose()
    {
        PaddleOcrModelWorkspace? workspace;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            workspace = _modelWorkspace;
            _modelWorkspace = null;
            _moduleDirectory = null;
        }

        workspace?.Dispose();
    }
}
