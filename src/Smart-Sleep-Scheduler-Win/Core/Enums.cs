namespace SmartSleepScheduler.Core;

public enum PowerAction
{
    Sleep,
    Hibernate,
    Shutdown,
    Restart,
    ForceShutdown,
    ForceRestart,
    Lock
}

public enum AppTheme
{
    Dark,
    Light,
    Auto
}

public enum TimerState
{
    Stopped,
    Running,
    Paused
}

public enum TimeUnit
{
    Seconds,
    Minutes,
    Hours
}

