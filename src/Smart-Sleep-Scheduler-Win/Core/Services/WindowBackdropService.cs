using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SmartSleepScheduler.Core.Services;

/// <summary>
/// Configures Windows 11/10 native translucent backdrops (Mica / Acrylic) and immersive dark mode
/// using Desktop Window Manager (DWM) APIs safely without crashing on older OS versions.
/// </summary>
public static class WindowBackdropService
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_ROUND = 2;

    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void EnableMicaOrAcrylic(Window window, bool isDarkMode)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1. Set immersive dark mode attribute
            int darkModeValue = isDarkMode ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));

            // 2. Set rounded corners (Win 11)
            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // 3. Set Backdrop Type: Try Mica (2) or Acrylic (3)
            int backdropType = DWMSBT_MAINWINDOW; // Mica
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
        catch
        {
            // Fall back silently to WPF brushed transparency on systems that don't support DWM backdrop types
        }
    }
}

