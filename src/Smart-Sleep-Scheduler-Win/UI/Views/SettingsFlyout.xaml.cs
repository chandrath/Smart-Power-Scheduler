using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core;
using SmartSleepScheduler.Core.Services;

namespace SmartSleepScheduler.UI.Views;

public partial class SettingsFlyout : UserControl
{
    private static readonly Regex NumericRegex = new("[^0-9]+");

    public event Action? Closed;
    public event Action? SettingsChanged;

    public SettingsFlyout()
    {
        InitializeComponent();
        LoadValues();
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

        // Defaults duration
        DecomposeSeconds(s.DefaultTimerDurationSeconds, out int defVal, out int defUnitIdx);
        DefaultDurationInput.Text = defVal.ToString();
        DefaultDurationUnitCombo.SelectedIndex = defUnitIdx;

        // Auto start
        AutoStartCheck.IsChecked = s.AutoStartOnLaunch;
        DecomposeSeconds(s.AutoStartDurationSeconds, out int autoVal, out int autoUnitIdx);
        AutoStartDurationInput.Text = autoVal.ToString();
        AutoStartDurationUnitCombo.SelectedIndex = autoUnitIdx;

        // Always on top
        AlwaysOnTopCheck.IsChecked = s.AlwaysOnTop;

        // Theme
        ThemeCombo.SelectedIndex = (int)s.Theme;

        // Power Action
        PowerActionCombo.SelectedIndex = (int)s.SelectedPowerAction;
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
        if (ForceWarningBanner == null) return;
        var action = (PowerAction)PowerActionCombo.SelectedIndex;
        ForceWarningBanner.Visibility = (action == PowerAction.ForceShutdown || action == PowerAction.ForceRestart)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var theme = (AppTheme)ThemeCombo.SelectedIndex;
        ThemeService.Instance.ApplyTheme(theme);
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
        s.Theme = (AppTheme)ThemeCombo.SelectedIndex;
        s.SelectedPowerAction = (PowerAction)PowerActionCombo.SelectedIndex;

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
        s.DefaultTimerDurationSeconds = defaults.DefaultTimerDurationSeconds;
        s.AutoStartOnLaunch = defaults.AutoStartOnLaunch;
        s.AutoStartDurationSeconds = defaults.AutoStartDurationSeconds;
        s.SelectedPowerAction = Enum.TryParse<PowerAction>(defaults.DefaultPowerAction, true, out var a) ? a : PowerAction.Sleep;
        s.AllowForceActions = defaults.AllowForceActions;

        s.Save();
        LoadValues();
        ThemeService.Instance.ApplyTheme(s.Theme);
        SettingsChanged?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Closed?.Invoke();
    }
}

