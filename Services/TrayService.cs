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
    private          ToolStripMenuItem _toggleItem  = null!;
    private          ToolStripMenuItem _startupItem = null!;

    public TrayService(AppSettings settings)
    {
        _settings   = settings;
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text    = "Sqwizzey Switch — keyboard layout indicator",
            Icon    = BuildTrayIcon()
        };

        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        _notifyIcon.DoubleClick     += (_, _) => ToggleOverlay();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings…");
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold); // highlight
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        _toggleItem = new ToolStripMenuItem(
            _settings.OverlayEnabled ? "Disable Overlay" : "Enable Overlay");
        _toggleItem.Click += (_, _) => ToggleOverlay();

        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupService.IsEnabled()
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ToggleOverlay()
    {
        _settings.OverlayEnabled = !_settings.OverlayEnabled;
        _settings.Save();
        _toggleItem.Text = _settings.OverlayEnabled ? "Disable Overlay" : "Enable Overlay";
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

    private static Icon BuildTrayIcon()
    {
        using var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(Color.FromArgb(220, 55, 110, 210));
            g.FillEllipse(bg, 1, 1, 14, 14);

            using var font = new Font("Segoe UI", 7f, FontStyle.Bold, GraphicsUnit.Point);
            using var fg   = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("L", font, fg, new RectangleF(1, 1, 14, 14), sf);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
