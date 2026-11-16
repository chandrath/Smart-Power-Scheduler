using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmartPowerScheduler.Config;
using SmartPowerScheduler.Core;
using SmartPowerScheduler.Core.Services;

namespace SmartPowerScheduler.UI.Controls;

public partial class CountdownDisplay : UserControl
{
    private Storyboard? _urgencyPulseStoryboard;
    private Storyboard? _dotBlinkStoryboard;
    private bool _isUrgent;
    private bool _isDotBlinking;
    private int _lastSoundSecond = -1;
    private TimerState _currentState = TimerState.Stopped;

    public event Action<TimeSpan>? CustomDurationSet;
    public event Action<TimeSpan>? DurationPreviewChanged;

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
        _currentState = state;

        // 1. Format Time Display
        string formattedTime = remaining.TotalHours >= 1
            ? remaining.ToString(@"hh\:mm\:ss")
            : remaining.ToString(@"mm\:ss");

        TimeText.Text = formattedTime;
        if (TimeEditBox.Visibility != Visibility.Visible)
        {
            TimeEditBox.Text = formattedTime;
        }

        // 2. Configure Action Typography (Pure Text, No Pill!)
        ConfigureActionTypography(action, state);

        // 3. Header & Status (Big Header is completely hidden when stopped!)
        if (state == TimerState.Running)
        {
            TopHeaderText.Text = "YOUR PC WILL BE";
            TopHeaderText.Visibility = Visibility.Visible;
            StatusSubText.Text = $"Scheduled {GetActionFriendlyName(action)}";
            TimeText.Cursor = Cursors.Arrow;
            TimeText.ToolTip = null;
        }
        else
        {
            TopHeaderText.Text = string.Empty;
            TopHeaderText.Visibility = Visibility.Collapsed;
            StatusSubText.Text = "Ready to schedule";
            TimeText.Cursor = Cursors.Hand;
            TimeText.ToolTip = "Click to type custom duration";
        }

        // 4. Sound Tick Timing (Default: < 60s, or Entire, or < 10s)
        int tickTrigger = App.Settings.TickTriggerSeconds;
        bool shouldTick = state == TimerState.Running && remaining.TotalSeconds > 0 &&
                          (tickTrigger <= 0 || remaining.TotalSeconds <= tickTrigger);

        if (shouldTick)
        {
            int currentSecond = (int)Math.Ceiling(remaining.TotalSeconds);
            if (currentSecond != _lastSoundSecond)
            {
                _lastSoundSecond = currentSecond;
                SoundService.PlayTick();
            }
        }
        else if (state != TimerState.Running)
        {
            _lastSoundSecond = -1;
        }

        // 5. Live Blinking Dot (< 60s while running)
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

        // 6. Ambient Red Urgency Pulse (<= 30s while running)
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

