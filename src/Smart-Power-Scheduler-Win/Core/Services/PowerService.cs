using System.Diagnostics;
using System.Runtime.InteropServices;
using SmartPowerScheduler.Core;

namespace SmartPowerScheduler.Core.Services;

/// <summary>
/// Handles execution of system power state transitions (Sleep, Hibernate, Shutdown, Restart).
/// Default action is strict safe Sleep with zero forced termination.
/// </summary>
public static class PowerService
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("powrprof.dll")]
    private static extern bool IsPwrHibernateAllowed();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemPowerState(bool fSuspend, bool fForce);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    public static bool IsHibernateSupported()
    {
        try { return IsPwrHibernateAllowed(); } catch { return false; }
    }

    public static bool ExecuteAction(PowerAction action, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            switch (action)
            {
                case PowerAction.Sleep:
                    bool sleepSuccess = SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
                    if (!sleepSuccess) SetSystemPowerState(fSuspend: true, fForce: false);
                    return true;

                case PowerAction.Hibernate:
                    if (!IsHibernateSupported())
                    {
                        // Safe fallback to Sleep if hibernate is disabled in Windows
                        return SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
                    }
                    return SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false);

                case PowerAction.Lock:
                    return LockWorkStation();

                case PowerAction.Shutdown:
                    RunCommand("shutdown.exe", "/s /t 0");
                    return true;

                case PowerAction.Restart:
                    RunCommand("shutdown.exe", "/r /t 0");
                    return true;

                case PowerAction.ForceShutdown:
                    RunCommand("shutdown.exe", "/s /f /t 0");
                    return true;

                case PowerAction.ForceRestart:
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

    public static string GetActionDisplayName(PowerAction action) => action switch
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

    public static PowerAction GetForceEquivalent(PowerAction action) => action switch
    {
        PowerAction.Shutdown => PowerAction.ForceShutdown,
        PowerAction.Restart => PowerAction.ForceRestart,
        _ => action
    };
}

