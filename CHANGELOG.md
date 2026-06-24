# Changelog

All notable changes to **Sqwizzey Switch** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-06-24

### Added
- Settings **style picker** that renders each overlay style for real, with the Accent style using your system accent colour.
- **3×3 position grid** in Settings for choosing where the card appears on screen.
- **Live animated preview** in Settings that plays the card's entrance (letter scramble and spring), with a replay button.

### Changed
- "Card animation (spring)" and "Scramble letters" are now **two independent switches**, so you can enable either one on its own.

### Fixed
- Removed the dark drop-shadow halo that trailed the card on dark themes; the lift is kept on light themes, and the Neon/Accent glows are unaffected.
- Pinned the overlay window to no DWM system backdrop, so a Mica/acrylic fill can no longer appear behind the card.

### Removed
- The old combined "scramble + spring" animation toggle, replaced by the two separate switches above.

## [1.2.0] - 2026-06-19

### Added
- Liquid "drop" animation when the card moves between windows on app switch.
- Transition-speed slider to control how fast the app-switch animation plays.
- New "Animations" section in Settings that groups all motion controls together.
- Apply button and click-outside-to-close in the Settings window.
- App logo now shows in the system tray.

### Changed
- The liquid transition can be toggled independently of the scramble/spring animation.
- The overlay on/off toggle moved into Settings (double-click the tray icon to open them).

### Fixed
- Flags and tray icons now display correctly in the single-file portable build (assets are bundled instead of missing).

## [1.1.0] - 2026-06-18

### Added
- Layout detection in modern Notepad and WinUI / XAML-island apps, tracking the keyboard layout across the focused window's input threads.
- Layout card in PowerShell and other consoles via a Win+Space / Alt+Shift hotkey hook (consoles don't expose their live layout).
- Optional calculator card ("123") with the same animation.
- Configurable per-window exclusion list (by process name, window-title substring, or `<process>:topmost`) to hide the overlay where you don't want it.
- Tray icon that shows the active language as a flag (default), circle, rounded square, or letter, with a "current → next" tooltip.
- New "Pill" overlay style and a refreshed app logo.

### Changed
- Redesigned settings window: scrollable, with pinned action buttons and options grouped into Overlay, Appearance, Behavior, Tray icon, and Windows-integration cards.
- Renamed the "Small Rounded" overlay style to "Pill".

## [1.0.1] - 2026-06-15

### Added
- Settings in 8 languages plus an automatic option that follows your system language.
- "Offset X" control to fine-tune the overlay's horizontal position.

### Changed
- Redesigned settings window with restyled sliders.
- Reworked the overlay glow/shadow into a smooth halo without banding.
- Split the overlay styles into separate "Minimal" and "Small Rounded".
- Overlay text is now optically centered.

### Removed
- The broken language-popup toggle in settings.
- The macOS overlay style.

## [1.0.0] - 2026-06-15

Initial public release.

### Added
- Overlay card that shows the active keyboard language (EN, RU, …) when you switch layout (Win+Space / Alt+Shift), in the style of the macOS indicator; it fades out automatically.
- Several overlay styles (macOS, Glass, Accent, Minimal, Neon) with Dark / Light / Auto theming.
- "Show on app switch" (follow-focus): the indicator appears and glides to the centre of the newly focused window.
- Fluent (WPF-UI) settings window with a live preview of the card.
- Skip-fullscreen option (no overlay over games), start-with-Windows, and a click-through overlay that never steals focus or shows in Alt+Tab.
- Windows installer (no administrator rights required) and a self-contained portable build.

[1.3.0]: https://github.com/fairdenn/Sqwizzey-Switch/releases/tag/v1.3.0
[1.2.0]: https://github.com/fairdenn/Sqwizzey-Switch/releases/tag/v1.2.0
[1.1.0]: https://github.com/fairdenn/Sqwizzey-Switch/releases/tag/v1.1.0
[1.0.1]: https://github.com/fairdenn/Sqwizzey-Switch/releases/tag/v1.0.1
[1.0.0]: https://github.com/fairdenn/Sqwizzey-Switch/releases/tag/v1.0.0
