using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core;
using SmartSleepScheduler.Core.Services;

namespace SmartSleepScheduler.UI.Views;

public partial class MainWindow : Window
{
    private readonly TimerService _timerService = new();
    private bool _isFullscreen;
    private Rect _restoreBounds;

    public MainWindow()
    {
        InitializeComponent();

        _timerService.Tick += OnTimerTick;
        _timerService.Completed += OnTimerCompleted;
        _timerService.StateChanged += OnTimerStateChanged;

        SettingsPanel.Closed += () => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsPanel.SettingsChanged += OnSettingsChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HeaderTitleText.Text = AppConfig.Instance.App.DisplayName;

        // Apply backdrop (Mica / Acrylic)
        WindowBackdropService.EnableMicaOrAcrylic(this, ThemeService.Instance.IsDarkActual);
        ThemeService.Instance.ThemeChanged += isDark =>
        {
            WindowBackdropService.EnableMicaOrAcrylic(this, isDark);
        };

        // Apply Always on top preference
        Topmost = App.Settings.AlwaysOnTop;
        UpdatePinVisual();

        // Auto-start on launch
        if (App.Settings.AutoStartOnLaunch)
        {
            int launchSeconds = App.Settings.AutoStartDurationSeconds > 0
                ? App.Settings.AutoStartDurationSeconds
                : 60;
            _timerService.Start(TimeSpan.FromSeconds(launchSeconds));
        }
        else
        {
            UpdateDisplay(TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds));
        }
    }

    private void OnTimerTick(TimeSpan remaining, double progressFraction)
    {
        UpdateDisplay(remaining);
    }

    private void OnTimerStateChanged(TimerState state)
    {
        PauseResumeButton.Content = state switch
        {
            TimerState.Running => "Pause",
            TimerState.Paused => "Resume",
            _ => "Start"
        };
        UpdateDisplay(_timerService.RemainingTime);
    }

    private void OnTimerCompleted()
    {
        var action = App.Settings.SelectedPowerAction;
        bool ok = PowerService.ExecuteAction(action, out string error);
        if (!ok && !string.IsNullOrWhiteSpace(error))
        {
            MessageBox.Show($"Failed to execute {action}: {error}", "Smart Sleep Scheduler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateDisplay(TimeSpan remaining)
    {
        CountdownControl.UpdateTime(remaining, App.Settings.SelectedPowerAction, _timerService.State);
    }

    private void OnSettingsChanged()
    {
        Topmost = App.Settings.AlwaysOnTop;
        UpdatePinVisual();
        UpdateDisplay(_timerService.RemainingTime);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.AlwaysOnTop = !App.Settings.AlwaysOnTop;
        Topmost = App.Settings.AlwaysOnTop;
        App.Settings.Save();
        UpdatePinVisual();
    }

    private void UpdatePinVisual()
    {
        PinButton.Opacity = Topmost ? 1.0 : 0.45;
        PinIconText.Foreground = Topmost
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        if (_isFullscreen)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            FullscreenIconText.Text = "🗗";
        }
        else
        {
            WindowState = WindowState.Normal;
            if (_restoreBounds.Width > 0 && _restoreBounds.Height > 0)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }
            FullscreenIconText.Text = "⛶";
        }
    }

    private void GearButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.LoadValues();
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _timerService.Stop();
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.State == TimerState.Running)
        {
            _timerService.Pause();
        }
        else if (_timerService.State == TimerState.Paused)
        {
            _timerService.Resume();
        }
        else
        {
            _timerService.Start(TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds));
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _timerService.Start(TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds));
    }

    private void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int seconds))
        {
            if (_timerService.State == TimerState.Stopped)
            {
                _timerService.Start(TimeSpan.FromSeconds(seconds));
            }
            else
            {
                _timerService.AddSeconds(seconds);
            }
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (SettingsOverlay.Visibility == Visibility.Visible)
            {
                SettingsOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }

            if (_isFullscreen)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }

            // ESC shortcut stops timer
            _timerService.Stop();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && SettingsOverlay.Visibility != Visibility.Visible)
        {
            PauseResumeButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFullscreen();
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed && !_isFullscreen)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
