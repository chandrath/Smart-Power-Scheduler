namespace SmartSleepScheduler.Core.Models;

/// <summary>
/// Session-scoped runtime parameter overrides passed via CLI or desktop shortcuts.
/// These values do NOT alter persistent UserSettings in settings.json.
/// </summary>
public sealed class LaunchOverrides
{
    public PowerAction? Action { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool? SoundEnabled { get; set; }
    public int? SoundVolume { get; set; }
    public bool AutoStart { get; set; } = true;

    public bool HasOverrides => Action != null || Duration != null || SoundEnabled != null || SoundVolume != null;
}

