namespace SmartSleepScheduler.Core;

public enum PowerAction
{
    Sleep,
    Hibernate,
    Shutdown,
    Restart,
    ForceShutdown,
    ForceRestart
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

