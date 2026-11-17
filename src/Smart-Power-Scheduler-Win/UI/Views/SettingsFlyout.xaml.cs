using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SmartPowerScheduler.Config;
using SmartPowerScheduler.Core;
using SmartPowerScheduler.Core.Services;

namespace SmartPowerScheduler.UI.Views;

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
        AboutDeveloperText.Text = $"Developer: {cfg.App.Developer}";
        AboutVendorText.Text = cfg.App.Vendor;
        AboutCopyrightText.Text = cfg.App.Copyright;
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
        UpdateStreamDeckStatus();

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
        SoundSchemeCombo.SelectedIndex = s.SoundScheme switch { "Classic" => 1, "Soft" => 2, _ => 0 };
        TickTimingCombo.SelectedIndex = s.TickTriggerSeconds switch { 10 => 0, 15 => 1, 20 => 2, 25 => 3, 30 => 4, 45 => 5, 60 => 6, 120 => 7, 300 => 8, 0 => 9, _ => 4 };

        // Theme
        ThemeCombo.SelectedIndex = (int)s.Theme;

        // Power Action & Hibernate capability
        bool hiberOk = PowerService.IsHibernateSupported();
        if (HibernateComboItem != null)
        {
            HibernateComboItem.IsEnabled = hiberOk;
            HibernateComboItem.ToolTip = hiberOk ? null : "Hibernate is disabled in Windows power settings.";
        }
        PowerActionCombo.SelectedIndex = (int)s.SelectedPowerAction;

        // Wake-Up Greeting
        WakeUpTitleInput.Text = s.WakeUpTitle;
        WakeUpMessageInput.Text = s.WakeUpMessage;

        // Custom Protected Apps (Beta)
        CustomAppsBetaBadge.Visibility = cfg.Defaults.CustomProtectedAppsIsBeta ? Visibility.Visible : Visibility.Collapsed;
        CustomProtectedAppsInput.Text = s.CustomProtectedProcesses != null && s.CustomProtectedProcesses.Length > 0
            ? string.Join(", ", s.CustomProtectedProcesses)
            : "";

        // Blocker Defense
        StartupBlockerSoundCheck.IsChecked = s.StartupBlockerAlertSoundEnabled;
        BlockerAlertSoundCheck.IsChecked = s.BlockerAlertSoundEnabled;
        PeriodicBlockerScanCheck.IsChecked = s.PeriodicBlockerScanEnabled;
        PeriodicScanIntervalPanel.Visibility = s.PeriodicBlockerScanEnabled ? Visibility.Visible : Visibility.Collapsed;
        PeriodicScanIntervalCombo.SelectedIndex = s.PeriodicBlockerScanIntervalSeconds switch { 5 => 0, 10 => 1, 15 => 2, 20 => 3, 30 => 4, 60 => 5, _ => 0 };

        // Updates
        CheckUpdatesOnLaunchCheck.IsChecked = s.CheckForUpdatesOnStartup;
        UpdateStatusText.Text = $"Current: v{cfg.App.Version}";
        CheckForUpdatesButton.Content = "Check for Updates";
        _discoveredUpdateUrl = null;

        RefreshHistoryView();
        UpdateShortcutPreview();
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

        // Parse Auto Start & Unified Timer Duration
        s.AutoStartOnLaunch = AutoStartCheck.IsChecked ?? true;
        if (int.TryParse(AutoStartDurationInput.Text, out int autoVal) && autoVal > 0)
        {
            int total = ComposeSeconds(autoVal, AutoStartDurationUnitCombo.SelectedIndex);
            int clamped = Math.Clamp(total, limits.MinTimerSeconds, limits.MaxTimerSeconds);
            s.AutoStartDurationSeconds = clamped;
            s.DefaultTimerDurationSeconds = clamped;
        }

        s.AlwaysOnTop = AlwaysOnTopCheck.IsChecked ?? true;
        s.SoundEnabled = SoundEnabledCheck.IsChecked ?? false;
        s.SoundVolume = (int)SoundVolumeSlider.Value;
        s.SoundScheme = SoundSchemeCombo.SelectedIndex switch { 1 => "Classic", 2 => "Soft", _ => "Modern" };
        s.TickTriggerSeconds = TickTimingCombo.SelectedIndex switch { 0 => 10, 1 => 15, 2 => 20, 3 => 25, 4 => 30, 5 => 45, 6 => 60, 7 => 120, 8 => 300, 9 => 0, _ => 30 };
        s.Theme = (AppTheme)ThemeCombo.SelectedIndex;
        s.SelectedPowerAction = (PowerAction)PowerActionCombo.SelectedIndex;
        s.WakeUpTitle = string.IsNullOrWhiteSpace(WakeUpTitleInput.Text) ? AppConfig.Instance.Defaults.WakeUpTitle : WakeUpTitleInput.Text;
        s.WakeUpMessage = string.IsNullOrWhiteSpace(WakeUpMessageInput.Text) ? AppConfig.Instance.Defaults.WakeUpMessage : WakeUpMessageInput.Text;
        s.StartupBlockerAlertSoundEnabled = StartupBlockerSoundCheck.IsChecked ?? true;
        s.BlockerAlertSoundEnabled = BlockerAlertSoundCheck.IsChecked ?? true;
        s.PeriodicBlockerScanEnabled = PeriodicBlockerScanCheck.IsChecked ?? false;
        s.PeriodicBlockerScanIntervalSeconds = PeriodicScanIntervalCombo.SelectedIndex switch { 0 => 5, 1 => 10, 2 => 15, 3 => 20, 4 => 30, 5 => 60, _ => 5 };
        s.CheckForUpdatesOnStartup = CheckUpdatesOnLaunchCheck.IsChecked ?? true;

        // Custom Protected Apps (Beta)
        var rawCustom = CustomProtectedAppsInput.Text
            .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        s.CustomProtectedProcesses = UserSettings.SanitizeProcesses(rawCustom);

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
        s.AutoStartOnLaunch = defaults.AutoStartOnLaunch;
        s.AutoStartDurationSeconds = defaults.AutoStartDurationSeconds;
        s.DefaultTimerDurationSeconds = defaults.AutoStartDurationSeconds;
        s.SelectedPowerAction = Enum.TryParse<PowerAction>(defaults.DefaultPowerAction, true, out var a) ? a : PowerAction.Sleep;
        s.AllowForceActions = defaults.AllowForceActions;
        s.WakeUpTitle = defaults.WakeUpTitle;
        s.WakeUpMessage = defaults.WakeUpMessage;
        s.StartupBlockerAlertSoundEnabled = defaults.StartupBlockerAlertSoundEnabled;
        s.BlockerAlertSoundEnabled = defaults.BlockerAlertSoundEnabled;
        s.PeriodicBlockerScanEnabled = defaults.PeriodicBlockerScanEnabled;
        s.PeriodicBlockerScanIntervalSeconds = defaults.PeriodicBlockerScanIntervalSeconds;
        s.CheckForUpdatesOnStartup = defaults.CheckForUpdatesOnStartup;
        s.CustomProtectedProcesses = UserSettings.SanitizeProcesses(defaults.CustomProtectedProcesses);

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

    private void BrowseCustomExeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Application (.exe) to Protect",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            if (dialog.ShowDialog() == true)
            {
                string cleanName = Path.GetFileNameWithoutExtension(dialog.FileName).ToLowerInvariant();
                AppendProtectedProcess(cleanName);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open file picker: {ex.Message}", "Browse App", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PickRunningAppButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var runningApps = Process.GetProcesses()
                .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                .Select(p => p.ProcessName.ToLowerInvariant())
                .Where(name => !name.Equals("smartpowerscheduler", StringComparison.OrdinalIgnoreCase)
                            && !name.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            if (runningApps.Count == 0)
            {
                MessageBox.Show("No active desktop applications with visible windows detected.", "Pick Running App", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var menu = new ContextMenu();
            foreach (var app in runningApps)
            {
                var item = new MenuItem { Header = $"⚡ {app}.exe" };
                string processName = app;
                item.Click += (_, _) => AppendProtectedProcess(processName);
                menu.Items.Add(item);
            }

            menu.PlacementTarget = PickRunningAppButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to scan active apps: {ex.Message}", "Pick Running App", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AppendProtectedProcess(string cleanName)
    {
        if (string.IsNullOrWhiteSpace(cleanName)) return;
        var existing = CustomProtectedAppsInput.Text
            .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Path.GetFileNameWithoutExtension(p.Trim()).ToLowerInvariant())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        if (!existing.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
        {
            existing.Add(cleanName);
            CustomProtectedAppsInput.Text = string.Join(", ", existing);
        }
    }

    private void RelaunchAdminButton_Click(object sender, RoutedEventArgs e)
    {
        SleepBlockerService.RelaunchAsAdministrator();
    }

    private void OpenGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = AppConfig.Instance.App.RepositoryUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private string? _discoveredUpdateUrl;

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_discoveredUpdateUrl))
        {
            UpdateService.OpenReleaseUrl(_discoveredUpdateUrl);
            return;
        }

        CheckForUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking GitHub for updates...";

        var result = await UpdateService.CheckForUpdatesAsync();

        CheckForUpdatesButton.IsEnabled = true;

        if (!result.IsSuccess)
        {
            UpdateStatusText.Text = string.IsNullOrEmpty(result.ErrorMessage)
                ? "Could not check updates."
                : result.ErrorMessage;
            return;
        }

        if (result.IsUpdateAvailable)
        {
            _discoveredUpdateUrl = result.ReleaseUrl;
            UpdateStatusText.Text = $"✨ New version v{result.LatestVersion} available!";
            CheckForUpdatesButton.Content = "⬇️ Download";
        }
        else
        {
            UpdateStatusText.Text = $"You're up to date! (v{result.CurrentVersion})";
        }
    }

    private void RefreshHistoryView()
    {
        var records = HistoryService.GetHistory();
        HistoryTotalStatsText.Text = records.Count switch
        {
            0 => "0 sessions recorded",
            1 => "1 session recorded",
            <= 20 => $"{records.Count} sessions recorded",
            _ => $"{records.Count} sessions recorded (showing latest 20)"
        };

        if (records.Count > 0)
        {
            HistoryEmptyNotice.Visibility = Visibility.Collapsed;
            HistoryScrollViewer.Visibility = Visibility.Visible;
            HistoryItemsList.ItemsSource = records.Take(20).ToList();
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

    private bool _userCustomizedShortcutName;

    private void ShortcutBuilder_Changed(object sender, RoutedEventArgs e) => UpdateShortcutPreview();
    private void ShortcutDurationValueInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateShortcutPreview();
    private void ShortcutNameInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ShortcutNameInput.IsFocused) _userCustomizedShortcutName = true;
    }

    private void UpdateShortcutPreview()
    {
        if (ShortcutActionCombo == null || ShortcutDurationValueInput == null || ShortcutDurationUnitCombo == null || ShortcutCommandPreviewText == null) return;

        var action = ShortcutActionCombo.SelectedIndex switch
        {
            1 => PowerAction.Hibernate,
            2 => PowerAction.Shutdown,
            3 => PowerAction.Restart,
            4 => PowerAction.ForceShutdown,
            5 => PowerAction.ForceRestart,
            6 => PowerAction.Lock,
            _ => PowerAction.Sleep
        };

        int val = int.TryParse(ShortcutDurationValueInput.Text, out int parsedVal) && parsedVal > 0 ? parsedVal : 30;
        int totalSecs = ComposeSeconds(val, ShortcutDurationUnitCombo.SelectedIndex);
        var duration = TimeSpan.FromSeconds(Math.Clamp(totalSecs, 1, 43200));

        bool sound = ShortcutSoundCheck?.IsChecked ?? true;
        string args = CommandLineService.GenerateArguments(action, duration, sound);
        ShortcutCommandPreviewText.Text = $"SmartPowerScheduler.exe {args}";

        if (!_userCustomizedShortcutName && ShortcutNameInput != null)
        {
            string actName = PowerService.GetActionDisplayName(action);
            string durLabel = FormatDuration(duration);
            string soundLabel = sound ? "" : " (Muted)";
            ShortcutNameInput.Text = $"{AppConfig.Instance.App.DisplayName} - {durLabel} {actName}{soundLabel}";
        }
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return ts.Minutes > 0 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{(int)ts.TotalHours}h";
        if (ts.TotalMinutes >= 1) return ts.Seconds > 0 ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s" : $"{(int)ts.TotalMinutes}m";
        return $"{Math.Max(1, (int)ts.TotalSeconds)}s";
    }

    private async void CreateShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateShortcutPreview();
        string name = string.IsNullOrWhiteSpace(ShortcutNameInput.Text) ? "Smart Power Scheduler" : ShortcutNameInput.Text;
        string args = ShortcutCommandPreviewText.Text.Replace("SmartPowerScheduler.exe ", "");
        string desc = $"Smart Power Scheduler: {name}";

        if (ShortcutService.CreateDesktopShortcut(name, args, desc, out string error))
        {
            ShortcutFeedbackText.Text = $"✓ Created on Desktop: '{name}.lnk'";
            ShortcutFeedbackPanel.Visibility = Visibility.Visible;
            await Task.Delay(4000);
            ShortcutFeedbackPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            MessageBox.Show($"Failed to create shortcut: {error}", "Shortcut Creator", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void CopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string fullCommand = $"{ShortcutService.GetExecutablePathQuoted()} {ShortcutCommandPreviewText.Text.Replace("SmartPowerScheduler.exe ", "")}";
            Clipboard.SetText(fullCommand);
            ShortcutFeedbackText.Text = "✓ Full path command copied! (Ready for Win+R, scripts, or hotkeys)";
            ShortcutFeedbackPanel.Visibility = Visibility.Visible;
            await Task.Delay(3000);
            ShortcutFeedbackPanel.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    private void OpenDesktopFolderButton_Click(object sender, RoutedEventArgs e) => ShortcutService.OpenDesktopFolder();

    private void UpdateStreamDeckStatus()
    {
        string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Elgato\StreamDeck\Plugins\com.appsphinx.smartpowerscheduler.sdPlugin");
        bool ok = Directory.Exists(p);
        StreamDeckStatusText.Text = ok ? "● Elgato Stream Deck: Installed & Ready" : "○ Elgato Stream Deck: Not Installed";
        StreamDeckStatusText.Foreground = ok ? (System.Windows.Media.Brush)FindResource("StartBrush") : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        StreamDeckDescText.Text = ok ? "Plugin detected. Control countdowns directly from your keypad." : "Run Setup installer to enable the Stream Deck integration.";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Closed?.Invoke();
    }
}

