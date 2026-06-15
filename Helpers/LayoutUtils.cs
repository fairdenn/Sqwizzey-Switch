using System.Globalization;

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

    /// <summary>Language code of a specific window, or "" if it can't be resolved.</summary>
    public static string LanguageOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;

        var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        var hkl      = NativeMethods.GetKeyboardLayout(threadId);
        if (hkl == IntPtr.Zero) return string.Empty;

        // The low 16 bits of HKL are the LANGID (language identifier).
        int langId = (int)(hkl.ToInt64() & 0xFFFF);
        if (langId == 0) return string.Empty;

        try
        {
            var culture = CultureInfo.GetCultureInfo(langId);
            return culture.TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch
        {
            // Unknown locale: return hex LCID as a fallback label.
            return langId.ToString("X4");
        }
    }
}
