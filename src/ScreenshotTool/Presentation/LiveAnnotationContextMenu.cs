using ScreenshotTool.Editing;

namespace ScreenshotTool.Presentation;

internal sealed class LiveAnnotationContextMenu : IDisposable
{
    private readonly ContextMenuStrip _menu = new();
    private MovableAnnotation? _target;

    public LiveAnnotationContextMenu(
        Action<MovableAnnotation> edit,
        Action<MovableAnnotation> delete)
    {
        _menu.Items.Add("编辑", null, (_, _) =>
        {
            if (_target is { } target)
            {
                edit(target);
            }
        });
        _menu.Items.Add("删除", null, (_, _) =>
        {
            if (_target is { } target)
            {
                delete(target);
            }
        });
        _menu.Opened += (_, _) => WindowCaptureProtection.TryExclude(_menu);
    }

    public bool Visible => _menu.Visible;

    public void Show(Control owner, Point screenLocation, MovableAnnotation target)
    {
        _target = target;
        _menu.Show(owner, owner.PointToClient(screenLocation));
    }

    public void Close() => _menu.Close();

    public void Dispose()
    {
        _target = null;
        _menu.Dispose();
    }
}
