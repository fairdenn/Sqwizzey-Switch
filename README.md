# Sqwizzey Switch

Индикатор раскладки клавиатуры для Windows 11. При переключении языка показывает по центру экрана небольшую полупрозрачную карточку с кодом языка (EN, RU и т.д.) — аналогично macOS.

## Что делает

- Всплывающий overlay при смене раскладки (Win+Space / Alt+Shift)
- Автоматически исчезает через 800 мс
- Не перехватывает фокус и клики мышью
- Не отображается в Alt+Tab
- Работает на всех мониторах с поддержкой разного DPI

## Стек

- C# / .NET 8 / WPF
- Windows Forms (NotifyIcon для системного трея)
- Win32 API (P/Invoke)

## Установка

Скачать последний релиз или собрать из исходников:

```cmd
git clone https://github.com/fairdenn/sqwizzey-switch.git
cd sqwizzey-switch
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\
```

Требования: Windows 10/11, .NET 8 SDK (для сборки).

## Возможности

- Настройки через системный трей: позиция, прозрачность, время показа, тема (Dark / Light / Auto)
- Исключение полноэкранных приложений (игры)
- Автозапуск с Windows
- Inno Setup скрипт для сборки установщика

## Статус

В разработке. Сборка и тестирование на Windows.
