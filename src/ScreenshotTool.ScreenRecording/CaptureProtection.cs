using System.Runtime.InteropServices;

namespace ScreenshotTool.ScreenRecording;

internal static class CaptureProtection
{
    private const uint ExcludeFromCapture = 0x00000011;

    public static void TryExclude(Form form)
    {
        if (!form.IsHandleCreated)
        {
            return;
        }

        try
        {
            SetWindowDisplayAffinity(form.Handle, ExcludeFromCapture);
        }
        catch (EntryPointNotFoundException)
        {
            // Older supported Windows builds may not expose the exclusion flag.
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
}
