using SqwizzeySwitch.Helpers;
using System.Runtime.InteropServices;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Determines whether the current foreground window covers the entire monitor,
/// which typically means a fullscreen game or video player.
/// </summary>
public static class FullscreenDetector
{
    public static bool IsForegroundFullscreen()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)           return false;
            if (hwnd == NativeMethods.GetShellWindow()) return false; // desktop/taskbar

            NativeMethods.GetWindowRect(hwnd, out var wnd);

            var hMon = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi   = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            NativeMethods.GetMonitorInfo(hMon, ref mi);

            var r = mi.rcMonitor;
            return wnd.Left  <= r.Left
                && wnd.Top   <= r.Top
                && wnd.Right >= r.Right
                && wnd.Bottom>= r.Bottom;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(IsForegroundFullscreen));
            return false;
        }
    }
}
