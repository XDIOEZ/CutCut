using ScreenshotTool.Contracts;

namespace ScreenshotTool.ScreenRecording;

internal static class RecordingCoordinator
{
    public static async Task<RecordingControlResult> RunAsync(
        RecordingTarget target,
        RecordingOptions options,
        ICaptureAnnotationSession annotationSession,
        string helperDirectory,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (annotationSession is not ICaptureAnnotationToolbarSession toolbarSession)
        {
            throw new NotSupportedException(
                "当前主程序未提供截图核心编辑工具栏，请同时更新轻截主程序。");
        }

        using var session = new ScreenRecorderSession(target, options, helperDirectory);
        using var controls = new RecordingControlSession(
            toolbarSession,
            session,
            cancellationToken);

        annotationSession.Show();
        try
        {
            await session.StartAsync(outputPath, cancellationToken);
            controls.MarkStarted();
            return await controls.Completion;
        }
        catch
        {
            annotationSession.Close();
            throw;
        }
        finally
        {
            annotationSession.Close();
        }
    }
}
