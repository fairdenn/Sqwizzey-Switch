using SqwizzeySwitch.Models;

namespace SqwizzeySwitch;

/// <summary>
/// Surface for the overlay window. Implemented by the layered
/// <see cref="OverlayWindow"/>, which renders every style (Glass shows as a
/// translucent "Frosted" card). App pushes settings updates and the per-switch
/// language flash through this interface.
/// </summary>
public interface IOverlay
{
    void ApplySettings(AppSettings s);
    void ShowLanguage(string lang);
}
