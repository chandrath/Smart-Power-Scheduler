using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core;
using SmartSleepScheduler.Core.Services;

namespace SmartSleepScheduler.UI.Views;

public partial class SettingsFlyout : UserControl
{
    private static readonly Regex NumericRegex = new("[^0-9]+");

    public event Action? Closed;
    public event Action? SettingsChanged;
    public event Action? HistoryRequested;

    public SettingsFlyout()
    {
        InitializeComponent();
        AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(OnFlyoutPreviewMouseWheel), true);
        LoadValues();
    }

    private void OnFlyoutPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // If mouse is inside an open popup dropdown, allow scrolling the dropdown items
        if (e.OriginalSource is DependencyObject d)
        {
            var popup = FindVisualAncestor<Popup>(d);
            if (popup != null && popup.IsOpen) return;
        }

        // Otherwise, bubble wheel event to the Settings scroll viewer so controls don't change value accidentally
        if (SettingsScrollViewer != null)
        {
            e.Handled = true;
            SettingsScrollViewer.ScrollToVerticalOffset(SettingsScrollViewer.VerticalOffset - (e.Delta > 0 ? 44 : -44));
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t) return t;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    public void LoadValues()
    {
        var cfg = AppConfig.Instance;
        var s = App.Settings;

        // About section (Single Source of Truth)
        AboutAppNameText.Text = cfg.App.DisplayName;
        AboutVersionText.Text = $"v{cfg.App.Version}";
        AboutCopyrightText.Text = $"{cfg.App.Copyright} • {cfg.App.Vendor}";
        AboutDescText.Text = cfg.App.Description;

        // Elevation Status
        bool isAdmin = SleepBlockerService.IsAdministrator();
        AdminStatusText.Text = isAdmin ? "🛡️ Administrator" : "🛡️ Standard User";
        AdminDescText.Text = isAdmin
            ? "Deep kernel power request inspection is active."
            : "Admin rights enable deep kernel sleep blocker inspection.";
        RelaunchAdminButton.IsEnabled = !isAdmin;
        RelaunchAdminButton.Opacity = isAdmin ? 0.4 : 1.0;
        if (isAdmin)
        {
            AdminStatusText.Foreground = (System.Windows.Media.Brush)FindResource("StartBrush");
            RelaunchAdminButton.Content = "Active";
        }

        // Defaults duration
        DecomposeSeconds(s.DefaultTimerDurationSeconds, out int defVal, out int defUnitIdx);
        DefaultDurationInput.Text = defVal.ToString();
        DefaultDurationUnitCombo.SelectedIndex = defUnitIdx;

        // Auto start
        AutoStartCheck.IsChecked = s.AutoStartOnLaunch;
        DecomposeSeconds(s.AutoStartDurationSeconds, out int autoVal, out int autoUnitIdx);
        AutoStartDurationInput.Text = autoVal.ToString();
        AutoStartDurationUnitCombo.SelectedIndex = autoUnitIdx;

        // Always on top & Sound
        AlwaysOnTopCheck.IsChecked = s.AlwaysOnTop;
        SoundEnabledCheck.IsChecked = s.SoundEnabled;
        SoundOptionsPanel.Visibility = s.SoundEnabled ? Visibility.Visible : Visibility.Collapsed;

        int vol = s.SoundVolume > 0 ? s.SoundVolume : 50;
        SoundVolumeSlider.Value = vol;
        VolumeValueText.Text = $"{vol}%";
        SoundSchemeCombo.SelectedIndex = s.SoundScheme switch
        {
            "Classic" => 1,
            "Soft" => 2,
            _ => 0
        };
        TickTimingCombo.SelectedIndex = s.TickTriggerSeconds switch
        {
            10 => 0,
            15 => 1,
            20 => 2,
            25 => 3,
            30 => 4,
            45 => 5,
            60 => 6,
            120 => 7,
            300 => 8,
            0 => 9,
            _ => 4
        };

        // Theme
        ThemeCombo.SelectedIndex = (int)s.Theme;

        // Power Action
        PowerActionCombo.SelectedIndex = (int)s.SelectedPowerAction;

        // Wake-Up Greeting
        WakeUpTitleInput.Text = s.WakeUpTitle;
        WakeUpMessageInput.Text = s.WakeUpMessage;

        // Blocker Defense
        StartupBlockerSoundCheck.IsChecked = s.StartupBlockerAlertSoundEnabled;
        BlockerAlertSoundCheck.IsChecked = s.BlockerAlertSoundEnabled;
        PeriodicBlockerScanCheck.IsChecked = s.PeriodicBlockerScanEnabled;
        PeriodicScanIntervalPanel.Visibility = s.PeriodicBlockerScanEnabled ? Visibility.Visible : Visibility.Collapsed;
        PeriodicScanIntervalCombo.SelectedIndex = s.PeriodicBlockerScanIntervalSeconds switch
        {
            5 => 0,
            10 => 1,
            15 => 2,
            20 => 3,
            30 => 4,
            60 => 5,
            _ => 0
        };

        RefreshHistoryView();
    }

    private void PeriodicBlockerScanCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (PeriodicScanIntervalPanel == null) return;
        PeriodicScanIntervalPanel.Visibility = (PeriodicBlockerScanCheck.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void DecomposeSeconds(int totalSeconds, out int value, out int unitIndex)
    {
        if (totalSeconds % 3600 == 0 && totalSeconds >= 3600)
        {
            value = totalSeconds / 3600;
            unitIndex = 2; // Hours
        }
        else if (totalSeconds % 60 == 0 && totalSeconds >= 60)
        {
            value = totalSeconds / 60;
            unitIndex = 1; // Minutes
        }
        else
        {
            value = totalSeconds;
            unitIndex = 0; // Seconds
        }
    }

    private static int ComposeSeconds(int value, int unitIndex)
    {
        return unitIndex switch
        {
            0 => value,
            1 => value * 60,
            2 => value * 3600,
            _ => value
        };
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = NumericRegex.IsMatch(e.Text);
    }

    private void PowerActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ForceWarningBanner == null || ForceWarningText == null) return;
        var action = (PowerAction)PowerActionCombo.SelectedIndex;
        if (action == PowerAction.ForceShutdown || action == PowerAction.ForceRestart)
        {
            ForceWarningBanner.Visibility = Visibility.Visible;
            ForceWarningText.Text = "⚠️ Warning: Force actions will terminate open applications and unsaved documents immediately.";
        }
        else
        {
            ForceWarningBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var theme = (AppTheme)ThemeCombo.SelectedIndex;
        ThemeService.Instance.ApplyTheme(theme);
    }

    private void SoundEnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (SoundOptionsPanel == null) return;
        SoundOptionsPanel.Visibility = (SoundEnabledCheck.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SoundVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeValueText != null)
        {
            VolumeValueText.Text = $"{(int)SoundVolumeSlider.Value}%";
        }
        if (IsLoaded)
        {
            App.Settings.SoundVolume = (int)SoundVolumeSlider.Value;
        }
    }

    private void SoundSchemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            App.Settings.SoundScheme = SoundSchemeCombo.SelectedIndex switch
            {
                1 => "Classic",
                2 => "Soft",
                _ => "Modern"
            };
        }
    }

    private void TestSoundButton_Click(object sender, RoutedEventArgs e)
    {
        int vol = (int)SoundVolumeSlider.Value;
        string scheme = SoundSchemeCombo.SelectedIndex switch
        {
            1 => "Classic",
            2 => "Soft",
            _ => "Modern"
        };

        App.Settings.SoundVolume = vol;
        App.Settings.SoundScheme = scheme;

        SoundService.PlayToggleTest(vol, scheme);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var s = App.Settings;
        var limits = AppConfig.Instance.Defaults;

        // Parse Default Duration
        if (int.TryParse(DefaultDurationInput.Text, out int defVal) && defVal > 0)
        {
            int total = ComposeSeconds(defVal, DefaultDurationUnitCombo.SelectedIndex);
            s.DefaultTimerDurationSeconds = Math.Clamp(total, limits.MinTimerSeconds, limits.MaxTimerSeconds);
        }

        // Parse Auto Start Duration
        s.AutoStartOnLaunch = AutoStartCheck.IsChecked ?? true;
        if (int.TryParse(AutoStartDurationInput.Text, out int autoVal) && autoVal > 0)
        {
            int total = ComposeSeconds(autoVal, AutoStartDurationUnitCombo.SelectedIndex);
            s.AutoStartDurationSeconds = Math.Clamp(total, limits.MinTimerSeconds, limits.MaxTimerSeconds);
        }

        s.AlwaysOnTop = AlwaysOnTopCheck.IsChecked ?? true;
        s.SoundEnabled = SoundEnabledCheck.IsChecked ?? false;
        s.SoundVolume = (int)SoundVolumeSlider.Value;
        s.SoundScheme = SoundSchemeCombo.SelectedIndex switch
        {
            1 => "Classic",
            2 => "Soft",
            _ => "Modern"
        };
        s.TickTriggerSeconds = TickTimingCombo.SelectedIndex switch
        {
            0 => 10,
            1 => 15,
            2 => 20,
            3 => 25,
            4 => 30,
            5 => 45,
            6 => 60,
            7 => 120,
            8 => 300,
            9 => 0,
            _ => 30
        };
        s.Theme = (AppTheme)ThemeCombo.SelectedIndex;
        s.SelectedPowerAction = (PowerAction)PowerActionCombo.SelectedIndex;
        s.WakeUpTitle = string.IsNullOrWhiteSpace(WakeUpTitleInput.Text) ? AppConfig.Instance.Defaults.WakeUpTitle : WakeUpTitleInput.Text;
        s.WakeUpMessage = string.IsNullOrWhiteSpace(WakeUpMessageInput.Text) ? AppConfig.Instance.Defaults.WakeUpMessage : WakeUpMessageInput.Text;
        s.StartupBlockerAlertSoundEnabled = StartupBlockerSoundCheck.IsChecked ?? true;
        s.BlockerAlertSoundEnabled = BlockerAlertSoundCheck.IsChecked ?? true;
        s.PeriodicBlockerScanEnabled = PeriodicBlockerScanCheck.IsChecked ?? false;
        s.PeriodicBlockerScanIntervalSeconds = PeriodicScanIntervalCombo.SelectedIndex switch
        {
            0 => 5,
            1 => 10,
            2 => 15,
            3 => 20,
            4 => 30,
            5 => 60,
            _ => 5
        };

        s.Save();
        SettingsChanged?.Invoke();
        Closed?.Invoke();
    }

    private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var defaults = AppConfig.Instance.Defaults;
        var s = App.Settings;

        s.Theme = Enum.TryParse<AppTheme>(defaults.Theme, true, out var t) ? t : AppTheme.Dark;
        s.AlwaysOnTop = defaults.AlwaysOnTop;
        s.SoundEnabled = defaults.SoundEnabled;
        s.SoundVolume = defaults.SoundVolume > 0 ? defaults.SoundVolume : 50;
        s.SoundScheme = !string.IsNullOrWhiteSpace(defaults.SoundScheme) ? defaults.SoundScheme : "Modern";
        s.TickTriggerSeconds = defaults.TickTriggerSeconds;
        s.DefaultTimerDurationSeconds = defaults.DefaultTimerDurationSeconds;
        s.AutoStartOnLaunch = defaults.AutoStartOnLaunch;
        s.AutoStartDurationSeconds = defaults.AutoStartDurationSeconds;
        s.SelectedPowerAction = Enum.TryParse<PowerAction>(defaults.DefaultPowerAction, true, out var a) ? a : PowerAction.Sleep;
        s.AllowForceActions = defaults.AllowForceActions;
        s.WakeUpTitle = defaults.WakeUpTitle;
        s.WakeUpMessage = defaults.WakeUpMessage;
        s.StartupBlockerAlertSoundEnabled = defaults.StartupBlockerAlertSoundEnabled;
        s.BlockerAlertSoundEnabled = defaults.BlockerAlertSoundEnabled;
        s.PeriodicBlockerScanEnabled = defaults.PeriodicBlockerScanEnabled;
        s.PeriodicBlockerScanIntervalSeconds = defaults.PeriodicBlockerScanIntervalSeconds;

        s.Save();
        LoadValues();
        ThemeService.Instance.ApplyTheme(s.Theme);
        SettingsChanged?.Invoke();
    }

    private void OpenSettingsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = UserSettings.GetSettingsDirectory();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", dir)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open folder: {ex.Message}", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RelaunchAdminButton_Click(object sender, RoutedEventArgs e)
    {
        SleepBlockerService.RelaunchAsAdministrator();
    }

    private void RefreshHistoryView()
    {
        var records = HistoryService.GetHistory();
        HistoryTotalStatsText.Text = records.Count switch
        {
            0 => "0 sessions recorded",
            1 => "1 session recorded",
            _ => $"{records.Count} sessions recorded"
        };

        if (records.Count > 0)
        {
            HistoryEmptyNotice.Visibility = Visibility.Collapsed;
            HistoryScrollViewer.Visibility = Visibility.Visible;
            HistoryItemsList.ItemsSource = records;
            ShareHistoryButton.IsEnabled = true;
            ClearHistoryButton.IsEnabled = true;
        }
        else
        {
            HistoryEmptyNotice.Visibility = Visibility.Visible;
            HistoryScrollViewer.Visibility = Visibility.Collapsed;
            HistoryItemsList.ItemsSource = null;
            ShareHistoryButton.IsEnabled = false;
            ClearHistoryButton.IsEnabled = false;
        }
    }

    private async void ShareHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string shareText = HistoryService.GenerateShareText();
            Clipboard.SetText(shareText);
            ShareNotificationText.Visibility = Visibility.Visible;
            await Task.Delay(2500);
            ShareNotificationText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // Clipboard access can rarely fail if locked by another app
        }
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear your session history?",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            HistoryService.ClearHistory();
            RefreshHistoryView();
        }
    }

    private void ViewFullAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryRequested?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Closed?.Invoke();
    }
}

