using System.Windows;
using System.Windows.Controls;

namespace SmartPowerScheduler.UI.Views;

public partial class WakeUpView : UserControl
{
    private TimeSpan _duration = TimeSpan.FromMinutes(1);

    public event Action<TimeSpan>? StartRequested;
    public event Action? ExitRequested;

    public WakeUpView()
    {
        InitializeComponent();
    }

    public void Initialize(string title, string message, TimeSpan sessionDuration)
    {
        TitleText.Text = string.IsNullOrWhiteSpace(title)
            ? "Welcome Back, Sleepyhead! 😴"
            : title;

        SubtitleText.Text = string.IsNullOrWhiteSpace(message)
            ? "Your PC just enjoyed a refreshing power nap."
            : message;

        int secs = (int)sessionDuration.TotalSeconds;
        if (secs < 10) secs = 60;
        _duration = TimeSpan.FromSeconds(Math.Clamp(secs, 10, 43200));
        UpdateDisplay();
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_duration <= TimeSpan.FromSeconds(10)) return;

        if (_duration <= TimeSpan.FromMinutes(1))
            _duration = _duration.Subtract(TimeSpan.FromSeconds(10));
        else if (_duration <= TimeSpan.FromMinutes(5))
            _duration = _duration.Subtract(TimeSpan.FromSeconds(30));
        else
            _duration = _duration.Subtract(TimeSpan.FromMinutes(1));

        if (_duration < TimeSpan.FromSeconds(10)) _duration = TimeSpan.FromSeconds(10);
        UpdateDisplay();
    }

    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_duration >= TimeSpan.FromHours(12)) return;

        if (_duration < TimeSpan.FromMinutes(1))
            _duration = _duration.Add(TimeSpan.FromSeconds(10));
        else if (_duration < TimeSpan.FromMinutes(5))
            _duration = _duration.Add(TimeSpan.FromSeconds(30));
        else
            _duration = _duration.Add(TimeSpan.FromMinutes(1));

        if (_duration > TimeSpan.FromHours(12)) _duration = TimeSpan.FromHours(12);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        DurationDisplayText.Text = _duration.TotalHours >= 1
            ? _duration.ToString(@"hh\:mm\:ss")
            : _duration.ToString(@"mm\:ss");
    }

    private void StartAgainButton_Click(object sender, RoutedEventArgs e)
    {
        StartRequested?.Invoke(_duration);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke();
    }
}

