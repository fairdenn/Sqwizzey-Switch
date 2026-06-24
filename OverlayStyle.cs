using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SqwizzeySwitch;

/// <summary>
/// Computes and applies the visual look of an overlay card for a given style/theme.
/// Shared by the live <see cref="OverlayWindow"/> and the Settings live preview, so the
/// preview renders exactly like the real overlay. Sets the card's fill, border, corner
/// radius and text, plus a soft glow/shadow rendered as a <see cref="RadialGradientBrush"/>
/// on a backing element behind the card — NOT a DropShadowEffect: on the layered overlay
/// window (AllowsTransparency=True) a WPF Effect paints an opaque rectangular bounding box
/// (a grey/dark rectangle, very visible on light backgrounds). The gradient halo gives the
/// same glow without that artefact. Returns the card geometry (width, height).
/// </summary>
internal static class OverlayStyle
{
    /// <summary>Applies <paramref name="style"/>/<paramref name="theme"/> to the card, text
    /// and (optional) <paramref name="glow"/> backing element. Returns the card (w, h).</summary>
    public static (double Width, double Height) Apply(
        string style, string theme, Border card, TextBlock text, Border? glow)
    {
        bool isDark = theme switch
        {
            "Dark"  => true,
            "Light" => false,
            _       => IsSystemDarkTheme()  // "Auto"
        };

        // macOS defaults
        double w = 110, h = 80, radius = 18, fontSize = 34;
        Brush  cardBg;
        Brush  textFg;
        Brush  borderBrush = Brushes.Transparent;
        double borderThk   = 0;

        // Glow/shadow halo (gradient, not an Effect). null = no halo.
        Color  glowColor   = Color.FromArgb(0x59, 0, 0, 0); // soft black shadow
        double glowCoreStop = 0.18;  // how far the solid colour holds before fading
        double glowSpread   = 34;    // how far the halo extends beyond the card (px)
        bool   hasGlow      = true;

        switch (style)
        {
            case "Glass": // shown as "Frosted" — translucent frosted-glass look.
            {
                if (isDark)
                {
                    cardBg = new LinearGradientBrush(
                        Color.FromArgb(0x82, 0x50, 0x52, 0x5C),
                        Color.FromArgb(0x82, 0x20, 0x21, 0x28),
                        new Point(0, 0), new Point(0, 1));
                    borderBrush = new LinearGradientBrush(
                        Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF),
                        new Point(0, 0), new Point(0, 1));
                    textFg = Brushes.White;
                }
                else
                {
                    cardBg = new LinearGradientBrush(
                        Color.FromArgb(0xA8, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(0x90, 0xEC, 0xEC, 0xF2),
                        new Point(0, 0), new Point(0, 1));
                    borderBrush = new LinearGradientBrush(
                        Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF),
                        Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF),
                        new Point(0, 0), new Point(0, 1));
                    textFg = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                }
                borderThk   = 1.2;
                glowColor   = Color.FromArgb(0x2E, 0, 0, 0);
                glowCoreStop = 0.0;
                glowSpread  = 28;
                break;
            }

            case "Accent":
            {
                var a = GetAccentColor();
                cardBg     = new SolidColorBrush(Color.FromArgb(0xEB, a.R, a.G, a.B));
                textFg     = Brushes.White;
                glowColor  = Color.FromArgb(0x72, a.R, a.G, a.B);
                glowCoreStop = 0.05;
                glowSpread = 24;
                break;
            }

            case "Pill": // pill
                w = 96; h = 44; radius = 22; fontSize = 22;
                cardBg = new SolidColorBrush(isDark
                    ? Color.FromArgb(0xED, 0x14, 0x14, 0x16)
                    : Color.FromArgb(0xF2, 0xFA, 0xFA, 0xFA));
                textFg     = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                glowColor  = Color.FromArgb(0x33, 0, 0, 0);
                glowCoreStop = 0.0;
                glowSpread = 28;
                break;

