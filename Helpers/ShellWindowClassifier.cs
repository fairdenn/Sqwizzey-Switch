using System.Diagnostics;
using System.Text;

namespace SqwizzeySwitch.Helpers;

/// <summary>
/// Decides whether a window belongs to the Windows shell (taskbar, desktop, Start
/// menu, search…) rather than a normal application. The follow-focus feature treats
/// shell windows differently — by default it doesn't show the indicator for them.
/// </summary>
internal static class ShellWindowClassifier
{
    // Window classes that are unmistakably shell surfaces.
    private static readonly string[] ShellClasses =
    {
        "Shell_TrayWnd",          // primary taskbar
        "Shell_SecondaryTrayWnd", // taskbar on additional monitors
        "Progman",                // desktop
        "WorkerW",                // desktop (wallpaper host)
        "TrayNotifyWnd",          // notification area
        "NotifyIconOverflowWindow",            // hidden-icon flyout (legacy)
        "TopLevelWindowForOverflowXamlIsland", // hidden-icon flyout (Win11)
        "SystemTray_ServerWindow",             // Win11 system tray
        "Xaml_WindowedPopupClass",             // Win11 XAML popups (tray/flyouts)
        "ControlCenterWindow",                 // Win11 quick settings
        "Windows.UI.Core.CoreWindow",          // UWP shell surfaces (Start/Search)
        "Shell_InputSwitchTopLevelWindow",     // Win+Space language switcher popup
        "XamlExplorerHostIslandWindow",        // Alt+Tab switcher / Task View / Snap Assist
        "MultitaskingViewFrame",               // Win+Tab task view (older)
    };

    // Process names (without .exe) that host shell UI like Start, Search, etc.
    private static readonly string[] ShellProcesses =
    {
        "StartMenuExperienceHost",
        "SearchHost",
        "ShellExperienceHost",
        "TextInputHost",
    };

    public static bool IsShell(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return true; // no real window → treat as "not an app"

        var sb = new StringBuilder(256);
        if (NativeMethods.GetClassName(hwnd, sb, sb.Capacity) > 0)
        {
            var cls = sb.ToString();
            foreach (var s in ShellClasses)
                if (string.Equals(cls, s, StringComparison.OrdinalIgnoreCase)) return true;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            var name = Process.GetProcessById((int)pid).ProcessName;
            foreach (var p in ShellProcesses)
                if (string.Equals(name, p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { /* process gone or access denied → assume not shell */ }

        return false;
    }

    /// <summary>
    /// True only for a normal top-level application window — the kind you Alt+Tab to.
    /// Filters out context menus, tooltips, owned popups, toast notifications and
    /// tool windows, which often share their owner app's window class.
    /// </summary>
    public static bool IsAppWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;

        int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        bool toolWindow = (ex & NativeMethods.WS_EX_TOOLWINDOW) != 0;
        bool appWindow  = (ex & NativeMethods.WS_EX_APPWINDOW)  != 0;
        if (toolWindow && !appWindow) return false; // palettes, toasts, flyouts

        // Owned popups (menus, tooltips, dialogs) resolve to a different root owner;
        // a real app window is its own root owner.
        if (NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER) != hwnd) return false;

        return true;
    }

    /// <summary>Diagnostic string: window class + owning process name.</summary>
    public static string Describe(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        string proc = "?";
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            proc = Process.GetProcessById((int)pid).ProcessName;
        }
        catch { }
        return $"cls='{sb}' proc='{proc}'";
    }
}
