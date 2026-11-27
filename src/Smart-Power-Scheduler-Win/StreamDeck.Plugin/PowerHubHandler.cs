using System.Collections.Concurrent;
using System.Diagnostics;

namespace SmartPowerScheduler.StreamDeck;

/// <summary>
/// Handles long-press detection and Power Hub profile switching.
/// Maintains clean separation from core Program.cs for best DX and zero regression.
/// </summary>
public static class PowerHubHandler
{
    private const int DefaultLongPressThresholdMs = 350;
    private const string PowerHubProfileName = "Smart Power Scheduler";

    private sealed record PressState(CancellationTokenSource Cts, Stopwatch Watch, string? Device, Action<object> SendEvent, Action<string> Log);
    private static readonly ConcurrentDictionary<string, PressState> _activePresses = new();

    /// <summary>
    /// Processes a keyDown event. If long-press is enabled for this key,
    /// starts a countdown timer to switch to the PowerHub profile.
    /// Otherwise executes the standard short-tap action immediately.
    /// </summary>
    public static void OnKeyDown(
        string context,
        string? device,
        KeyConfig config,
        Action executeShortTap,
        Action<object> sendEvent,
        Action<string> log)
    {
        if (!config.LongPressEnabled)
        {
            executeShortTap();
            return;
        }

        CancelActivePress(context);

        var cts = new CancellationTokenSource();
        var state = new PressState(cts, Stopwatch.StartNew(), device, sendEvent, log);
        _activePresses[context] = state;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DefaultLongPressThresholdMs, cts.Token);
                if (!cts.IsCancellationRequested && _activePresses.TryRemove(context, out _))
                {
                    log($"Long-press triggered on context={context}, device={device}. Switching to profile '{PowerHubProfileName}'");
                    sendEvent(new { @event = "showOk", context });
                    SwitchToProfile(context, device, PowerHubProfileName, sendEvent);
                }
            }
            catch (OperationCanceledException)
            {
                // Key was released before threshold - handled in OnKeyUp
            }
            catch (Exception ex)
            {
                log($"Error during long-press evaluation: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Processes a keyUp event. Uses exact elapsed time so a release right at
    /// the threshold never races with Task.Delay and misfires the timer.
    /// </summary>
    public static void OnKeyUp(string context, Action executeShortTap)
    {
        if (_activePresses.TryRemove(context, out var state))
        {
            long elapsedMs = state.Watch.ElapsedMilliseconds;
            state.Cts.Cancel();
            state.Cts.Dispose();

            if (elapsedMs >= DefaultLongPressThresholdMs)
            {
                state.Log($"Long-press confirmed on keyUp ({elapsedMs}ms) for context={context}. Switching to profile '{PowerHubProfileName}'");
                SwitchToProfile(context, state.Device, PowerHubProfileName, state.SendEvent);
                return;
            }

            executeShortTap();
        }
    }

    /// <summary>
    /// Handles back navigation to the previous Stream Deck profile.
    /// </summary>
    public static void OnBackAction(string context, string? device, Action<object> sendEvent, Action<string> log)
    {
        log($"Back action triggered on context={context}, device={device}. Restoring previous profile.");
        SwitchToProfile(context, device, "", sendEvent);
    }

    /// <summary>
    /// Cleanup when a key disappears.
    /// </summary>
    public static void Cleanup(string context)
    {
        CancelActivePress(context);
    }

    private static void SwitchToProfile(string context, string? device, string profileName, Action<object> sendEvent)
    {
        var payload = new
        {
            @event = "switchToProfile",
            context = Program.PluginUuid ?? context,
            device = device,
            payload = new
            {
                profile = profileName
            }
        };

        sendEvent(payload);
    }

    private static void CancelActivePress(string context)
    {
        if (_activePresses.TryRemove(context, out var existing))
        {
            try
            {
                existing.Cts.Cancel();
                existing.Cts.Dispose();
            }
            catch { }
        }
    }
}
