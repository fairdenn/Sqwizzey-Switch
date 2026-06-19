# Settings Visual Pickers — Implementation Plan (Plan 1 of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the text dropdowns in Settings with a visual style picker (real `OverlayStyle` renders), a 3×3 position grid, and a live preview that actually plays the overlay animation — packaged as reusable controls.

**Architecture:** Two new WPF `UserControl`s — `StylePicker` (a grid of cards, each rendering the real card via `OverlayStyle.Apply`) and `AnimatedPreview` (the existing static preview upgraded to replay scramble/spring). `SettingsWindow` swaps `CbStyle`/`CbPosition` for these. Follow-focus already drives position-section visibility; reuse that. No new dependencies.

**Tech Stack:** C# / .NET 8 / WPF + WPF-UI 4.3.0. No test framework in this repo — **verification is `dotnet build -c Release` (must be 0 errors) plus a screenshot via the temporary `--opensettings` CLI hook + `PrintWindow(h,hdc,2)`** (the pattern used in prior sessions). Commits authored as `fairdenn <fairdenn@yandex.ru>`, no Co-Authored-By.

## Global Constraints

- .NET 8 / WPF; target framework already set — do not change it.
- All user-facing strings go through `Loc.T(key, lang)` in `Localization.cs`, populated for all 8 languages (`en, ru, uk, es, de, fr, zh, pt`). English is index 0 and the fallback.
- Card visuals MUST come from `OverlayStyle.Apply(style, theme, Border, TextBlock, Border?)` — never hand-roll a second renderer (single source of truth with the live overlay).
- Real style geometry (from `OverlayStyle.cs`): Frosted/Accent/Neon 110×80, Minimal 92×66, Pill 96×44. Accent fill = system accent (`OverlayStyle.GetAccentColor()`).
- Commit author: `git -c user.name=fairdenn -c user.email=fairdenn@yandex.ru commit …`.
- Build command: `dotnet build -c Release -nologo`. A task is "green" only at 0 errors.

---

### Task 1: Add `OnboardingDone` setting (foundation flag, used by Plan 2)

**Files:**
- Modify: `Models/AppSettings.cs`

**Interfaces:**
- Produces: `AppSettings.OnboardingDone` (bool, default false), copied in `Clone()`.

- [ ] **Step 1: Add the property.** In `Models/AppSettings.cs`, in the `--- Behavior ---` region, after `CloseSettingsOnClickOutside`, add:

```csharp
    // First-run onboarding wizard shown once; set true when finished/skipped.
    public bool   OnboardingDone { get; set; } = false;
```

- [ ] **Step 2: Copy it in `Clone()`.** After the `CloseSettingsOnClickOutside = CloseSettingsOnClickOutside,` line in `Clone()`, add:

```csharp
        OnboardingDone   = OnboardingDone,
```

- [ ] **Step 3: Build.** Run `dotnet build -c Release -nologo`. Expected: 0 errors.

- [ ] **Step 4: Commit.**

```bash
git add Models/AppSettings.cs
git -c user.name=fairdenn -c user.email=fairdenn@yandex.ru commit -m "Add OnboardingDone setting flag"
```

---

### Task 2: `StylePicker` control — grid of real-render style cards

**Files:**
- Create: `Controls/StylePicker.xaml`
- Create: `Controls/StylePicker.xaml.cs`

**Interfaces:**
- Produces:
  - `StylePicker.SelectedStyle` (string get/set; one of `Glass|Accent|Minimal|Pill|Neon`).
  - `StylePicker.Theme` (string get/set; `Dark|Light|Auto`) — re-renders cards on change.
  - `event SelectionChanged` (`System.Action<string>?`) fired when the user picks a style.
- Consumes: `OverlayStyle.Apply(...)` (existing).

- [ ] **Step 1: Create the XAML.** `Controls/StylePicker.xaml` — a `UniformGrid` of 5 selectable cards. Each card hosts a `Border`(card) + `TextBlock`(text) + backing `Border`(glow) so `OverlayStyle.Apply` can render into it at half scale via a `LayoutTransform`.

