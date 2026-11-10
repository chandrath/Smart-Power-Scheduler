using System.Diagnostics;
using System.Runtime.InteropServices;
using SmartSleepScheduler.Core;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Handles execution of system power state transitions (Sleep, Hibernate, Shutdown, Restart).
/// Default action is strict safe Sleep with zero forced termination.
/// </summary>
public static class PowerService
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemPowerState(bool fSuspend, bool fForce);

    public static bool ExecuteAction(PowerAction action, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            switch (action)
            {
                case PowerAction.Sleep:
                    // Request Windows Sleep (Standby S3/Modern Standby) safely.
                    // Do not hibernate, do not force critical suspension, allow wake events.
                    bool sleepSuccess = SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
                    if (!sleepSuccess)
                    {
                        // Fallback attempt via SetSystemPowerState
                        SetSystemPowerState(fSuspend: true, fForce: false);
                    }
                    return true;

                case PowerAction.Hibernate:
                    // Explicit hibernation requested by user in advanced settings
                    return SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false);

                case PowerAction.Shutdown:
                    // Graceful shutdown (prompts unsaved work if needed)
                    RunCommand("shutdown.exe", "/s /t 0");
                    return true;

                case PowerAction.Restart:
                    // Graceful reboot
                    RunCommand("shutdown.exe", "/r /t 0");
                    return true;

                case PowerAction.ForceShutdown:
                    // Force shutdown immediately
                    RunCommand("shutdown.exe", "/s /f /t 0");
                    return true;

                case PowerAction.ForceRestart:
                    // Force restart immediately
                    RunCommand("shutdown.exe", "/r /f /t 0");
                    return true;

                default:
                    errorMessage = $"Unknown power action: {action}";
                    return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static void RunCommand(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var process = Process.Start(psi);
    }
}

