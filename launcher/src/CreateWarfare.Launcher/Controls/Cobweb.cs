using System.Windows;
using System.Windows.Media;

namespace CreateWarfare.Launcher.Controls;

/// <summary>
/// Décor du thème Halloween : toile d'araignée tendue dans un coin, avec une araignée qui se balance
/// au bout de son fil (côté droit seulement).
/// </summary>
public sealed class Cobweb : FrameworkElement
{
    public static readonly DependencyProperty LeftCornerProperty =
        DependencyProperty.Register(nameof(LeftCorner), typeof(bool), typeof(Cobweb), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Toile dans le coin haut gauche (sinon haut droit).</summary>
    public bool LeftCorner { get => (bool)GetValue(LeftCornerProperty); set => SetValue(LeftCornerProperty, value); }

    double _time;
    TimeSpan _last;

    public Cobweb()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    void OnFrame(object? sender, EventArgs e)
    {
        if (!IsVisible || LeftCorner) return;
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (_last != TimeSpan.Zero) _time += (t - _last).TotalSeconds;
        _last = t;
        InvalidateVisual();
    }

    static readonly Pen Silk = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(70, 0xE8, 0xE0, 0xF0)), 1));
    static readonly Brush Spider = Freeze(new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)));
    static readonly Pen Legs = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)), 1.4));

    static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 1 || h < 1) return;
        double side = LeftCorner ? 1 : -1;
        var corner = new Point(LeftCorner ? 0 : w, 0);
        double len = Math.Min(w, h);
        const int rays = 6, rings = 5;

        Point Ray(int i, double r)
        {
            double a = Math.PI / 2 * i / (rays - 1); // de l'horizontale à la verticale
            return new Point(corner.X + side * r * Math.Cos(a), corner.Y + r * Math.Sin(a));
        }

        for (int i = 0; i < rays; i++) dc.DrawLine(Silk, corner, Ray(i, len));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (int k = 1; k <= rings; k++)
            {
                double r = len * k / (rings + 1.2);
                ctx.BeginFigure(Ray(0, r), false, false);
                for (int i = 1; i < rays; i++)
                {
                    Point a = Ray(i - 1, r), b = Ray(i, r);
                    var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                    var sag = new Point(corner.X + (mid.X - corner.X) * 0.86, corner.Y + (mid.Y - corner.Y) * 0.86);
                    ctx.QuadraticBezierTo(sag, b, true, true);
                }
            }
        }
        g.Freeze();
        dc.DrawGeometry(null, Silk, g);

        if (LeftCorner) return;
        // araignée au bout de son fil
        double x = w - len * 0.30, y = h * 0.62 + 8 * Math.Sin(_time * 1.3);
        dc.DrawLine(Silk, new Point(x, 0), new Point(x, y));
        for (int i = 0; i < 4; i++)
        {
            double dy = (i - 1.5) * 3.2, spread = 9 + (i == 1 || i == 2 ? 2 : 0);
            foreach (var s in new[] { -1.0, 1.0 })
            {
                var knee = new Point(x + s * spread * 0.7, y + dy - 4);
                dc.DrawLine(Legs, new Point(x, y + dy * 0.4), knee);
                dc.DrawLine(Legs, knee, new Point(x + s * spread, y + dy * 1.4 + 3));
            }
        }
        dc.DrawEllipse(Spider, null, new Point(x, y + 3), 5, 6);
        dc.DrawEllipse(Spider, null, new Point(x, y - 4), 3.2, 3.2);
    }
}
