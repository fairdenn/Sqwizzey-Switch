using SqwizzeySwitch.Helpers;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Polls every 150 ms and fires LayoutChanged ONLY when the keyboard layout
/// changes while staying in the same app window (i.e. a real Win+Space). A switch
/// to a different window is owned by ForegroundChangeService, so when the effective
/// app window changes we silently re-baseline and stay quiet — otherwise both
/// services would fire for one window switch and the overlay would show twice.
/// </summary>
public sealed class KeyboardLayoutService : IDisposable
{
    public event Action<string>? LayoutChanged;          // layout changed in same window
    public event Action<IntPtr>? WindowChanged;          // effective app window changed

    private IntPtr  _lastHwnd        = IntPtr.Zero;   // effective app window we track
    private string  _currentLanguage = string.Empty;
    private System.Collections.Generic.Dictionary<uint, int> _lastLangs = new(); // input-thread → LANGID
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
            // Effective window: the foreground app window, or — if focus is on the
            // shell / a popup / menu — keep tracking the last real app window so a
            // layout change made with a menu open still counts against the app.
            var fg  = NativeMethods.GetForegroundWindow();
            var eff = (!ShellWindowClassifier.IsShell(fg) && ShellWindowClassifier.IsAppWindow(fg))
                ? fg : _lastHwnd;

            // Label for window-switch shows (top-thread layout, original behaviour) plus
            // the per-thread layout map used to detect in-window switches across the
            // multiple input threads modern apps use.
            var lang  = LayoutUtils.LanguageOf(eff);
            var langs = LayoutUtils.InputThreadLangs(eff);
            if (string.IsNullOrEmpty(lang) && langs.Count == 0) return; // transient null during a switch

            if (_firstPoll)
            {
                _firstPoll       = false;
                _lastHwnd        = eff;
                _currentLanguage = lang;
                _lastLangs       = langs;
                return; // remember initial state without showing
            }

            if (eff != _lastHwnd)
            {
                // Window changed. The WinEvent hook usually fires first, but it can
                // miss switches (e.g. Explorer churns through a ForegroundStaging
                // window). Raise WindowChanged as a safety net — the app dedups by
                // window handle so the hook and the poll never double-show.
                _lastHwnd        = eff;
                _currentLanguage = lang;
                _lastLangs       = langs;
                WindowChanged?.Invoke(eff);
                return;
            }

            // Same window: a real Win+Space changes the layout of whichever input thread
            // the user is typing in. Compare each thread present in both snapshots; if one
            // flipped, that's the new language. Covers single-thread apps (only the top
            // thread) and multi-thread apps (new Notepad, Explorer…) identically.
            int newLangId = DetectChangedLang(_lastLangs, langs);
            _lastLangs = langs;

            if (newLangId > 0)
            {
                var newLang = LayoutUtils.CodeOf(newLangId);
                if (!string.IsNullOrEmpty(newLang) && newLang != _currentLanguage)
                {
                    _currentLanguage = newLang;
                    LayoutChanged?.Invoke(newLang); // real in-window layout change
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(Poll));
        }
    }

    // Returns the new LANGID of a thread whose layout changed between two snapshots of
    // the same window, or 0 if none did. Only threads present in BOTH are compared, so a
    // thread appearing/leaving (e.g. switching Notepad tabs) is not mistaken for a switch.
    private static int DetectChangedLang(
        System.Collections.Generic.Dictionary<uint, int> before,
        System.Collections.Generic.Dictionary<uint, int> after)
    {
        foreach (var kv in after)
            if (before.TryGetValue(kv.Key, out int old) && old != kv.Value && kv.Value > 0)
                return kv.Value;
        return 0;
    }

    public string CurrentLanguage => _currentLanguage;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
