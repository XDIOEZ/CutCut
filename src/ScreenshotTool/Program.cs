using ScreenshotTool.Application;
using ScreenshotTool.Core;

namespace ScreenshotTool;

internal static class Program
{
    private const string MutexName = @"Local\ScreenshotTool.SingleInstance";
    private const string RestartEventName = @"Local\ScreenshotTool.RestartRequested";
    private static readonly TimeSpan RestartTimeout = TimeSpan.FromSeconds(10);

    [STAThread]
    private static void Main(string[] args)
    {
        var launchOptions = ApplicationLaunchOptions.Parse(args);
        using var mutex = new Mutex(initiallyOwned: false, MutexName);
        using var restartEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            RestartEventName);
        var ownsMutex = TryAcquireMutex(mutex, TimeSpan.Zero);
        if (!ownsMutex && launchOptions.StartInBackground)
        {
            return;
        }
        if (SingleInstanceLaunchPolicy.RequiresRestartConfirmation(ownsMutex))
        {
            var restartChoice = MessageBox.Show(
                "检测到旧实例仍在运行。是否关闭旧实例并重新启动？\n\n这是再次启动 EXE 时的特殊启动流程。旧实例未运行时，程序会直接正常启动且不会显示此提示。",
                "重新启动截图工具",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (restartChoice != DialogResult.Yes)
            {
                return;
            }

            restartEvent.Set();
            ownsMutex = TryAcquireMutex(mutex, RestartTimeout);
        }

        if (!ownsMutex)
        {
            MessageBox.Show(
                "旧实例未能及时退出，请稍后重试。",
                "截图工具重启失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            using var app = CompositionRoot.Create(launchOptions.StartInBackground);
            var restartRegistration = ThreadPool.RegisterWaitForSingleObject(
                restartEvent,
                (_, timedOut) =>
                {
                    if (timedOut || app.MainForm.IsDisposed)
                    {
                        return;
                    }

                    try
                    {
                        app.MainForm.BeginInvoke(app.MainForm.RequestRestartExit);
                    }
                    catch (InvalidOperationException) when (app.MainForm.IsDisposed || !app.MainForm.IsHandleCreated)
                    {
                        // The old instance already completed shutdown.
                    }
                },
                state: null,
                Timeout.Infinite,
                executeOnlyOnce: true);
            try
            {
                System.Windows.Forms.Application.Run(app.MainForm);
            }
            finally
            {
                restartRegistration.Unregister(waitObject: null);
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static bool TryAcquireMutex(Mutex mutex, TimeSpan timeout)
    {
        try
        {
            return mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}

internal static class SingleInstanceLaunchPolicy
{
    public static bool RequiresRestartConfirmation(bool ownsMutex) => !ownsMutex;
}