```xml
<UserControl x:Class="SqwizzeySwitch.Controls.StylePicker"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="Cell" TargetType="Border">
      <Setter Property="Background" Value="#222229"/>
      <Setter Property="BorderBrush" Value="#3A3A46"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CornerRadius" Value="10"/>
      <Setter Property="Margin" Value="5"/>
      <Setter Property="Padding" Value="6,8"/>
      <Setter Property="Cursor" Value="Hand"/>
    </Style>
  </UserControl.Resources>
  <ItemsControl x:Name="Items">
    <ItemsControl.ItemsPanel>
      <ItemsPanelTemplate><UniformGrid Columns="3"/></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
  </ItemsControl>
</UserControl>
```

- [ ] **Step 2: Create the code-behind.** `Controls/StylePicker.xaml.cs`. Builds one cell per style; each cell renders the real card at 0.5 scale and centres it in a fixed-height stage so cells align. Clicking a cell selects it and raises `SelectionChanged`.

```csharp
using SqwizzeySwitch; // OverlayStyle
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SqwizzeySwitch.Controls;

public partial class StylePicker : UserControl
{
    private static readonly (string tag, string name)[] Styles =
    {
        ("Glass", "Frosted"), ("Accent", "Accent"), ("Minimal", "Minimal"),
        ("Pill", "Pill"), ("Neon", "Neon"),
    };

    private string _selected = "Pill";
    private string _theme = "Dark";
    private readonly Dictionary<string, Border> _cells = new();

    public event Action<string>? SelectionChanged;

    public string SelectedStyle
    {
        get => _selected;
        set { _selected = value; Rebuild(); }
    }

    public string Theme
    {
        get => _theme;
        set { _theme = value; Rebuild(); }
    }

    public StylePicker()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    // Localized display names can be applied by the host after construction.
    public void SetDisplayName(string tag, string name)
    {
        if (_cells.TryGetValue(tag, out var cell) && cell.Child is StackPanel sp
            && sp.Children.Count > 1 && sp.Children[1] is TextBlock t)
            t.Text = name;
    }

    private void Rebuild()
    {
        Items.Items.Clear();
        _cells.Clear();
        foreach (var (tag, name) in Styles)
        {
            var cardHost = BuildMiniCard(tag);
            var label = new TextBlock
            {
                Text = name, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 0)
            };
            var stack = new StackPanel();
            stack.Children.Add(cardHost);
            stack.Children.Add(label);

            var cell = new Border { Style = (Style)Resources["Cell"], Child = stack };
            if (tag == _selected)
            {
                cell.BorderBrush = new SolidColorBrush(Color.FromRgb(0xA9, 0x8B, 0xEC));
                cell.BorderThickness = new Thickness(2);
            }
            string captured = tag;
            cell.MouseLeftButtonUp += (_, _) =>
            {
                if (_selected == captured) return;
                _selected = captured;
                Rebuild();
                SelectionChanged?.Invoke(captured);
            };
            _cells[tag] = cell;
            Items.Items.Add(cell);
        }
    }

    // Renders the real card for `style` via OverlayStyle, scaled to 0.5, centred in a
    // fixed 46px-high stage so cells with different card heights stay aligned.
    private FrameworkElement BuildMiniCard(string style)
    {
        var glow = new Border { IsHitTestVisible = false };
        var text = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Segoe UI")
        };
        var card = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Child = text
        };
        OverlayStyle.Apply(style, _theme, card, text, glow);
        text.Text = "EN";

        var inner = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        inner.Children.Add(glow);
        inner.Children.Add(card);
        inner.LayoutTransform = new ScaleTransform(0.5, 0.5);

        return new Grid { Height = 46, Children = { inner } };
    }
}
```

- [ ] **Step 3: Build.** `dotnet build -c Release -nologo`. Expected: 0 errors. (Namespace note: `OverlayStyle` is in `SqwizzeySwitch`; `StylePicker` is in `SqwizzeySwitch.Controls`. The `using SqwizzeySwitch;` covers it.)

- [ ] **Step 4: Commit.**

```bash
git add Controls/StylePicker.xaml Controls/StylePicker.xaml.cs
git -c user.name=fairdenn -c user.email=fairdenn@yandex.ru commit -m "Add StylePicker control (real-render style cards)"
```

---

### Task 3: `AnimatedPreview` control — preview that replays the overlay animation

**Files:**
- Create: `Controls/AnimatedPreview.xaml`
- Create: `Controls/AnimatedPreview.xaml.cs`

