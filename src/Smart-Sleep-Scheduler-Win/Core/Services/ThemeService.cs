using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SmartSleepScheduler.Core;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Manages application theming (Dark, Light, Auto) and dynamically updates WPF application resources.
/// Listens to Windows OS theme changes when set to Auto mode.
/// </summary>
public sealed class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public bool IsDarkActual { get; private set; } = true;

    public event Action<bool>? ThemeChanged;

    private ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        bool isDark = theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            AppTheme.Auto => DetectWindowsDarkTheme(),
            _ => true
        };

        IsDarkActual = isDark;
        UpdateResources(isDark);
        ThemeChanged?.Invoke(isDark);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (CurrentTheme == AppTheme.Auto && e.Category == UserPreferenceCategory.General)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyTheme(AppTheme.Auto);
            });
        }
    }

    private static bool DetectWindowsDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0;
            }
        }
        catch
        {
            // Fall back to dark theme on error
        }
        return true;
    }

    private static void UpdateResources(bool isDark)
    {
        var dict = Application.Current?.Resources;
        if (dict == null) return;

        if (isDark)
        {
            dict["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)); // Deep slate with opacity
            dict["CardBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(180, 30, 41, 59));
            dict["CardBorderBrush"] = new SolidColorBrush(Color.FromArgb(90, 71, 85, 105));
            dict["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            dict["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            dict["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            dict["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(160, 51, 65, 85));
            dict["ButtonHoverBrush"] = new SolidColorBrush(Color.FromArgb(200, 71, 85, 105));
            dict["AccentBrush"] = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Vibrant cyan
            dict["UrgencyBrush"] = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Pulse red
        }
        else
        {
            dict["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(238, 248, 250, 252)); // Crisp light slate
            dict["CardBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255));
            dict["CardBorderBrush"] = new SolidColorBrush(Color.FromArgb(120, 203, 213, 225));
            dict["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            dict["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            dict["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            dict["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(180, 241, 245, 249));
            dict["ButtonHoverBrush"] = new SolidColorBrush(Color.FromArgb(220, 226, 232, 240));
            dict["AccentBrush"] = new SolidColorBrush(Color.FromRgb(2, 132, 199)); // Sky blue
            dict["UrgencyBrush"] = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Deep red
        }
    }
}

