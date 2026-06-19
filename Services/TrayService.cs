using SqwizzeySwitch.Helpers;
using SqwizzeySwitch.Models;
using System.Drawing;
using System.Windows.Forms;

namespace SqwizzeySwitch.Services;

public sealed class TrayService : IDisposable
{
    public event Action? ExitRequested;
    public event Action? SettingsRequested;

    private readonly NotifyIcon       _notifyIcon;
    private readonly AppSettings      _settings;
    private          ToolStripMenuItem _startupItem = null!;
    private          IntPtr            _lastIconHandle = IntPtr.Zero; // HICON to free on next swap
    private          Icon?             _logoIcon;                     // cached app-logo tray icon

    private string L => Loc.Resolve(_settings.Language);

    public TrayService(AppSettings settings)
    {
        _settings   = settings;
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text    = Loc.T("tray.title", L)
        };
        ApplyLogoIcon(); // default icon = app logo (App calls SetLanguage if the option is on)

        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        // Overlay on/off now lives in Settings; double-click opens it (the app has no main window).
        _notifyIcon.DoubleClick     += (_, _) => SettingsRequested?.Invoke();
    }

    // Rebuilds the menu in the current language — called after settings are saved.
    public void Relocalize()
    {
        _notifyIcon.Text = Loc.T("tray.title", L);
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem(Loc.T("tray.settings", L));
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold); // highlight
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        _startupItem = new ToolStripMenuItem(Loc.T("startup", L))
        {
            Checked = StartupService.IsEnabled()
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        var exitItem = new ToolStripMenuItem(Loc.T("tray.exit", L));
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ToggleStartup()
    {
        var enable = !StartupService.IsEnabled();
        StartupService.SetEnabled(enable);
        _settings.StartWithWindows = enable;
        _settings.Save();
        _startupItem.Checked = enable;
    }

    public void RefreshStartupCheck()
        => _startupItem.Checked = StartupService.IsEnabled();

    /// <summary>Render the tray icon as the active language (e.g. "RU") with a current→other
    /// tooltip. Called by App on every window/layout change when the option is on.</summary>
    public void SetLanguage(string code, string tooltip)
    {
        // A single big letter (first of the code) stays crisp at the tiny tray size; the
        // full code lives in the tooltip.
        _notifyIcon.Text = string.IsNullOrEmpty(tooltip) ? code : tooltip;

        if (string.Equals(_settings.TrayIconStyle, "Flag", StringComparison.OrdinalIgnoreCase)
            && TryApplyFlagIcon(code))
            return;

        var glyph = string.IsNullOrEmpty(code) ? "?" : code.Substring(0, 1);
        ApplyTextIcon(glyph, accent: true);
    }

    // Loads the embedded assets/flags/<code>.png resource and renders it as the tray icon
    // with rounded corners. Reading from an embedded resource (not a file on disk) is what
    // makes flags work in the single-file portable build. Returns false if there's no flag
    // for this language → caller falls back to the lettered icon, keeping the look uniform.
    private bool TryApplyFlagIcon(string code)
    {
        try
        {
            var uri  = new Uri($"pack://application:,,,/assets/flags/{code.ToLowerInvariant()}.png");
            var info = System.Windows.Application.GetResourceStream(uri); // throws if missing
            if (info == null) return false;

            const int S = 32;
            using var stream = info.Stream;
            using var src = new Bitmap(stream);
            using var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                using var clip = new System.Drawing.Drawing2D.GraphicsPath();
                float r = 6;
                clip.AddArc(0, 0, r * 2, r * 2, 180, 90);
                clip.AddArc(S - r * 2, 0, r * 2, r * 2, 270, 90);
                clip.AddArc(S - r * 2, S - r * 2, r * 2, r * 2, 0, 90);
                clip.AddArc(0, S - r * 2, r * 2, r * 2, 90, 90);
                clip.CloseFigure();
                g.SetClip(clip);
                // Letterbox the flag (keep aspect) centred in the square.
                float scale = Math.Min((float)S / src.Width, (float)S / src.Height);
                float w = src.Width * scale, h = src.Height * scale;
                g.DrawImage(src, (S - w) / 2, (S - h) / 2, w, h);
            }
            IntPtr handle = bmp.GetHicon();
            _notifyIcon.Icon = Icon.FromHandle(handle);
            if (_lastIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(_lastIconHandle);
            _lastIconHandle = handle;
            return true;
        }
        catch { return false; }
    }

    /// <summary>Restore the app-logo icon and title (the language option is turned off).</summary>
    public void ResetIcon()
    {
        ApplyLogoIcon();
        _notifyIcon.Text = Loc.T("tray.title", L);
    }

    // Sets the tray icon to the embedded app logo (assets/icon.ico). Loaded once and cached.
    // Used as the default icon and whenever the live-language indicator is off. Falls back to
    // the lettered icon if the resource can't be loaded for any reason.
    private void ApplyLogoIcon()
    {
        try
        {
            if (_logoIcon == null)
            {
                var uri  = new Uri("pack://application:,,,/assets/icon.ico");
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info != null)
                {
                    using var s = info.Stream;
                    _logoIcon = new Icon(s, new Size(32, 32)); // multi-res .ico; tray picks a small frame
                }
            }
            if (_logoIcon != null)
            {
                _notifyIcon.Icon = _logoIcon;
                // The logo is a managed Icon (not a GetHicon handle) — just drop any prior HICON.
                if (_lastIconHandle != IntPtr.Zero) { NativeMethods.DestroyIcon(_lastIconHandle); _lastIconHandle = IntPtr.Zero; }
                return;
            }
        }
        catch { /* fall through to the lettered fallback */ }

        ApplyTextIcon("L", accent: false);
    }

    // Builds a 16×16 text icon, applies it, and frees the previous HICON (GetHicon leaks
    // otherwise). accent=true → green tint for the live language indicator.
    private void ApplyTextIcon(string text, bool accent)
    {
        var (fill, fg) = TrayPalette(_settings.Style, _settings.Theme);
        var (icon, handle) = BuildTextIcon(text, _settings.TrayIconStyle, fill, fg);
        _notifyIcon.Icon = icon;
        if (_lastIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(_lastIconHandle);
        _lastIconHandle = handle;
    }

    // Tray icon colours. Shaped styles use the classic vivid blue with white text (always
    // readable on the taskbar, regardless of card theme); Plain uses white text only.
    private static (Color fill, Color text) TrayPalette(string style, string theme)
        => (Color.FromArgb(55, 110, 210), Color.White);

    // style: "Plain" (text only, like Windows) | "Circle" | "Square". Rendered at 32×32 so
    // two-letter codes (RU/EN) stay crisp; Windows scales it down for the tray.
    private static (Icon icon, IntPtr handle) BuildTextIcon(string text, string style, Color fill, Color textColor)
    {
        const int S = 32;
        using var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            bool plain = string.Equals(style, "Plain", StringComparison.OrdinalIgnoreCase);
            // Plain mode follows Windows: no shape, text in the theme's foreground colour.
            Color ink = plain ? textColor : textColor;

            if (!plain)
            {
                using var bg = new SolidBrush(fill);
                if (string.Equals(style, "Square", StringComparison.OrdinalIgnoreCase))
                    FillRoundedRect(g, bg, 0, 0, S, S, 8);
                else
                    g.FillEllipse(bg, 0, 0, S - 1, S - 1); // Circle
            }

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap,
            };

            // Auto-fit: grow the font until the text just fills the available box (the full
            // icon for Plain, the circle's inner square otherwise), so RU/EN are as large as
            // possible without clipping — readable even after the 16px tray downscale.
            float box = plain ? S * 0.96f : S * 0.80f;
            float fontSize = box; // upper bound; shrink to fit
            using var fg = new SolidBrush(ink);
            for (; fontSize > 4f; fontSize -= 0.5f)
            {
                using var probe = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                var sz = g.MeasureString(text, probe, S, sf);
                if (sz.Width <= box && sz.Height <= box) break;
            }
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString(text, font, fg, new RectangleF(0, 0, S, S), sf);
        }
        IntPtr h = bmp.GetHicon();
        return (Icon.FromHandle(h), h);
    }

    private static void FillRoundedRect(Graphics g, Brush b, float x, float y, float w, float h, float r)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(b, path);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (_lastIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(_lastIconHandle);
        _logoIcon?.Dispose();
    }
}
