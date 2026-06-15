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

            var lang = LayoutUtils.LanguageOf(eff);
            if (string.IsNullOrEmpty(lang)) return; // transient null during a switch

            if (_firstPoll)
            {
                _firstPoll       = false;
                _lastHwnd        = eff;
                _currentLanguage = lang;
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
                WindowChanged?.Invoke(eff);
                return;
            }

            if (lang != _currentLanguage)
            {
                _currentLanguage = lang;
                LayoutChanged?.Invoke(lang); // real in-window layout change
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(Poll));
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
