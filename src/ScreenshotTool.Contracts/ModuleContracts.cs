namespace ScreenshotTool.Contracts;

public interface IScreenshotToolModule : IDisposable
{
    string Id { get; }

    string DisplayName { get; }

    Version Version { get; }

    void Initialize(IModuleContext context);

    IEnumerable<ICaptureFeature> CreateCaptureFeatures();
}

public abstract class ScreenshotToolModuleBase : IScreenshotToolModule
{
    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public virtual Version Version => new(1, 0, 0);

    public virtual void Initialize(IModuleContext context)
    {
    }

    public virtual IEnumerable<ICaptureFeature> CreateCaptureFeatures() => [];

    public virtual void Dispose()
    {
    }
}

public interface IModuleContext
{
    string ModuleDirectory { get; }

    Version HostVersion { get; }
}

public interface ICaptureFeature : IDisposable
{
    string Id { get; }

    int Order { get; }

    void Attach(ICaptureFeatureHost host);

    bool HandleKeyDown(KeyEventArgs e);

    bool HandleMouseDown(MouseEventArgs e);

    bool HandleMouseMove(MouseEventArgs e);

    bool HandleMouseUp(MouseEventArgs e);

    void Render(Graphics graphics, CaptureRenderTarget target);
}

public abstract class CaptureFeatureBase : ICaptureFeature
{
    protected ICaptureFeatureHost Host { get; private set; } = null!;

    public abstract string Id { get; }

    public virtual int Order => 0;

    public virtual void Attach(ICaptureFeatureHost host) => Host = host;

    public virtual bool HandleKeyDown(KeyEventArgs e) => false;

    public virtual bool HandleMouseDown(MouseEventArgs e) => false;

    public virtual bool HandleMouseMove(MouseEventArgs e) => false;

    public virtual bool HandleMouseUp(MouseEventArgs e) => false;

    public virtual void Render(Graphics graphics, CaptureRenderTarget target)
    {
    }

    public virtual void Dispose()
    {
    }
}

public sealed record CaptureToolbarCommand(
    string Id,
    string Text,
    string ToolTip,
    int Width = 58);

public interface ICaptureToolbarCommandProvider
{
    IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands();

    Task ExecuteToolbarCommandAsync(
        string commandId,
        CancellationToken cancellationToken);
}

public interface ICaptureFeatureHost
{
    bool HasSelection { get; }

    Rectangle Selection { get; }

    Point CursorClientPosition { get; }

    int Dpi { get; }

    bool GetBooleanPreference(string id, bool defaultValue);

    void InvalidateAll();

    void Invalidate(Rectangle bounds);

    void SetCursor(Cursor cursor);

    void SetMouseCapture(bool capture);

    Bitmap CopyDesktopSelection();
}

public static class CaptureFeaturePreferenceIds
{
    public const string LongCaptureSafetyChecks =
        "screenshot-tool.long-capture.safety-checks";
}

public interface ILiveCaptureFeatureHost : ICaptureFeatureHost
{
    bool HasEdits { get; }

    Rectangle SelectionScreenBounds { get; }

    void SetOverlayVisible(bool visible);

    Bitmap CaptureLiveSelection();

    void ReplaceCaptureResult(Bitmap image);
}

public enum CaptureRenderTarget
{
    Preview,
    Export
}
