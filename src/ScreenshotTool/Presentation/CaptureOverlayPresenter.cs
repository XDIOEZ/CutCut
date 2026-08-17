namespace ScreenshotTool.Presentation;

internal static class CaptureOverlayPresenter
{
    public static Task ShowAsync(CaptureOverlayForm overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ObjectDisposedException.ThrowIf(overlay.IsDisposed, overlay);
        if (overlay.Visible)
        {
            throw new InvalidOperationException("截图浮层已经显示。");
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleClosed(object? sender, FormClosedEventArgs e)
        {
            overlay.FormClosed -= HandleClosed;
            completion.TrySetResult(true);
        }

        overlay.FormClosed += HandleClosed;
        try
        {
            // A modal dialog disables the other WinForms top-level windows on this UI
            // thread, including pinned images. Keep the overlay non-modal and await its
            // lifetime so the caller still retains the same capture-session sequencing.
            overlay.Show();
        }
        catch
        {
            overlay.FormClosed -= HandleClosed;
            throw;
        }

        return completion.Task;
    }
}