    private void ConfigureActionTypography(PowerAction action, TimerState state)
    {
        if (state == TimerState.Paused)
        {
            TopHeaderText.Text = "COUNTDOWN PAUSED";
            TopHeaderText.Visibility = Visibility.Visible;
            ActionLabelText.Text = "⚠️ BLOCKED";
            ActionLabelText.Foreground = (Brush)FindResource("UrgencyBrush");
            ProgressTrackRing.Stroke = (Brush)FindResource("UrgencyBrush");
            return;
        }

        if (state == TimerState.Running)
        {
            TopHeaderText.Text = "YOUR PC WILL BE";
            TopHeaderText.Visibility = Visibility.Visible;
            ActionLabelText.Text = action switch
            {
                PowerAction.Sleep => "🌙 ASLEEP",
                PowerAction.Hibernate => "❄️ HIBERNATED",
                PowerAction.Shutdown => "🛑 SHUT DOWN",
                PowerAction.Restart => "🔄 RESTARTED",
                PowerAction.ForceShutdown => "⚡ FORCE SHUT DOWN",
                PowerAction.ForceRestart => "⚡ FORCE RESTARTED",
                PowerAction.Lock => "🔒 LOCKED",
                _ => "🌙 ASLEEP"
            };
        }
        else
        {
            TopHeaderText.Text = string.Empty;
            TopHeaderText.Visibility = Visibility.Collapsed;
            ActionLabelText.Text = action switch
            {
                PowerAction.Sleep => "🌙 Sleep Mode",
                PowerAction.Hibernate => "❄️ Hibernate Mode",
                PowerAction.Shutdown => "🛑 Shutdown Mode",
                PowerAction.Restart => "🔄 Restart Mode",
                PowerAction.ForceShutdown => "⚡ Force Shutdown",
                PowerAction.ForceRestart => "⚡ Force Restart",
                PowerAction.Lock => "🔒 Lock Mode",
                _ => "🌙 Sleep Mode"
            };
        }

        // Set Colors
        switch (action)
        {
            case PowerAction.Sleep:
                ActionLabelText.Foreground = (Brush)FindResource("AccentBrush");
                ProgressTrackRing.Stroke = (Brush)FindResource("AccentBrush");
                break;

            case PowerAction.Shutdown:
            case PowerAction.ForceShutdown:
                ActionLabelText.Foreground = (Brush)FindResource("UrgencyBrush");
                ProgressTrackRing.Stroke = (Brush)FindResource("UrgencyBrush");
                break;

            case PowerAction.Restart:
            case PowerAction.ForceRestart:
                var amberBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                ActionLabelText.Foreground = amberBrush;
                ProgressTrackRing.Stroke = amberBrush;
                break;

            case PowerAction.Hibernate:
                var iceBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                ActionLabelText.Foreground = iceBrush;
                ProgressTrackRing.Stroke = iceBrush;
                break;

            case PowerAction.Lock:
                var lockBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247));
                ActionLabelText.Foreground = lockBrush;
                ProgressTrackRing.Stroke = lockBrush;
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
        PowerAction.Lock => "Lock PC",
        _ => "Sleep"
    };

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

    public void StartInlineEdit()
    {
        if (_currentState == TimerState.Running) return; // Disallow edit while running
        TimeText.Visibility = Visibility.Collapsed;
        TimeEditBox.Visibility = Visibility.Visible;
        TimeEditBox.Focus();
        TimeEditBox.SelectAll();
    }

    public void CommitIfEditing()
    {
        if (TimeEditBox.Visibility == Visibility.Visible)
        {
            CommitTimeEdit();
        }
    }

    private void TimeText_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentState != TimerState.Running)
        {
            StartInlineEdit();
        }
    }

    private void TimeEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TryParseDuration(TimeEditBox.Text.Trim(), out var preview))
        {
            DurationPreviewChanged?.Invoke(preview);
        }
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

    public void CancelInlineEdit()
    {
        CancelTimeEdit();
    }

    private void CancelTimeEdit()
    {
        TimeEditBox.Visibility = Visibility.Collapsed;
        TimeText.Visibility = Visibility.Visible;
    }

    public static bool TryParseDuration(string input, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim().ToLowerInvariant();

        // 1. Colon-separated formats: mm:ss or hh:mm:ss
        if (input.Contains(':'))
        {
            var parts = input.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int s))
            {
                if (m >= 0 && s >= 0 && (m > 0 || s > 0))
                {
                    duration = TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(s);
                    return true;
                }
            }
            else if (parts.Length == 3 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m3) && int.TryParse(parts[2], out int s3))
            {
                if (h >= 0 && m3 >= 0 && s3 >= 0 && (h > 0 || m3 > 0 || s3 > 0))
                {
                    duration = TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m3) + TimeSpan.FromSeconds(s3);
                    return true;
                }
            }
        }

        // 2. Unit formats: "90s", "5m", "1h 30m"
        var matches = System.Text.RegularExpressions.Regex.Matches(input, @"(\d+)\s*([hms])");
        if (matches.Count > 0)
        {
            int totalSeconds = 0;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                int val = int.Parse(match.Groups[1].Value);
                string unit = match.Groups[2].Value;
                if (unit == "h") totalSeconds += val * 3600;
                else if (unit == "m") totalSeconds += val * 60;
                else if (unit == "s") totalSeconds += val;
            }
            if (totalSeconds > 0)
            {
                duration = TimeSpan.FromSeconds(totalSeconds);
                return true;
            }
        }

        // 3. Plain integer: treat as minutes
        if (int.TryParse(input, out int minutes) && minutes > 0)
        {
            duration = TimeSpan.FromMinutes(minutes);
            return true;
        }

        return false;
    }
}
