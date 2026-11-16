namespace SmartPowerScheduler.Core.Services;

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using SmartPowerScheduler.Config;

/// <summary>
/// Ultra-lightweight service to inspect active sleep blockers (e.g. Caffeine, Don't Sleep, Awake).
/// Uses zero-allocation Win32 kernel snapshots and elevation detection.
/// </summary>
public static class SleepBlockerService
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static (bool HasBlocker, string? BlockerName) DetectActiveBlocker()
    {
        var knownBlockers = AppConfig.Instance.Defaults.KnownSleepBlockerProcesses;
        var (foundProcess, processName) = ScanKernelProcesses(knownBlockers);

        if (foundProcess)
        {
            return (true, processName);
        }

        // If running as Administrator, perform deep kernel power request inspection
        if (IsAdministrator())
        {
            var (hasKernelLock, reason) = InspectKernelPowerRequests();
            if (hasKernelLock)
            {
                return (true, reason);
            }
        }

        return (false, null);
    }

    private static (bool Found, string? Name) ScanKernelProcesses(string[] knownBlockers)
    {
        IntPtr hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnapshot == IntPtr.Zero || hSnapshot == (IntPtr)(-1))
        {
            return (false, null);
        }

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(hSnapshot, ref entry))
            {
                do
                {
                    string exeName = Path.GetFileNameWithoutExtension(entry.szExeFile).ToLowerInvariant();
                    foreach (var target in knownBlockers)
                    {
                        if (exeName.Contains(target.ToLowerInvariant()))
                        {
                            return (true, Path.GetFileNameWithoutExtension(entry.szExeFile));
                        }
                    }
                } while (Process32Next(hSnapshot, ref entry));
            }
        }
        catch
        {
            // Fail silently without crashing
        }
        finally
        {
            CloseHandle(hSnapshot);
        }

        return (false, null);
    }

    private static (bool HasLock, string? Reason) InspectKernelPowerRequests()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg.exe", "/requests")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(1500);

                // Check if any category contains non-"None." active requests
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed) &&
                        !trimmed.EndsWith(":") &&
                        !trimmed.Equals("None.", StringComparison.OrdinalIgnoreCase))
                    {
                        return (true, trimmed);
                    }
                }
            }
        }
        catch
        {
            // Fail silently
        }

        return (false, null);
    }

    public static bool TryCloseBlockerProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        try
        {
            string cleanName = Path.GetFileNameWithoutExtension(processName);
            var processes = Process.GetProcessesByName(cleanName);
            bool anyTerminated = false;
            foreach (var p in processes)
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(350))
                    {
                        p.Kill();
                    }
                    anyTerminated = true;
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }
            return anyTerminated;
        }
        catch
        {
            return false;
        }
    }

    public static void RelaunchAsAdministrator()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "SmartPowerScheduler.exe",
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(psi);
            Application.Current.Shutdown();
        }
        catch
        {
            // User cancelled UAC prompt
        }
    }
}