**Interfaces:**
- Produces:
  - `AnimatedPreview.Apply(string style, string theme, double opacity)` — re-renders via `OverlayStyle` and plays the entrance.
  - `AnimatedPreview.Play()` — replays scramble + spring on demand (the ▶ button calls this).
- Consumes: `OverlayStyle.Apply(...)`.

**Note on animation reuse:** The overlay's scramble/spring lives in `OverlayWindow.xaml.cs` (`ScrambleTo`, `StartFadeIn`). To avoid duplicating it verbatim, this control implements a **self-contained** scramble timer + a `BackEase` scale storyboard mirroring `StartFadeIn` (0.55→1, 420ms) and scramble (~360ms). This is intentional duplication of ~30 lines kept simple; a shared helper is out of scope for Plan 1.

- [ ] **Step 1: Create the XAML.** `Controls/AnimatedPreview.xaml` — gradient stage, a glow + card + text with a `ScaleTransform`/`TranslateTransform`, and a small ▶ button.

```xml
<UserControl x:Class="SqwizzeySwitch.Controls.AnimatedPreview"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Border Height="104" CornerRadius="8">
    <Border.Background>
      <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="#2B2B30" Offset="0"/><GradientStop Color="#3A3A42" Offset="1"/>
      </LinearGradientBrush>
    </Border.Background>
    <Grid>
      <Grid x:Name="CardRoot" HorizontalAlignment="Center" VerticalAlignment="Center"
            RenderTransformOrigin="0.5,0.5">
        <Grid.RenderTransform>
          <TransformGroup>
            <ScaleTransform x:Name="CardScale" ScaleX="1" ScaleY="1"/>
            <TranslateTransform x:Name="CardT" Y="0"/>
          </TransformGroup>
        </Grid.RenderTransform>
        <Border x:Name="Glow" IsHitTestVisible="False"/>
        <Border x:Name="Card">
          <TextBlock x:Name="LangText" Text="EN" FontFamily="Segoe UI"
                     HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
      </Grid>
      <Button x:Name="ReplayBtn" Content="▶" Width="22" Height="22" Padding="0"
              HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,8,8"
              Click="ReplayBtn_Click"/>
    </Grid>
  </Border>
</UserControl>
```

- [ ] **Step 2: Create the code-behind.** `Controls/AnimatedPreview.xaml.cs`:

```csharp
using SqwizzeySwitch; // OverlayStyle
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SqwizzeySwitch.Controls;

public partial class AnimatedPreview : UserControl
{
    private static readonly Random _rng = new();
    private const string Scramble = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private DispatcherTimer? _scrambleTimer;
    private string _text = "EN";

    public AnimatedPreview() => InitializeComponent();

    /// <summary>Re-render the card for the given style/theme/opacity and play the entrance.</summary>
    public void Apply(string style, string theme, double opacity)
    {
        OverlayStyle.Apply(style, theme, Card, LangText, Glow);
        Card.Opacity = opacity;
        Play();
    }

    public void Play()
    {
        // spring entrance — mirrors OverlayWindow.StartFadeIn
        var spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 };
        var dur = new Duration(TimeSpan.FromMilliseconds(420));
        var scale = new DoubleAnimation(0.55, 1.0, dur) { EasingFunction = spring };
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        CardT.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(16, 0, dur) { EasingFunction = spring });
        ScrambleTo(_text);
    }

    private void ScrambleTo(string target)
    {
        _scrambleTimer?.Stop();
        var total = TimeSpan.FromMilliseconds(360);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _scrambleTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(28) };
        _scrambleTimer.Tick += (_, _) =>
        {
            double p = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / total.TotalMilliseconds);
            int locked = (int)Math.Floor(p * target.Length);
            var chars = new char[target.Length];
            for (int k = 0; k < target.Length; k++)
                chars[k] = k < locked ? target[k] : Scramble[_rng.Next(Scramble.Length)];
            LangText.Text = new string(chars);
            if (p >= 1.0) { LangText.Text = target; _scrambleTimer!.Stop(); }
        };
        _scrambleTimer.Start();
    }

    private void ReplayBtn_Click(object sender, RoutedEventArgs e) => Play();
}
```

- [ ] **Step 3: Build.** `dotnet build -c Release -nologo`. Expected: 0 errors.

- [ ] **Step 4: Commit.**

