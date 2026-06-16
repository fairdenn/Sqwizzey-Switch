using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SqwizzeySwitch.Helpers;

/// <summary>
/// Resolves the two-letter language code (EN, RU…) for a given window's
/// keyboard layout. Shared by the language-poll service and the
/// foreground-change service so both label windows the same way.
/// </summary>
internal static class LayoutUtils
{
    /// <summary>Language code of the window that currently has focus.</summary>
    public static string ForegroundLanguage() => LanguageOf(NativeMethods.GetForegroundWindow());

    /// <summary>
    /// Language code of a specific window, or "" if it can't be resolved. Reads the
    /// top-level window's thread layout — correct for ordinary apps and used as the
    /// label on window switches. In-window layout *changes* are detected separately by
    /// <see cref="InputThreadLangs"/>, which also covers multi-thread apps (modern
    /// Notepad, Explorer…) whose text input lives on a different thread.
    /// </summary>
    public static string LanguageOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;

        var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        return CodeOf(LangIdOf(threadId));
    }

    /// <summary>
    /// Layout (LANGID) of every GUI thread that holds keyboard focus or a caret in the
    /// window's process, keyed by thread id — always including the top-level thread.
    ///
    /// Why a map of threads instead of one value: modern WinUI / XAML-island apps (the
    /// new Notepad, parts of Explorer/Settings) edit text on a *separate* UI thread from
    /// the top-level window, and console hosts spread input across helper threads. The
    /// thread the user is actually typing in — and whose HKL flips on Win+Space — is not
    /// the top window's thread, and the island focus delegation can't be followed
    /// synchronously to pick it out. Instead the poll service watches this whole map and
    /// fires when *any* thread's layout changes in the same window. For an ordinary
    /// single-thread app the map is just { topThread } and behaviour is unchanged.
    /// </summary>
    public static Dictionary<uint, int> InputThreadLangs(IntPtr hwnd)
    {
        var result = new Dictionary<uint, int>();
        if (hwnd == IntPtr.Zero) return result;

        uint topThread = NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        result[topThread] = LangIdOf(topThread); // always tracked → preserves old behaviour

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            foreach (ProcessThread t in proc.Threads)
            {
                var tid = (uint)t.Id;
                if (tid == topThread || result.ContainsKey(tid)) continue;

                var gti = NewGui();
                if (NativeMethods.GetGUIThreadInfo(tid, ref gti)
                    && (gti.hwndCaret != IntPtr.Zero || gti.hwndFocus != IntPtr.Zero))
                    result[tid] = LangIdOf(tid);
            }
        }
        catch { /* process gone / access denied → just the top thread */ }

        return result;
    }

    /// <summary>Installed input locales (HKLs) in the system's cycle order.</summary>
    public static IntPtr[] InstalledLayouts()
    {
        int n = NativeMethods.GetKeyboardLayoutList(0, null);
        if (n <= 0) return Array.Empty<IntPtr>();
        var list = new IntPtr[n];
        NativeMethods.GetKeyboardLayoutList(n, list);
        return list;
    }

    /// <summary>LANGID (low 16 bits of the thread's HKL), or 0 if unavailable.</summary>
    private static int LangIdOf(uint threadId)
    {
        var hkl = NativeMethods.GetKeyboardLayout(threadId);
        return hkl == IntPtr.Zero ? 0 : (int)(hkl.ToInt64() & 0xFFFF);
    }

    /// <summary>Two-letter code (EN, RU…) for a LANGID, "" for 0, hex LCID if unknown.</summary>
    public static string CodeOf(int langId)
    {
        if (langId == 0) return string.Empty;
        try
        {
            return CultureInfo.GetCultureInfo(langId).TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch
        {
            return langId.ToString("X4");
        }
    }

    private static NativeMethods.GUITHREADINFO NewGui() =>
        new() { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
}
