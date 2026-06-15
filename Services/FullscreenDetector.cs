using SqwizzeySwitch.Helpers;
using System.Runtime.InteropServices;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Determines whether the current foreground window is a genuine fullscreen app
/// (exclusive or borderless game / video player) — as opposed to an ordinary
/// window, even a maximized one. Only true fullscreen should suppress the overlay.
/// </summary>
public static class FullscreenDetector
{
    public static bool IsForegroundFullscreen()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)                     return false;
            if (hwnd == NativeMethods.GetShellWindow())  return false; // desktop/taskbar

            // A maximized window or any window with normal chrome (title bar /
            // sizing border) is NOT fullscreen — it's a regular app. The overlay
            // must appear over it. Without this check a maximized window reads as
            // fullscreen because its rect spills a few px past the monitor edges.
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            if ((style & NativeMethods.WS_MAXIMIZE) != 0)                              return false;
            if ((style & (NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME)) != 0) return false;

            // Chromeless, non-maximized window: fullscreen only if it covers the
            // whole monitor.
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
