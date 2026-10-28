using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CreateWarfare.Launcher.Controls;

/// <summary>
/// Illustration de gauche quand Assets\hero.jpg n'est pas fourni : engrenages de laiton qui tournent
/// lentement devant une lueur d'explosion, bande de signalisation et nom du pack.
/// </summary>
public sealed class HeroArt : FrameworkElement
{
    double _angle;
    TimeSpan _last;

    public HeroArt()
    {
        Loaded += (_, _) => CompositionTarget.Rendering += OnFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    void OnFrame(object? sender, EventArgs e)
    {
        if (!IsVisible) return;
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (_last != TimeSpan.Zero) _angle += (t - _last).TotalSeconds * 4; // 4 °/s
        _last = t;
        InvalidateVisual();
    }

    static readonly Brush Brass = Freeze(new SolidColorBrush(Color.FromArgb(150, 0xC8, 0x93, 0x3A)));
    static readonly Brush BrassDark = Freeze(new SolidColorBrush(Color.FromArgb(90, 0x8A, 0x63, 0x24)));
    static readonly Pen BrassEdge = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 0xE8, 0xB4, 0x5A)), 1.5));

    static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 1 || h < 1) return;
        var area = new Rect(0, 0, w, h);
        dc.PushClip(new RectangleGeometry(area));

        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x1B, 0x21, 0x38), Color.FromRgb(0x0A, 0x0E, 0x1A), 60), null, area);
        Glow(dc, new Point(w * 0.30, h * 0.72), Math.Max(w, h) * 0.75, Color.FromArgb(150, 0xFF, 0x7A, 0x1A));
        Glow(dc, new Point(w * 0.42, h * 0.62), Math.Max(w, h) * 0.30, Color.FromArgb(120, 0xFF, 0xD0, 0x60));
        Glow(dc, new Point(w * 0.95, h * 0.05), Math.Max(w, h) * 0.55, Color.FromArgb(90, 0x8B, 0x93, 0xFF));

        var s = Math.Min(w, h);
        var big = new Point(w * 0.40, h * 0.50);
        double rBig = s * 0.34, rSmall = s * 0.20;
        var small = new Point(big.X + (rBig + rSmall) * 0.93 * Math.Cos(-0.75), big.Y + (rBig + rSmall) * 0.93 * Math.Sin(-0.75));
        Gear(dc, new Point(w * 0.08, h * 0.95), s * 0.22, 11, -_angle * 1.5, BrassDark, null);
        Gear(dc, big, rBig, 16, _angle, Brass, BrassEdge);
        Gear(dc, small, rSmall, 10, -_angle * 1.6 + 9, Brass, BrassEdge);

        // bande de signalisation en bas
        var band = new Rect(0, h - 26, w, 26);
        dc.PushClip(new RectangleGeometry(band));
        dc.PushOpacity(0.22);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1C)), null, band);
        var yellow = new SolidColorBrush(Color.FromRgb(0xF4, 0xC4, 0x30));
        for (double x = -40; x < w + 40; x += 36)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(x, h), true, true);
                c.LineTo(new Point(x + 18, h), false, false);
                c.LineTo(new Point(x + 44, h - 26), false, false);
                c.LineTo(new Point(x + 26, h - 26), false, false);
            }
            dc.DrawGeometry(yellow, null, g);
        }
        dc.Pop();
        dc.Pop();

        // nom du pack
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var black = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
        var mono = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        double size = Math.Clamp(w * 0.11, 30, 64);
        var t1 = new FormattedText("CREATE", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, black, size, new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), dpi);
        var t2 = new FormattedText("WARFARE", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, black, size, new SolidColorBrush(Color.FromRgb(0x8B, 0x93, 0xFF)), dpi);
        var t3 = new FormattedText("MINECRAFT 1.21.1 · NEOFORGE · CREATE 6", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, mono, 11, new SolidColorBrush(Color.FromArgb(170, 0xE8, 0xEB, 0xF4)), dpi);
        double x0 = 32, y3 = h - 26 - 22 - t3.Height;
        dc.DrawText(t3, new Point(x0 + 2, y3));
        dc.DrawText(t2, new Point(x0, y3 - t2.Height + size * 0.12));
        dc.DrawText(t1, new Point(x0, y3 - t2.Height - t1.Height + size * 0.30));

        dc.Pop();
    }

    static void Glow(DrawingContext dc, Point c, double r, Color color)
    {
        var b = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B));
        dc.DrawEllipse(b, null, c, r, r);
    }

    /// <summary>Roue dentée avec un moyeu évidé (remplissage pair-impair).</summary>
    static void Gear(DrawingContext dc, Point c, double r, int teeth, double angleDeg, Brush fill, Pen? edge)
    {
        var g = new StreamGeometry { FillRule = FillRule.EvenOdd };
        double step = 2 * Math.PI / teeth, a0 = angleDeg * Math.PI / 180, root = r * 0.84;
        Point P(double rad, double a) => new(c.X + rad * Math.Cos(a), c.Y + rad * Math.Sin(a));
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(P(root, a0), true, true);
            for (int i = 0; i < teeth; i++)
            {
                double a = a0 + i * step;
                ctx.LineTo(P(r, a + step * 0.12), true, true);
                ctx.LineTo(P(r, a + step * 0.42), true, true);
                ctx.LineTo(P(root, a + step * 0.54), true, true);
                ctx.ArcTo(P(root, a + step), new Size(root, root), 0, false, SweepDirection.Clockwise, true, true);
            }
            double hub = r * 0.30;
            ctx.BeginFigure(P(hub, a0), true, true);
            ctx.ArcTo(P(hub, a0 + Math.PI), new Size(hub, hub), 0, false, SweepDirection.Clockwise, true, true);
            ctx.ArcTo(P(hub, a0), new Size(hub, hub), 0, false, SweepDirection.Clockwise, true, true);
        }
        g.Freeze();
        dc.DrawGeometry(fill, edge, g);
        // rayons
        var spoke = new Pen(fill, r * 0.10);
        for (int i = 0; i < 4; i++)
        {
            double a = a0 + i * Math.PI / 2 + step / 2;
            dc.DrawLine(spoke, P(r * 0.30, a), P(root * 0.92, a));
        }
    }
}
