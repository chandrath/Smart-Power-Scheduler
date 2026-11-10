using System.Windows;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core.Services;
using SmartSleepScheduler.UI.Views;

namespace SmartSleepScheduler;

public partial class App : Application
{
    public static UserSettings Settings { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 1. Initialize Single Source of Truth
        _ = AppConfig.Instance;

        // 2. Load User Settings
        Settings = UserSettings.Load();

        // 3. Apply Theme
        ThemeService.Instance.ApplyTheme(Settings.Theme);

        // 4. Launch Main Window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
