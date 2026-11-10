using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core;

namespace SmartSleepScheduler.UI.Controls;

public partial class CountdownDisplay : UserControl
{
    private Storyboard? _urgencyPulseStoryboard;
    private Storyboard? _dotBlinkStoryboard;
    private bool _isUrgent;
    private bool _isDotBlinking;

    public event Action<TimeSpan>? CustomDurationSet;

    public CountdownDisplay()
    {
        InitializeComponent();
        SetupAnimations();
    }

    private void SetupAnimations()
    {
        // 1. Ambient Glow Animation
        var glowAnim = new DoubleAnimation
        {
            From = 0.1,
            To = 0.55,
            Duration = new Duration(TimeSpan.FromSeconds(0.9)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(glowAnim, GlowCircle);
        Storyboard.SetTargetProperty(glowAnim, new PropertyPath(OpacityProperty));

        _urgencyPulseStoryboard = new Storyboard();
        _urgencyPulseStoryboard.Children.Add(glowAnim);

        // 2. Red Live Dot Blink Animation (< 60s)
        var dotAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.15,
            Duration = new Duration(TimeSpan.FromSeconds(0.65)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(dotAnim, LiveBlinkDot);
        Storyboard.SetTargetProperty(dotAnim, new PropertyPath(OpacityProperty));

        _dotBlinkStoryboard = new Storyboard();
        _dotBlinkStoryboard.Children.Add(dotAnim);
    }

    public void UpdateTime(TimeSpan remaining, PowerAction action, TimerState state)
    {
        // 1. Format Time Display
        string formattedTime = remaining.TotalHours >= 1
            ? remaining.ToString(@"hh\:mm\:ss")
            : remaining.ToString(@"mm\:ss");

        TimeText.Text = formattedTime;
        if (TimeEditBox.Visibility != Visibility.Visible)
        {
            TimeEditBox.Text = formattedTime;
        }

        // 2. Configure Action Badge & Theming
        ConfigureActionBadge(action);

        // 3. Format Header and Sub-Status
        if (state == TimerState.Running || state == TimerState.Paused)
        {
            TopHeaderText.Text = "YOUR PC WILL BE";
            InPrefixText.Visibility = Visibility.Visible;
            StatusSubText.Text = state == TimerState.Running
                ? $"Scheduled {GetActionFriendlyName(action)}"
                : "Countdown Paused";
        }
        else
        {
            TopHeaderText.Text = "READY TO SCHEDULE";
            InPrefixText.Visibility = Visibility.Collapsed;
            StatusSubText.Text = "Timer Ready";
        }

        // 4. Live Blinking Dot (< 60s while running)
        bool shouldBlinkDot = state == TimerState.Running && remaining.TotalSeconds > 0 && remaining.TotalSeconds <= 60;
        if (shouldBlinkDot && !_isDotBlinking)
        {
            _isDotBlinking = true;
            LiveBlinkDot.Visibility = Visibility.Visible;
            _dotBlinkStoryboard?.Begin(this, true);
        }
        else if (!shouldBlinkDot && _isDotBlinking)
        {
            _isDotBlinking = false;
            LiveBlinkDot.Visibility = Visibility.Collapsed;
            _dotBlinkStoryboard?.Stop(this);
        }

        // 5. Ambient Red Urgency Pulse (<= 30s while running)
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

    private void ConfigureActionBadge(PowerAction action)
    {
        switch (action)
        {
            case PowerAction.Sleep:
                ActionBadgeText.Text = "🌙 SLEEP";
                ActionBadgeText.Foreground = (Brush)FindResource("AccentBrush");
                ActionBadge.Background = (Brush)FindResource("ButtonBackgroundBrush");
                ActionBadge.BorderBrush = (Brush)FindResource("CardBorderBrush");
                ProgressTrackRing.Stroke = (Brush)FindResource("AccentBrush");
                break;

            case PowerAction.Shutdown:
            case PowerAction.ForceShutdown:
                ActionBadgeText.Text = action == PowerAction.ForceShutdown ? "⚡ FORCE SHUTDOWN" : "🛑 SHUTDOWN";
                ActionBadgeText.Foreground = (Brush)FindResource("UrgencyBrush");
                ActionBadge.Background = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68));
                ActionBadge.BorderBrush = (Brush)FindResource("UrgencyBrush");
                ProgressTrackRing.Stroke = (Brush)FindResource("UrgencyBrush");
                break;

            case PowerAction.Restart:
            case PowerAction.ForceRestart:
                ActionBadgeText.Text = action == PowerAction.ForceRestart ? "⚡ FORCE RESTART" : "🔄 RESTART";
                var amberBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                ActionBadgeText.Foreground = amberBrush;
                ActionBadge.Background = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                ActionBadge.BorderBrush = amberBrush;
                ProgressTrackRing.Stroke = amberBrush;
                break;

            case PowerAction.Hibernate:
                ActionBadgeText.Text = "❄️ HIBERNATE";
                var iceBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                ActionBadgeText.Foreground = iceBrush;
                ActionBadge.Background = (Brush)FindResource("ButtonBackgroundBrush");
                ActionBadge.BorderBrush = (Brush)FindResource("CardBorderBrush");
                ProgressTrackRing.Stroke = iceBrush;
                break;
        }
    }

    private static string GetActionFriendlyName(PowerAction action) => action switch
    {
        PowerAction.Sleep => "Sleep",
        PowerAction.Hibernate => "Hibernate",
        PowerAction.Shutdown => "Shutdown",
        PowerAction.Restart => "Restart",
        PowerAction.ForceShutdown => "Force Shutdown",
        PowerAction.ForceRestart => "Force Restart",
        _ => "Sleep"
    };

    private static string GetActionVerb(PowerAction action) => action switch
    {
        PowerAction.Sleep => "SLEEP",
        PowerAction.Hibernate => "HIBERNATE",
        PowerAction.Shutdown or PowerAction.ForceShutdown => "SHUT DOWN",
        PowerAction.Restart or PowerAction.ForceRestart => "RESTART",
        _ => "SLEEP"
    };

    private void SetUrgentState(bool urgent)
    {
        _isUrgent = urgent;
        if (urgent)
        {
            TimeText.Foreground = (Brush)FindResource("UrgencyBrush");
            InPrefixText.Foreground = (Brush)FindResource("UrgencyBrush");
            _urgencyPulseStoryboard?.Begin(this, true);
        }
        else
        {
            TimeText.Foreground = (Brush)FindResource("TextPrimaryBrush");
            InPrefixText.Foreground = (Brush)FindResource("TextSecondaryBrush");
            _urgencyPulseStoryboard?.Stop(this);
            GlowCircle.Opacity = 0;
        }
    }

    public void StartInlineEdit()
    {
        TimeText.Visibility = Visibility.Collapsed;
        TimeEditBox.Visibility = Visibility.Visible;
        TimeEditBox.Focus();
        TimeEditBox.SelectAll();
    }

    private void TimeText_MouseDown(object sender, MouseButtonEventArgs e)
    {
        StartInlineEdit();
    }

    private void TimeEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitTimeEdit();
    }

    private void TimeEditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTimeEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTimeEdit();
            e.Handled = true;
        }
    }

    private void CommitTimeEdit()
    {
        string raw = TimeEditBox.Text.Trim();
        CancelTimeEdit();

        if (TryParseDuration(raw, out var duration))
        {
            CustomDurationSet?.Invoke(duration);
        }
    }

    private void CancelTimeEdit()
    {
        TimeEditBox.Visibility = Visibility.Collapsed;
        TimeText.Visibility = Visibility.Visible;
    }

    private static bool TryParseDuration(string input, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;

        // Try format mm:ss or hh:mm:ss
        if (TimeSpan.TryParse(input, out var ts) && ts > TimeSpan.Zero)
        {
            duration = ts;
            return true;
        }

        // Try single number (interpreted as minutes)
        if (int.TryParse(input, out int minutes) && minutes > 0)
        {
            duration = TimeSpan.FromMinutes(minutes);
            return true;
        }

        return false;
    }
}
