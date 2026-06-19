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
    private bool _closing;        // guards against a re-entrant Close() from Deactivated
    private string _positionMode = "Center"; // selected PositionMode (3x3 grid)

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
        SliderSpeed.Value    = _settings.TransitionSpeed;
        SliderOffsetX.Value  = _settings.OffsetX;
        SliderOffsetY.Value  = _settings.OffsetY;

        StylePick.Theme = _settings.Theme;
        StylePick.SelectedStyle = _settings.Style;
        StylePick.SelectionChanged += _ => UpdatePreview();
        _positionMode = _settings.PositionMode;
        SyncPosGrid();
        SelectComboByTag(CbTheme,    _settings.Theme);
        SelectComboByTag(CbLanguage, _settings.Language);

        ChkOverlayEnabled.IsChecked = _settings.OverlayEnabled;
        ChkAnimations.IsChecked     = _settings.AnimationsEnabled;
        ChkScramble.IsChecked       = _settings.ScrambleEnabled;
        ChkLiquid.IsChecked         = _settings.LiquidTransition;
        ChkFollowFocus.IsChecked    = _settings.FollowFocusEnabled;
        ChkSkipFullscreen.IsChecked = _settings.SkipFullscreen;
        ChkStartup.IsChecked        = StartupService.IsEnabled();
        ChkCalculator.IsChecked     = _settings.CalculatorCardEnabled;
        ChkCloseOnClickOutside.IsChecked = _settings.CloseSettingsOnClickOutside;
        TxtExclusions.Text          = _settings.ExcludedProcesses;
        ChkTrayLang.IsChecked       = _settings.TrayLanguageIcon;
        SelectComboByTag(CbTrayStyle, _settings.TrayIconStyle);

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

    // 3x3 position grid: a cell was clicked. Selecting flashes the real overlay so you
    // can see where the card lands.
    private void Pos_Click(object sender, RoutedEventArgs e)
    {
        var b = (System.Windows.Controls.Primitives.ToggleButton)sender;
        _positionMode = (string)b.Tag;
        SyncPosGrid();
        if (_initDone) RaisePreview();
    }

    // Checks the cell matching the current PositionMode; the two disabled middle-side
    // cells (no MiddleLeft/MiddleRight in PositionMode) never check.
    private void SyncPosGrid()
    {
        foreach (var child in PosGrid.Children)
            if (child is System.Windows.Controls.Primitives.ToggleButton b)
                b.IsChecked = b.IsEnabled && (string?)b.Tag == _positionMode;
    }

    private void RaisePreview()
    {
        var s = _settings.Clone();
        s.Style          = StylePick.SelectedStyle;
        s.Theme          = TagOf(CbTheme)    ?? "Dark";
        s.PositionMode   = _positionMode;
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
        LblSpeedCap.Text    = Loc.T("transitionSpeed", _lang);

        HdrAppearance.Text  = Loc.T("appearance", _lang);
        LblStyleCap.Text    = Loc.T("style", _lang);
        LblPositionCap.Text = Loc.T("position", _lang);
        LblOffsetXCap.Text  = Loc.T("offsetX", _lang);
        LblOffsetCap.Text   = Loc.T("offsetY", _lang);
        LblThemeCap.Text    = Loc.T("theme", _lang);
        LblThemeNote.Text   = Loc.T("themeNote", _lang);

        HdrAnimations.Text        = Loc.T("hdrAnimations", _lang);
        HdrBehavior.Text          = Loc.T("behavior", _lang);
        ChkOverlayEnabled.Content = Loc.T("overlayEnabled", _lang);
        ChkAnimations.Content     = Loc.T("animations", _lang);
        ChkScramble.Content       = Loc.T("scrambleLetters", _lang);
        ChkLiquid.Content         = Loc.T("liquidTransition", _lang);
        ChkFollowFocus.Content    = Loc.T("followFocus", _lang);
        ChkSkipFullscreen.Content = Loc.T("skipFs", _lang);
        ChkStartup.Content        = Loc.T("startup", _lang);
        ChkCalculator.Content     = Loc.T("calculatorCard", _lang);
        ChkCloseOnClickOutside.Content = Loc.T("closeOnClickOutside", _lang);
        LblExclusionsCap.Text     = Loc.T("exclusions", _lang);

        HdrTray.Text              = Loc.T("hdrTray", _lang);
        ChkTrayLang.Content       = Loc.T("trayLang", _lang);
        LblTrayStyleCap.Text      = Loc.T("trayStyle", _lang);
        foreach (ComboBoxItem item in CbTrayStyle.Items)
            item.Content = item.Tag?.ToString() switch
            {
                "Flag"   => Loc.T("trayFlag", _lang),
                "Plain"  => Loc.T("trayPlain", _lang),
                "Circle" => Loc.T("trayCircle", _lang),
                "Square" => Loc.T("traySquare", _lang),
                _        => item.Content,
            };

        HdrWindows.Text           = Loc.T("hdrWindows", _lang);
        LblPerWindowDesc.Text     = Loc.T("perWindowDesc", _lang);
        BtnPerWindow.Content      = Loc.T("perWindowBtn", _lang);
        LblTrayDesc.Text          = Loc.T("trayDesc", _lang);
        BtnTrayWin.Content        = Loc.T("trayWinBtn", _lang);

        BtnCancel.Content = Loc.T("cancel", _lang);
        BtnApply.Content  = Loc.T("apply", _lang);

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
        UpdateSpeedLabel();
        UpdateOffsetXLabel();
        UpdateOffsetLabel();
    }

    // -------------------------------------------------------------------------
    // Live preview — renders the in-window card exactly like the real overlay
    // -------------------------------------------------------------------------

    private void UpdatePreview()
    {
        // Fired during InitializeComponent (combo coercion) before the fields exist.
        if (Preview is null || StylePick is null) return;
        Preview.Apply(StylePick.SelectedStyle, TagOf(CbTheme) ?? "Dark", SliderOpacity?.Value ?? 1.0);
    }

    // Theme/tray-style combo changed: keep the style picker's theme in sync and refresh preview.
    private void Preview_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (StylePick != null) StylePick.Theme = TagOf(CbTheme) ?? "Dark";
        UpdatePreview();
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

    private void UpdateSpeedLabel()
    {
        if (LblSpeed is null) return;
        LblSpeed.Text = $"{SliderSpeed.Value:0.0}×";
    }

    private void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initDone) return;
        UpdateSpeedLabel();
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

    // Opens Windows' typing settings, where "Let me use a different input method for each app
    // window" (Advanced keyboard settings) lives. We don't toggle it ourselves: it's packed
    // into the multi-purpose UserPreferencesMask, so writing it blind risks other UI prefs.
    private void BtnPerWindow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("ms-settings:typing") { UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Log(ex, nameof(BtnPerWindow_Click)); }
    }

    // Opens the recommended Windhawk mod page — it hides the standard Windows language
    // indicator (Windows itself offers no setting for this; see the mod's "Hide language bar").
    private void BtnTrayWin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://windhawk.net/mods/taskbar-tray-system-icon-tweaks") { UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Log(ex, nameof(BtnTrayWin_Click)); }
    }

    // "Apply" writes the controls back to settings, persists, and applies them live —
    // but keeps the window open so you can keep tweaking. Close via Cancel / the title
    // bar, or (when enabled) by clicking outside the window.
    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowDurationMs = (int)SliderDuration.Value;
        _settings.MaxOpacity     = SliderOpacity.Value;
        _settings.TransitionSpeed = SliderSpeed.Value;
        _settings.OffsetX        = (int)SliderOffsetX.Value;
        _settings.OffsetY        = (int)SliderOffsetY.Value;
        _settings.Style          = StylePick.SelectedStyle;
        _settings.PositionMode   = _positionMode;
        _settings.Theme          = TagOf(CbTheme)     ?? "Dark";
        _settings.Language       = TagOf(CbLanguage)  ?? "en";
        _settings.OverlayEnabled     = ChkOverlayEnabled.IsChecked == true;
        _settings.AnimationsEnabled  = ChkAnimations.IsChecked == true;
        _settings.ScrambleEnabled    = ChkScramble.IsChecked == true;
        _settings.LiquidTransition   = ChkLiquid.IsChecked == true;
        _settings.FollowFocusEnabled = ChkFollowFocus.IsChecked == true;
        _settings.SkipFullscreen     = ChkSkipFullscreen.IsChecked == true;
        _settings.CalculatorCardEnabled = ChkCalculator.IsChecked == true;
        _settings.CloseSettingsOnClickOutside = ChkCloseOnClickOutside.IsChecked == true;
        _settings.ExcludedProcesses     = TxtExclusions.Text?.Trim() ?? "";
        _settings.TrayLanguageIcon      = ChkTrayLang.IsChecked == true;
        _settings.TrayIconStyle         = TagOf(CbTrayStyle) ?? "Plain";

        bool wantStartup = ChkStartup.IsChecked == true;
        if (wantStartup != StartupService.IsEnabled())
        {
            StartupService.SetEnabled(wantStartup);
            _settings.StartWithWindows = wantStartup;
        }

        _settings.Save();
        SettingsSaved?.Invoke(_settings);
    }

    // Click-outside-to-close: the window deactivated (lost focus). Honour the live toggle
    // value, not the saved one, so turning it on takes effect immediately. The overlay
    // preview is a no-activate window, so flashing it doesn't deactivate us.
    //
    // _closing guards against re-entrancy: closing the window raises another Deactivated
    // mid-close, and calling Close() again throws "cannot Close while closing". We also
    // defer via the dispatcher so we never Close() inside the WM_ACTIVATE handler itself.
    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_initDone || _closing) return;
        if (ChkCloseOnClickOutside.IsChecked != true) return;
        _closing = true;
        Dispatcher.BeginInvoke(new Action(Close));
    }

    // Any close path (Cancel, the title-bar button, click-outside) trips the guard so a
    // trailing Deactivated can't fire a second Close().
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    private static string? TagOf(ComboBox cb)
        => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString();
}
