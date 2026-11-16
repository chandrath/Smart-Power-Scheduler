using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartPowerScheduler.Core.Services;

/// <summary>
/// Provides native Windows memory optimization and working set trimming
/// to maintain a clean ~10-15MB idle footprint for Smart Power Scheduler.
/// </summary>
public static class MemoryService
{
    [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    /// <summary>
    /// Forces a non-blocking garbage collection pass and flushes working set pages to standby list,
    /// dropping physical RAM usage immediately.
    /// </summary>
    public static void TrimWorkingSet()
    {
        try
        {
            GC.Collect(1, GCCollectionMode.Optimized, false);
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch
        {
            // Best effort memory trimming
        }
    }
}

