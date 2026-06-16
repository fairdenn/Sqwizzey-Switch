using SqwizzeySwitch.Helpers;
using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using System.Windows;
using System.Windows.Threading;

namespace SqwizzeySwitch;

public partial class App : Application
{
    private System.Threading.Mutex? _mutex;
    private AppSettings             _settings = null!;
    private KeyboardLayoutService?  _keyboard;
    private ForegroundChangeService? _foreground;
    private KeyboardHotkeyService?  _hotkey;
    private IntPtr                  _consoleHkl;  // tracked layout for console (advanced on hotkey)
    private TrayService?            _tray;
    private OverlayWindow?          _overlay;   // every style (Glass = "Frosted" card)
    private SettingsWindow?         _settingsWindow;
    private IntPtr                  _lastAppHwnd;          // last app window — slide origin
    private IntPtr                  _lastShownHwnd;        // last window we showed a card for
    private DispatcherTimer?        _focusTimer;           // debounce for focus changes
    private IntPtr                  _pendingHwnd;          // window awaiting the debounce
    private int                     _focusRetries;         // rect-not-ready retry budget
    private NativeMethods.RECT      _pendingRect;          // last rect read while settling

    // A single layered overlay renders every style. A real DWM-acrylic Glass window was
    // tried (Phase 2) and dropped: a window region for rounded corners disables the
    // acrylic backdrop, and a separate acrylic window leaves artefacts over the other
    // styles. So Glass renders as a translucent "Frosted" card here.
    private IOverlay? OverlayFor(string style) => _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Elevated helper path: when relaunched with --set-langswitch <0|1>,
        // just write the HKLM value and exit — don't start the tray app.
        if (e.Args.Length >= 2 && e.Args[0] == NativeLanguageSwitchService.CliArg)
        {
            bool suppress = e.Args[1] == "0";
            Shutdown(NativeLanguageSwitchService.WriteRegistry(suppress));
            return;
        }

        _mutex = new System.Threading.Mutex(true, "SqwizzeySwitch_SingleInstance_v1", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        // Apply the WPF-UI theme colours so the Fluent settings window follows the
        // system light/dark theme — but with WindowBackdropType.None: the default
        // ApplySystemTheme() pushes a Mica backdrop onto *every* app window, including
        // the layered overlay (AllowsTransparency=True), where it can't composite and
        // leaves a grey fallback rectangle. The settings window asks for Mica itself
        // (WindowBackdropType="Mica" in XAML), so no global backdrop is needed.
        var appTheme = OverlayStyle.IsSystemDarkTheme()
            ? Wpf.Ui.Appearance.ApplicationTheme.Dark
            : Wpf.Ui.Appearance.ApplicationTheme.Light;
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
            appTheme, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: true);

