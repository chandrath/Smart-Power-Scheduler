namespace SmartPowerScheduler.Core.Services;

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using SmartPowerScheduler.Config;

public readonly record struct BlockerScanResult(
    bool HasInhibitor,
    string? DisplayName,
    bool IsCustomApp = false,
    int CustomCount = 0,
    string[]? RunningCustomApps = null
)
{
    public void Deconstruct(out bool hasInhibitor, out string? displayName)
    {
        hasInhibitor = HasInhibitor;
        displayName = DisplayName;
    }
}

/// <summary>
/// Ultra-lightweight service to inspect active sleep blockers (e.g. Caffeine, Don't Sleep, Awake)
/// and user-defined custom protected applications.
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

    public static string TruncateName(string name, int maxLen = 28)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLen) return name;
        return name[..(maxLen - 3)] + "...";
    }

    public static string FormatCustomAppsSummary(string[] runningApps)
    {
        if (runningApps == null || runningApps.Length == 0) return "";
        if (runningApps.Length == 1)
        {
            return $"'{TruncateName(runningApps[0])}'";
        }
        if (runningApps.Length <= 3)
        {
            var truncated = runningApps.Select(a => $"'{TruncateName(a)}'").ToArray();
            if (truncated.Length == 2) return $"{truncated[0]} and {truncated[1]}";
            return $"{truncated[0]}, {truncated[1]} and {truncated[2]}";
        }
        return $"{runningApps.Length} custom protected apps";
    }

    public static BlockerScanResult DetectActiveBlocker()
    {
        var knownBlockers = AppConfig.Instance.Defaults.KnownSleepBlockerProcesses;
        var customProcesses = App.Settings?.CustomProtectedProcesses ?? [];

        var scan = ScanKernelProcesses(knownBlockers, customProcesses);

        // Prioritize user's custom protected applications (active work protection)
        if (scan.RunningCustomApps.Length > 0)
        {
            string summary = FormatCustomAppsSummary(scan.RunningCustomApps);
            return new BlockerScanResult(true, summary, true, scan.RunningCustomApps.Length, scan.RunningCustomApps);
        }

        if (scan.FoundBlocker)
        {
            return new BlockerScanResult(true, scan.BlockerName, false, 0, []);
        }

        // If running as Administrator, perform deep kernel power request inspection
        if (IsAdministrator())
        {
            var (hasKernelLock, reason) = InspectKernelPowerRequests();
            if (hasKernelLock)
            {
                return new BlockerScanResult(true, reason, false, 0, []);
            }
        }

        return new BlockerScanResult(false, null, false, 0, []);
    }

    private static (bool FoundBlocker, string? BlockerName, string[] RunningCustomApps) ScanKernelProcesses(string[] knownBlockers, string[] customProcesses)
    {
        IntPtr hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnapshot == IntPtr.Zero || hSnapshot == (IntPtr)(-1))
        {
            return (false, null, []);
        }

        var customSet = new HashSet<string>(customProcesses, StringComparer.OrdinalIgnoreCase);
        var detectedCustom = new List<string>();
        string? foundBlockerName = null;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(hSnapshot, ref entry))
            {
                do
                {
                    string rawName = Path.GetFileNameWithoutExtension(entry.szExeFile);
                    string exeName = rawName.ToLowerInvariant();

                    if (customSet.Contains(exeName))
                    {
                        if (!detectedCustom.Contains(rawName, StringComparer.OrdinalIgnoreCase))
                        {
                            detectedCustom.Add(rawName);
                        }
                    }

                    if (foundBlockerName == null)
                    {
                        foreach (var target in knownBlockers)
                        {
                            if (exeName.Contains(target.ToLowerInvariant()))
                            {
                                foundBlockerName = rawName;
                                break;
                            }
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

        return (foundBlockerName != null, foundBlockerName, detectedCustom.ToArray());
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

