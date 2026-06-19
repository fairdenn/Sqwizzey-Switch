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
    private int    _offsetX;
    private int    _offsetY;
    private bool   _animationsEnabled = true;
    private bool   _scrambleEnabled   = true;
    private bool   _liquidTransition;
    private double _transitionSpeed = 1.0; // app-switch animation speed multiplier

    // ---- Liquid bridge transition state ----
    private bool          _liquidActive;
    private readonly System.Diagnostics.Stopwatch _liquidSw = new();
    private EventHandler? _liquidRender;
    private Point  _liqO, _liqT;          // origin & target centres, window-local DIP
    private double _liqRadius;            // blob radius (DIP)
    private string _liqLang = "";
    private double _cardW = 190, _cardH = 160; // card-mode window size, restored after the bridge
    private const double LiquidMs = 520;

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
            _offsetX           = s.OffsetX;
            _offsetY           = s.OffsetY;
            _animationsEnabled = s.AnimationsEnabled;
            _scrambleEnabled   = s.ScrambleEnabled;
            _liquidTransition  = s.LiquidTransition;
            _transitionSpeed   = s.TransitionSpeed < 0.1 ? 1.0 : s.TransitionSpeed;
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
        if (!_liquidActive) { _cardW = Width; _cardH = Height; } // remember for post-bridge restore
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
            if (_liquidActive) StopLiquid();
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            ResetSmear(); // a centred show is never a liquid slide

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
            // A new show interrupts any running liquid transition.
            if (_liquidActive) StopLiquid();

            // Cancel any in-flight slide so we can reset to the start position.
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            // Liquid bridge: melt into a droplet at the old window, stretch a pinching
            // neck across, and reform as the card at the new one. Independent of the
            // scramble/spring toggle — it's its own motion. Needs a real gap and both
            // window centres; otherwise fall through to the plain slide below.
            if (_liquidTransition
                && fromRect is { } lfr && RectCenterDip(lfr, out var lo)
                && RectCenterDip(toRect, out var lt) && (lt - lo).Length >= 120)
            {
                StartLiquidTransition(lo, lt, lang);
                return;
            }

            ScrambleTo(lang);

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

            if (!haveTarget || !_animationsEnabled) { ResetSmear(); return; }

            // Liquid drop: stretch the card along its flight path while it slides
            // (only worth it over a real distance, else it just wobbles in place).
            double dx = tx - Left, dy = ty - Top;
            bool liquid = _liquidTransition && Math.Sqrt(dx * dx + dy * dy) >= 60;
            if (liquid) StartSmear(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            else        ResetSmear();

            // Fly both axes from the start point to the new window centre.
            // EaseOut → quick start, soft landing.
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur  = new Duration(TimeSpan.FromMilliseconds(350 / _transitionSpeed));
            BeginAnimation(LeftProperty, new DoubleAnimation(Left, tx, dur) { EasingFunction = ease });
            BeginAnimation(TopProperty,  new DoubleAnimation(Top,  ty, dur) { EasingFunction = ease });
        });
    }

    // Liquid "droplet" smear: orient the stretch along the travel direction (angle in
    // degrees), then elongate the card along it and squash across, snapping back to 1:1
    // with a jelly overshoot on landing. Runs a touch longer than the slide so the
    // wobble settles after arrival.
    private void StartSmear(double angleDeg)
    {
        SmearRotNeg.Angle = -angleDeg;
        SmearRotPos.Angle =  angleDeg;

        var total = new Duration(TimeSpan.FromMilliseconds(470));
        var settle = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.9 };
        var launch = new CubicEase { EasingMode = EasingMode.EaseOut };

        var sx = new DoubleAnimationUsingKeyFrames { Duration = total };
        sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(0.00)));
        sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.55, KeyTime.FromPercent(0.24), launch));
        sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.40, KeyTime.FromPercent(0.62)));
        sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.00), settle));

        var sy = new DoubleAnimationUsingKeyFrames { Duration = total };
        sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(0.00)));
        sy.KeyFrames.Add(new EasingDoubleKeyFrame(0.68, KeyTime.FromPercent(0.24), launch));
        sy.KeyFrames.Add(new EasingDoubleKeyFrame(0.74, KeyTime.FromPercent(0.62)));
        sy.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(1.00), settle));

        SmearScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        SmearScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
    }

    // Clear any smear so a non-liquid show isn't left stretched/rotated.
    private void ResetSmear()
    {
        SmearScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SmearScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SmearScale.ScaleX = SmearScale.ScaleY = 1.0;
        SmearRotNeg.Angle = SmearRotPos.Angle = 0;
    }

    // -------------------------------------------------------------------------
    // Liquid bridge transition (app-switch): the card melts into a droplet at the
    // old window, a pinching liquid neck stretches across, and it reforms as the
    // card at the new window. Text is hidden during the stretch (just the droplet).
    // -------------------------------------------------------------------------

    private void StartLiquidTransition(Point origin, Point target, string lang)
    {
        ++_showGeneration; // invalidate any pending hide from a prior show

        _liqRadius = 52;
        double pad = _liqRadius + 44; // room for the blob + soft edge
        double minX = Math.Min(origin.X, target.X) - pad;
        double minY = Math.Min(origin.Y, target.Y) - pad;
        double maxX = Math.Max(origin.X, target.X) + pad;
        double maxY = Math.Max(origin.Y, target.Y) + pad;

        // Expand the window to span both points; draw the bridge in window-local coords.
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty,  null);
        Left = minX; Top = minY; Width = maxX - minX; Height = maxY - minY;

        _liqO   = new Point(origin.X - minX, origin.Y - minY);
        _liqT   = new Point(target.X - minX, target.Y - minY);
        _liqLang = lang;

        CardRoot.Visibility = Visibility.Collapsed;
        LangText.Text = "";                 // no language text while it's liquid
        ResetSmear();
        LiquidPath.Fill = Card.Background;   // match the current style's fill
        LiquidPath.Data = LiquidBridge.Build(_liqO, _liqRadius, _liqO, _liqRadius, 1.0);
        LiquidPath.Visibility = Visibility.Visible;

        // Force visible & topmost; cancel any in-flight fade.
        BeginAnimation(OpacityProperty, null);
        Opacity = _maxOpacity;
        _isVisible = true;
        var h = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(h, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        _liquidSw.Restart();
        _liquidActive = true;
        _liquidRender ??= (_, _) => LiquidTick();
        CompositionTarget.Rendering += _liquidRender;
    }

    private void LiquidTick()
    {
        double p  = Math.Min(1.0, _liquidSw.Elapsed.TotalMilliseconds / (LiquidMs / _transitionSpeed));
        double pe = p < 0.5 ? 2 * p * p : 1 - Math.Pow(-2 * p + 2, 2) / 2; // ease in-out

        var head = new Point(_liqO.X + (_liqT.X - _liqO.X) * pe,
                             _liqO.Y + (_liqT.Y - _liqO.Y) * pe);

        double originR = _liqRadius * Math.Max(0, 1 - p / 0.55); // origin puddle drains away
        double full    = (_liqT - _liqO).Length;
        double sep     = (head - _liqO).Length;
        double neck    = full <= 1 ? 0 : Math.Max(0, 1 - sep / (full * 0.42)); // thins, then snaps

        LiquidPath.Data = LiquidBridge.Build(_liqO, originR, head, _liqRadius, neck);

        if (p >= 1.0) EndLiquidTransition();
    }

    // Bridge arrived: drop the liquid layer, snap the window back to card size centred
    // on the target, and pop the card in with the usual spring + scramble.
    private void EndLiquidTransition()
    {
        double targetX = _liqT.X + Left, targetY = _liqT.Y + Top; // local → screen DIP
        StopLiquid();

        Width = _cardW; Height = _cardH;
        Left  = targetX - Width / 2;
        Top   = targetY - Height / 2;

        _isVisible = false; // force the spring entrance
        ScrambleTo(_liqLang);
        PresentAndScheduleHide();
    }

    // Tear down the liquid layer and restore the card (without presenting).
    private void StopLiquid()
    {
        if (!_liquidActive) return;
        _liquidActive = false;
        _liquidSw.Stop();
        if (_liquidRender != null) CompositionTarget.Rendering -= _liquidRender;
        LiquidPath.Visibility = Visibility.Collapsed;
        LiquidPath.Data = null;
        CardRoot.Visibility = Visibility.Visible;
    }

    // Centre of a window rect in DIP screen coords; false for a degenerate rect.
    private bool RectCenterDip(NativeMethods.RECT rect, out Point center)
    {
        center = default;
        if (rect.Right - rect.Left < 8 || rect.Bottom - rect.Top < 8) return false;
        var src = PresentationSource.FromVisual(this);
        double m11 = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double m22 = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
        center = new Point((rect.Left + rect.Right) / 2.0 * m11,
                           (rect.Top + rect.Bottom) / 2.0 * m22);
        return true;
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
            double dx  = _offsetX;
            double dy  = _offsetY;
            double pad = 80; // edge padding for non-center modes

            (Left, Top) = _positionMode switch
            {
                "TopCenter"    => (cx + dx,                         dipT + pad + dy),
                "BottomCenter" => (cx + dx,                         dipT + dipH - Height - pad + dy),
                "TopLeft"      => (dipL + pad + dx,                 dipT + pad + dy),
                "TopRight"     => (dipL + dipW - Width - pad + dx,  dipT + pad + dy),
                "BottomLeft"   => (dipL + pad + dx,                 dipT + dipH - Height - pad + dy),
                "BottomRight"  => (dipL + dipW - Width - pad + dx,  dipT + dipH - Height - pad + dy),
                _              => (cx + dx,                         cy + dy)  // "Center"
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

        if (!_scrambleEnabled || string.IsNullOrEmpty(target)) { LangText.Text = target; return; }

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