        try
        {
            _settings = AppSettings.Load();

            _overlay = new OverlayWindow(
                _settings.ShowDurationMs,
                _settings.MaxOpacity,
                _settings.PositionMode,
                _settings.OffsetY);
            _overlay.Show(); // HWND created; window is invisible (Opacity=0)
            _overlay.ApplySettings(_settings); // apply initial style/theme

            _tray = new TrayService(_settings);
            _tray.ExitRequested    += () => Dispatcher.Invoke(Shutdown);
            _tray.SettingsRequested += OpenSettingsWindow;

            _keyboard = new KeyboardLayoutService();
            _keyboard.LayoutChanged += OnLayoutChanged;
            _keyboard.WindowChanged += OnPollWindowChanged; // safety net for missed hooks

            // Hook must be created on the UI thread (OUTOFCONTEXT callbacks arrive
            // via this thread's message pump).
            _foreground = new ForegroundChangeService();
            _foreground.ForegroundChanged += OnForegroundChanged;

            // Low-level hotkey hook (Win+Space / Alt+Shift) — the only trigger that reaches
            // us for console windows, whose layout the poll can't read. Acted on only for
            // console foreground (see handler).
            _hotkey = new KeyboardHotkeyService();
            _hotkey.SwitchPressed += OnSwitchHotkey;

            // Seed the slide origin with whatever's focused now (and treat it as
            // already-shown so the first focus event for it doesn't pop a card).
            var fg = NativeMethods.GetForegroundWindow();
            if (!ShellWindowClassifier.IsShell(fg) && ShellWindowClassifier.IsAppWindow(fg))
                _lastAppHwnd = _lastShownHwnd = fg;

            UpdateTrayLanguage(); // seed the tray indicator if the option is on

            Logger.Log("Started");
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(OnStartup));
            MessageBox.Show(
                $"Sqwizzey Switch failed to start.\nSee log for details.\n\n{ex.Message}",
                "SqwizzeySwitch", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    // A real in-window layout change (Win+Space). Shows in place at the focused
    // window's centre. Runs on a thread-pool poll thread → marshal to the UI thread
    // before touching shared state.
    private void OnLayoutChanged(string lang)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => OnLayoutChanged(lang)); return; }

        UpdateTrayLanguage(); // tray reflects the active language even if the card is suppressed
        if (!_settings.OverlayEnabled) return;
        if (_settings.SkipFullscreen && FullscreenDetector.IsForegroundFullscreen()) return;
        if (IsExcludedApp(NativeMethods.GetForegroundWindow())) return; // user-excluded process

        if (_settings.FollowFocusEnabled)
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            bool fgIsApp = !ShellWindowClassifier.IsShell(hwnd)
                && !ShellWindowClassifier.IsElevationUI(hwnd) // UAC prompt → not an app target
                && ShellWindowClassifier.IsAppWindow(hwnd);
            var target = fgIsApp ? hwnd : _lastAppHwnd; // on shell/popup/UAC → last app window

            if (target != IntPtr.Zero && NativeMethods.GetWindowRect(target, out var rect)
                && rect.Right - rect.Left >= 8)
            {
                _overlay?.ShowLanguageAtWindow(lang, rect, rect); // start == target → no slide
                if (fgIsApp) _lastAppHwnd = hwnd;
                _lastShownHwnd = target;
                return;
            }
        }

        OverlayFor(_settings.Style)?.ShowLanguage(lang); // default: centre of screen
    }

    // A layout-switch hotkey was pressed (Win+Space / Alt+Shift). The poll already handles
    // every window whose thread HKL tracks the layout, so we act ONLY for console windows
    // (conhost), which the poll is blind to — avoiding double-shows everywhere else.
    //
    // conhost never exposes its live layout, so we can't read the new one: instead we track
    // it ourselves, seeding from the (best-guess) current read and advancing through the
    // installed-layout cycle on each hotkey — exactly what Win+Space/Alt+Shift do. Runs on
    // the UI thread (dispatched from the hook).
    private void OnSwitchHotkey()
    {
        if (!ShellWindowClassifier.IsConsole(NativeMethods.GetForegroundWindow())) return;

        var list = LayoutUtils.InstalledLayouts();
        if (list.Length == 0) return;

        int idx = Array.IndexOf(list, ConsoleLayout());
        idx = (idx < 0 ? 0 : idx + 1) % list.Length; // advance one step in the cycle
        _consoleHkl = list[idx];

        var lang = LayoutUtils.CodeOf((int)(_consoleHkl.ToInt64() & 0xFFFF));
        if (!string.IsNullOrEmpty(lang)) OnLayoutChanged(lang);
    }

    // Tracked console layout. The console's own window-thread HKL is frozen and unreadable,
    // so we maintain a best guess: re-seeded whenever focus enters a console (see the focus
    // timer) to the language of the app we came from, and advanced by the hotkey handler.
    private IntPtr ConsoleLayout()
    {
        if (_consoleHkl == IntPtr.Zero) _consoleHkl = NativeMethods.GetKeyboardLayout(0);
        return _consoleHkl;
    }

    // True when the window matches the user's exclusion list — those windows never get a card.
    // Each entry matches either the owning process name (exact, .exe optional) OR the window
    // title (substring). Title matching lets you exclude one specific window (e.g. the Phone
    // Link call popup "Звонок на компьютере") while the app's other windows still show cards.
    private bool IsExcludedApp(IntPtr hwnd)
    {
        var list = _settings.ExcludedProcesses;
        if (string.IsNullOrWhiteSpace(list)) return false;

        var proc  = ShellWindowClassifier.ProcessNameOf(hwnd);
        var title = ShellWindowClassifier.TitleOf(hwnd);

        foreach (var raw in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // "<process>:topmost" → match only topmost windows of that process. Lets you
            // exclude one window (e.g. the Phone Link call popup, which is topmost) while the
            // app's other windows (not topmost) still get a card.
            bool topmostOnly = raw.EndsWith(":topmost", StringComparison.OrdinalIgnoreCase);
            var entry = topmostOnly ? raw[..^8].Trim() : raw;
            var name  = entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? entry[..^4] : entry;

            if (!string.IsNullOrEmpty(proc) && proc.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (!topmostOnly) return true;
                int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                if ((ex & NativeMethods.WS_EX_TOPMOST) != 0) return true;
                continue; // process matches but window isn't topmost → keep its card
            }
            if (!topmostOnly && title.Length > 0 && title.Contains(entry, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Raw HKL of a window's top thread (the keyboard layout it reports), or Zero.
    private static IntPtr HklOf(IntPtr hwnd)
        => hwnd == IntPtr.Zero
            ? IntPtr.Zero
            : NativeMethods.GetKeyboardLayout(NativeMethods.GetWindowThreadProcessId(hwnd, out _));

    // Updates the tray icon to the active window's language (when the option is on). Console
    // uses the tracked heuristic layout; everything else its real layout. Tooltip = cur→other.
    private void UpdateTrayLanguage()
    {
        if (_tray == null || !_settings.TrayLanguageIcon) return;
        var fg  = NativeMethods.GetForegroundWindow();
        var cur = ShellWindowClassifier.IsConsole(fg)
            ? LayoutUtils.CodeOf((int)(ConsoleLayout().ToInt64() & 0xFFFF))
            : LayoutUtils.LanguageOf(fg);
        if (string.IsNullOrEmpty(cur)) return;
        var other = OtherLang(cur);
        _tray.SetLanguage(cur, string.IsNullOrEmpty(other) ? cur : $"{cur} → {other}");
    }

    // The next installed layout after the current one (cycle), for the tooltip; "" if single.
    private static string OtherLang(string cur)
    {
        var list = LayoutUtils.InstalledLayouts();
        if (list.Length < 2) return string.Empty;
        int idx = Array.FindIndex(list, h => LayoutUtils.CodeOf((int)(h.ToInt64() & 0xFFFF)) == cur);
        if (idx < 0) return string.Empty;
        return LayoutUtils.CodeOf((int)(list[(idx + 1) % list.Length].ToInt64() & 0xFFFF));
    }

    // Window became foreground (instant WinEvent hook). Runs on the UI thread.
    private void OnForegroundChanged(IntPtr hwnd) { UpdateTrayLanguage(); ArmFocusShow(hwnd); }

    // Poll-detected window change (safety net for switches the hook missed, e.g.
    // Explorer). Runs on a thread-pool poll thread → marshal to the UI thread.
    private void OnPollWindowChanged(IntPtr hwnd)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => OnPollWindowChanged(hwnd)); return; }
        UpdateTrayLanguage();
        ArmFocusShow(hwnd);
    }

    // Arms the debounced show for a window-switch. Both the hook and the poll funnel
    // here; dedup by _lastShownHwnd means they never double-show. Debounced so a
    // just-opened window has time to settle before we read its rect (see the tick).
    private void ArmFocusShow(IntPtr hwnd)
    {
        if (!_settings.OverlayEnabled || !_settings.FollowFocusEnabled) return;
        if (_settings.SkipFullscreen && FullscreenDetector.IsForegroundFullscreen()) return;

        // Our own windows (the settings window): the WinEvent hook skips our process
        // via WINEVENT_SKIPOWNPROCESS, but the poll safety net doesn't — filter here.
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Environment.ProcessId) return;

        // Taskbar, tray, desktop, Start — never show. Menus/popups/tooltips/toasts too.
        if (ShellWindowClassifier.IsShell(hwnd)) return;
        if (ShellWindowClassifier.IsElevationUI(hwnd)) return; // UAC prompt — never show
        if (!ShellWindowClassifier.IsAppWindow(hwnd)) return;
        if (IsExcludedApp(hwnd)) return; // user-excluded process (e.g. Phone Link)
        if (hwnd == _lastShownHwnd) return; // returned to the window we last showed

        _pendingHwnd  = hwnd;
        _focusRetries = 0;
        _pendingRect  = default;
        _focusTimer ??= CreateFocusTimer();
        _focusTimer.Stop();
        _focusTimer.Start();
    }

    private DispatcherTimer CreateFocusTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            var hwnd = _pendingHwnd;

            if (ShellWindowClassifier.IsShell(hwnd) || ShellWindowClassifier.IsElevationUI(hwnd)
                || !ShellWindowClassifier.IsAppWindow(hwnd) || IsExcludedApp(hwnd)) return;
            if (hwnd == _lastShownHwnd) return;

            // A just-opened/maximizing window (e.g. Explorer) reports an intermediate
            // rect for a while. Read the true visible rect and wait until it stops
            // moving — show only once two consecutive reads match.
            if (!TryGetVisibleRect(hwnd, out var toRect)
                || toRect.Right - toRect.Left < 8 || NativeMethods.IsIconic(hwnd)
                || (!RectsClose(toRect, _pendingRect) && _focusRetries < 4))
            {
                _pendingRect = toRect;
                if (_focusRetries++ < 4) t.Start();
                return;
            }

            // Calculator: a fun "numbers only" card instead of a language. Same animation.
            string lang;
            if (_settings.CalculatorCardEnabled && ShellWindowClassifier.IsCalculator(hwnd))
            {
                lang = "123";
            }
            // Console windows don't expose their layout (frozen HKL). On focus-in assume the
            // language of the app we came from (the user carries their current language) and
            // re-baseline the toggle there; everything else reads its real layout.
            else if (ShellWindowClassifier.IsConsole(hwnd))
            {
                var seed = HklOf(_lastAppHwnd);
                if (seed == IntPtr.Zero) seed = NativeMethods.GetKeyboardLayout(0);
                _consoleHkl = seed;
                lang = LayoutUtils.CodeOf((int)(seed.ToInt64() & 0xFFFF));
            }
            else
            {
                lang = LayoutUtils.LanguageOf(hwnd);
            }
            if (string.IsNullOrEmpty(lang)) return;

            // Fly in from the previously-focused app window, else from the monitor centre.
            NativeMethods.RECT? fromRect = null;
            if (_lastAppHwnd != IntPtr.Zero && _lastAppHwnd != hwnd
                && NativeMethods.GetWindowRect(_lastAppHwnd, out var fr))
                fromRect = fr;

            _overlay?.ShowLanguageAtWindow(lang, fromRect, toRect);
            _lastAppHwnd   = hwnd;
            _lastShownHwnd = hwnd;
        };
        return t;
    }

    // True visible bounds (excludes the DWM invisible resize border), falling back to
    // GetWindowRect if the DWM call isn't available.
    private static bool TryGetVisibleRect(IntPtr hwnd, out NativeMethods.RECT rect)
    {
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out rect, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) == 0
            && rect.Right - rect.Left > 0)
            return true;
        return NativeMethods.GetWindowRect(hwnd, out rect);
    }

    private static bool RectsClose(NativeMethods.RECT a, NativeMethods.RECT b)
        => Math.Abs(a.Left - b.Left) <= 4 && Math.Abs(a.Top - b.Top) <= 4
        && Math.Abs(a.Right - b.Right) <= 4 && Math.Abs(a.Bottom - b.Bottom) <= 4;

    private void OpenSettingsWindow()
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                Logger.Log("OpenSettingsWindow: enter");

                // Bring existing window to front instead of opening a second one
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    _settingsWindow.Topmost = true;
                    _settingsWindow.Topmost = false;
                    return;
                }

                _settingsWindow = new SettingsWindow(_settings);
                _settingsWindow.SettingsSaved    += OnSettingsSaved;
                // Flash the real overlay at the chosen position while editing it.
                _settingsWindow.PreviewRequested += s =>
                {
                    _overlay?.ApplySettings(s);
                    var lang = string.IsNullOrEmpty(_keyboard?.CurrentLanguage) ? "EN" : _keyboard!.CurrentLanguage;
                    _overlay?.ShowLanguage(lang);
                };
                _settingsWindow.Closed           += (_, _) =>
                {
                    _settingsWindow = null;
                    // discard any unsaved preview
                    _overlay?.ApplySettings(_settings);
                };
                _settingsWindow.Show();
                _settingsWindow.Activate();
                Logger.Log("OpenSettingsWindow: shown");
            }
            catch (Exception ex)
            {
                Logger.Log(ex, nameof(OpenSettingsWindow));
                _settingsWindow = null;
                MessageBox.Show(
                    $"Settings failed to open.\n\n{ex.GetType().Name}: {ex.Message}",
                    "Sqwizzey Switch", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    private void OnSettingsSaved(AppSettings updated)
    {
        _overlay?.ApplySettings(updated);
        _tray?.RefreshStartupCheck();
        _tray?.Relocalize(); // update tray menu language
        if (updated.TrayLanguageIcon) UpdateTrayLanguage(); else _tray?.ResetIcon();
        Logger.Log($"Settings saved: style={updated.Style} theme={updated.Theme} pos={updated.PositionMode} dur={updated.ShowDurationMs}ms");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _keyboard?.Dispose();
            _foreground?.Dispose();
            _hotkey?.Dispose();
            _focusTimer?.Stop();
            _tray?.Dispose();
            _settingsWindow?.Close();
            _overlay?.Close();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            Logger.Log("Stopped");
        }
        catch { }

        base.OnExit(e);
    }
}
