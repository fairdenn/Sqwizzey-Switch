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
            "Theme applies to macOS, Frosted and Minimal. Accent and Neon have a fixed look.",
            "Тема влияет на macOS, Frosted и Minimal. Accent и Neon имеют фиксированный вид.",
            "Тема впливає на macOS, Frosted і Minimal. Accent і Neon мають фіксований вигляд.",
            "El tema se aplica a macOS, Frosted y Minimal. Accent y Neon tienen un aspecto fijo.",
            "Das Design gilt für macOS, Frosted und Minimal. Accent und Neon haben ein festes Aussehen.",
            "Le thème s'applique à macOS, Frosted et Minimal. Accent et Neon ont un aspect fixe.",
            "主题适用于 macOS、Frosted 和 Minimal；Accent 和 Neon 外观固定。",
            "O tema se aplica a macOS, Frosted e Minimal. Accent e Neon têm aparência fixa."},
        ["behavior"]     = new[]{"Behavior",                    "Поведение",                           "Поведінка",                           "Comportamiento",                   "Verhalten",                            "Comportement",                          "行为",                    "Comportamento"},
        ["animations"]   = new[]{"Animations (scramble + spring)","Анимации (скрэмбл + пружина)",       "Анімації (скрембл + пружина)",        "Animaciones (scramble + spring)",  "Animationen (Scramble + Spring)",      "Animations (scramble + spring)",        "动画（乱码 + 弹簧）",      "Animações (scramble + spring)"},
        ["followFocus"]  = new[]{"Show on the active window",     "Показывать на активном окне",         "Показувати на активному вікні",       "Mostrar en la ventana activa",     "Im aktiven Fenster anzeigen",          "Afficher sur la fenêtre active",        "在活动窗口上显示",        "Mostrar na janela ativa"},
        ["skipFs"]       = new[]{"Skip fullscreen apps (games)", "Не показывать в полноэкранных (игры)","Не показувати в повноекранних (ігри)","Omitir apps a pantalla completa (juegos)","Vollbild-Apps überspringen (Spiele)","Ignorer les applis plein écran (jeux)","跳过全屏应用（游戏）",    "Ignorar apps em tela cheia (jogos)"},
        ["startup"]      = new[]{"Start with Windows",           "Запускать вместе с Windows",          "Запускати разом із Windows",          "Iniciar con Windows",              "Mit Windows starten",                  "Démarrer avec Windows",                 "随 Windows 启动",         "Iniciar com o Windows"},
        ["cancel"]       = new[]{"Cancel",                       "Отмена",                              "Скасувати",                           "Cancelar",                         "Abbrechen",                            "Annuler",                               "取消",                    "Cancelar"},
        ["save"]         = new[]{"Save",                         "Сохранить",                           "Зберегти",                            "Guardar",                          "Speichern",                            "Enregistrer",                           "保存",                    "Salvar"},
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
