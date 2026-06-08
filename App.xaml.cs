using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using System.Windows;

namespace SqwizzeySwitch;

public partial class App : Application
{
    private System.Threading.Mutex? _mutex;
    private AppSettings             _settings = null!;
    private KeyboardLayoutService?  _keyboard;
    private TrayService?            _tray;
    private OverlayWindow?          _overlay;
    private SettingsWindow?         _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new System.Threading.Mutex(true, "SqwizzeySwitch_SingleInstance_v1", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        try
        {
            _settings = AppSettings.Load();

            _overlay = new OverlayWindow(
                _settings.ShowDurationMs,
                _settings.MaxOpacity,
                _settings.PositionMode,
                _settings.OffsetY);
            _overlay.Show(); // HWND created; window is invisible (Opacity=0)
            _overlay.ApplySettings(_settings); // apply initial theme

            _tray = new TrayService(_settings);
            _tray.ExitRequested    += () => Dispatcher.Invoke(Shutdown);
            _tray.SettingsRequested += OpenSettingsWindow;

            _keyboard = new KeyboardLayoutService();
            _keyboard.LayoutChanged += OnLayoutChanged;

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

    private void OnLayoutChanged(string lang)
    {
        if (!_settings.OverlayEnabled) return;
        if (_settings.SkipFullscreen && FullscreenDetector.IsForegroundFullscreen()) return;
        _overlay?.ShowLanguage(lang);
    }

    private void OpenSettingsWindow()
    {
        Dispatcher.Invoke(() =>
        {
            // Bring existing window to front instead of opening a second one
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.PreviewRequested += () => _overlay?.ShowLanguage("EN");
            _settingsWindow.SettingsSaved    += OnSettingsSaved;
            _settingsWindow.Closed           += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        });
    }

    private void OnSettingsSaved(AppSettings updated)
    {
        _overlay?.ApplySettings(updated);
        _tray?.RefreshStartupCheck();
        Logger.Log($"Settings saved: theme={updated.Theme} pos={updated.PositionMode} dur={updated.ShowDurationMs}ms");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _keyboard?.Dispose();
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
