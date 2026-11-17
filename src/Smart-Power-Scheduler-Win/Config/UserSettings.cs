using System.IO;
using System.Text.Json;
using SmartPowerScheduler.Core;

namespace SmartPowerScheduler.Config;

/// <summary>
/// User runtime preferences persisted to %APPDATA%\Appsphinx\SmartPowerScheduler\settings.json.
/// Uses AppConfig defaults as the Single Source of Truth when initial preferences do not exist.
/// </summary>
public sealed class UserSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConfig.Instance.App.Vendor,
        AppConfig.Instance.App.Name
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    public AppTheme Theme { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool SoundEnabled { get; set; }
    public int SoundVolume { get; set; }
    public string SoundScheme { get; set; }
    public int TickTriggerSeconds { get; set; }
    public int DefaultTimerDurationSeconds { get; set; }
    public bool AutoStartOnLaunch { get; set; }
    public int AutoStartDurationSeconds { get; set; }
    public PowerAction SelectedPowerAction { get; set; }
    public bool AllowForceActions { get; set; }
    public string WakeUpTitle { get; set; }
    public string WakeUpMessage { get; set; }
    public bool PeriodicBlockerScanEnabled { get; set; }
    public int PeriodicBlockerScanIntervalSeconds { get; set; }
    public bool BlockerAlertSoundEnabled { get; set; }
    public bool StartupBlockerAlertSoundEnabled { get; set; }
    public bool CheckForUpdatesOnStartup { get; set; }
    public string[] CustomProtectedProcesses { get; set; } = [];

    public UserSettings()
    {
        var defaults = AppConfig.Instance.Defaults;
        Theme = Enum.TryParse<AppTheme>(defaults.Theme, true, out var t) ? t : AppTheme.Dark;
        AlwaysOnTop = defaults.AlwaysOnTop;
        SoundEnabled = defaults.SoundEnabled;
        SoundVolume = defaults.SoundVolume > 0 ? defaults.SoundVolume : 50;
        SoundScheme = !string.IsNullOrWhiteSpace(defaults.SoundScheme) ? defaults.SoundScheme : "Modern";
        TickTriggerSeconds = defaults.TickTriggerSeconds;
        DefaultTimerDurationSeconds = defaults.DefaultTimerDurationSeconds;
        AutoStartOnLaunch = defaults.AutoStartOnLaunch;
        AutoStartDurationSeconds = defaults.AutoStartDurationSeconds;
        SelectedPowerAction = Enum.TryParse<PowerAction>(defaults.DefaultPowerAction, true, out var a) ? a : PowerAction.Sleep;
        AllowForceActions = defaults.AllowForceActions;
        WakeUpTitle = defaults.WakeUpTitle;
        WakeUpMessage = defaults.WakeUpMessage;
        PeriodicBlockerScanEnabled = defaults.PeriodicBlockerScanEnabled;
        PeriodicBlockerScanIntervalSeconds = defaults.PeriodicBlockerScanIntervalSeconds > 0 ? defaults.PeriodicBlockerScanIntervalSeconds : 5;
        BlockerAlertSoundEnabled = defaults.BlockerAlertSoundEnabled;
        StartupBlockerAlertSoundEnabled = defaults.StartupBlockerAlertSoundEnabled;
        CheckForUpdatesOnStartup = defaults.CheckForUpdatesOnStartup;
        CustomProtectedProcesses = SanitizeProcesses(defaults.CustomProtectedProcesses);
    }

    public static string[] SanitizeProcesses(IEnumerable<string>? processes)
    {
        if (processes == null) return [];
        return processes
            .Select(p => Path.GetFileNameWithoutExtension(p?.Trim() ?? "").ToLowerInvariant())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToArray();
    }

    public static string GetSettingsDirectory() => SettingsDirectory;

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null)
                {
                    bool needSave = false;
                    if (settings.DefaultTimerDurationSeconds == 180)
                    {
                        settings.DefaultTimerDurationSeconds = 60;
                        needSave = true;
                    }
                    if (settings.SoundVolume == 80)
                    {
                        settings.SoundVolume = 50;
                        needSave = true;
                    }
                    if (settings.TickTriggerSeconds == 60)
                    {
                        settings.TickTriggerSeconds = 30;
                        needSave = true;
                    }
                    if (settings.PeriodicBlockerScanIntervalSeconds <= 0 || settings.PeriodicBlockerScanIntervalSeconds == 30)
                    {
                        settings.PeriodicBlockerScanIntervalSeconds = 5;
                        settings.PeriodicBlockerScanEnabled = true;
                        needSave = true;
                    }
                    settings.CustomProtectedProcesses = SanitizeProcesses(settings.CustomProtectedProcesses);
                    if (needSave)
                    {
                        settings.Save();
                    }
                    return settings;
                }
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

