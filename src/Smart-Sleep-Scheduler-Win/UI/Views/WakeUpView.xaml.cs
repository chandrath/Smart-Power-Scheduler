using System.Windows;
using System.Windows.Controls;

namespace SmartSleepScheduler.UI.Views;

public partial class WakeUpView : UserControl
{
    private int _durationMinutes = 1;

    public event Action<TimeSpan>? StartRequested;
    public event Action? ExitRequested;

    public WakeUpView()
    {
        InitializeComponent();
    }

    public void Initialize(string title, string message, int initialMinutes)
    {
        TitleText.Text = string.IsNullOrWhiteSpace(title)
            ? "Welcome Back, Sleepyhead! 😴"
            : title;

        SubtitleText.Text = string.IsNullOrWhiteSpace(message)
            ? "Your PC just enjoyed a refreshing power nap."
            : message;

        _durationMinutes = Math.Clamp(initialMinutes > 0 ? initialMinutes : 1, 1, 720);
        UpdateDisplay();
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_durationMinutes > 1)
        {
            _durationMinutes--;
            UpdateDisplay();
        }
    }

    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_durationMinutes < 720)
        {
            _durationMinutes++;
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        DurationDisplayText.Text = _durationMinutes >= 60
            ? TimeSpan.FromMinutes(_durationMinutes).ToString(@"hh\:mm")
            : TimeSpan.FromMinutes(_durationMinutes).ToString(@"mm\:ss");
    }

    private void StartAgainButton_Click(object sender, RoutedEventArgs e)
    {
        StartRequested?.Invoke(TimeSpan.FromMinutes(_durationMinutes));
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke();
    }
}

