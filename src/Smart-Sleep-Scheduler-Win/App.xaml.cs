using System.Diagnostics;
using System.IO;
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
    private static CancellationTokenSource? _ipcCancelSource;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 1. Single Instance Check & Activate Existing Instance
        _mutex = new Mutex(true, UniqueAppGuid, out bool isNewInstance);
        if (!isNewInstance)
        {
            if (e.Args.Length > 0)
            {
                IpcService.TrySendToExistingInstance(e.Args);
            }

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

        // 2. Parse Startup CLI Arguments
        var overrides = CommandLineService.Parse(e.Args);

        // 3. Initialize Single Source of Truth
        _ = AppConfig.Instance;

        // 4. Load User Settings & Cache App Path for Integrations
        Settings = UserSettings.Load();
        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppConfig.Instance.App.Vendor, AppConfig.Instance.App.Name);
            Directory.CreateDirectory(appData);
            File.WriteAllText(Path.Combine(appData, "app_path.txt"), Environment.ProcessPath ?? "");
        }
        catch { }

        // 5. Apply Theme
        ThemeService.Instance.ApplyTheme(Settings.Theme);

        // 6. Launch Main Window with Overrides
        var mainWindow = new MainWindow(overrides);
        mainWindow.Show();

        // 7. Start Background IPC Listener & State Server
        _ipcCancelSource = new CancellationTokenSource();
        IpcService.StartServer(args =>
        {
            Dispatcher.Invoke(() =>
            {
                var incoming = CommandLineService.Parse(args);
                mainWindow.ApplyRuntimeOverrides(incoming);
            });
        }, _ipcCancelSource.Token);
        IpcService.StartStateServer(_ipcCancelSource.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ipcCancelSource?.Cancel();
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
