using System.Windows;
using System.Windows.Media;
using CreateWarfare.Launcher.Themes;

namespace CreateWarfare.Launcher.Controls;

/// <summary>
/// Calque transparent posé sur l'accueil : dessine le décor d'ambiance du thème (neige, pétales…), images du
/// carrousel comprises. Ne capte pas la souris. Immobile si le décor est coupé (réglage ou Windows).
/// </summary>
public sealed class AmbientLayer : FrameworkElement
{
    double _time;
    TimeSpan _last;

    public AmbientLayer()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { CompositionTarget.Rendering += OnFrame; ThemeManager.Changed += InvalidateVisual; };
        Unloaded += (_, _) => { CompositionTarget.Rendering -= OnFrame; ThemeManager.Changed -= InvalidateVisual; };
    }

    void OnFrame(object? sender, EventArgs e)
    {
        if (!IsVisible || ThemeRegistry.Get(ThemeManager.Current).Ambient == null || !Motion.DecorEnabled) { _last = TimeSpan.Zero; return; }
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (_last != TimeSpan.Zero) _time += Math.Min(0.1, (t - _last).TotalSeconds);
        _last = t;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 1 || h < 1) return;
        var ambient = ThemeRegistry.Get(ThemeManager.Current).Ambient;
        if (ambient == null) return;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        ambient.Render(dc, w, h, _time);
        dc.Pop();
    }
}
