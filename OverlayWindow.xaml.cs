using SqwizzeySwitch.Helpers;
using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SqwizzeySwitch;

public partial class OverlayWindow : Window, IOverlay
{
    private int    _showDurationMs;
    private double _maxOpacity;
    private string _positionMode;
    private int    _offsetY;
    private bool   _animationsEnabled = true;

    private System.Threading.Timer? _hideTimer;
    private bool _isVisible;
    private int  _showGeneration; // bumped per show; a stale hide timer no-ops

    // scrambleText effect (anime.js 4.4 style)
    private System.Windows.Threading.DispatcherTimer? _scrambleTimer;
    private static readonly Random _rng = new();
    private const string ScrambleChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public OverlayWindow(int showDurationMs = 800, double maxOpacity = 0.88,
                         string positionMode = "Center", int offsetY = 0)
    {
        InitializeComponent();
        _showDurationMs = showDurationMs;
        _maxOpacity     = maxOpacity;
        _positionMode   = positionMode;
        _offsetY        = offsetY;

        SourceInitialized += OnSourceInitialized;
    }

    // -------------------------------------------------------------------------
    // Win32 style setup
    // -------------------------------------------------------------------------

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd    = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

        exStyle |= NativeMethods.WS_EX_TRANSPARENT  // click-through
                 | NativeMethods.WS_EX_TOOLWINDOW   // hide from Alt+Tab / taskbar
                 | NativeMethods.WS_EX_NOACTIVATE   // never steal focus
                 | NativeMethods.WS_EX_LAYERED;     // required for transparency

        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        // Commit the extended-style change and assert topmost z-order.
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    // -------------------------------------------------------------------------
    // Settings hot-reload — called after user saves the settings dialog
    // -------------------------------------------------------------------------

    public void ApplySettings(AppSettings s)
    {
        Dispatcher.Invoke(() =>
        {
            _showDurationMs    = s.ShowDurationMs;
            _maxOpacity        = s.MaxOpacity;
            _positionMode      = s.PositionMode;
            _offsetY           = s.OffsetY;
            _animationsEnabled = s.AnimationsEnabled;
            ApplyStyle(s.Style, s.Theme);
        });
    }

    // -------------------------------------------------------------------------
    // Style presets — each defines the card geometry, fill, border, glow & text
    // -------------------------------------------------------------------------

    private void ApplyStyle(string style, string theme)
    {
        // Card/text visuals are computed by the shared helper so the Settings live
        // preview renders identically. The overlay window only owns its own sizing:
        // a transparent margin around the card so the glow isn't clipped at the edge.
        var (w, h) = OverlayStyle.Apply(style, theme, Card, LangText, Glow);
        const double pad = 44;
        Width  = w + pad * 2;
        Height = h + pad * 2;
    }

    // -------------------------------------------------------------------------
    // Public API called from the keyboard service
    // -------------------------------------------------------------------------

    public void ShowLanguage(string lang)
    {
        Dispatcher.Invoke(() =>
        {
            // Clear any leftover slide from a follow-focus move so this centred
            // show isn't stuck at the previous window's position.
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            ScrambleTo(lang);
            PositionOnActiveMonitor();
            PresentAndScheduleHide();
        });
    }

