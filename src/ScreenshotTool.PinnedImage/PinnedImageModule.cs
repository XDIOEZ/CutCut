using ScreenshotTool.Contracts;

namespace ScreenshotTool.PinnedImage;

public sealed class PinnedImageModule : ScreenshotToolModuleBase
{
    private readonly IPinnedImageWindowFactory _windowFactory;
    private readonly HashSet<IPinnedImageWindow> _windows = [];
    private IModuleImageHost? _imageHost;
    private bool _disposed;

    public PinnedImageModule()
        : this(new PinnedImageWindowFactory())
    {
    }

    internal PinnedImageModule(IPinnedImageWindowFactory windowFactory)
    {
        _windowFactory = windowFactory;
    }

    public override string Id => "screenshot-tool.pinned-image";

    public override string DisplayName => "贴图悬浮窗";

    public override Version Version => new(1, 0, 0);

    internal int WindowCount => _windows.Count;

    public override void Initialize(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _imageHost = context.ImageHost;
    }

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return [new PinnedImageFeature(this)];
    }

    internal void ShowPinnedImage(Bitmap image, Rectangle suggestedBounds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(image);
        var imageHost = _imageHost ??
                        throw new InvalidOperationException("贴图模块尚未初始化。");
        Bitmap? ownedImage = new(image);
        IPinnedImageWindow? window = null;
        try
        {
            window = _windowFactory.Create(ownedImage, suggestedBounds, imageHost);
            ownedImage = null;
            window.WindowClosed += HandleWindowClosed;
            _windows.Add(window);
            window.Show();
        }
        catch
        {
            if (window is not null)
            {
                window.WindowClosed -= HandleWindowClosed;
                _windows.Remove(window);
                window.Dispose();
            }
            ownedImage?.Dispose();
            throw;
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var window in _windows.ToArray())
        {
            window.WindowClosed -= HandleWindowClosed;
            window.Close();
            window.Dispose();
        }
        _windows.Clear();
        _imageHost = null;
    }

    private void HandleWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not IPinnedImageWindow window)
        {
            return;
        }

        window.WindowClosed -= HandleWindowClosed;
        _windows.Remove(window);
    }
}

internal sealed class PinnedImageFeature(PinnedImageModule module) :
    CaptureFeatureBase,
    ICaptureToolbarCommandProvider
{
    private const string PinCommandId = "screenshot-tool.pinned-image.pin";

    public override string Id => PinCommandId;

    public override int Order => 400;

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() =>
    [
        new(
            PinCommandId,
            "贴图",
            "把当前截图内容固定为置顶悬浮窗",
            48)
    ];

    public Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(commandId, PinCommandId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"未知贴图命令：{commandId}", nameof(commandId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!Host.HasSelection || Host is not ICaptureArtifactHost artifactHost)
        {
            throw new InvalidOperationException("请先选择要贴到屏幕上的截图区域。");
        }

        using var image = artifactHost.RenderSelection();
        module.ShowPinnedImage(image, artifactHost.SelectionScreenBounds);
        artifactHost.CompleteCaptureSession();
        return Task.CompletedTask;
    }
}
