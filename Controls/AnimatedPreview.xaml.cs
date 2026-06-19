using SqwizzeySwitch; // OverlayStyle
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SqwizzeySwitch.Controls;

public partial class AnimatedPreview : UserControl
{
    private static readonly Random _rng = new();
    private const string Scramble = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private DispatcherTimer? _scrambleTimer;
    private string _text = "EN";
    private bool _animate = true;   // mirror AnimationsEnabled (spring)
    private bool _scramble = true;  // mirror ScrambleEnabled (letters)

    public AnimatedPreview() => InitializeComponent();

    /// <summary>Re-render the card for the given style/theme/opacity and play the entrance,
    /// honouring the live toggles so the preview matches the real overlay's behaviour.</summary>
    public void Apply(string style, string theme, double opacity, bool animate, bool scramble)
    {
        OverlayStyle.Apply(style, theme, Card, LangText, Glow);
        Card.Opacity = opacity;
        _animate = animate;
        _scramble = scramble;
        Play();
    }

    public void Play()
    {
        if (_animate)
        {
            // spring entrance — mirrors OverlayWindow.StartFadeIn
            var spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 };
            var dur = new Duration(TimeSpan.FromMilliseconds(420));
            var scale = new DoubleAnimation(0.55, 1.0, dur) { EasingFunction = spring };
            CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scale);
            CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scale);
            CardT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation(16, 0, dur) { EasingFunction = spring });
        }
        else
        {
            // snap — clear any running spring and sit at rest
            CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            CardT.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
            CardScale.ScaleX = CardScale.ScaleY = 1.0;
            CardT.Y = 0;
        }

        if (_scramble) ScrambleTo(_text);
        else { _scrambleTimer?.Stop(); LangText.Text = _text; }
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