            case "Minimal": // small dark rounded square
                w = 92; h = 66; radius = 16; fontSize = 30;
                cardBg = new SolidColorBrush(isDark
                    ? Color.FromArgb(0xEC, 0x1B, 0x1B, 0x1E)
                    : Color.FromArgb(0xF2, 0xFA, 0xFA, 0xFA));
                textFg     = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                glowColor  = Color.FromArgb(0x33, 0, 0, 0);
                glowCoreStop = 0.0;
                glowSpread = 28;
                break;

            case "Neon":
            {
                var neon = Color.FromRgb(0x5A, 0xF2, 0xFF);
                cardBg      = new SolidColorBrush(Color.FromArgb(0xE0, 0x0A, 0x0A, 0x10));
                textFg      = new SolidColorBrush(neon);
                borderBrush = new SolidColorBrush(neon);
                borderThk   = 1.5;
                glowColor   = Color.FromArgb(0x72, neon.R, neon.G, neon.B);
                glowCoreStop = 0.05;
                glowSpread  = 26;
                break;
            }

            default: // "macOS"
                cardBg = new SolidColorBrush(isDark
                    ? Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E)
                    : Color.FromArgb(0xCC, 0xFA, 0xFA, 0xFA));
                textFg = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                glowColor  = Color.FromArgb(0x38, 0, 0, 0);
                glowCoreStop = 0.0;
                glowSpread = 34;
                break;
        }

        card.Width           = w;
        card.Height          = h;
        card.CornerRadius    = new CornerRadius(radius);
        card.Background      = cardBg;
        card.BorderBrush     = borderBrush;
        card.BorderThickness = new Thickness(borderThk);
        text.FontSize        = fontSize;
        text.Foreground      = textFg;
        // Optical centering — the font sits a hair low; nudge it up.
        text.Margin          = new Thickness(0, 0, 0, fontSize * 0.10);

        // A pure-black drop-shadow halo is meant to lift a near-white card off a light
        // background. On a DARK theme it sits as a dark blob under an already-dark card —
        // no contrast gained, and it reads as a travelling "blur" rectangle behind the
        // overlay. So drop the shadow halos on dark themes; keep the coloured Neon/Accent
        // halos (non-black) always — those are a deliberate look, not a shadow.
        if (isDark && glowColor.R == 0 && glowColor.G == 0 && glowColor.B == 0)
            hasGlow = false;

        if (glow != null)
        {
            if (hasGlow)
            {
                glow.Visibility  = Visibility.Visible;
                glow.Width       = w + glowSpread * 2;
                glow.Height      = h + glowSpread * 2;
                glow.Background   = Halo(glowColor, glowCoreStop);
                glow.CornerRadius = new CornerRadius((radius + glowSpread) / 2);
            }
            else
            {
                glow.Visibility = Visibility.Collapsed;
            }
        }

        return (w, h);
    }

    /// <summary>A radial halo brush: solid <paramref name="core"/> in the centre fading to
    /// fully transparent at the edge — a soft glow/shadow without a DropShadowEffect.</summary>
    private static RadialGradientBrush Halo(Color core, double coreStop)
    {
        var transparent = Color.FromArgb(0, core.R, core.G, core.B);
        var b = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center         = new Point(0.5, 0.5),
            RadiusX        = 0.5,
            RadiusY        = 0.5
        };
        byte a = core.A;
        // Many closely-spaced stops on a smooth curve → a soft blur-like falloff with
        // no visible rings/banding. coreStop sets how long the centre stays solid.
        const int n = 24;
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / n;
            double f = t <= coreStop ? 1.0 : 1.0 - (t - coreStop) / (1.0 - coreStop);
            f = f * f * f; // cubic ease-out → concentrated near the card, smooth tail
            b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * f), core.R, core.G, core.B), t));
        }
        b.Freeze();
        return b;
    }

    public static Color GetAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int v)
            {
                // Stored as 0xAABBGGRR (ABGR).
                return Color.FromRgb((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF));
            }
        }
        catch { /* fall through to default */ }

        return Color.FromRgb(0x00, 0x67, 0xD1); // Windows default blue
    }

    public static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }
}
