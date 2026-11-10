using System.Diagnostics;
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
    private static bool _ownsMutex;

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    public static UserSettings Settings { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 1. Single Instance Check & Activate Existing Instance
        _mutex = new Mutex(true, UniqueAppGuid, out bool isNewInstance);
        if (!isNewInstance)
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var existing = Process.GetProcessesByName(current.ProcessName)
                    .FirstOrDefault(p => p.Id != current.Id);

                if (existing != null && existing.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindowAsync(existing.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(existing.MainWindowHandle);
                }
            }
            catch
            {
                // Silently exit if activation fails
            }

            Shutdown(0);
            return;
        }

        _ownsMutex = true;

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
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // Ignore if mutex already abandoned or released
            }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
