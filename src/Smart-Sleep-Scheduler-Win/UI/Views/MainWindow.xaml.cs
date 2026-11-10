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
    private TimeSpan _readyDuration;

    public MainWindow()
    {
        InitializeComponent();

        _readyDuration = TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds);

        _timerService.Tick += OnTimerTick;
        _timerService.Completed += OnTimerCompleted;
        _timerService.StateChanged += OnTimerStateChanged;

        CountdownControl.CustomDurationSet += OnCustomDurationSet;

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
            _readyDuration = TimeSpan.FromSeconds(launchSeconds);
            _timerService.Start(_readyDuration);
        }
        else
        {
            _readyDuration = TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds);
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
        int initialMins = App.Settings.AutoStartDurationSeconds > 0
            ? Math.Max(1, App.Settings.AutoStartDurationSeconds / 60)
            : 1;

        WakeUpControl.Initialize(App.Settings.WakeUpTitle, App.Settings.WakeUpMessage, initialMins);
        WakeUpOverlay.Visibility = Visibility.Visible;
    }

    private void OnWakeUpStartRequested(TimeSpan duration)
    {
        WakeUpOverlay.Visibility = Visibility.Collapsed;
        _readyDuration = duration;
        _timerService.Start(duration);
    }

    private void OnTimerTick(TimeSpan remaining, double progressFraction)
    {
        UpdateDisplay(remaining);
        UpdateMinusButtonState(remaining);
    }

    private void OnTimerStateChanged(TimerState state)
    {
        if (state == TimerState.Running)
        {
            PrimaryActionButton.Content = "STOP (Esc)";
            PrimaryActionButton.Style = (Style)FindResource("DangerButtonStyle");
            PrimaryActionButton.ToolTip = "Stop Countdown (Shortcut: Esc)";
            UpdateDisplay(_timerService.RemainingTime);
            UpdateMinusButtonState(_timerService.RemainingTime);
        }
        else
        {
            PrimaryActionButton.Style = (Style)FindResource("StartButtonStyle");
            PrimaryActionButton.ToolTip = "Start Countdown";
            UpdateStartButtonContent();
            UpdateDisplay(_readyDuration);
            UpdateMinusButtonState(_readyDuration);
        }
    }

    private void UpdateStartButtonContent()
    {
        string label = FormatShortDuration(_readyDuration);
        PrimaryActionButton.Content = $"START ({label})";
    }

    private void UpdateMinusButtonState(TimeSpan duration)
    {
        bool canSubtract = duration.TotalSeconds > 60;
        MinusOneButton.IsEnabled = canSubtract;
        MinusOneButton.Opacity = canSubtract ? 1.0 : 0.35;
    }

    private static string FormatShortDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return ts.Minutes > 0 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{(int)ts.TotalHours}h";
        }
        if (ts.TotalMinutes >= 1)
        {
            return ts.Seconds > 0 ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s" : $"{(int)ts.TotalMinutes}m";
        }
        return $"{Math.Max(1, (int)ts.TotalSeconds)}s";
    }

    private void OnTimerCompleted()
    {
        var action = App.Settings.SelectedPowerAction;
        bool ok = PowerService.ExecuteAction(action, out string error);
        if (!ok && !string.IsNullOrWhiteSpace(error))
        {
            MessageBox.Show($"Failed to execute {action}: {error}", "Smart Sleep Scheduler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (action == PowerAction.Sleep)
        {
            ShowWakeUpView();
        }
    }

    private void UpdateDisplay(TimeSpan remaining)
    {
        CountdownControl.UpdateTime(remaining, App.Settings.SelectedPowerAction, _timerService.State);
    }

    private void OnCustomDurationSet(TimeSpan newDuration)
    {
        _readyDuration = newDuration;
        if (_timerService.State == TimerState.Running)
        {
            _timerService.Start(newDuration);
        }
        else
        {
            UpdateDisplay(_readyDuration);
            UpdateStartButtonContent();
            UpdateMinusButtonState(_readyDuration);
        }
    }

    private void OnSettingsChanged()
    {
        Topmost = App.Settings.AlwaysOnTop;
        UpdatePinVisual();
        if (_timerService.State == TimerState.Stopped)
        {
            _readyDuration = TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds);
            UpdateStartButtonContent();
            UpdateDisplay(_readyDuration);
            UpdateMinusButtonState(_readyDuration);
        }
        else
        {
            UpdateDisplay(_timerService.RemainingTime);
            UpdateMinusButtonState(_timerService.RemainingTime);
        }
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
        if (_timerService.State == TimerState.Running)
        {
            _timerService.Stop();
        }
        else
        {
            _timerService.Start(_readyDuration);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _readyDuration = TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds);
        if (_timerService.State == TimerState.Running)
        {
            _timerService.Start(_readyDuration);
        }
        else
        {
            UpdateDisplay(_readyDuration);
            UpdateStartButtonContent();
            UpdateMinusButtonState(_readyDuration);
        }
    }

    private void MinusOneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.State == TimerState.Stopped)
        {
            if (_readyDuration > TimeSpan.FromMinutes(1))
            {
                _readyDuration = _readyDuration.Subtract(TimeSpan.FromMinutes(1));
                UpdateDisplay(_readyDuration);
                UpdateStartButtonContent();
                UpdateMinusButtonState(_readyDuration);
            }
        }
        else
        {
            if (_timerService.RemainingTime > TimeSpan.FromMinutes(1))
            {
                _timerService.AddSeconds(-60);
            }
        }
    }

    private void PlusOneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.State == TimerState.Stopped)
        {
            _readyDuration = _readyDuration.Add(TimeSpan.FromMinutes(1));
            UpdateDisplay(_readyDuration);
            UpdateStartButtonContent();
            UpdateMinusButtonState(_readyDuration);
        }
        else
        {
            _timerService.AddSeconds(60);
        }
    }

    private void PencilButton_Click(object sender, RoutedEventArgs e)
    {
        CountdownControl.StartInlineEdit();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

            if (_timerService.State == TimerState.Running)
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
