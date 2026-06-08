using System.Text.Json;

namespace SqwizzeySwitch.Models;

public class AppSettings
{
    // --- Overlay ---
    public bool   OverlayEnabled  { get; set; } = true;
    public int    ShowDurationMs  { get; set; } = 800;
    public double MaxOpacity      { get; set; } = 0.88;

    // --- Appearance ---
    // "Center" | "TopCenter" | "BottomCenter" | "TopLeft" | "TopRight" | "BottomLeft" | "BottomRight"
    public string PositionMode    { get; set; } = "Center";
    public int    OffsetY         { get; set; } = 0;
    // "Dark" | "Light" | "Auto"
    public string Theme           { get; set; } = "Dark";

    // --- Behavior ---
    public bool   SkipFullscreen  { get; set; } = true;
    public bool   StartWithWindows{ get; set; } = false;

    // -------------------------------------------------------------------------

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SqwizzeySwitch", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json   = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch { /* fall through to defaults */ }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    public AppSettings Clone() => new()
    {
        OverlayEnabled   = OverlayEnabled,
        ShowDurationMs   = ShowDurationMs,
        MaxOpacity       = MaxOpacity,
        PositionMode     = PositionMode,
        OffsetY          = OffsetY,
        Theme            = Theme,
        SkipFullscreen   = SkipFullscreen,
        StartWithWindows = StartWithWindows,
    };
}
