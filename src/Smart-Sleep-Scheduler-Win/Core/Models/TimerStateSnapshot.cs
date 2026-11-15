namespace SmartSleepScheduler.Core.Models;

/// <summary>
/// Snapshot of the active timer state, broadcasted over IPC for companion integrations like Elgato Stream Deck.
/// </summary>
public sealed class TimerStateSnapshot
{
    public string State { get; set; } = "Stopped";
    public string Action { get; set; } = "Sleep";
    public int RemainingSeconds { get; set; }
    public int TotalSeconds { get; set; }

    public static TimerStateSnapshot Stopped(string action = "Sleep", int totalSeconds = 0) => new()
    {
        State = "Stopped",
        Action = action,
        RemainingSeconds = 0,
        TotalSeconds = totalSeconds
    };

    public static TimerStateSnapshot Running(string action, int remainingSeconds, int totalSeconds) => new()
    {
        State = "Running",
        Action = action,
        RemainingSeconds = remainingSeconds,
        TotalSeconds = totalSeconds
    };

    public static TimerStateSnapshot Paused(string action, int remainingSeconds, int totalSeconds) => new()
    {
        State = "Paused",
        Action = action,
        RemainingSeconds = remainingSeconds,
        TotalSeconds = totalSeconds
    };

    public static TimerStateSnapshot Completed(string action = "Sleep") => new()
    {
        State = "Completed",
        Action = action,
        RemainingSeconds = 0,
        TotalSeconds = 0
    };
}
