using System.Text.Json;

namespace SqwizzeySwitch.Models;

public class AppSettings
{
    // --- Overlay ---
    public bool   OverlayEnabled  { get; set; } = true;
    public int    ShowDurationMs  { get; set; } = 800;
    public double MaxOpacity      { get; set; } = 0.88;
    // scrambleText + spring entrance; off → instant text & snap show/hide
    public bool   AnimationsEnabled { get; set; } = true;

    // --- Appearance ---
    // "macOS" | "Glass" | "Accent" | "Minimal" | "Neon"
    public string Style           { get; set; } = "Rounded";
    // "Center" | "TopCenter" | "BottomCenter" | "TopLeft" | "TopRight" | "BottomLeft" | "BottomRight"
    public string PositionMode    { get; set; } = "Center";
    public int    OffsetX         { get; set; } = 0;
    public int    OffsetY         { get; set; } = 0;
    // "Dark" | "Light" | "Auto" — light/dark modifier (ignored by Accent & Neon)
    public string Theme           { get; set; } = "Dark";

    // --- Behavior ---
    public bool   SkipFullscreen  { get; set; } = true;
    public bool   StartWithWindows{ get; set; } = false;
    // Show the indicator on app switch and slide it to the focused window's centre
    public bool   FollowFocusEnabled { get; set; } = false;

    // --- UI language: "auto" (follow system) | "en" | "ru" | "uk" | "es" | "de" | "fr" | "zh" | "pt" ---
    public string Language { get; set; } = "auto";

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
        OverlayEnabled    = OverlayEnabled,
        ShowDurationMs    = ShowDurationMs,
        MaxOpacity        = MaxOpacity,
        AnimationsEnabled = AnimationsEnabled,
        Style            = Style,
        PositionMode     = PositionMode,
        OffsetX          = OffsetX,
        OffsetY          = OffsetY,
        Theme            = Theme,
        SkipFullscreen   = SkipFullscreen,
        StartWithWindows = StartWithWindows,
        FollowFocusEnabled = FollowFocusEnabled,
        Language           = Language,
    };
}