```bash
git add Controls/AnimatedPreview.xaml Controls/AnimatedPreview.xaml.cs
git -c user.name=fairdenn -c user.email=fairdenn@yandex.ru commit -m "Add AnimatedPreview control (replays scramble + spring)"
```

---

### Task 4: Position 3×3 grid + localization keys

**Files:**
- Modify: `Localization.cs`
- (Position grid is built inline in `SettingsWindow` in Task 5 — this task only adds the strings it needs.)

**Interfaces:**
- Produces: loc keys `stylePicker` already exist as `style`; add `replay` (tooltip). No new position strings needed — the 3×3 grid uses icons, not text; the existing `position` caption stays.

- [ ] **Step 1: Add the replay tooltip key.** In `Localization.cs`, after the `["transitionSpeed"]` entry, add:

```csharp
        ["replay"]       = new[]{"Replay",                      "Повторить",                           "Повторити",                           "Repetir",                          "Erneut",                               "Rejouer",                               "重播",                    "Repetir"},
```

- [ ] **Step 2: Build.** `dotnet build -c Release -nologo`. Expected: 0 errors.

- [ ] **Step 3: Commit.**

```bash
git add Localization.cs
git -c user.name=fairdenn -c user.email=fairdenn@yandex.ru commit -m "Add replay tooltip localization"
```

---

### Task 5: Integrate pickers + animated preview into SettingsWindow

**Files:**
- Modify: `SettingsWindow.xaml` (replace `CbStyle` ComboBox with `StylePicker`; replace `CbPosition` ComboBox with a 3×3 grid; replace the static preview `Border` block with `AnimatedPreview`)
- Modify: `SettingsWindow.xaml.cs` (load/save through the new controls; wire `SelectionChanged` to preview; keep follow-focus visibility)

**Interfaces:**
- Consumes: `StylePicker` (Task 2), `AnimatedPreview` (Task 3).

**Pattern to follow:** the existing `SelectComboByTag`/`TagOf` helpers and the `RaisePreview()`/`UpdatePreview()` flow. Position still maps to `PositionMode` strings (`Center`, `TopLeft`, …). The 3×3 grid: 9 `ToggleButton`s (or `Border`s) whose `Tag` is the `PositionMode` value; selecting one updates `_positionMode` and calls `RaisePreview()`.

- [ ] **Step 1: Add the XAML namespace.** In `SettingsWindow.xaml` root element add:

```xml
        xmlns:ctl="clr-namespace:SqwizzeySwitch.Controls"
```

- [ ] **Step 2: Replace the preview block.** Replace the `<!-- ── Live preview ── -->` `Border` (the whole `PreviewContainer` block) with:

```xml
            <!-- ── Live preview ── -->
            <ctl:AnimatedPreview x:Name="Preview" Margin="0,0,0,14"/>
```

- [ ] **Step 3: Replace the Style row.** In the Appearance card, replace the Style `<Grid>` (the one with `CbStyle`) with:

```xml
                    <ui:TextBlock x:Name="LblStyleCap" Text="Style" Margin="0,0,0,6"/>
                    <ctl:StylePicker x:Name="StylePick"/>
```

- [ ] **Step 4: Replace the Position row.** Replace the Position `<Grid>` (with `CbPosition`) inside `PositionGroup` with a 3×3 grid of `ToggleButton`s, each `Tag` set to its `PositionMode` (top-left→bottom-right; centre = `Center`):

```xml
                        <ui:TextBlock x:Name="LblPositionCap" Text="Position" Margin="0,8,0,6"/>
                        <UniformGrid x:Name="PosGrid" Columns="3" Width="96" HorizontalAlignment="Left">
                          <ToggleButton Tag="TopLeft"     Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="TopCenter"   Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="TopRight"    Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="BottomLeft"  Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="Center"      Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="BottomCenter" Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="BottomLeft"  Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="BottomCenter" Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                          <ToggleButton Tag="BottomRight" Width="28" Height="22" Margin="2" Click="Pos_Click"/>
                        </UniformGrid>
```

> NOTE: the visible 3×3 maps the 7 supported `PositionMode` values plus repeats for unused cells — when implementing, use exactly the 7 real values mapped to their geometric cell (TL, TC, TR, —, Center, —, BL, BC, BR) and leave the two side-middle cells disabled (`IsEnabled="False"`), since `PositionMode` has no MiddleLeft/MiddleRight. Correct the placeholder above accordingly during implementation.

