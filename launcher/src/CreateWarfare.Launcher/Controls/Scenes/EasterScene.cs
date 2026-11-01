using System.Windows;
using System.Windows.Media;
using static CreateWarfare.Launcher.Controls.Scenes.SceneKit;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>
/// Pâques : crépuscule de printemps, collines fleuries, cloches qui se balancent et engrenages peints comme des œufs
/// (motifs en zigzag, bandes et pois qui tournent avec la roue).
/// </summary>
public sealed class EasterScene : HeroScene
{
    /// <summary>Couleurs d'un œuf : fond, bandes, zigzag, pois.</summary>
    sealed record EggPaint(Color Base, Color Band, Color Zigzag, Color Dots);

    static readonly EggPaint Butter = new(Rgb(0xF6, 0xCF, 0x6A), Rgb(0xF3, 0xA9, 0xC4), Rgb(0x8F, 0xCF, 0x8A), Rgb(0xFF, 0xFB, 0xF0));
    static readonly EggPaint Mint = new(Rgb(0x9A, 0xDB, 0x8F), Rgb(0xFF, 0xFB, 0xF0), Rgb(0xF3, 0xA9, 0xC4), Rgb(0xF6, 0xCF, 0x6A));
    static readonly EggPaint Rose = new(Rgb(0xF3, 0xA9, 0xC4), Rgb(0xF6, 0xCF, 0x6A), Rgb(0xFF, 0xFB, 0xF0), Rgb(0xA9, 0xC7, 0xF5));

