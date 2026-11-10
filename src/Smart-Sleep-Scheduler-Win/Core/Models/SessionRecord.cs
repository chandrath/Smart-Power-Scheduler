using System.Text.Json.Serialization;
using System.Windows;

namespace SmartSleepScheduler.Core.Models;

public sealed class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public PowerAction Action { get; set; } = PowerAction.Sleep;
    public int ScheduledDurationSeconds { get; set; }
    public string Status { get; set; } = "Executed"; // "Initiated", "Completed", "Executed"
    public DateTime? WakeUpTimestamp { get; set; }

    [JsonIgnore]
    public string ActionDisplayName => Action switch
    {
        PowerAction.Sleep => "Sleep",
        PowerAction.Hibernate => "Hibernate",
        PowerAction.Shutdown => "Shutdown",
        PowerAction.Restart => "Restart",
        PowerAction.ForceShutdown => "Force Shutdown",
        PowerAction.ForceRestart => "Force Restart",
        _ => Action.ToString()
    };

    [JsonIgnore]
    public string ActionIcon => Action switch
    {
        PowerAction.Sleep => "🌙",
        PowerAction.Hibernate => "❄️",
        PowerAction.Restart or PowerAction.ForceRestart => "🔄",
        _ => "⚡"
    };

    [JsonIgnore]
    public string FormattedDate => Timestamp.ToString("MMM dd, yyyy • hh:mm tt");

    [JsonIgnore]
    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromSeconds(ScheduledDurationSeconds);
            if (ts.TotalHours >= 1) return ts.Minutes > 0 ? $"{(int)ts.TotalHours}h {ts.Minutes}m timer" : $"{(int)ts.TotalHours}h timer";
            if (ts.TotalMinutes >= 1) return ts.Seconds > 0 ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s timer" : $"{(int)ts.TotalMinutes}m timer";
            return $"{ts.Seconds}s timer";
        }
    }

    [JsonIgnore]
    public string? SleepDurationText
    {
        get
        {
            if (WakeUpTimestamp == null || WakeUpTimestamp <= Timestamp) return null;
            var slept = WakeUpTimestamp.Value - Timestamp;
            if (slept.TotalHours >= 1) return $"Slept {(int)slept.TotalHours}h {slept.Minutes}m";
            if (slept.TotalMinutes >= 1) return $"Slept {(int)slept.TotalMinutes}m";
            return $"Slept {(int)slept.TotalSeconds}s";
        }
    }

    [JsonIgnore]
    public bool HasSleepDuration => !string.IsNullOrEmpty(SleepDurationText);

    [JsonIgnore]
    public Visibility SleepDurationVisibility => HasSleepDuration ? Visibility.Visible : Visibility.Collapsed;
}