- [ ] **Step 5: Update code-behind — fields + load.** In `SettingsWindow.xaml.cs`:
  - Add field `private string _positionMode = "Center";`
  - In `LoadValues()`, replace `SelectComboByTag(CbStyle, …)` with `StylePick.SelectedStyle = _settings.Style; StylePick.Theme = _settings.Theme;` and `StylePick.SelectionChanged += s => { UpdatePreview(); };`
  - Replace `SelectComboByTag(CbPosition, …)` with `_positionMode = _settings.PositionMode; SyncPosGrid();`
  - Replace `UpdatePreview()` body to call `Preview.Apply(StylePick.SelectedStyle, TagOf(CbTheme) ?? "Dark", SliderOpacity.Value);`

```csharp
    private void SyncPosGrid()
    {
        foreach (System.Windows.Controls.Primitives.ToggleButton b in PosGrid.Children)
            b.IsChecked = (string)b.Tag == _positionMode && b.IsEnabled;
    }

    private void Pos_Click(object sender, RoutedEventArgs e)
    {
        var b = (System.Windows.Controls.Primitives.ToggleButton)sender;
        _positionMode = (string)b.Tag;
        SyncPosGrid();
        if (_initDone) RaisePreview();
    }
```

- [ ] **Step 6: Update code-behind — save + preview + theme.**
  - In `BtnApply_Click`, replace `_settings.PositionMode = TagOf(CbPosition) ?? "Center";` with `_settings.PositionMode = _positionMode;` and `_settings.Style = StylePick.SelectedStyle;`
  - In `RaisePreview()`, set `s.Style = StylePick.SelectedStyle;` and `s.PositionMode = _positionMode;`
  - In `CbTheme` `Preview_Changed`, also set `StylePick.Theme = TagOf(CbTheme) ?? "Dark";`
  - Remove now-dead references to `CbStyle`/`CbPosition` (and their localization loops in `ApplyLanguage`). Apply localized style names via `StylePick.SetDisplayName("Glass", Loc.T("trayFlag"...))` — NOTE: style names (Frosted/Accent/…) are currently hard-coded English in the ComboBox; keep them English (they are brand-style names, not translated) — so no per-language style strings are needed.

- [ ] **Step 7: Build.** `dotnet build -c Release -nologo`. Expected: 0 errors. Fix any remaining `CbStyle`/`CbPosition` references the compiler flags.

- [ ] **Step 8: Verify visually.** Add the temporary `--opensettings` hook (see spec verification), launch `bin/Release/.../SqwizzeySwitch.exe --opensettings`, screenshot the settings window via `PrintWindow`, confirm: style cards render real looks; Pill is short/wide; Accent blue; position 3×3 present; preview plays on style change; position hides when "Show on active window" is on. Remove the temp hook.

- [ ] **Step 9: Commit.**

```bash
git add SettingsWindow.xaml SettingsWindow.xaml.cs
git -c user.name=fairdenn -c user.email=fairdenn@yandex.ru commit -m "Settings: visual style picker, 3x3 position grid, animated preview"
```

---

## Self-Review (done against the spec)

- **Coverage:** Spec B1 (StylePicker real renders) → Task 2; B2 (3×3 position, follow-focus conditional) → Task 5 steps 4–5 (visibility reuses existing `UpdatePositionVisibility`/`FollowFocus_Toggled` — unchanged, still toggles `PositionGroup` which now contains `PosGrid`); B3 (animated preview) → Task 3 + Task 5 step 5; `OnboardingDone` foundation → Task 1; localization → Task 4. Onboarding wizard (spec Part A) is **Plan 2**, intentionally deferred.
- **Placeholders:** Task 5 Step 4 contains a deliberately-flagged placeholder (the 3×3 cell mapping) with an explicit NOTE on how to finalize — resolve during implementation using the 7 real `PositionMode` values + 2 disabled cells.
- **Type consistency:** `SelectedStyle`/`Theme`/`SelectionChanged` (Task 2) used consistently in Task 5; `Apply(style, theme, opacity)`/`Play()` (Task 3) used in Task 5.

## After Plan 1

Plan 2 (onboarding wizard) builds `OnboardingWindow` reusing `StylePicker` + `AnimatedPreview`, plus the `App.xaml.cs` first-run trigger on `OnboardingDone`. Write it after Plan 1 is green.
