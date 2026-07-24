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

    IModuleImageHost ImageHost { get; }
}

public interface IModuleImageHost
{
    void CopyImage(Bitmap image);

    string SaveImage(Bitmap image);

    void EditImage(Bitmap image);
}

public interface IModuleSettingsPageProvider
{
    IEnumerable<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host);
}

public interface IModuleSettingsPage : IDisposable
{
    string Id { get; }

    string Title { get; }

    string Description { get; }

    int Order { get; }

    Control Content { get; }
}

public interface IModuleSettingsHost
{
    bool GetBoolean(string id, bool defaultValue);

    int GetInteger(string id, int defaultValue);

    string GetString(string id, string defaultValue);

    void SetBoolean(string id, bool value);

    void SetInteger(string id, int value);

    void SetString(string id, string value);

    void Save();
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

public interface ICaptureToolbarCommandProgressProvider
{
    bool UsesIndeterminateProgress(string commandId);
}

public interface ICaptureFeatureHost
{
    bool HasSelection { get; }

    Rectangle Selection { get; }

    Point CursorClientPosition { get; }

    int Dpi { get; }

    bool GetBooleanPreference(string id, bool defaultValue);

    int GetIntegerPreference(string id, int defaultValue);

    void InvalidateAll();

    void Invalidate(Rectangle bounds);

    void SetCursor(Cursor cursor);

    void SetMouseCapture(bool capture);

    /// <summary>
    /// Copies the current unannotated capture source. During image re-editing this is
    /// the replacement image rather than the desktop hidden behind the editor.
    /// </summary>
    Bitmap CopyDesktopSelection();
}

public interface ILiveCaptureFeatureHost : ICaptureFeatureHost
{
    bool HasEdits { get; }

    Rectangle SelectionScreenBounds { get; }

    void SetOverlayVisible(bool visible);

    Bitmap CaptureLiveSelection();

    void ReplaceCaptureResult(Bitmap image);
}

public interface ICaptureArtifactHost : ICaptureFeatureHost
{
    string OutputFolder { get; }

    Rectangle SelectionScreenBounds { get; }

    Bitmap RenderSelection();

    void NotifyArtifactSaved(string path);

    void CompleteCaptureSession();
}

public interface ICaptureTextResultHost : ICaptureFeatureHost
{
    void ShowTextResult(string title, string text);
}

public enum CaptureAnnotationTool
{
    Operation,
    Select,
    Rectangle,
    Ellipse,
    Arrow,
    Pen,
    Text,
    Mosaic
}

public sealed record CaptureAnnotationToolDefinition(
    CaptureAnnotationTool Tool,
    string Text,
    string ToolTip,
    int Width = 48);

public interface ICaptureAnnotationSession : IDisposable
{
    IReadOnlyList<CaptureAnnotationToolDefinition> Tools { get; }

    IReadOnlyList<Color> Palette { get; }

    CaptureAnnotationTool ActiveTool { get; set; }

    Color Color { get; set; }

    int ToolWidth { get; }

    int MinimumWidth { get; }

    int MaximumWidth { get; }

    int AnnotationCount { get; }

    bool AdjustWidth(int steps);

    bool CycleWidth();

    bool Undo();

    void Clear();

    void Show();

    void BringToFrontForEditing();

    void Close();
}

public enum CaptureAnnotationToolbarCommandStyle
{
    Default,
    Primary,
    Danger
}

public sealed record CaptureAnnotationToolbarCommand(
    string Id,
    string Text,
    string ToolTip,
    int Width = 48,
    CaptureAnnotationToolbarCommandStyle Style = CaptureAnnotationToolbarCommandStyle.Default);

public sealed class CaptureAnnotationToolbarCommandEventArgs(string commandId) : EventArgs
{
    public string CommandId { get; } = commandId;
}

public interface ICaptureAnnotationToolbarSession : ICaptureAnnotationSession
{
    event EventHandler<CaptureAnnotationToolbarCommandEventArgs>? ToolbarCommandInvoked;

    void SetToolVisible(CaptureAnnotationTool tool, bool visible);

    void ConfigureToolbar(
        string? statusText,
        IReadOnlyList<CaptureAnnotationToolbarCommand> commands);

    void UpdateToolbarStatus(string? statusText);

    void UpdateToolbarCommand(string commandId, string text, bool enabled);
}

public interface ICaptureAnnotationHost : ICaptureFeatureHost
{
    ICaptureAnnotationSession CreateAnnotationSession(Rectangle screenBounds);
}

public enum CaptureRegionIndicatorStyle
{
    Solid,
    Dashed,
    None
}

public sealed record CaptureAnnotationSessionOptions(
    CaptureRegionIndicatorStyle RegionIndicatorStyle = CaptureRegionIndicatorStyle.Dashed,
    bool ShowMouseClickIndicator = true);

public interface IConfigurableCaptureAnnotationHost : ICaptureAnnotationHost
{
    ICaptureAnnotationSession CreateAnnotationSession(
        Rectangle screenBounds,
        CaptureAnnotationSessionOptions options);
}

public enum CaptureRenderTarget
{
    Preview,
    Export
}
