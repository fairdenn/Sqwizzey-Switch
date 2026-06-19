using SqwizzeySwitch; // OverlayStyle
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
