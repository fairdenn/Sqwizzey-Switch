using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SqwizzeySwitch;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    // _settings is the live object owned by App — we write back to it only on Save.
    private readonly AppSettings _settings;
    private string _lang = "en"; // resolved UI language code
    private bool _initDone;       // suppress combo events during InitializeComponent/LoadValues

    public event Action<AppSettings>? SettingsSaved;
    public event Action<AppSettings>? PreviewRequested; // flash the real overlay at the chosen position

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
        SliderOffsetX.Value  = _settings.OffsetX;
        SliderOffsetY.Value  = _settings.OffsetY;

        SelectComboByTag(CbStyle,    _settings.Style);
        SelectComboByTag(CbPosition, _settings.PositionMode);
        SelectComboByTag(CbTheme,    _settings.Theme);
        SelectComboByTag(CbLanguage, _settings.Language);

        ChkAnimations.IsChecked     = _settings.AnimationsEnabled;
        ChkFollowFocus.IsChecked    = _settings.FollowFocusEnabled;
        ChkSkipFullscreen.IsChecked = _settings.SkipFullscreen;
        ChkStartup.IsChecked        = StartupService.IsEnabled();

        ApplyLanguage(_settings.Language);
        UpdatePreview();
        UpdatePositionVisibility();
        _initDone = true;
    }

    // Position / Offset Y only matter when follow-focus is OFF (fixed monitor spot);
    // with app-switch on, the card always flies to the focused window's centre.
    private void UpdatePositionVisibility() // instant, used on load
    {
        bool show = ChkFollowFocus.IsChecked != true;
        PositionGroup.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PositionGroup.Opacity = show ? 1 : 0;
    }

    private void FollowFocus_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;

        bool show = ChkFollowFocus.IsChecked != true;
        if (show)
        {
            PositionGroup.Visibility = Visibility.Visible;
            PositionGroup.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(220)))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            PosGroupT.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-10, 0, new Duration(TimeSpan.FromMilliseconds(260)))
                { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } });
        }
        else
        {
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(150)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += (_, _) => { if (ChkFollowFocus.IsChecked == true) PositionGroup.Visibility = Visibility.Collapsed; };
            PositionGroup.BeginAnimation(OpacityProperty, fade);
            PosGroupT.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, -8, new Duration(TimeSpan.FromMilliseconds(150))));
        }
    }

    // Adjusting position/offset flashes the real overlay so you can see where it lands.
    private void Position_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initDone) return;
        RaisePreview();
    }

    private void RaisePreview()
    {
        var s = _settings.Clone();
        s.Style          = TagOf(CbStyle)    ?? "macOS";
        s.Theme          = TagOf(CbTheme)    ?? "Dark";
        s.PositionMode   = TagOf(CbPosition) ?? "Center";
        s.OffsetX        = (int)SliderOffsetX.Value;
        s.OffsetY        = (int)SliderOffsetY.Value;
        s.MaxOpacity     = SliderOpacity.Value;
        s.ShowDurationMs = (int)SliderDuration.Value;
        PreviewRequested?.Invoke(s);
    }

    // -------------------------------------------------------------------------
    // Localization — applies all UI strings for the chosen language live
    // -------------------------------------------------------------------------

    private void CbLanguage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initDone) return;
        ApplyLanguage(TagOf(CbLanguage) ?? "auto");
    }

    private void ApplyLanguage(string setting)
    {
        _lang = Loc.Resolve(setting); // "auto" → system language (or English fallback)

        Title = TitleBarMain.Title = Loc.T("title", _lang);
        LblLanguageCap.Text = Loc.T("language", _lang);
        if (LangAuto != null) LangAuto.Content = Loc.T("auto", _lang); // relabel the Auto item

        HdrOverlay.Text     = Loc.T("overlay", _lang);
        LblDurationCap.Text = Loc.T("duration", _lang);
        LblOpacityCap.Text  = Loc.T("opacity", _lang);

        HdrAppearance.Text  = Loc.T("appearance", _lang);
        LblStyleCap.Text    = Loc.T("style", _lang);
        LblPositionCap.Text = Loc.T("position", _lang);
        LblOffsetXCap.Text  = Loc.T("offsetX", _lang);
        LblOffsetCap.Text   = Loc.T("offsetY", _lang);
        LblThemeCap.Text    = Loc.T("theme", _lang);
        LblThemeNote.Text   = Loc.T("themeNote", _lang);

        HdrBehavior.Text          = Loc.T("behavior", _lang);
        ChkAnimations.Content     = Loc.T("animations", _lang);
        ChkFollowFocus.Content    = Loc.T("followFocus", _lang);
        ChkSkipFullscreen.Content = Loc.T("skipFs", _lang);
        ChkStartup.Content        = Loc.T("startup", _lang);

        BtnCancel.Content = Loc.T("cancel", _lang);
        BtnSave.Content   = Loc.T("save", _lang);

        foreach (ComboBoxItem item in CbPosition.Items)
            if (item.Tag?.ToString() is { } t) item.Content = Loc.T(t, _lang);
        foreach (ComboBoxItem item in CbTheme.Items)
            item.Content = item.Tag?.ToString() switch
            {
                "Dark"  => Loc.T("Dark", _lang),
                "Light" => Loc.T("Light", _lang),
                "Auto"  => Loc.T("ThemeAuto", _lang),
                _       => item.Content,
            };

        UpdateDurationLabel();
        UpdateOpacityLabel();
        UpdateOffsetXLabel();
        UpdateOffsetLabel();
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
    // Slider labels (units are localized)
    // -------------------------------------------------------------------------

    private void UpdateDurationLabel()
    {
        if (LblDuration is null) return;
        LblDuration.Text = $"{(int)SliderDuration.Value} {Loc.T("ms", _lang)}";
    }

    private void UpdateOpacityLabel()
    {
        if (LblOpacity is null) return;
        LblOpacity.Text = $"{(int)(SliderOpacity.Value * 100)}%";
    }

    private void UpdateOffsetXLabel()
    {
        if (LblOffsetX is null) return;
        LblOffsetX.Text = $"{(int)SliderOffsetX.Value} {Loc.T("px", _lang)}";
    }

    private void UpdateOffsetLabel()
    {
        if (LblOffsetY is null) return;
        LblOffsetY.Text = $"{(int)SliderOffsetY.Value} {Loc.T("px", _lang)}";
    }

    private void SliderOffsetX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initDone) return;
        UpdateOffsetXLabel();
        RaisePreview();
    }

    // These fire during InitializeComponent (slider value coercion) before the rest
    // of the controls exist — skip until the window is fully loaded.
    private void SliderDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initDone) return;
        UpdateDurationLabel();
    }

    private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initDone) return;
        UpdateOpacityLabel();
        UpdatePreview();
    }

    private void SliderOffsetY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initDone) return;
        UpdateOffsetLabel();
        RaisePreview();
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
        _settings.OffsetX        = (int)SliderOffsetX.Value;
        _settings.OffsetY        = (int)SliderOffsetY.Value;
        _settings.Style          = TagOf(CbStyle)     ?? "macOS";
        _settings.PositionMode   = TagOf(CbPosition)  ?? "Center";
        _settings.Theme          = TagOf(CbTheme)     ?? "Dark";
        _settings.Language       = TagOf(CbLanguage)  ?? "en";
        _settings.AnimationsEnabled  = ChkAnimations.IsChecked == true;
        _settings.FollowFocusEnabled = ChkFollowFocus.IsChecked == true;
        _settings.SkipFullscreen     = ChkSkipFullscreen.IsChecked == true;

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
