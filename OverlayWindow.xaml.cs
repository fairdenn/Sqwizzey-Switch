using SqwizzeySwitch.Helpers;
using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SqwizzeySwitch;

public partial class OverlayWindow : Window
{
    private int    _showDurationMs;
    private double _maxOpacity;
    private string _positionMode;
    private int    _offsetY;

    private System.Threading.Timer? _hideTimer;
    private bool _isVisible;

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
    }

    // -------------------------------------------------------------------------
    // Settings hot-reload — called after user saves the settings dialog
    // -------------------------------------------------------------------------

    public void ApplySettings(AppSettings s)
    {
        Dispatcher.Invoke(() =>
        {
            _showDurationMs = s.ShowDurationMs;
            _maxOpacity     = s.MaxOpacity;
            _positionMode   = s.PositionMode;
            _offsetY        = s.OffsetY;
            ApplyTheme(s.Theme);
        });
    }

    private void ApplyTheme(string theme)
    {
        bool isDark = theme switch
        {
            "Dark"  => true,
            "Light" => false,
            _       => IsSystemDarkTheme()  // "Auto"
        };

        Card.Background = new SolidColorBrush(isDark
            ? Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E)
            : Color.FromArgb(0xCC, 0xFA, 0xFA, 0xFA));

        LangText.Foreground = isDark
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }

    // -------------------------------------------------------------------------
    // Public API called from the keyboard service
    // -------------------------------------------------------------------------

    public void ShowLanguage(string lang)
    {
        Dispatcher.Invoke(() =>
        {
            LangText.Text = lang;
            PositionOnActiveMonitor();

            _hideTimer?.Dispose();
            _hideTimer = new System.Threading.Timer(
                _ => Dispatcher.Invoke(StartFadeOut),
                null, _showDurationMs, Timeout.Infinite);

            if (!_isVisible)
            {
                _isVisible = true;
                StartFadeIn();
            }
            // If already visible: just text + timer updated, no new animation → no flicker
        });
    }

    // -------------------------------------------------------------------------
    // Positioning
    // -------------------------------------------------------------------------

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

    private void StartFadeIn()
    {
        var anim = new DoubleAnimation(Opacity, _maxOpacity,
            new Duration(TimeSpan.FromMilliseconds(130)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private void StartFadeOut()
    {
        _isVisible = false;
        var anim = new DoubleAnimation(Opacity, 0,
            new Duration(TimeSpan.FromMilliseconds(200)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        BeginAnimation(OpacityProperty, anim);
    }
}
