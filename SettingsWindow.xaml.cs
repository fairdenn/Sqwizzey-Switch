using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using System.Windows;
using System.Windows.Controls;

namespace SqwizzeySwitch;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    // _settings is the live object owned by App — we write back to it only on Save.
    private readonly AppSettings _settings;

    public event Action<AppSettings>? SettingsSaved;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadValues();
    }

    // -------------------------------------------------------------------------
    // Populate controls from current settings
    // -------------------------------------------------------------------------

    private void LoadValues()
    {
        SliderDuration.Value = _settings.ShowDurationMs;
        SliderOpacity.Value  = _settings.MaxOpacity;
        SliderOffsetY.Value  = _settings.OffsetY;

        SelectComboByTag(CbStyle,    _settings.Style);
        SelectComboByTag(CbPosition, _settings.PositionMode);
        SelectComboByTag(CbTheme,    _settings.Theme);

        ChkAnimations.IsChecked     = _settings.AnimationsEnabled;
        ChkFollowFocus.IsChecked    = _settings.FollowFocusEnabled;
        ChkSkipFullscreen.IsChecked = _settings.SkipFullscreen;
        ChkStartup.IsChecked        = StartupService.IsEnabled();

        // Reflects the live registry state, not a stored setting (system-wide).
        ChkHidePopup.IsChecked      = NativeLanguageSwitchService.IsSuppressed();

        UpdatePreview();
    }

    // -------------------------------------------------------------------------
    // Live preview — renders the in-window card exactly like the real overlay
    // -------------------------------------------------------------------------

    private void UpdatePreview()
    {
        // Fired during InitializeComponent (combo coercion) before the fields exist.
        if (PreviewCard is null || PreviewText is null) return;

        OverlayStyle.Apply(TagOf(CbStyle) ?? "macOS", TagOf(CbTheme) ?? "Dark", PreviewCard, PreviewText, PreviewGlow);
        if (SliderOpacity != null)
            PreviewCard.Opacity = SliderOpacity.Value; // mirror MaxOpacity
    }

    private void Preview_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    // -------------------------------------------------------------------------
    // System: suppress Windows' own language popup (needs admin once)
    // -------------------------------------------------------------------------

    private void ChkHidePopup_Click(object sender, RoutedEventArgs e)
    {
        bool desired = ChkHidePopup.IsChecked == true;

        if (!NativeLanguageSwitchService.Apply(desired))
        {
            ChkHidePopup.IsChecked = !desired; // revert: UAC declined or write failed
            return;
        }

        var restart = MessageBox.Show(this,
            "Setting applied. Restart Explorer now so it takes effect?\n" +
            "Your taskbar will blink for a moment.",
            "Sqwizzey Switch", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (restart == MessageBoxResult.Yes)
            NativeLanguageSwitchService.RestartExplorer();
    }

    private static void SelectComboByTag(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem item in cb.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedItem = item;
                return;
            }
        }
        cb.SelectedIndex = 0;
    }

    // -------------------------------------------------------------------------
    // Slider labels
    // -------------------------------------------------------------------------

    // The labels are declared after the sliders in XAML, so these events fire
    // during InitializeComponent (value coercion) before the label fields exist.
    // Guard against the null label until the window is fully built.
    private void SliderDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblDuration is null) return;
        LblDuration.Text = $"{(int)SliderDuration.Value} ms";
    }

    private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblOpacity is null) return;
        LblOpacity.Text = $"{(int)(SliderOpacity.Value * 100)}%";
        UpdatePreview();
    }

    private void SliderOffsetY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblOffsetY is null) return;
        LblOffsetY.Text = $"{(int)SliderOffsetY.Value} px";
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
        => Close();

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowDurationMs = (int)SliderDuration.Value;
        _settings.MaxOpacity     = SliderOpacity.Value;
        _settings.OffsetY        = (int)SliderOffsetY.Value;
        _settings.Style          = TagOf(CbStyle)     ?? "macOS";
        _settings.PositionMode   = TagOf(CbPosition)  ?? "Center";
        _settings.Theme          = TagOf(CbTheme)     ?? "Dark";
        _settings.AnimationsEnabled = ChkAnimations.IsChecked == true;
        _settings.FollowFocusEnabled = ChkFollowFocus.IsChecked == true;
        _settings.SkipFullscreen = ChkSkipFullscreen.IsChecked == true;

        bool wantStartup = ChkStartup.IsChecked == true;
        if (wantStartup != StartupService.IsEnabled())
        {
            StartupService.SetEnabled(wantStartup);
            _settings.StartWithWindows = wantStartup;
        }

        _settings.Save();
        SettingsSaved?.Invoke(_settings);
        Close();
    }

    private static string? TagOf(ComboBox cb)
        => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString();
}
