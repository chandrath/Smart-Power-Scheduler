using System.Windows.Threading;

namespace SmartPowerScheduler.Core.Services;

/// <summary>
/// High-precision countdown timer anchored against absolute UTC timestamps.
/// Immune to timing drift when the window is minimized or throttled by Windows OS.
/// </summary>
public sealed class TimerService
{
    private readonly DispatcherTimer _timer;
    private DateTime _targetTime;
    private TimeSpan _totalDuration;
    private TimeSpan _remainingOnPause;

    public TimerState State { get; private set; } = TimerState.Stopped;
    public TimeSpan RemainingTime { get; private set; } = TimeSpan.Zero;
    public TimeSpan TotalDuration => _totalDuration;

    public event Action<TimeSpan, double>? Tick;
    public event Action? Completed;
    public event Action<TimerState>? StateChanged;

    public TimerService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _timer.Tick += OnInternalTick;
    }

    public void Start(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;

        _totalDuration = duration;
        _targetTime = DateTime.UtcNow.Add(duration);
        RemainingTime = duration;

        State = TimerState.Running;
        StateChanged?.Invoke(State);

        _timer.Start();
        Tick?.Invoke(RemainingTime, 1.0);
    }

    public void Stop()
    {
        if (State == TimerState.Stopped) return;

        _timer.Stop();
        State = TimerState.Stopped;
        RemainingTime = TimeSpan.Zero;

        StateChanged?.Invoke(State);
        Tick?.Invoke(RemainingTime, 0.0);
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;

        _timer.Stop();
        _remainingOnPause = _targetTime - DateTime.UtcNow;
        if (_remainingOnPause < TimeSpan.Zero) _remainingOnPause = TimeSpan.Zero;

        State = TimerState.Paused;
        StateChanged?.Invoke(State);
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;

        _targetTime = DateTime.UtcNow.Add(_remainingOnPause);
        State = TimerState.Running;
        StateChanged?.Invoke(State);
        _timer.Start();
    }

    public void AddSeconds(int seconds)
    {
        if (State == TimerState.Running)
        {
            _targetTime = _targetTime.AddSeconds(seconds);
            _totalDuration = _totalDuration.Add(TimeSpan.FromSeconds(seconds));
            CalculateRemaining();
        }
        else if (State == TimerState.Paused)
        {
            _remainingOnPause = _remainingOnPause.Add(TimeSpan.FromSeconds(seconds));
            _totalDuration = _totalDuration.Add(TimeSpan.FromSeconds(seconds));
            RemainingTime = _remainingOnPause;
            double progress = _totalDuration.TotalSeconds > 0
                ? Math.Clamp(RemainingTime.TotalSeconds / _totalDuration.TotalSeconds, 0.0, 1.0)
                : 0.0;
            Tick?.Invoke(RemainingTime, progress);
        }
    }

    private void OnInternalTick(object? sender, EventArgs e)
    {
        if (State != TimerState.Running) return;

        CalculateRemaining();
    }

    private void CalculateRemaining()
    {
        var diff = _targetTime - DateTime.UtcNow;

        if (diff <= TimeSpan.Zero)
        {
            _timer.Stop();
            RemainingTime = TimeSpan.Zero;
            State = TimerState.Stopped;
            StateChanged?.Invoke(State);
            Tick?.Invoke(TimeSpan.Zero, 0.0);
            Completed?.Invoke();
            return;
        }

        RemainingTime = diff;
        double progress = _totalDuration.TotalSeconds > 0
            ? Math.Clamp(RemainingTime.TotalSeconds / _totalDuration.TotalSeconds, 0.0, 1.0)
            : 0.0;

        Tick?.Invoke(RemainingTime, progress);
    }
}

