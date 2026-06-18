<p align="center">
  <img src="assets/banner.png" alt="Sqwizzey Switch" width="100%">
</p>

<h1 align="center">Sqwizzey Switch</h1>

<p align="center">
  Лёгкий индикатор раскладки для Windows 11 — небольшая карточка с кодом языка (<b>EN / RU</b>) при смене раскладки, в стиле macOS.
  <br>
  <a href="README.md">English</a> · <b>Русский</b>
</p>

## Как это работает

1. **Смени раскладку** (Win+Space / Alt+Shift) — появляется карточка с кодом языка и сама исчезает.
2. **Включи «режим активного окна»** — карточка показывается на сфокусированном окне и при переключении приложений *перелетает в его центр*, чтобы ты всегда видел текущий язык там, где печатаешь.
3. **Настрой под себя** — стиль, позицию, анимации и тайминг в окне настроек на Fluent, с живым превью.

## Стили

<p align="center">
  <img src="assets/themes.png" alt="Стили: Frosted, Accent, Minimal, Pill, Neon" width="90%">
</p>

## Возможности

- Карточка по центру при смене раскладки, с анимациями scramble + пружина
- **Режим активного окна** — карточка следует за фокусным окном и перелетает в его центр
- 5 стилей с живым превью
- Настройка длительности, прозрачности, позиции и сдвига по X/Y
- Пропуск полноэкранных приложений (игры) · Запуск вместе с Windows
- Многоязычный интерфейс: **Авто + English, Русский, Українська, Español, Deutsch, Français, 中文, Português**
- Прозрачен для кликов, скрыт из Alt+Tab, несколько мониторов с разным DPI

## Значок языка в трее

В настройках можно включить **«Язык на значке в трее»** — иконка показывает язык активного
окна (стили: только текст как в Windows, кружок, скруглённый квадрат), при наведении —
подсказка `тек → след`.

> **Рекомендация:** чтобы вместо стандартного индикатора языка Windows был только наш значок,
> установите мод [**Taskbar tray system icon tweaks**](https://windhawk.net/mods/taskbar-tray-system-icon-tweaks)
> (Windhawk) и включите опцию **Hide language bar**. Стандартную панель самому приложению не
> скрыть: Windows не хранит эту настройку в реестре/API — её прячут только инъекцией в Explorer
> (что и делает мод). Наш индикатор + этот мод отлично дополняют друг друга.

## Скачать

Установщик — на странице [последнего релиза](../../releases/latest): `SqwizzeySwitch-Setup-1.1.0.exe`. Прав администратора не требует; приложение самодостаточно (.NET ставить не нужно).

## Сборка из исходников

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (и [Inno Setup](https://jrsoftware.org/isdl.php) для установщика).

```cmd
git clone https://github.com/fairdenn/Sqwizzey-Switch.git
cd Sqwizzey-Switch

dotnet run                                   :: запуск в dev-режиме

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o publish\      :: self-contained .exe

iscc setup.iss                               :: установщик (нужен Inno Setup)
```

## Стек

C# · .NET 8 · WPF · [WPF-UI](https://github.com/lepoco/wpfui) · Win32 API
