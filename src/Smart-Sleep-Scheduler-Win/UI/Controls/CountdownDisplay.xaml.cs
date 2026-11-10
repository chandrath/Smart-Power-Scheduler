using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core;

namespace SmartSleepScheduler.UI.Controls;

public partial class CountdownDisplay : UserControl
{
    private Storyboard? _urgencyPulseStoryboard;
    private bool _isUrgent;

    public CountdownDisplay()
    {
        InitializeComponent();
        SetupPulseAnimation();
    }

    private void SetupPulseAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = 0.1,
            To = 0.55,
            Duration = new Duration(TimeSpan.FromSeconds(0.9)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(animation, GlowCircle);
        Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));

        _urgencyPulseStoryboard = new Storyboard();
        _urgencyPulseStoryboard.Children.Add(animation);
    }

    public void UpdateTime(TimeSpan remaining, PowerAction action, TimerState state)
    {
        // 1. Format Display Time
        TimeText.Text = remaining.TotalHours >= 1
            ? remaining.ToString(@"hh\:mm\:ss")
            : remaining.ToString(@"mm\:ss");

        // 2. Format Action Label
        ActionLabel.Text = action switch
        {
            PowerAction.Sleep => "🌙 SLEEP",
            PowerAction.Hibernate => "❄️ HIBERNATE",
            PowerAction.Shutdown => "🛑 SHUTDOWN",
            PowerAction.Restart => "🔄 RESTART",
            PowerAction.ForceShutdown => "⚡ FORCE SHUTDOWN",
            PowerAction.ForceRestart => "⚡ FORCE RESTART",
            _ => "🌙 SLEEP"
        };

        // 3. Format Sub-Status
        StatusSubText.Text = state switch
        {
            TimerState.Running => $"Scheduled {action}",
            TimerState.Paused => "Countdown Paused",
            TimerState.Stopped => "Timer Stopped",
            _ => "Ready"
        };

        // 4. Handle Urgency Animation
        int threshold = AppConfig.Instance.Defaults.UrgencyWarningThresholdSeconds;
        bool shouldBeUrgent = state == TimerState.Running && remaining.TotalSeconds > 0 && remaining.TotalSeconds <= threshold;

        if (shouldBeUrgent && !_isUrgent)
        {
            SetUrgentState(true);
        }
        else if (!shouldBeUrgent && _isUrgent)
        {
            SetUrgentState(false);
        }
    }

    private void SetUrgentState(bool urgent)
    {
        _isUrgent = urgent;
        if (urgent)
        {
            TimeText.Foreground = (Brush)FindResource("UrgencyBrush");
            _urgencyPulseStoryboard?.Begin(this, true);
        }
        else
        {
            TimeText.Foreground = (Brush)FindResource("TextPrimaryBrush");
            _urgencyPulseStoryboard?.Stop(this);
            GlowCircle.Opacity = 0;
        }
    }
}

