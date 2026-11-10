using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SmartSleepScheduler.Config;
using SmartSleepScheduler.Core;
using SmartSleepScheduler.Core.Services;

namespace SmartSleepScheduler.UI.Views;

public partial class MainWindow : Window
{
    private readonly TimerService _timerService = new();
    private bool _isFullscreen;
    private Rect _restoreBounds;
    private bool _sleepTriggeredByApp;

    public MainWindow()
    {
        InitializeComponent();

        _timerService.Tick += OnTimerTick;
        _timerService.Completed += OnTimerCompleted;
        _timerService.StateChanged += OnTimerStateChanged;

        SettingsPanel.Closed += () => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsPanel.SettingsChanged += OnSettingsChanged;

        WakeUpControl.StartRequested += OnWakeUpStartRequested;
        WakeUpControl.ExitRequested += () => Close();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
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
            OnTimerStateChanged(TimerState.Stopped);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Dispatcher.Invoke(ShowWakeUpView);
        }
    }

    private void ShowWakeUpView()
    {
        _sleepTriggeredByApp = false;
        int initialMins = App.Settings.AutoStartDurationSeconds > 0
            ? Math.Max(1, App.Settings.AutoStartDurationSeconds / 60)
            : 1;

        WakeUpControl.Initialize(App.Settings.WakeUpTitle, App.Settings.WakeUpMessage, initialMins);
        WakeUpOverlay.Visibility = Visibility.Visible;
    }

    private void OnWakeUpStartRequested(TimeSpan duration)
    {
        WakeUpOverlay.Visibility = Visibility.Collapsed;
        _timerService.Start(duration);
    }

    private void OnTimerTick(TimeSpan remaining, double progressFraction)
    {
        UpdateDisplay(remaining);
    }

    private void OnTimerStateChanged(TimerState state)
    {
        if (state == TimerState.Running)
        {
            PauseResumeButton.Content = "Pause";
            PauseResumeButton.IsEnabled = true;
            PrimaryActionButton.Content = "STOP (Esc)";
            PrimaryActionButton.Style = (Style)FindResource("DangerButtonStyle");
            PrimaryActionButton.ToolTip = "Stop Countdown (Shortcut: Esc)";
            UpdateDisplay(_timerService.RemainingTime);
        }
        else if (state == TimerState.Paused)
        {
            PauseResumeButton.Content = "Resume";
            PauseResumeButton.IsEnabled = true;
            PrimaryActionButton.Content = "STOP (Esc)";
            PrimaryActionButton.Style = (Style)FindResource("DangerButtonStyle");
            PrimaryActionButton.ToolTip = "Stop Countdown (Shortcut: Esc)";
            UpdateDisplay(_timerService.RemainingTime);
        }
        else
        {
            PauseResumeButton.Content = "Pause";
            PauseResumeButton.IsEnabled = false;
            PrimaryActionButton.Content = "START";
            PrimaryActionButton.Style = (Style)FindResource("AccentButtonStyle");
            PrimaryActionButton.ToolTip = "Start Countdown";
            UpdateDisplay(TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds));
        }
    }

    private void OnTimerCompleted()
    {
        var action = App.Settings.SelectedPowerAction;
        _sleepTriggeredByApp = (action == PowerAction.Sleep);

        bool ok = PowerService.ExecuteAction(action, out string error);
        if (!ok && !string.IsNullOrWhiteSpace(error))
        {
            MessageBox.Show($"Failed to execute {action}: {error}", "Smart Sleep Scheduler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (action == PowerAction.Sleep)
        {
            // Transition directly into WakeUpView when sleep finishes or on return
            ShowWakeUpView();
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
        UpdateDisplay(_timerService.State == TimerState.Stopped
            ? TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds)
            : _timerService.RemainingTime);
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

    private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.State == TimerState.Running || _timerService.State == TimerState.Paused)
        {
            _timerService.Stop();
        }
        else
        {
            _timerService.Start(TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds));
        }
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
            if (WakeUpOverlay.Visibility == Visibility.Visible)
            {
                WakeUpOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }

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

            if (_timerService.State == TimerState.Running || _timerService.State == TimerState.Paused)
            {
                _timerService.Stop();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && SettingsOverlay.Visibility != Visibility.Visible && WakeUpOverlay.Visibility != Visibility.Visible)
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