    // Makes the card visible and (re)arms the hide timer. Safe to call while a
    // previous show is still on screen: a generation token stops a stale hide from
    // killing the fresh card, and opacity is forced back up so an in-place re-show
    // (e.g. a layout change while the card is mid-fade) never stays invisible.
    private void PresentAndScheduleHide()
    {
        // Re-assert real topmost z-order on every show — the WS_EX_TOPMOST style bit
        // alone doesn't guarantee the window sits above the foreground app.
        var h = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(h, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        int gen = ++_showGeneration;
        _hideTimer?.Dispose();
        _hideTimer = new System.Threading.Timer(
            _ => Dispatcher.Invoke(() => { if (gen == _showGeneration) StartFadeOut(); }),
            null, _showDurationMs, Timeout.Infinite);

        if (!_isVisible)
        {
            _isVisible = true;
            StartFadeIn();
        }
        else
        {
            // Already visible: cancel any in-flight fade-out and guarantee full
            // opacity (no re-spring, so rapid updates don't bounce).
            BeginAnimation(OpacityProperty, null);
            Opacity = _maxOpacity;
        }
    }

    // Show on app-switch: appear in the default position, then slide the whole
    // window to the centre of the newly-focused window and hold for ShowDurationMs.
    // Show on app-switch: the card flies from the centre of the previously-focused
    // window (fromRect) to the centre of the newly-focused one (toRect), then holds.
    // When there's no usable previous window, it starts from the monitor centre.
    internal void ShowLanguageAtWindow(string lang, NativeMethods.RECT? fromRect, NativeMethods.RECT toRect)
    {
        Dispatcher.Invoke(() =>
        {
            ScrambleTo(lang);

            // Cancel any in-flight slide so we can reset to the start position.
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            // Target = new window centre. Degenerate rect → just show at default.
            bool haveTarget = TryWindowCenterTarget(toRect, out double tx, out double ty);

            if (haveTarget)
            {
                // Start = previous window centre, else the monitor centre.
                if (fromRect is { } fr && TryWindowCenterTarget(fr, out double sx, out double sy))
                {
                    Left = _animationsEnabled ? sx : tx;
                    Top  = _animationsEnabled ? sy : ty;
                }
                else
                {
                    PositionOnActiveMonitor(); // start from monitor centre / PositionMode
                    if (!_animationsEnabled) { Left = tx; Top = ty; }
                }
            }
            else
            {
                PositionOnActiveMonitor();
            }

            PresentAndScheduleHide();

            if (!haveTarget || !_animationsEnabled) return;

            // Fly both axes from the start point to the new window centre.
            // EaseOut → quick start, soft landing.
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur  = new Duration(TimeSpan.FromMilliseconds(350));
            BeginAnimation(LeftProperty, new DoubleAnimation(Left, tx, dur) { EasingFunction = ease });
            BeginAnimation(TopProperty,  new DoubleAnimation(Top,  ty, dur) { EasingFunction = ease });
        });
    }

    // -------------------------------------------------------------------------
    // Positioning
    // -------------------------------------------------------------------------

    // Converts a focused-window rect (physical px) to the Left/Top (DIP) that
    // centres this overlay window on it. Returns false for a degenerate rect.
    private bool TryWindowCenterTarget(NativeMethods.RECT rect, out double left, out double top)
    {
        left = top = 0;
        if (rect.Right - rect.Left < 8 || rect.Bottom - rect.Top < 8) return false;

        var src    = PresentationSource.FromVisual(this);
        double m11 = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double m22 = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double cxDip = (rect.Left + rect.Right)  / 2.0 * m11;
        double cyDip = (rect.Top  + rect.Bottom) / 2.0 * m22;

        left = cxDip - Width  / 2;
        top  = cyDip - Height / 2;
        return true;
    }

    private void PositionOnActiveMonitor()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.FromPoint(
                System.Windows.Forms.Cursor.Position);

            // TransformFromDevice converts physical pixels → WPF DIPs (M11 = 96/DPI)
            var src = PresentationSource.FromVisual(this);
            double m11 = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double m22 = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            double dipL = screen.Bounds.Left   * m11;
            double dipT = screen.Bounds.Top    * m22;
            double dipW = screen.Bounds.Width  * m11;
            double dipH = screen.Bounds.Height * m22;

            double cx  = dipL + (dipW - Width)  / 2;
            double cy  = dipT + (dipH - Height) / 2;
            double dy  = _offsetY;
            double pad = 80; // edge padding for non-center modes

            (Left, Top) = _positionMode switch
            {
                "TopCenter"    => (cx,                         dipT + pad + dy),
                "BottomCenter" => (cx,                         dipT + dipH - Height - pad + dy),
                "TopLeft"      => (dipL + pad,                 dipT + pad + dy),
                "TopRight"     => (dipL + dipW - Width - pad,  dipT + pad + dy),
                "BottomLeft"   => (dipL + pad,                 dipT + dipH - Height - pad + dy),
                "BottomRight"  => (dipL + dipW - Width - pad,  dipT + dipH - Height - pad + dy),
                _              => (cx,                         cy + dy)  // "Center"
            };
        }
        catch (Exception ex)
        {
            Logger.Log(ex, nameof(PositionOnActiveMonitor));
            Left = (SystemParameters.PrimaryScreenWidth  - Width)  / 2;
            Top  = (SystemParameters.PrimaryScreenHeight - Height) / 2;
        }
    }

    // -------------------------------------------------------------------------
    // Animations
    // -------------------------------------------------------------------------

    // scrambleText: shuffle through random letters, then lock in the target
    // characters left-to-right — the anime.js 4.4 scrambleText() effect.
    private void ScrambleTo(string target)
    {
        _scrambleTimer?.Stop();

        if (!_animationsEnabled || string.IsNullOrEmpty(target)) { LangText.Text = target; return; }

        int len    = target.Length;
        var total  = TimeSpan.FromMilliseconds(360);
        var sw     = System.Diagnostics.Stopwatch.StartNew();

        _scrambleTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(28)
        };
        _scrambleTimer.Tick += (_, _) =>
        {
            double p = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / total.TotalMilliseconds);
            int locked = (int)Math.Floor(p * len);
            var chars = new char[len];
            for (int k = 0; k < len; k++)
                chars[k] = k < locked
                    ? target[k]
                    : ScrambleChars[_rng.Next(ScrambleChars.Length)];
            LangText.Text = new string(chars);

            if (p >= 1.0)
            {
                LangText.Text = target;
                _scrambleTimer!.Stop();
            }
        };
        LangText.Text = target; // avoid an empty first frame
        _scrambleTimer.Start();
    }

    // anime.js-style spring entrance: the card scales up from small with a slight
    // overshoot (BackEase), rises a few px into place, and fades in together.
    private void StartFadeIn()
    {
        if (!_animationsEnabled)
        {
            // Snap to visible: clear any running animations and set final values.
            BeginAnimation(OpacityProperty, null);
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            CardTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            CardScale.ScaleX = CardScale.ScaleY = 1.0;
            CardTranslate.Y  = 0;
            Opacity = _maxOpacity;
            return;
        }

        var fade = new DoubleAnimation(0, _maxOpacity,
            new Duration(TimeSpan.FromMilliseconds(220)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fade);

        var spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 };
        var dur    = new Duration(TimeSpan.FromMilliseconds(420));

        var scale = new DoubleAnimation(0.55, 1.0, dur) { EasingFunction = spring };
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);

        var rise = new DoubleAnimation(16, 0, dur) { EasingFunction = spring };
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, rise);
    }

    private void StartFadeOut()
    {
        _isVisible = false;

        if (!_animationsEnabled)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            return;
        }

        var fade = new DoubleAnimation(Opacity, 0,
            new Duration(TimeSpan.FromMilliseconds(180)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        BeginAnimation(OpacityProperty, fade);

        var dur   = new Duration(TimeSpan.FromMilliseconds(180));
        var ease  = new CubicEase { EasingMode = EasingMode.EaseIn };
        var scale = new DoubleAnimation(1.0, 0.85, dur) { EasingFunction = ease };
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }
}
