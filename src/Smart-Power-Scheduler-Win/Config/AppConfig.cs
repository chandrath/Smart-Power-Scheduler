using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartPowerScheduler.Core;

namespace SmartPowerScheduler.Config;

/// <summary>
/// Single Source of Truth (SSOT) configuration model loaded from DevConfig.json.
/// Centralizes all metadata, default preferences, and system thresholds.
/// </summary>
public sealed class AppConfig
{
    private static AppConfig? _instance;
    public static AppConfig Instance => _instance ??= Load();

    [JsonPropertyName("App")]
    public AppMetadata App { get; set; } = new();

    [JsonPropertyName("Defaults")]
    public AppDefaults Defaults { get; set; } = new();

    [JsonPropertyName("StreamDeck")]
    public StreamDeckConfig StreamDeck { get; set; } = new();

    public static AppConfig Load()
    {
        try
        {
            // 1. Try loading from file directly adjacent to executable or project
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "DevConfig.json");
            if (File.Exists(localPath))
            {
                string json = File.ReadAllText(localPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded != null) return loaded;
            }

            // 2. Try loading from embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("SmartPowerScheduler.Config.DevConfig.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fallback gracefully to default values below if file I/O is restricted
        }

        return new AppConfig();
    }
}

public sealed class AppMetadata
{
    public string Name { get; set; } = "SmartPowerScheduler";
    public string DisplayName { get; set; } = "Smart Power Scheduler";
    public string Description { get; set; } = "A clean, modern Windows smart power timer and scheduler with Fluent translucent design.";
    public string Version { get; set; } = "0.9.0.3";
    public string Vendor { get; set; } = "Appsphinx";
    public string Developer { get; set; } = "Shree";
    public string Copyright { get; set; } = "Copyright © 2026 Appsphinx";
    public string AppId { get; set; } = "9E2F9F24-B1F6-4A0B-88EE-C2A8389DB67B";
    public string RepositoryUrl { get; set; } = "https://github.com/chandrath/Smart-Sleep-Scheduler";
}

public sealed class AppDefaults
{
    public string Theme { get; set; } = "Dark";
    public bool AlwaysOnTop { get; set; } = true;
    public bool SoundEnabled { get; set; } = false;
    public int SoundVolume { get; set; } = 50;
    public string SoundScheme { get; set; } = "Modern";
    public int TickTriggerSeconds { get; set; } = 30;
    public int DefaultTimerDurationSeconds { get; set; } = 60;
    public bool AutoStartOnLaunch { get; set; } = true;
    public int AutoStartDurationSeconds { get; set; } = 60;
    public string DefaultPowerAction { get; set; } = "Sleep";
    public bool AllowForceActions { get; set; } = false;
    public int MinTimerSeconds { get; set; } = 1;
    public int MaxTimerSeconds { get; set; } = 43200;
    public int UrgencyWarningThresholdSeconds { get; set; } = 30;
    public string WakeUpTitle { get; set; } = "Welcome Back, Sleepyhead! 😴";
    public string WakeUpMessage { get; set; } = "Your PC just enjoyed a refreshing power nap.";
    public int[] BlockerCheckMilestonesSeconds { get; set; } = [900, 300, 60, 30];
    public bool PeriodicBlockerScanEnabled { get; set; } = true;
    public int PeriodicBlockerScanIntervalSeconds { get; set; } = 5;
    public bool BlockerAlertSoundEnabled { get; set; } = true;
    public bool StartupBlockerAlertSoundEnabled { get; set; } = true;
    public int MaxHistoryRecords { get; set; } = 100000;
    public string[] KnownSleepBlockerProcesses { get; set; } =
    [
        "caffeine", "dontsleep", "powertoys.awake", "mousejiggler", "nosleep", "stayawake", "amphetamine"
    ];
    public bool CustomProtectedAppsIsBeta { get; set; } = true;
    public string[] CustomProtectedProcesses { get; set; } = [];
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string UpdateApiEndpoint { get; set; } = "https://api.github.com/repos/chandrath/Smart-Sleep-Scheduler/releases/latest";
}

public sealed class StreamDeckConfig
{
    public string PluginUUID { get; set; } = "com.appsphinx.smartpowerscheduler.sdPlugin";
    public string ActionTimerUUID { get; set; } = "com.appsphinx.smartpowerscheduler.action.timer";
    public string PluginName { get; set; } = "Smart Power Scheduler";
    public string DefaultAction { get; set; } = "Sleep";
    public string DefaultDuration { get; set; } = "30s";
    public int DefaultDurationMinutes { get; set; } = 30;
}

