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
        "OperationStatusWindow",               // file copy/move/delete progress dialog
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
    /// True for a classic console host window (PowerShell, cmd in conhost). These manage
    /// their input language internally and never update their window thread's HKL, so the
    /// layout poll is blind to their Win+Space — the shell-hook path handles them instead.
    /// </summary>
    public static bool IsConsole(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        var sb = new StringBuilder(64);
        return NativeMethods.GetClassName(hwnd, sb, sb.Capacity) > 0
            && sb.ToString() == "ConsoleWindowClass";
    }

    /// <summary>
    /// True for the Windows Calculator window. Handles both the modern WinUI desktop build
    /// (the window itself belongs to the Calculator process) and the older UWP build (hosted
    /// in an ApplicationFrameWindow whose CoreWindow child belongs to the Calculator process).
    /// </summary>
    public static bool IsCalculator(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (ProcessNameContains(hwnd, "calculator")) return true;

        // UWP: the visible frame is ApplicationFrameWindow (process ApplicationFrameHost);
        // the real app is the hosted CoreWindow child.
        var sb = new StringBuilder(64);
        if (NativeMethods.GetClassName(hwnd, sb, sb.Capacity) > 0 && sb.ToString() == "ApplicationFrameWindow")
        {
            bool found = false;
            NativeMethods.EnumChildWindows(hwnd, (child, _) =>
            {
                if (ProcessNameContains(child, "calculator")) { found = true; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }
        return false;
    }

    private static bool ProcessNameContains(IntPtr hwnd, string needle)
        => ProcessNameOf(hwnd).Contains(needle, StringComparison.OrdinalIgnoreCase);

    /// <summary>Owning process name (without .exe) for a window, or "" if it can't be read.</summary>
    public static string ProcessNameOf(IntPtr hwnd)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return string.Empty; }
    }

    /// <summary>Window title text, or "" if none.</summary>
    public static string TitleOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return NativeMethods.GetWindowTextW(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
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

    /// <summary>
    /// True for the UAC elevation prompt (consent.exe). Follow-focus never shows the
    /// indicator on it: we can't read inside the window (it runs at a higher integrity
    /// level, so UIPI blocks us) and a language card over a UAC prompt is just noise.
    /// consent.exe runs as SYSTEM, so its image name is read via QueryFullProcessImageName
    /// with PROCESS_QUERY_LIMITED_INFORMATION — Process.GetProcessById can fail with
    /// Access Denied across the integrity boundary.
    /// </summary>
    public static bool IsElevationUI(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return false;

        IntPtr h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return false;
        try
        {
            var sb = new StringBuilder(260);
            uint size = (uint)sb.Capacity;
            if (NativeMethods.QueryFullProcessImageName(h, 0, sb, ref size))
                return string.Equals(Path.GetFileName(sb.ToString()), "consent.exe",
                    StringComparison.OrdinalIgnoreCase);
        }
        finally { NativeMethods.CloseHandle(h); }

        return false;
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
