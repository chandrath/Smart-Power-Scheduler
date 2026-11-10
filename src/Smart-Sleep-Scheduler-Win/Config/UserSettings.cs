using System.IO;
using System.Text.Json;
using SmartSleepScheduler.Core;

namespace SmartSleepScheduler.Config;

/// <summary>
/// User runtime preferences persisted to %LOCALAPPDATA%\SmartSleepScheduler\settings.json.
/// Uses AppConfig defaults as the Single Source of Truth when initial preferences do not exist.
/// </summary>
public sealed class UserSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConfig.Instance.App.Name
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    public AppTheme Theme { get; set; }
    public bool AlwaysOnTop { get; set; }
    public int DefaultTimerDurationSeconds { get; set; }
    public bool AutoStartOnLaunch { get; set; }
    public int AutoStartDurationSeconds { get; set; }
    public PowerAction SelectedPowerAction { get; set; }
    public bool AllowForceActions { get; set; }
    public string WakeUpTitle { get; set; }
    public string WakeUpMessage { get; set; }

    public UserSettings()
    {
        var defaults = AppConfig.Instance.Defaults;
        Theme = Enum.TryParse<AppTheme>(defaults.Theme, true, out var t) ? t : AppTheme.Dark;
        AlwaysOnTop = defaults.AlwaysOnTop;
        DefaultTimerDurationSeconds = defaults.DefaultTimerDurationSeconds;
        AutoStartOnLaunch = defaults.AutoStartOnLaunch;
        AutoStartDurationSeconds = defaults.AutoStartDurationSeconds;
        SelectedPowerAction = Enum.TryParse<PowerAction>(defaults.DefaultPowerAction, true, out var a) ? a : PowerAction.Sleep;
        AllowForceActions = defaults.AllowForceActions;
        WakeUpTitle = defaults.WakeUpTitle;
        WakeUpMessage = defaults.WakeUpMessage;
    }

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Fall back to clean defaults if corrupted
        }

        var newSettings = new UserSettings();
        newSettings.Save();
        return newSettings;
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silently ignore or log if filesystem is temporarily locked
        }
    }
}

