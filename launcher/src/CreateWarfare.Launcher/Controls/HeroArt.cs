using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CreateWarfare.Launcher.Controls;

/// <summary>
/// Illustration de gauche quand Assets\hero.jpg n'est pas fourni : engrenages de laiton qui tournent
/// lentement devant une lueur d'explosion, bande de signalisation et nom du pack.
/// Thème Halloween : engrenages-citrouilles sous la lune, chauves-souris, cimetière et brume verte.
/// </summary>
public sealed class HeroArt : FrameworkElement
{
    /// <summary>Activé par App.ApplyHalloween avant l'ouverture de la fenêtre.</summary>
    public static bool Halloween { get; set; }

    public static readonly FontFamily Creepster = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Creepster");

    double _angle, _time;
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
        if (_last != TimeSpan.Zero)
        {
            var dt = (t - _last).TotalSeconds;
            _angle += dt * 4; // 4 °/s
            _time += dt;
        }
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
        if (Halloween)
        {
            RenderHalloween(dc, w, h);
            dc.Pop();
            return;
        }

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

    static readonly Brush Night = Freeze(new SolidColorBrush(Color.FromRgb(0x0B, 0x07, 0x10)));
    static readonly Brush Pumpkin = Freeze(new RadialGradientBrush(Color.FromRgb(0xFF, 0x8C, 0x1A), Color.FromRgb(0xA8, 0x3A, 0x08)));
    static readonly Pen Moss = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(190, 0x7B, 0xD1, 0x3A)), 2));
    static readonly Brush Candle = Freeze(new RadialGradientBrush(Color.FromRgb(0xFF, 0xF0, 0x9A), Color.FromRgb(0xFF, 0xA5, 0x1F)));

    void RenderHalloween(DrawingContext dc, double w, double h)
    {
        double s = Math.Min(w, h), m = Math.Max(w, h);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x3A, 0x17, 0x52), Color.FromRgb(0x0B, 0x07, 0x10), 90), null, new Rect(0, 0, w, h));
        Glow(dc, new Point(w * 0.90, h * 0.10), m * 0.60, Color.FromArgb(110, 0x9B, 0x3D, 0xD6));

        // lune, à demi cachée par le grand engrenage
        var moon = new Point(w * 0.24, h * 0.18);
        double rMoon = s * 0.14;
        Glow(dc, moon, rMoon * 3.2, Color.FromArgb(120, 0xFF, 0x9A, 0x2E));
        dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(0xFF, 0xD8, 0x80), Color.FromRgb(0xF0, 0x8A, 0x26)), null, moon, rMoon, rMoon);
        var crater = new SolidColorBrush(Color.FromArgb(45, 0xA0, 0x48, 0x10));
        dc.DrawEllipse(crater, null, new Point(moon.X - rMoon * 0.35, moon.Y - rMoon * 0.15), rMoon * 0.20, rMoon * 0.20);
        dc.DrawEllipse(crater, null, new Point(moon.X + rMoon * 0.30, moon.Y + rMoon * 0.35), rMoon * 0.14, rMoon * 0.14);

        Bat(dc, new Point(w * 0.56 + 24 * Math.Sin(_time * 0.35), h * 0.10 + 6 * Math.Sin(_time * 0.8)), s * 0.050, _time * 9);
        Bat(dc, new Point(w * 0.86 + 16 * Math.Sin(_time * 0.5 + 2), h * 0.36 + 5 * Math.Sin(_time * 0.9)), s * 0.035, _time * 11 + 1);
        Bat(dc, new Point(w * 0.10 + 12 * Math.Sin(_time * 0.4 + 4), h * 0.40), s * 0.030, _time * 10 + 2);

        Graveyard(dc, w, h);
        Glow(dc, new Point(w * 0.20, h * 1.02), m * 0.55, Color.FromArgb(120, 0x5F, 0xD4, 0x3A));
        Glow(dc, new Point(w * 0.90, h * 1.05), m * 0.45, Color.FromArgb(90, 0x5F, 0xD4, 0x3A));

        // engrenages-citrouilles : les dents tournent, le visage reste droit
        var big = new Point(w * 0.40, h * 0.48);
        double rBig = s * 0.33, rSmall = s * 0.19;
        var small = new Point(big.X + (rBig + rSmall) * 0.93 * Math.Cos(-0.75), big.Y + (rBig + rSmall) * 0.93 * Math.Sin(-0.75));
        Gear(dc, big, rBig, 16, _angle, Pumpkin, Moss, hollow: false);
        Face(dc, big, rBig * 0.78);
        Gear(dc, small, rSmall, 10, -_angle * 1.6 + 9, Pumpkin, Moss, hollow: false);
        Face(dc, small, rSmall * 0.78);

        // nom du pack
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var creepy = new Typeface(Creepster, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var mono = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        double size = Math.Clamp(w * 0.13, 34, 76);
        var shadow = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
        FormattedText Text(string text, Brush b) => new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, creepy, size, b, dpi);
        var t3 = new FormattedText("MINECRAFT 1.21.1 · NEOFORGE · CREATE 6", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, mono, 11, new SolidColorBrush(Color.FromArgb(190, 0xE8, 0xDF, 0xD8)), dpi);
        double x0 = 30, y3 = h - 20 - t3.Height, y2 = y3 - size * 1.05, y1 = y2 - size * 0.92;
        foreach (var (text, b, y) in new[] { ("CREATE", (Brush)new SolidColorBrush(Color.FromRgb(0xEE, 0xE4, 0xDA)), y1), ("WARFARE", new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x10)), y2) })
        {
            dc.DrawText(Text(text, shadow), new Point(x0 + 3, y + 4));
            dc.DrawText(Text(text, b), new Point(x0, y));
        }
        dc.DrawText(t3, new Point(x0 + 2, y3));
    }

    /// <summary>Visage de citrouille lumineux (yeux, nez, bouche dentée), centré sur c, de demi-largeur r.</summary>
    static void Face(DrawingContext dc, Point c, double r)
    {
        Glow(dc, new Point(c.X, c.Y + r * 0.1), r * 1.1, Color.FromArgb(80, 0xFF, 0xC0, 0x30));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            void Poly(params double[] xy)
            {
                ctx.BeginFigure(new Point(c.X + xy[0] * r, c.Y + xy[1] * r), true, true);
                for (int i = 2; i < xy.Length; i += 2) ctx.LineTo(new Point(c.X + xy[i] * r, c.Y + xy[i + 1] * r), true, true);
            }
            Poly(-0.62, -0.10, -0.12, -0.02, -0.30, -0.48);                 // œil gauche, regard en colère
            Poly(0.62, -0.10, 0.12, -0.02, 0.30, -0.48);                    // œil droit
            Poly(-0.08, 0.16, 0.08, 0.16, 0, 0.04);                         // nez
            Poly(-0.70, 0.22, -0.42, 0.34, -0.30, 0.24, -0.16, 0.36, 0, 0.26, 0.16, 0.36, 0.30, 0.24, 0.42, 0.34, 0.70, 0.22,
                 0.48, 0.58, 0.28, 0.52, 0.16, 0.66, 0, 0.54, -0.16, 0.66, -0.28, 0.52, -0.48, 0.58); // bouche
        }
        g.Freeze();
        dc.DrawGeometry(Candle, null, g);
    }

    /// <summary>Chauve-souris en silhouette ; phase fait battre les ailes.</summary>
    static void Bat(DrawingContext dc, Point c, double size, double phase)
    {
        double f = 0.35 + 0.65 * Math.Sin(phase);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            foreach (var side in new[] { 1.0, -1.0 })
            {
                Point P(double x, double y) => new(c.X + side * x * size, c.Y + y * size);
                ctx.BeginFigure(P(0.12, -0.05), true, true);
                ctx.LineTo(P(0.55, -0.55 * f), true, true);
                ctx.LineTo(P(1.00, -0.35 * f - 0.05), true, true);
                ctx.LineTo(P(0.80, 0.08), true, true);
                ctx.LineTo(P(0.60, 0.00), true, true);
                ctx.LineTo(P(0.45, 0.14), true, true);
                ctx.LineTo(P(0.30, 0.02), true, true);
                ctx.LineTo(P(0.12, 0.16), true, true);
            }
        }
        g.Freeze();
        dc.DrawGeometry(Night, null, g);
        dc.DrawEllipse(Night, null, c, size * 0.16, size * 0.24);
        dc.DrawEllipse(Night, null, new Point(c.X, c.Y - size * 0.26), size * 0.11, size * 0.11);
    }

    /// <summary>Colline, tombes, croix et clôture en silhouette dans le bas de l'image.</summary>
    static void Graveyard(DrawingContext dc, double w, double h)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, h * 0.84), true, true);
            ctx.QuadraticBezierTo(new Point(w * 0.35, h * 0.74), new Point(w * 0.65, h * 0.80), true, true);
            ctx.QuadraticBezierTo(new Point(w * 0.85, h * 0.84), new Point(w, h * 0.76), true, true);
            ctx.LineTo(new Point(w, h), true, true);
            ctx.LineTo(new Point(0, h), true, true);
        }
        g.Freeze();
        dc.DrawGeometry(Night, null, g);
        foreach (var (x, top, tw) in new[] { (0.62, 0.745, 0.050), (0.76, 0.755, 0.040), (0.88, 0.725, 0.055) })
        {
            var r = new Rect(w * x, h * top, w * tw, h * 0.09);
            dc.DrawRoundedRectangle(Night, null, r, r.Width / 2, r.Width / 2);
        }
        double cx = w * 0.70, cy = h * 0.71;
        dc.DrawRectangle(Night, null, new Rect(cx - 3, cy, 6, h * 0.10));
        dc.DrawRectangle(Night, null, new Rect(cx - 13, cy + 12, 26, 6));
        var fence = new Pen(Night, 3);
        for (double x = w * 0.50; x < w; x += 14) dc.DrawLine(fence, new Point(x, h * 0.80), new Point(x, h * 0.87));
        dc.DrawLine(fence, new Point(w * 0.50, h * 0.82), new Point(w, h * 0.82));
    }

    static void Glow(DrawingContext dc, Point c, double r, Color color)
    {
        var b = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B));
        dc.DrawEllipse(b, null, c, r, r);
    }

    /// <summary>Roue dentée, avec un moyeu évidé (remplissage pair-impair) et des rayons si hollow.</summary>
    static void Gear(DrawingContext dc, Point c, double r, int teeth, double angleDeg, Brush fill, Pen? edge, bool hollow = true)
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
            if (hollow)
            {
                double hub = r * 0.30;
                ctx.BeginFigure(P(hub, a0), true, true);
                ctx.ArcTo(P(hub, a0 + Math.PI), new Size(hub, hub), 0, false, SweepDirection.Clockwise, true, true);
                ctx.ArcTo(P(hub, a0), new Size(hub, hub), 0, false, SweepDirection.Clockwise, true, true);
            }
        }
        g.Freeze();
        dc.DrawGeometry(fill, edge, g);
        if (!hollow) return;
        // rayons
        var spoke = new Pen(fill, r * 0.10);
        for (int i = 0; i < 4; i++)
        {
            double a = a0 + i * Math.PI / 2 + step / 2;
            dc.DrawLine(spoke, P(r * 0.30, a), P(root * 0.92, a));
        }
    }
}
