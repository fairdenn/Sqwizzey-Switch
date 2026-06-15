# Sqwizzey Switch

A lightweight keyboard-layout overlay for Windows 11 — shows a small **EN / RU** badge when you switch input language, macOS-style.

**English** · [Русский](#sqwizzey-switch--русский)

## Features

- Centered overlay badge on layout switch, with scramble + spring animations
- **Active-window mode** — show the badge on the focused window and slide it to that window's centre when you switch apps
- 5 theme presets: **macOS, Frosted, Accent, Minimal, Neon** — with a live preview
- Fluent settings window (WPF-UI, Mica)
- Adjustable duration, opacity, position and X/Y offset
- Skip fullscreen apps (games) · Start with Windows
- Multi-language UI: **Auto + English, Русский, Українська, Español, Deutsch, Français, 中文, Português**
- Click-through, hidden from Alt+Tab, works across multiple monitors with per-monitor DPI

## Download

Grab the installer from the [latest release](../../releases/latest) — `SqwizzeySwitch-Setup-1.0.0.exe`. No administrator rights required; the app is self-contained (no .NET install needed).

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (and [Inno Setup](https://jrsoftware.org/isdl.php) for the installer).

```cmd
git clone https://github.com/fairdenn/Sqwizzey-Switch.git
cd Sqwizzey-Switch

dotnet run                                   :: dev run

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o publish\      :: self-contained .exe

iscc setup.iss                               :: installer (needs Inno Setup)
```

## Tech stack

C# · .NET 8 · WPF · [WPF-UI](https://github.com/lepoco/wpfui) · Win32 API

---

# Sqwizzey Switch · Русский

Лёгкий индикатор раскладки клавиатуры для Windows 11 — при смене языка показывает небольшую карточку с кодом языка (**EN / RU**), в стиле macOS.

[English](#sqwizzey-switch) · **Русский**

## Возможности

- Карточка по центру при смене раскладки, с анимациями scramble + пружина
- **Режим активного окна** — показывать карточку на сфокусированном окне и при переключении приложений «перелетать» в его центр
- 5 стилей: **macOS, Frosted, Accent, Minimal, Neon** — с живым превью
- Окно настроек на Fluent (WPF-UI, Mica)
- Настройка длительности, прозрачности, позиции и сдвига по X/Y
- Пропуск полноэкранных приложений (игры) · Запуск вместе с Windows
- Многоязычный интерфейс: **Авто + English, Русский, Українська, Español, Deutsch, Français, 中文, Português**
- Прозрачен для кликов, скрыт из Alt+Tab, работает на нескольких мониторах с разным DPI

## Скачать

Установщик — на странице [последнего релиза](../../releases/latest): `SqwizzeySwitch-Setup-1.0.0.exe`. Прав администратора не требует; приложение самодостаточно (.NET ставить не нужно).

## Сборка из исходников

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (и [Inno Setup](https://jrsoftware.org/isdl.php) для установщика) — команды см. в английском разделе выше.

## Стек

C# · .NET 8 · WPF · [WPF-UI](https://github.com/lepoco/wpfui) · Win32 API
