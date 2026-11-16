using System.Diagnostics;
using System.IO;

namespace SmartPowerScheduler.Core.Services;

/// <summary>
/// Creates native Windows Desktop shell shortcuts (.lnk) with custom arguments and icons.
/// </summary>
public static class ShortcutService
{
    public static bool CreateDesktopShortcut(string shortcutName, string arguments, string description, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            string desktopDir = GetDesktopDirectory();

            string sanitizedName = SanitizeFileName(shortcutName);
            if (string.IsNullOrWhiteSpace(sanitizedName)) sanitizedName = "Smart Power Scheduler";
            string linkPath = Path.Combine(desktopDir, $"{sanitizedName}.lnk");

            string exePath = GetExecutablePath();

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                errorMessage = "Windows Script Host shell is not available.";
                return false;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);

            shortcut.TargetPath = exePath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = string.IsNullOrWhiteSpace(description) ? "Smart Power Scheduler Custom Action" : description;
            shortcut.IconLocation = $"{exePath},0";
            shortcut.Save();

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static string GetDesktopDirectory()
    {
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        return dir;
    }

    public static string GetExecutablePath() =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartPowerScheduler.exe");

    public static string GetExecutablePathQuoted() => $"\"{GetExecutablePath()}\"";

    public static void OpenDesktopFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", GetDesktopDirectory()) { UseShellExecute = true });
        }
        catch { }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !invalid.Contains(c)).ToArray();
        return new string(chars).Trim();
    }
}

