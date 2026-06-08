using SqwizzeySwitch.Helpers;
using System.Globalization;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Polls the active window's keyboard layout every 150 ms and fires
/// LayoutChanged whenever the two-letter language code changes.
/// The first detection is silently consumed so no overlay shows at launch.
/// </summary>
public sealed class KeyboardLayoutService : IDisposable
{
    public event Action<string>? LayoutChanged;

    private string  _currentLanguage = string.Empty;
    private bool    _firstPoll       = true;
    private bool    _disposed        = false;

    private readonly System.Threading.Timer _timer;

    public KeyboardLayoutService()
    {
        // 1 s initial delay so the overlay window can fully initialise before
        // we might fire an event; then poll every 150 ms.
        _timer = new System.Threading.Timer(Poll, null,
            dueTime: TimeSpan.FromSeconds(1),
            period:  TimeSpan.FromMilliseconds(150));
    }

    private void Poll(object? state)
    {
        if (_disposed) return;

        try
        {
            var lang = GetForegroundWindowLanguage();
            if (lang == _currentLanguage) return;

            _currentLanguage = lang;

            if (_firstPoll)
            {
                _firstPoll = false;
                return; // remember initial language without showing overlay
            }

            if (!string.IsNullOrEmpty(lang))
                LayoutChanged?.Invoke(lang);
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(Poll));
        }
    }

    private static string GetForegroundWindowLanguage()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return string.Empty;

        var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        var hkl      = NativeMethods.GetKeyboardLayout(threadId);
        if (hkl == IntPtr.Zero) return string.Empty;

        // The low 16 bits of HKL are the LANGID (language identifier)
        int langId = (int)(hkl.ToInt64() & 0xFFFF);
        if (langId == 0) return string.Empty;

        try
        {
            var culture = CultureInfo.GetCultureInfo(langId);
            return culture.TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch
        {
            // Unknown locale: return hex LCID as fallback label
            return langId.ToString("X4");
        }
    }

    public string CurrentLanguage => _currentLanguage;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