    static readonly Pen EggEdge = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(150, 0x4A, 0x3B, 0x2E)), 1.5));
    static readonly Brush HillBack = Solid(255, 0x2A, 0x40, 0x31);
    static readonly Brush HillFront = Solid(255, 0x1C, 0x2C, 0x22);
    static readonly Brush[] FlowerColors = { Solid(230, 0xF3, 0xA9, 0xC4), Solid(230, 0xFF, 0xFB, 0xF0), Solid(230, 0xA9, 0xC7, 0xF5), Solid(230, 0xF6, 0xCF, 0x6A) };
    static readonly Brush FlowerHeart = Solid(255, 0xF6, 0xB9, 0x3B);
    static readonly Brush BellBrush = Freeze(new LinearGradientBrush(Color.FromRgb(0xF8, 0xDC, 0x8C), Color.FromRgb(0xB0, 0x7E, 0x2A), 0));
    static readonly Brush BellLip = Solid(255, 0x9A, 0x6A, 0x22);
    static readonly Pen Ribbon = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 0xF3, 0xA9, 0xC4)), 2));
    static readonly Brush Bow = Solid(255, 0xF3, 0xA9, 0xC4);

    static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public override void Render(DrawingContext dc, in SceneFrame f)
    {
        double w = f.Width, h = f.Height, time = f.Time;
        double s = Math.Min(w, h), m = Math.Max(w, h);

        var sky = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        sky.GradientStops.Add(new GradientStop(Rgb(0x3B, 0x33, 0x56), 0));
        sky.GradientStops.Add(new GradientStop(Rgb(0x4E, 0x40, 0x5E), 0.35));
        sky.GradientStops.Add(new GradientStop(Rgb(0x2C, 0x3D, 0x34), 0.75));
        sky.GradientStops.Add(new GradientStop(Rgb(0x14, 0x1A, 0x16), 1));
        dc.DrawRectangle(sky, null, new Rect(0, 0, w, h));
        Glow(dc, new Point(w * 0.88, h * 0.06), m * 0.50, Color.FromArgb(90, 0xF3, 0xA9, 0xC4));
        Glow(dc, new Point(w * 0.40, h * 0.50), m * 0.45, Color.FromArgb(80, 0xF6, 0xCF, 0x6A));

        var t = Train(new Point(w * 0.40, h * 0.48), s * 0.33, f.Angle);

        // la roue basse sort de l'herbe
        dc.PushOpacity(0.75);
        EggGear(dc, t.Low, LowTeeth, t.Module, t.LowAngle, Rose);
        dc.Pop();
        Hills(dc, w, h, s, time);

        EggGear(dc, t.Big, BigTeeth, t.Module, t.BigAngle, Butter);
        EggGear(dc, t.Small, SmallTeeth, t.Module, t.SmallAngle, Mint);

        // cloches dans le coin haut droit, hors des engrenages
        Bell(dc, new Point(w * 0.86, h * 0.04), s * 0.075, 9 * Math.Sin(time * 1.2));
        Bell(dc, new Point(w * 0.95, h * 0.09), s * 0.058, 9 * Math.Sin(time * 1.2 + 1.3));
    }

    /// <summary>Roue dentée peinte comme un œuf : dégradé, bandes, zigzag et pois, dans le repère de la roue.</summary>
    static void EggGear(DrawingContext dc, Point c, int teeth, double module, double angleDeg, EggPaint p)
    {
        var geo = GearGeometry(c, teeth, module, angleDeg, hollow: false);
        var light = Color.FromRgb((byte)((p.Base.R + 255) / 2), (byte)((p.Base.G + 255) / 2), (byte)((p.Base.B + 255) / 2));
        var fill = new RadialGradientBrush(light, p.Base) { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.42, 0.38), RadiusX = 0.75, RadiusY = 0.75 };
        dc.DrawGeometry(fill, EggEdge, geo);

        double r = OuterRadius(teeth, module);
        dc.PushClip(geo);
        dc.PushTransform(new RotateTransform(angleDeg, c.X, c.Y));
        var band = new Pen(new SolidColorBrush(p.Band), r * 0.07);
        dc.DrawLine(band, new Point(c.X - r, c.Y - r * 0.42), new Point(c.X + r, c.Y - r * 0.42));
        dc.DrawLine(band, new Point(c.X - r, c.Y + r * 0.30), new Point(c.X + r, c.Y + r * 0.30));
        var zig = new StreamGeometry();
        using (var ctx = zig.Open())
        {
            double step = r * 0.16, top = c.Y - r * 0.20, bottom = c.Y + r * 0.04;
            ctx.BeginFigure(new Point(c.X - r, bottom), false, false);
            int k = 0;
            for (double x = c.X - r + step; x <= c.X + r + step; x += step, k++)
                ctx.LineTo(new Point(x, k % 2 == 0 ? top : bottom), true, true);
        }
        zig.Freeze();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(p.Zigzag), r * 0.06) { LineJoin = PenLineJoin.Round }, zig);
        var dot = new SolidColorBrush(p.Dots);
        for (double x = c.X - r; x <= c.X + r; x += r * 0.22)
        {
            dc.DrawEllipse(dot, null, new Point(x, c.Y + r * 0.58), r * 0.045, r * 0.045);
            dc.DrawEllipse(dot, null, new Point(x + r * 0.11, c.Y - r * 0.66), r * 0.04, r * 0.04);
        }
        dc.Pop();
        dc.Pop();
        // moyeu en cocarde
        dc.DrawEllipse(new SolidColorBrush(p.Band), EggEdge, c, r * 0.12, r * 0.12);
        dc.DrawEllipse(new SolidColorBrush(p.Dots), null, c, r * 0.05, r * 0.05);
    }

    /// <summary>Deux collines et des fleurs à cinq pétales qui se balancent.</summary>
    static void Hills(DrawingContext dc, double w, double h, double s, double time)
    {
        double Back(double x) => h * (0.80 - 0.05 * Math.Sin(x / w * Math.PI * 1.4 + 0.6));
        double Front(double x) => h * (0.87 - 0.04 * Math.Sin(x / w * Math.PI * 1.1 + 2.2));
        dc.DrawGeometry(HillBack, null, Ground(w, h, Back));
        dc.DrawGeometry(HillFront, null, Ground(w, h, Front));

        for (int i = 0; i < 16; i++)
        {
            double x = w * (0.02 + 0.96 * Frac(Math.Sin(i * 91.7) * 4375.3));
            double y = Front(x) + h * (0.015 + 0.07 * Frac(Math.Sin(i * 37.1) * 1931.7));
            double r = s * (0.010 + 0.006 * Frac(Math.Sin(i * 13.3) * 917.1));
            x += 1.5 * Math.Sin(time * 1.4 + i);
            var petal = FlowerColors[i % FlowerColors.Length];
            for (int k = 0; k < 5; k++)
            {
                double a = k * 2 * Math.PI / 5 + i;
                dc.DrawEllipse(petal, null, new Point(x + r * Math.Cos(a), y + r * Math.Sin(a)), r * 0.75, r * 0.75);
            }
            dc.DrawEllipse(FlowerHeart, null, new Point(x, y), r * 0.55, r * 0.55);
        }
    }

    static double Frac(double v) => Math.Abs(v - Math.Truncate(v));

    static StreamGeometry Ground(double w, double h, Func<double, double> top)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, h), true, true);
            for (double x = 0; x <= w + 8; x += 8) ctx.LineTo(new Point(x, top(x)), true, true);
            ctx.LineTo(new Point(w + 8, h), true, true);
        }
        g.Freeze();
        return g;
    }

    /// <summary>Cloche de Pâques suspendue à un ruban, qui se balance autour de son point d'attache.</summary>
    static void Bell(DrawingContext dc, Point pivot, double u, double swingDeg)
    {
        dc.DrawLine(Ribbon, new Point(pivot.X, 0), pivot);
        dc.PushTransform(new RotateTransform(swingDeg, pivot.X, pivot.Y));
        Point P(double x, double y) => new(pivot.X + x * u, pivot.Y + y * u);
        var body = new StreamGeometry();
        using (var ctx = body.Open())
        {
            ctx.BeginFigure(P(-0.22, 0.18), true, true);
            ctx.BezierTo(P(-0.26, 0.55), P(-0.36, 0.86), P(-0.62, 1.02), true, true);
            ctx.LineTo(P(0.62, 1.02), true, true);
            ctx.BezierTo(P(0.36, 0.86), P(0.26, 0.55), P(0.22, 0.18), true, true);
            ctx.BezierTo(P(0.16, 0.02), P(-0.16, 0.02), P(-0.22, 0.18), true, true);
        }
        body.Freeze();
        dc.DrawEllipse(BellLip, null, P(0, 1.10), u * 0.10, u * 0.10);       // battant
        dc.DrawGeometry(BellBrush, null, body);
        dc.DrawEllipse(BellLip, null, P(0, 1.02), u * 0.62, u * 0.07);
        dc.DrawLine(new Pen(Bow, u * 0.07), P(-0.40, 0.80), P(0.40, 0.80));
        // nœud
        var bow = new StreamGeometry();
        using (var ctx = bow.Open())
        {
            ctx.BeginFigure(P(0, 0.08), true, true);
            ctx.LineTo(P(-0.30, -0.06), true, true);
            ctx.LineTo(P(-0.30, 0.22), true, true);
            ctx.BeginFigure(P(0, 0.08), true, true);
            ctx.LineTo(P(0.30, -0.06), true, true);
            ctx.LineTo(P(0.30, 0.22), true, true);
        }
        bow.Freeze();
        dc.DrawGeometry(Bow, null, bow);
        dc.Pop();
    }
}
