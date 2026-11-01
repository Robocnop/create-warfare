using System.Windows;
using System.Windows.Media;
using CreateWarfare.Launcher.Controls.Scenes;
using CreateWarfare.Launcher.Themes;

namespace CreateWarfare.Launcher.Controls;

/// <summary>
/// Illustration de l'accueil quand le carrousel n'a pas d'image (et des écrans de travail et de premier lancement) :
/// dessine la scène du thème (ThemeRegistry) et fait tourner son train d'engrenages.
/// </summary>
public sealed class HeroArt : FrameworkElement
{
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(string), typeof(HeroArt), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Thème dessiné ; vide = le thème appliqué (ThemeManager.Current). Sert aux aperçus des paramètres.</summary>
    public string? Theme { get => (string?)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }

    /// <summary>Faux dans le carrousel : le titre est dans la page d'accueil, pas dans l'image.</summary>
    public bool ShowTitle { get; set; } = true;

    double _angle, _time;
    TimeSpan _last;

    public HeroArt()
    {
        Loaded += (_, _) => { CompositionTarget.Rendering += OnFrame; ThemeManager.Changed += InvalidateVisual; };
        Unloaded += (_, _) => { CompositionTarget.Rendering -= OnFrame; ThemeManager.Changed -= InvalidateVisual; };
    }

    void OnFrame(object? sender, EventArgs e)
    {
        // décor coupé (réglage ou Windows) : image fixe, plus aucun rendu par image
        if (!IsVisible || !Motion.DecorEnabled) { _last = TimeSpan.Zero; return; }
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (_last != TimeSpan.Zero)
        {
            var dt = Math.Min(0.1, (t - _last).TotalSeconds);
            _angle += dt * 4; // 4 °/s
            _time += dt;
        }
        _last = t;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 1 || h < 1) return;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        var frame = new SceneFrame(w, h, _angle, _time, ShowTitle, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        ThemeRegistry.Get(string.IsNullOrEmpty(Theme) ? ThemeManager.Current : Theme).Scene.Render(dc, frame);
        dc.Pop();
    }
}
