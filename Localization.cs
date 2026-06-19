using System.Collections.Generic;
using System.Globalization;

namespace SqwizzeySwitch;

/// <summary>
/// UI string table for the settings window and tray menu. Each entry holds the
/// translations aligned to <see cref="Codes"/>; missing entries fall back to English.
/// </summary>
public static class Loc
{
    //                              0     1     2     3     4     5     6     7
    public static readonly string[] Codes = { "en", "ru", "uk", "es", "de", "fr", "zh", "pt" };

    /// <summary>Resolves a stored setting ("auto" or a code) to a concrete language code.</summary>
    public static string Resolve(string setting)
    {
        if (!string.IsNullOrEmpty(setting) && setting != "auto")
            return System.Array.IndexOf(Codes, setting) >= 0 ? setting : "en";

        var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return System.Array.IndexOf(Codes, sys) >= 0 ? sys : "en";
    }

    public static string T(string key, string lang)
    {
        int i = System.Array.IndexOf(Codes, lang);
        if (i < 0) i = 0;
        if (D.TryGetValue(key, out var a))
            return i < a.Length && !string.IsNullOrEmpty(a[i]) ? a[i] : a[0];
        return key;
    }

    private static readonly Dictionary<string, string[]> D = new()
    {
        //                       en                              ru                                     uk                                     es                                  de                                      fr                                       zh                       pt
        ["title"]        = new[]{"Sqwizzey Switch — Settings",   "Sqwizzey Switch — Настройки",         "Sqwizzey Switch — Налаштування",      "Sqwizzey Switch — Ajustes",        "Sqwizzey Switch — Einstellungen",      "Sqwizzey Switch — Paramètres",          "Sqwizzey Switch — 设置",   "Sqwizzey Switch — Configurações"},
        ["language"]     = new[]{"Language",                     "Язык",                                "Мова",                                "Idioma",                           "Sprache",                              "Langue",                                "语言",                    "Idioma"},
        ["auto"]         = new[]{"Auto",                         "Авто",                                "Авто",                                "Auto",                             "Auto",                                 "Auto",                                  "自动",                    "Auto"},
        ["overlay"]      = new[]{"Overlay",                      "Оверлей",                             "Оверлей",                             "Overlay",                          "Overlay",                              "Overlay",                               "浮层",                    "Overlay"},
        ["duration"]     = new[]{"Duration",                    "Длительность",                        "Тривалість",                          "Duración",                         "Dauer",                                "Durée",                                 "时长",                    "Duração"},
        ["opacity"]      = new[]{"Opacity",                     "Прозрачность",                        "Прозорість",                          "Opacidad",                         "Deckkraft",                            "Opacité",                               "不透明度",                "Opacidade"},
        ["appearance"]   = new[]{"Appearance",                  "Вид",                                 "Вигляд",                              "Apariencia",                       "Darstellung",                          "Apparence",                             "外观",                    "Aparência"},
        ["style"]        = new[]{"Style",                       "Стиль",                               "Стиль",                               "Estilo",                           "Stil",                                 "Style",                                 "样式",                    "Estilo"},
        ["position"]     = new[]{"Position",                    "Позиция",                             "Позиція",                             "Posición",                         "Position",                             "Position",                              "位置",                    "Posição"},
        ["offsetX"]      = new[]{"Offset X",                    "Сдвиг X",                             "Зсув X",                              "Desplaz. X",                       "Versatz X",                            "Décalage X",                            "X 偏移",                  "Desloc. X"},
        ["offsetY"]      = new[]{"Offset Y",                    "Сдвиг Y",                             "Зсув Y",                              "Desplaz. Y",                       "Versatz Y",                            "Décalage Y",                            "Y 偏移",                  "Desloc. Y"},
        ["theme"]        = new[]{"Theme",                       "Тема",                                "Тема",                                "Tema",                             "Design",                               "Thème",                                 "主题",                    "Tema"},
        ["themeNote"]    = new[]{
            "Theme applies to Frosted, Minimal and Pill. Accent and Neon have a fixed look.",
            "Тема влияет на Frosted, Minimal и Pill. Accent и Neon имеют фиксированный вид.",
            "Тема впливає на Frosted, Minimal і Pill. Accent і Neon мають фіксований вигляд.",
            "El tema se aplica a Frosted, Minimal y Pill. Accent y Neon tienen un aspecto fijo.",
            "Das Design gilt für Frosted, Minimal und Pill. Accent und Neon haben ein festes Aussehen.",
            "Le thème s'applique à Frosted, Minimal et Pill. Accent et Neon ont un aspect fixe.",
            "主题适用于 Frosted、Minimal 和 Pill；Accent 和 Neon 外观固定。",
            "O tema se aplica a Frosted, Minimal e Pill. Accent e Neon têm aparência fixa."},
        ["behavior"]     = new[]{"Behavior",                    "Поведение",                           "Поведінка",                           "Comportamiento",                   "Verhalten",                            "Comportement",                          "行为",                    "Comportamento"},
        ["hdrAnimations"]= new[]{"Animations",                  "Анимации",                            "Анімації",                            "Animaciones",                      "Animationen",                          "Animations",                            "动画",                    "Animações"},
        ["overlayEnabled"]= new[]{"Enable overlay",             "Включить оверлей",                    "Увімкнути оверлей",                   "Activar overlay",                  "Overlay aktivieren",                   "Activer l'overlay",                     "开启浮层",                "Ativar overlay"},
        ["closeOnClickOutside"]= new[]{"Close settings when clicking outside","Закрывать настройки при клике вне окна","Закривати налаштування при кліку поза вікном","Cerrar los ajustes al hacer clic fuera","Einstellungen bei Klick außerhalb schließen","Fermer les paramètres en cliquant à l'extérieur","点击窗口外关闭设置","Fechar as configurações ao clicar fora"},
        ["animations"]   = new[]{"Animations (scramble + spring)","Анимации (скрэмбл + пружина)",       "Анімації (скрембл + пружина)",        "Animaciones (scramble + spring)",  "Animationen (Scramble + Spring)",      "Animations (scramble + spring)",        "动画（乱码 + 弹簧）",      "Animações (scramble + spring)"},
        ["liquidTransition"]= new[]{"Liquid drop on app switch",    "Капля при смене окна",                "Крапля при зміні вікна",              "Gota líquida al cambiar de app",   "Flüssiger Tropfen beim Wechsel",       "Goutte liquide au changement",          "切换窗口时的液滴效果",     "Gota líquida ao trocar de app"},
        ["transitionSpeed"]= new[]{"Transition speed",             "Скорость перехода",                   "Швидкість переходу",                  "Velocidad de transición",          "Übergangstempo",                       "Vitesse de transition",                 "过渡速度",                "Velocidade da transição"},
        ["replay"]       = new[]{"Replay",                       "Повторить",                           "Повторити",                           "Repetir",                          "Erneut",                               "Rejouer",                               "重播",                    "Repetir"},
        ["followFocus"]  = new[]{"Show on the active window",     "Показывать на активном окне",         "Показувати на активному вікні",       "Mostrar en la ventana activa",     "Im aktiven Fenster anzeigen",          "Afficher sur la fenêtre active",        "在活动窗口上显示",        "Mostrar na janela ativa"},
        ["skipFs"]       = new[]{"Skip fullscreen apps (games)", "Не показывать в полноэкранных (игры)","Не показувати в повноекранних (ігри)","Omitir apps a pantalla completa (juegos)","Vollbild-Apps überspringen (Spiele)","Ignorer les applis plein écran (jeux)","跳过全屏应用（游戏）",    "Ignorar apps em tela cheia (jogos)"},
        ["startup"]      = new[]{"Start with Windows",           "Запускать вместе с Windows",          "Запускати разом із Windows",          "Iniciar con Windows",              "Mit Windows starten",                  "Démarrer avec Windows",                 "随 Windows 启动",         "Iniciar com o Windows"},
        ["calculatorCard"]= new[]{"Calculator card (123)",       "Карточка калькулятора (123)",         "Картка калькулятора (123)",           "Tarjeta de calculadora (123)",     "Rechner-Karte (123)",                  "Carte calculatrice (123)",              "计算器卡片 (123)",        "Cartão da calculadora (123)"},
        ["perWindowBtn"] = new[]{"Open keyboard settings…",     "Открыть параметры клавиатуры…",       "Відкрити параметри клавіатури…",      "Abrir ajustes de teclado…",        "Tastatureinstellungen öffnen…",        "Ouvrir les paramètres du clavier…",     "打开键盘设置…",            "Abrir configurações do teclado…"},
        ["exclusions"]   = new[]{
            "Don't show for (process name or window title, comma-separated):",
            "Не показывать для (имя процесса или заголовок окна, через запятую):",
            "Не показувати для (ім'я процесу або заголовок вікна, через кому):",
            "No mostrar para (nombre de proceso o título de ventana, separados por comas):",
            "Nicht anzeigen für (Prozessname oder Fenstertitel, durch Komma getrennt):",
            "Ne pas afficher pour (nom de processus ou titre de fenêtre, séparés par des virgules) :",
            "不显示（进程名或窗口标题，逗号分隔）：",
            "Não mostrar para (nome de processo ou título de janela, separados por vírgula):"},
        ["hdrTray"]      = new[]{"Tray icon",                   "Значок в трее",                       "Значок у треї",                       "Icono de bandeja",                 "Infobereich-Symbol",                   "Icône de la barre d'état",              "托盘图标",                "Ícone da bandeja"},
        ["hdrWindows"]   = new[]{"Windows integration",         "Интеграция с Windows",                "Інтеграція з Windows",                "Integración con Windows",          "Windows-Integration",                  "Intégration Windows",                   "Windows 集成",            "Integração com o Windows"},
        ["trayLang"]     = new[]{"Show the active language on the tray icon", "Показывать язык активного окна на значке", "Показувати мову активного вікна на значку", "Mostrar el idioma activo en el icono", "Aktive Sprache im Symbol anzeigen", "Afficher la langue active sur l'icône", "在托盘图标上显示当前语言", "Mostrar o idioma ativo no ícone"},
        ["perWindowDesc"]= new[]{
            "Give each app window its own keyboard language, so switching in one app doesn't change it everywhere. Enable 'Let me set a different input method for each app window' in Windows.",
            "Чтобы у каждого окна был свой язык и переключение в одном приложении не меняло его везде. Включите в Windows «Разрешить выбирать метод ввода для каждого окна приложения».",
            "Щоб кожне вікно мало свою мову й перемикання в одному застосунку не змінювало її всюди. Увімкніть у Windows «Дозволити вибирати метод введення для кожного вікна».",
            "Para que cada ventana tenga su propio idioma y cambiarlo en una app no lo cambie en todas. Active en Windows 'Permitir un método de entrada por ventana'.",
            "Damit jedes Fenster seine eigene Sprache hat und ein Wechsel nicht überall gilt. Aktivieren Sie in Windows 'Für jedes App-Fenster eine andere Eingabemethode'.",
            "Pour que chaque fenêtre ait sa propre langue et qu'un changement ne s'applique pas partout. Activez dans Windows « Définir une méthode de saisie par fenêtre ».",
            "让每个窗口拥有自己的语言，在一个应用中切换不会影响全部。在 Windows 中启用“允许为每个应用窗口设置不同的输入法”。",
            "Para que cada janela tenha seu idioma e trocar em um app não mude em todos. Ative no Windows 'Permitir um método de entrada por janela'."},
        ["trayDesc"]     = new[]{
            "To replace the standard Windows language indicator with this app's tray icon, install the Windhawk mod 'Taskbar tray system icon tweaks' and enable 'Hide language bar'. Windows has no setting for this.",
            "Чтобы вместо стандартного индикатора языка Windows был значок этого приложения, установите мод Windhawk «Taskbar tray system icon tweaks» и включите «Hide language bar». В Windows такой настройки нет.",
            "Щоб замість стандартного індикатора мови Windows був значок цього застосунку, встановіть мод Windhawk «Taskbar tray system icon tweaks» і ввімкніть «Hide language bar». У Windows такого немає.",
            "Para reemplazar el indicador de idioma estándar por este icono, instale el mod de Windhawk 'Taskbar tray system icon tweaks' y active 'Hide language bar'. Windows no tiene esta opción.",
            "Um die Standard-Sprachanzeige durch dieses Symbol zu ersetzen, installieren Sie den Windhawk-Mod 'Taskbar tray system icon tweaks' und aktivieren Sie 'Hide language bar'. Windows bietet dies nicht.",
            "Pour remplacer l'indicateur de langue standard par cette icône, installez le mod Windhawk « Taskbar tray system icon tweaks » et activez « Hide language bar ». Windows ne le propose pas.",
            "要用此应用的图标替换标准语言指示器，请安装 Windhawk 模组“Taskbar tray system icon tweaks”并启用“Hide language bar”。Windows 无此设置。",
            "Para substituir o indicador de idioma padrão por este ícone, instale o mod Windhawk 'Taskbar tray system icon tweaks' e ative 'Hide language bar'. O Windows não tem essa opção."},
        ["trayStyle"]    = new[]{"Icon style:",                 "Стиль значка:",                       "Стиль значка:",                       "Estilo de icono:",                 "Symbolstil:",                          "Style d'icône :",                       "图标样式：",              "Estilo do ícone:"},
        ["trayFlag"]     = new[]{"Flag",                        "Флаг",                                "Прапор",                              "Bandera",                          "Flagge",                               "Drapeau",                               "国旗",                    "Bandeira"},
        ["trayPlain"]    = new[]{"Text only (no shape)",        "Только буква (без фона)",             "Лише літера (без тла)",               "Solo letra (sin fondo)",           "Nur Buchstabe (ohne Form)",            "Lettre seule (sans forme)",             "仅字母（无底）",          "Apenas letra (sem forma)"},
        ["trayCircle"]   = new[]{"Circle",                      "Кружок",                              "Кружок",                              "Círculo",                          "Kreis",                                "Cercle",                                "圆形",                    "Círculo"},
        ["traySquare"]   = new[]{"Rounded square",              "Скруглённый квадрат",                 "Заокруглений квадрат",                "Cuadrado redondeado",              "Abgerundetes Quadrat",                 "Carré arrondi",                         "圆角方形",                "Quadrado arredondado"},
        ["trayWinBtn"]   = new[]{"Get the Windhawk mod…",       "Открыть мод Windhawk…",               "Відкрити мод Windhawk…",              "Obtener el mod de Windhawk…",      "Windhawk-Mod öffnen…",                 "Obtenir le mod Windhawk…",              "获取 Windhawk 模组…",     "Obter o mod do Windhawk…"},
        ["trayNote"]     = new[]{
            "Windows controls tray visibility: pin this icon under 'Other system tray icons', and hide the standard language indicator there.",
            "Видимостью в трее управляет Windows: закрепи этот значок в «Другие значки в области уведомлений» и там же скрой стандартный индикатор языка.",
            "Видимістю в треї керує Windows: закріпи цей значок у «Інші значки в області сповіщень» і там же сховай стандартний індикатор мови.",
            "Windows controla la visibilidad: ancla este icono en 'Otros iconos de la bandeja' y oculta allí el indicador de idioma estándar.",
            "Windows steuert die Sichtbarkeit: Symbol unter 'Weitere Symbole im Infobereich' anheften und dort die Standard-Sprachanzeige ausblenden.",
            "Windows contrôle la visibilité : épinglez cette icône dans « Autres icônes » et masquez-y l'indicateur de langue standard.",
            "可见性由 Windows 控制：在“其他系统托盘图标”中固定此图标，并在那里隐藏标准语言指示器。",
            "O Windows controla a visibilidade: fixe este ícone em 'Outros ícones' e oculte ali o indicador de idioma padrão."},
        ["perWindowNote"]= new[]{
            "Opens Windows settings. Note: does not work with PowerShell / console.",
            "Откроет настройки Windows. Не работает с PowerShell / консолью.",
            "Відкриє налаштування Windows. Не працює з PowerShell / консоллю.",
            "Abre la configuración de Windows. No funciona con PowerShell / consola.",
            "Öffnet die Windows-Einstellungen. Funktioniert nicht mit PowerShell / Konsole.",
            "Ouvre les paramètres Windows. Ne fonctionne pas avec PowerShell / console.",
            "打开 Windows 设置。注意：不适用于 PowerShell / 控制台。",
            "Abre as configurações do Windows. Não funciona com PowerShell / console."},
        ["cancel"]       = new[]{"Cancel",                       "Отмена",                              "Скасувати",                           "Cancelar",                         "Abbrechen",                            "Annuler",                               "取消",                    "Cancelar"},
        ["save"]         = new[]{"Save",                         "Сохранить",                           "Зберегти",                            "Guardar",                          "Speichern",                            "Enregistrer",                           "保存",                    "Salvar"},
        ["apply"]        = new[]{"Apply",                        "Применить",                           "Застосувати",                         "Aplicar",                          "Übernehmen",                           "Appliquer",                             "应用",                    "Aplicar"},
        ["ms"]           = new[]{"ms",                           "мс",                                  "мс",                                  "ms",                               "ms",                                   "ms",                                    "毫秒",                    "ms"},
        ["px"]           = new[]{"px",                           "пкс",                                 "пкс",                                 "px",                               "px",                                   "px",                                    "像素",                    "px"},

        // Position dropdown
        ["Center"]       = new[]{"Center",                      "По центру",                           "По центру",                           "Centro",                           "Mitte",                                "Centre",                                "居中",                    "Centro"},
        ["TopCenter"]    = new[]{"Top Center",                  "Сверху по центру",                    "Зверху по центру",                    "Arriba centro",                    "Oben Mitte",                           "Haut centre",                           "顶部居中",                "Topo centro"},
        ["BottomCenter"] = new[]{"Bottom Center",               "Снизу по центру",                     "Знизу по центру",                     "Abajo centro",                     "Unten Mitte",                          "Bas centre",                            "底部居中",                "Base centro"},
        ["TopLeft"]      = new[]{"Top Left",                    "Сверху слева",                        "Зверху ліворуч",                      "Arriba izq.",                      "Oben links",                           "Haut gauche",                           "左上",                    "Topo esq."},
        ["TopRight"]     = new[]{"Top Right",                   "Сверху справа",                       "Зверху праворуч",                     "Arriba der.",                      "Oben rechts",                          "Haut droite",                           "右上",                    "Topo dir."},
        ["BottomLeft"]   = new[]{"Bottom Left",                 "Снизу слева",                         "Знизу ліворуч",                       "Abajo izq.",                       "Unten links",                          "Bas gauche",                            "左下",                    "Base esq."},
        ["BottomRight"]  = new[]{"Bottom Right",                "Снизу справа",                        "Знизу праворуч",                      "Abajo der.",                       "Unten rechts",                         "Bas droite",                            "右下",                    "Base dir."},

        // Theme dropdown
        ["Dark"]         = new[]{"Dark",                        "Тёмная",                              "Темна",                               "Oscuro",                           "Dunkel",                               "Sombre",                                "深色",                    "Escuro"},
        ["Light"]        = new[]{"Light",                       "Светлая",                             "Світла",                              "Claro",                            "Hell",                                 "Clair",                                 "浅色",                    "Claro"},
        ["ThemeAuto"]    = new[]{"Auto (system)",               "Авто (система)",                      "Авто (система)",                      "Auto (sistema)",                   "Auto (System)",                        "Auto (système)",                        "自动（系统）",            "Auto (sistema)"},

        // Tray menu
        ["tray.title"]   = new[]{"Sqwizzey Switch — keyboard layout indicator","Sqwizzey Switch — индикатор раскладки","Sqwizzey Switch — індикатор розкладки","Sqwizzey Switch — indicador de teclado","Sqwizzey Switch — Tastaturlayout-Anzeige","Sqwizzey Switch — indicateur de clavier","Sqwizzey Switch — 键盘布局指示器","Sqwizzey Switch — indicador de teclado"},
        ["tray.settings"]= new[]{"Settings…",                   "Настройки…",                          "Налаштування…",                       "Ajustes…",                         "Einstellungen…",                       "Paramètres…",                           "设置…",                   "Configurações…"},
        ["tray.disable"] = new[]{"Disable Overlay",             "Выключить оверлей",                   "Вимкнути оверлей",                    "Desactivar overlay",               "Overlay deaktivieren",                 "Désactiver l'overlay",                  "关闭浮层",                "Desativar overlay"},
        ["tray.enable"]  = new[]{"Enable Overlay",              "Включить оверлей",                    "Увімкнути оверлей",                   "Activar overlay",                  "Overlay aktivieren",                   "Activer l'overlay",                     "开启浮层",                "Ativar overlay"},
        ["tray.exit"]    = new[]{"Exit",                        "Выход",                               "Вихід",                               "Salir",                            "Beenden",                              "Quitter",                               "退出",                    "Sair"},
    };
}
