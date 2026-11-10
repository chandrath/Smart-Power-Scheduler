using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private bool _blockerWarningDismissed;
    private string? _currentBlockerName;
    private int _lastMilestoneChecked = -1;

    public MainWindow()
    {
        InitializeComponent();

        _readyDuration = TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds);

        _timerService.Tick += OnTimerTick;
        _timerService.Completed += OnTimerCompleted;
        _timerService.StateChanged += OnTimerStateChanged;
        CountdownControl.CustomDurationSet += OnCustomDurationSet;
        CountdownControl.DurationPreviewChanged += OnDurationPreviewChanged;
        SettingsPanel.Closed += () => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsPanel.SettingsChanged += OnSettingsChanged;
        SettingsPanel.HistoryRequested += ShowHistoryView;
        HistoryControl.Closed += () => HistoryOverlay.Visibility = Visibility.Collapsed;
        WakeUpControl.StartRequested += OnWakeUpStartRequested;
        WakeUpControl.ExitRequested += () => Close();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HeaderTitleText.Text = AppConfig.Instance.App.DisplayName;

        WindowBackdropService.EnableMicaOrAcrylic(this, ThemeService.Instance.IsDarkActual);
        ThemeService.Instance.ThemeChanged += isDark => WindowBackdropService.EnableMicaOrAcrylic(this, isDark);

        Topmost = App.Settings.AlwaysOnTop;
        UpdatePinVisual();
        UpdateSoundVisual();

        CheckForSleepBlockers(isStartup: true);
        Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(MemoryService.TrimWorkingSet));

        if (App.Settings.AutoStartOnLaunch)
        {
            int launchSeconds = App.Settings.AutoStartDurationSeconds > 0 ? App.Settings.AutoStartDurationSeconds : 60;
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
            HistoryService.LogSessionResumed(DateTime.Now);
            Dispatcher.Invoke(ShowWakeUpView);
        }
    }

    private void ShowWakeUpView()
    {
        int initialMins = Math.Max(1, App.Settings.AutoStartDurationSeconds > 0 ? App.Settings.AutoStartDurationSeconds / 60 : 1);
        WakeUpControl.Initialize(App.Settings.WakeUpTitle, App.Settings.WakeUpMessage, initialMins);
        WakeUpOverlay.Visibility = Visibility.Visible;
    }

    private void ShowHistoryView()
    {
        HistoryControl.RefreshData();
        HistoryOverlay.Visibility = Visibility.Visible;
    }

    private void OnWakeUpStartRequested(TimeSpan duration)
    {
        WakeUpOverlay.Visibility = Visibility.Collapsed;
        _lastMilestoneChecked = -1;
        _blockerWarningDismissed = false;
        CheckForSleepBlockers();
        _readyDuration = duration;
        _timerService.Start(duration);
    }

    private void OnTimerTick(TimeSpan remaining, double progressFraction)
    {
        if (_timerService.State == TimerState.Running)
        {
            UpdateDisplay(remaining);

            int remainingSecs = (int)Math.Ceiling(remaining.TotalSeconds);
            if (remainingSecs != _lastMilestoneChecked)
            {
                bool shouldCheck = false;
                if (remainingSecs == 5)
                {
                    shouldCheck = true; // Final 5-second sanity check before 0
                }
                else if (App.Settings.PeriodicBlockerScanEnabled)
                {
                    int interval = Math.Max(2, App.Settings.PeriodicBlockerScanIntervalSeconds);
                    shouldCheck = (remainingSecs % interval == 0);
                }
                else
                {
                    var milestones = AppConfig.Instance.Defaults.BlockerCheckMilestonesSeconds;
                    shouldCheck = Array.IndexOf(milestones, remainingSecs) >= 0;
                }

                if (shouldCheck)
                {
                    _lastMilestoneChecked = remainingSecs;
                    CheckForSleepBlockers();
                }
            }
        }
    }

    private void OnTimerStateChanged(TimerState state)
    {
        if (state == TimerState.Running)
        {
            PrimaryActionButton.Content = "STOP (Esc)";
            PrimaryActionButton.Style = (Style)FindResource("DangerButtonStyle");
            PrimaryActionButton.ToolTip = "Stop Countdown (Shortcut: Esc)";

            MinusOneButton.IsEnabled = PlusOneButton.IsEnabled = PencilButton.IsEnabled = ResetButton.IsEnabled = GearButton.IsEnabled = false;
            MinusOneButton.Opacity = PlusOneButton.Opacity = PencilButton.Opacity = ResetButton.Opacity = GearButton.Opacity = 0.35;
            GearButton.ToolTip = "Settings locked during active countdown";

            UpdateDisplay(_timerService.RemainingTime);
        }
        else if (state == TimerState.Paused)
        {
            PrimaryActionButton.Content = "RESUME";
            PrimaryActionButton.Style = (Style)FindResource("AccentButtonStyle");
            PrimaryActionButton.ToolTip = "Resume Countdown";

            MinusOneButton.IsEnabled = PlusOneButton.IsEnabled = PencilButton.IsEnabled = ResetButton.IsEnabled = GearButton.IsEnabled = false;
            MinusOneButton.Opacity = PlusOneButton.Opacity = PencilButton.Opacity = ResetButton.Opacity = GearButton.Opacity = 0.35;

            UpdateDisplay(_timerService.RemainingTime);
        }
        else
        {
            PrimaryActionButton.Style = (Style)FindResource("StartButtonStyle");
            PrimaryActionButton.ToolTip = "Start Countdown";

            MinusOneButton.IsEnabled = PlusOneButton.IsEnabled = PencilButton.IsEnabled = ResetButton.IsEnabled = GearButton.IsEnabled = true;
            MinusOneButton.Opacity = PlusOneButton.Opacity = PencilButton.Opacity = ResetButton.Opacity = GearButton.Opacity = 1.0;
            GearButton.ToolTip = "Settings";

            _blockerWarningDismissed = false;
            BlockerWarningPill.Visibility = Visibility.Collapsed;
            BlockerDialogOverlay.Visibility = Visibility.Collapsed;

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
        if (ts.TotalHours >= 1) return ts.Minutes > 0 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{(int)ts.TotalHours}h";
        if (ts.TotalMinutes >= 1) return ts.Seconds > 0 ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s" : $"{(int)ts.TotalMinutes}m";
        return $"{Math.Max(1, (int)ts.TotalSeconds)}s";
    }

    private void OnTimerCompleted()
    {
        _blockerWarningDismissed = false;
        SoundService.PlayCompletion();
        var action = App.Settings.SelectedPowerAction;
        string recordId = HistoryService.LogSessionTriggered(action, _readyDuration);
        if (!PowerService.ExecuteAction(action, out string error) && !string.IsNullOrWhiteSpace(error))
        {
            HistoryService.LogSessionFailed(recordId, error);
            MessageBox.Show($"Failed to execute {action}: {error}", "Smart Sleep Scheduler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (action == PowerAction.Sleep)
        {
            ShowWakeUpView();
        }
    }

    private void UpdateDisplay(TimeSpan remaining) =>
        CountdownControl.UpdateTime(remaining, App.Settings.SelectedPowerAction, _timerService.State);

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

    private void OnDurationPreviewChanged(TimeSpan preview)
    {
        if (_timerService.State == TimerState.Stopped)
        {
            _readyDuration = preview;
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

    private void SoundButton_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.SoundEnabled = !App.Settings.SoundEnabled;
        App.Settings.Save();
        UpdateSoundVisual();
        if (App.Settings.SoundEnabled) SoundService.PlayToggleTest();
    }

    private void UpdateSoundVisual()
    {
        bool sound = App.Settings.SoundEnabled;
        SoundButton.Opacity = sound ? 1.0 : 0.45;
        SoundIconText.Text = sound ? "🔊" : "🔇";
        SoundIconText.Foreground = (Brush)FindResource(sound ? "AccentBrush" : "TextSecondaryBrush");
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
        PinIconText.Foreground = (Brush)FindResource(Topmost ? "AccentBrush" : "TextSecondaryBrush");
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

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
        if (_timerService.State == TimerState.Running) return;
        SettingsPanel.LoadValues();
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.State == TimerState.Running)
        {
            _timerService.Stop();
        }
        else if (_timerService.State == TimerState.Paused)
        {
            _timerService.Resume();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(_currentBlockerName) && !_blockerWarningDismissed)
            {
                ShowBlockerDialog("⚠️ Blocker Detected Before Start");
                return;
            }

            CountdownControl.CommitIfEditing();
            _lastMilestoneChecked = -1;
            _blockerWarningDismissed = false;
            _timerService.Start(_readyDuration);
            CheckForSleepBlockers();
        }
    }

    private void CheckForSleepBlockers(bool isStartup = false)
    {
        var action = App.Settings.SelectedPowerAction;
        if (action == PowerAction.ForceShutdown || action == PowerAction.ForceRestart) return;

        Task.Run(() =>
        {
            var (hasBlocker, blockerName) = SleepBlockerService.DetectActiveBlocker();
            Dispatcher.Invoke(() =>
            {
                if (hasBlocker)
                {
                    _currentBlockerName = blockerName;
                    BlockerWarningText.Text = $"Blocker: {blockerName}";
                    BlockerWarningPill.Visibility = Visibility.Visible;

                    if (_timerService.State == TimerState.Running && !_blockerWarningDismissed)
                    {
                        _timerService.Pause();
                        if (App.Settings.BlockerAlertSoundEnabled) SoundService.PlayBlockerAlert();
                        ShowBlockerDialog("⚠️ Timer Paused: Blocker Detected");
                    }
                    else if (_timerService.State == TimerState.Stopped && !_blockerWarningDismissed)
                    {
                        if (isStartup && App.Settings.StartupBlockerAlertSoundEnabled)
                            SoundService.PlayBlockerAlert();
                        ShowBlockerDialog("⚠️ Blocker Detected: Action Required");
                    }
                }
                else
                {
                    _currentBlockerName = null;
                    _blockerWarningDismissed = false;
                    BlockerWarningPill.Visibility = Visibility.Collapsed;
                    BlockerDialogOverlay.Visibility = Visibility.Collapsed;
                }
            });
        });
    }

    private void ShowBlockerDialog(string title)
    {
        string actName = PowerService.GetActionDisplayName(App.Settings.SelectedPowerAction);
        string blocker = _currentBlockerName ?? "Blocker";
        BlockerDialogTitle.Text = title;
        BlockerDialogMessage.Text = $"Application '{blocker}' is actively preventing scheduled {actName}.\n\nNormal {actName} will be vetoed by Windows. Choose an action below:";
        CloseBlockerAndResumeButton.Content = $"🛑 Close {blocker} & {(_timerService.State == TimerState.Stopped ? "Start" : "Resume")}";
        BlockerDialogOverlay.Visibility = Visibility.Visible;
    }

    private void BlockerWarningPill_MouseDown(object sender, MouseButtonEventArgs e) =>
        ShowBlockerDialog(_timerService.State == TimerState.Paused ? "⚠️ Timer Paused: Blocker Detected" : "⚠️ Blocker Active");

    private void CloseBlockerAndResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentBlockerName))
            SleepBlockerService.TryCloseBlockerProcess(_currentBlockerName);
        _currentBlockerName = null;
        _blockerWarningDismissed = false;
        BlockerDialogOverlay.Visibility = Visibility.Collapsed;
        BlockerWarningPill.Visibility = Visibility.Collapsed;
        if (_timerService.State == TimerState.Paused) _timerService.Resume();
        else if (_timerService.State == TimerState.Stopped) _timerService.Start(_readyDuration);
    }

    private void DismissBlockerWarningButton_Click(object sender, RoutedEventArgs e)
    {
        BlockerDialogOverlay.Visibility = Visibility.Collapsed;
        _blockerWarningDismissed = true;
        BlockerWarningPill.Visibility = Visibility.Visible;
        if (_timerService.State == TimerState.Paused) _timerService.Resume();
        else if (_timerService.State == TimerState.Stopped) _timerService.Start(_readyDuration);
    }

    private void StopBlockerTimerButton_Click(object sender, RoutedEventArgs e)
    {
        BlockerDialogOverlay.Visibility = Visibility.Collapsed;
        _timerService.Stop();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.State != TimerState.Stopped) return;
        CountdownControl.CancelInlineEdit();
        _readyDuration = TimeSpan.FromSeconds(App.Settings.DefaultTimerDurationSeconds);
        AdjustDuration(TimeSpan.Zero);
    }

    private void AdjustDuration(TimeSpan delta)
    {
        _readyDuration = _readyDuration.Add(delta);
        UpdateDisplay(_readyDuration);
        UpdateStartButtonContent();
        UpdateMinusButtonState(_readyDuration);
    }

    private void MinusOneButton_Click(object sender, RoutedEventArgs e) { if (_timerService.State == TimerState.Stopped && _readyDuration > TimeSpan.FromMinutes(1)) AdjustDuration(TimeSpan.FromMinutes(-1)); }
    private void PlusOneButton_Click(object sender, RoutedEventArgs e) { if (_timerService.State == TimerState.Stopped) AdjustDuration(TimeSpan.FromMinutes(1)); }
    private void PencilButton_Click(object sender, RoutedEventArgs e) { if (_timerService.State == TimerState.Stopped) CountdownControl.StartInlineEdit(); }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (BlockerDialogOverlay.Visibility == Visibility.Visible) { BlockerDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (HistoryOverlay.Visibility == Visibility.Visible) { HistoryOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (WakeUpOverlay.Visibility == Visibility.Visible) { WakeUpOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (SettingsOverlay.Visibility == Visibility.Visible) { SettingsOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (_isFullscreen) { ToggleFullscreen(); e.Handled = true; return; }
            if (_timerService.State == TimerState.Running) { _timerService.Stop(); e.Handled = true; }
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

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized) MemoryService.TrimWorkingSet();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        MemoryService.TrimWorkingSet();
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => Close();
}
