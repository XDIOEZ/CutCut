using System.ComponentModel;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.PinnedImage;

// Coordinates optional OCR, selection input and feedback without adding recognition state to the form.
internal sealed class PinnedImageTextController : IDisposable
{
    private readonly Form _owner;
    private readonly Bitmap _image;
    private readonly IModuleImageTextHost? _host;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _recognize = new("识别文字");
    private readonly ToolStripMenuItem _copy = new("复制文字");
    private readonly ToolStripMenuItem _exit = new("退出文字选择");
    private readonly ToolTip _hint = new();
    private ImageTextSelection? _selection;
    private CancellationTokenSource? _recognitionCancellation;
    private bool _selecting;
    private bool _disposed;

    // Adds optional text actions to the existing image menu and owns their event lifetime.
    public PinnedImageTextController(Form owner, Bitmap image, IModuleImageHost host, ContextMenuStrip menu)
    {
        _owner = owner;
        _image = image;
        _host = host as IModuleImageTextHost;
        _menu = menu;
        _recognize.Name = "RecognizePinnedImageTextMenuItem";
        _copy.Name = "CopyPinnedImageTextMenuItem";
        _copy.ShortcutKeyDisplayString = "Ctrl+C";
        _exit.Name = "ExitPinnedImageTextMenuItem";
        _menu.Items.AddRange([_recognize, _copy, _exit]);
        _menu.Opening += HandleMenuOpening;
        _recognize.Click += HandleRecognizeClick;
        _copy.Click += HandleCopyClick;
        _exit.Click += HandleExitClick;
    }

    public bool IsTextMode => _selection is not null;

    // Refreshes available OCR engines at menu-open time so install and disable changes are respected.
    private void HandleMenuOpening(object? sender, CancelEventArgs e)
    {
        var providers = _host?.GetTextRecognizers() ?? [];
        while (_recognize.DropDownItems.Count > 0)
        {
            var item = _recognize.DropDownItems[0];
            _recognize.DropDownItems.RemoveAt(0);
            item.Dispose();
        }
        _recognize.Visible = providers.Count > 0 || _recognitionCancellation is not null;
        _recognize.Enabled = providers.Count > 0 && _recognitionCancellation is null;
        _recognize.Text = _recognitionCancellation is not null ? "正在识别文字…" : "识别文字";
        _recognize.Tag = providers.Count == 1 ? providers[0].Id : null;
        if (providers.Count > 1)
        {
            foreach (var provider in providers)
            {
                var item = new ToolStripMenuItem(provider.DisplayName) { Tag = provider.Id };
                item.Click += HandleRecognizeClick;
                _recognize.DropDownItems.Add(item);
            }
        }
        _copy.Visible = IsTextMode;
        _copy.Enabled = _selection?.HasSelection == true;
        _exit.Visible = IsTextMode;
    }

