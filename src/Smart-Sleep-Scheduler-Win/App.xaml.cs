using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core.Services;
using SmartSleepScheduler.UI.Views;

namespace SmartSleepScheduler;

public partial class App : Application
{
    private const string UniqueAppGuid = "SmartSleepScheduler_SingleInstance_Mutex_9E2F9F24";
    private static Mutex? _mutex;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    public static UserSettings Settings { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 1. Single Instance Check
        _mutex = new Mutex(true, UniqueAppGuid, out bool isNewInstance);
        if (!isNewInstance)
        {
            // Activate existing instance
            IntPtr existingHwnd = FindWindow(null, AppConfig.Instance.App.DisplayName);
            if (existingHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(existingHwnd);
            }
            Shutdown();
            return;
        }

        // 2. Initialize Single Source of Truth
        _ = AppConfig.Instance;

        // 3. Load User Settings
        Settings = UserSettings.Load();

        // 4. Apply Theme
        ThemeService.Instance.ApplyTheme(Settings.Theme);

        // 5. Launch Main Window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
