using SqwizzeySwitch.Models;
using SqwizzeySwitch.Services;
using System.Windows;
using System.Windows.Controls;

namespace SqwizzeySwitch;

public partial class SettingsWindow : Window
{
    // _settings is the live object owned by App — we write back to it only on Save.
    private readonly AppSettings _settings;

    public event Action<AppSettings>? SettingsSaved;
    public event Action?              PreviewRequested;

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

        SelectComboByTag(CbPosition, _settings.PositionMode);
        SelectComboByTag(CbTheme,    _settings.Theme);

        ChkSkipFullscreen.IsChecked = _settings.SkipFullscreen;
        ChkStartup.IsChecked        = StartupService.IsEnabled();
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

    private void SliderDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => LblDuration.Text = $"{(int)SliderDuration.Value} ms";

    private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => LblOpacity.Text = $"{(int)(SliderOpacity.Value * 100)}%";

    private void SliderOffsetY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => LblOffsetY.Text = $"{(int)SliderOffsetY.Value} px";

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private void BtnPreview_Click(object sender, RoutedEventArgs e)
        => PreviewRequested?.Invoke();

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
        => Close();

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowDurationMs = (int)SliderDuration.Value;
        _settings.MaxOpacity     = SliderOpacity.Value;
        _settings.OffsetY        = (int)SliderOffsetY.Value;
        _settings.PositionMode   = TagOf(CbPosition)  ?? "Center";
        _settings.Theme          = TagOf(CbTheme)     ?? "Dark";
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
