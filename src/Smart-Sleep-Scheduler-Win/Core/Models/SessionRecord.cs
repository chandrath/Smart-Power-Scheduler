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
    public string? DurationBadgeText
    {
        get
        {
            if (WakeUpTimestamp == null || WakeUpTimestamp <= Timestamp) return null;
            var elapsed = WakeUpTimestamp.Value - Timestamp;
            string prefix = Action switch
            {
                PowerAction.Sleep => "Slept",
                PowerAction.Hibernate => "Hibernated",
                PowerAction.Restart or PowerAction.ForceRestart => "Rebooted in",
                _ => "Off for"
            };

            if (Action is PowerAction.Restart or PowerAction.ForceRestart)
            {
                if (elapsed.TotalMinutes >= 1) return $"{prefix} {(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
                return $"{prefix} {(int)Math.Max(1, elapsed.TotalSeconds)}s";
            }

            if (elapsed.TotalDays >= 1)
            {
                int days = (int)elapsed.TotalDays;
                int hours = elapsed.Hours;
                return hours > 0 ? $"{prefix} {days}d {hours}h" : $"{prefix} {days}d";
            }

            if (elapsed.TotalHours >= 1) return $"{prefix} {(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            if (elapsed.TotalMinutes >= 1) return $"{prefix} {(int)elapsed.TotalMinutes}m";
            return $"{prefix} {(int)elapsed.TotalSeconds}s";
        }
    }

    [JsonIgnore]
    public string? SleepDurationText => DurationBadgeText;

    [JsonIgnore]
    public bool HasSleepDuration => !string.IsNullOrEmpty(DurationBadgeText);

    [JsonIgnore]
    public Visibility SleepDurationVisibility => HasSleepDuration ? Visibility.Visible : Visibility.Collapsed;
}