    // Recognizes a private bitmap snapshot, cancels on close, and never opens a text result window.
    private async void HandleRecognizeClick(object? sender, EventArgs e)
    {
        if (_disposed || _host is null || _recognitionCancellation is not null ||
            sender is not ToolStripItem { Tag: string providerId })
        {
            return;
        }
        using var cancellation = new CancellationTokenSource();
        _recognitionCancellation = cancellation;
        try
        {
            ShowHint("正在识别文字…");
            using var snapshot = new Bitmap(_image);
            var result = await _host.RecognizeImageTextAsync(providerId, snapshot, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var selection = await Task.Run(() => new ImageTextSelection(result), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (_disposed)
            {
                return;
            }
            if (!selection.HasText)
            {
                ShowHint("没有识别到文字，请换用更清晰的图片或其他识别插件。");
                return;
            }
            _selection = selection;
            _owner.Cursor = Cursors.IBeam;
            _owner.Invalidate();
            ShowHint("左键拖选文字，Ctrl+C 复制；Alt+左键移动贴图，Alt+拖动边角缩放。");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Closing a pin cancels its own request without showing stale feedback.
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ShowHint($"识别失败：{exception.Message}");
            }
        }
        finally
        {
            _recognitionCancellation = null;
        }
    }

    // Consumes plain left-drag throughout the image, including text near window edges.
    public bool HandleMouseDown(MouseEventArgs e, Keys modifiers)
    {
        if (_selection is null || e.Button != MouseButtons.Left || (modifiers & Keys.Alt) != 0)
        {
            return false;
        }
        _hint.Hide(_owner);
        _selection.Begin(ToImagePoint(e.Location), (modifiers & Keys.Shift) != 0);
        _selecting = true;
        _owner.Capture = true;
        _owner.Cursor = Cursors.IBeam;
        _owner.Invalidate();
        return true;
    }

    // Extends the text range even when a captured pointer leaves the image bounds.
    public bool HandleMouseMove(MouseEventArgs e)
    {
        if (!_selecting || _selection is null)
        {
            return false;
        }
        _selection.Extend(ToImagePoint(e.Location));
        _owner.Invalidate();
        return true;
    }

    // Finishes the selection while keeping its blue highlight for copying.
    public bool HandleMouseUp(MouseEventArgs e)
    {
        if (!_selecting || e.Button != MouseButtons.Left)
        {
            return false;
        }
        _selection?.Extend(ToImagePoint(e.Location));
        _selecting = false;
        _owner.Capture = false;
        _owner.Invalidate();
        return true;
    }

    // Ends only the active gesture when Windows transfers mouse capture.
    public void ReleaseCapture() => _selecting = false;

    // Handles standard text selection shortcuts without consuming unrelated system keys.
    public bool HandleKey(Keys keyData)
    {
        if (_selection is null)
        {
            return false;
        }
        switch (keyData)
        {
            case Keys.Control | Keys.C:
                CopySelection();
                return true;
            case Keys.Control | Keys.A:
                _selection.SelectAll();
                _owner.Invalidate();
                return true;
            case Keys.Escape:
                _selection.Clear();
                _owner.Invalidate();
                return true;
            default:
                return false;
        }
    }

    // Draws only selection feedback; the source bitmap remains untouched.
    public void Draw(Graphics graphics) => _selection?.Draw(graphics, _image.Size, _owner.ClientSize);

    // Forwards the explicit text-copy menu action to the same shortcut behavior.
    private void HandleCopyClick(object? sender, EventArgs e) => CopySelection();

    // Writes selected text through the host and contains clipboard contention errors in the pin.
    private void CopySelection()
    {
        if (_selection?.HasSelection != true)
        {
            return;
        }
        try
        {
            _host!.CopyText(_selection.SelectedText);
        }
        catch (Exception exception)
        {
            ShowHint($"复制文字失败：{exception.Message}");
        }
    }

    // Restores the original move-and-resize gestures without discarding the pinned image.
    private void HandleExitClick(object? sender, EventArgs e)
    {
        _selection = null;
        _selecting = false;
        _owner.Capture = false;
        _owner.Cursor = Cursors.Default;
        _owner.Invalidate();
    }

    // Converts the current display coordinates back to the unchanged source image.
    private PointF ToImagePoint(Point point) => new(
        point.X * _image.Width / (float)Math.Max(1, _owner.ClientSize.Width),
        point.Y * _image.Height / (float)Math.Max(1, _owner.ClientSize.Height));

    // Shows transient feedback near the pin rather than placing a results window over its text.
    private void ShowHint(string message) => _hint.Show(message, _owner, new Point(8, 8), 5000);

    // Cancels outstanding OCR and releases UI resources while the request owns its snapshot and token.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _recognitionCancellation?.Cancel();
        _menu.Opening -= HandleMenuOpening;
        _recognize.Click -= HandleRecognizeClick;
        _copy.Click -= HandleCopyClick;
        _exit.Click -= HandleExitClick;
        _hint.Dispose();
        _selection = null;
    }
}
