using System.Windows;
using System.Windows.Media;
using static CreateWarfare.Launcher.Controls.Scenes.SceneKit;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>
/// Noël : nuit bleue étoilée, guirlande lumineuse, collines enneigées et sapins, engrenages en boules de Noël
/// suspendues à des fils dorés (rouge, or, argent).
/// </summary>
public sealed class ChristmasScene : HeroScene
{
    sealed record Bauble(Color Light, Color Dark, Color Trim);

    static readonly Bauble Red = new(Color.FromRgb(0xF0, 0x50, 0x5A), Color.FromRgb(0x86, 0x10, 0x1E), Color.FromRgb(0xF2, 0xD2, 0x7A));
    static readonly Bauble Gold = new(Color.FromRgb(0xFF, 0xE6, 0x9A), Color.FromRgb(0xA0, 0x70, 0x1C), Color.FromRgb(0xC6, 0x22, 0x33));
    static readonly Bauble Silver = new(Color.FromRgb(0xE4, 0xEC, 0xF8), Color.FromRgb(0x58, 0x6A, 0x8E), Color.FromRgb(0xF2, 0xD2, 0x7A));

    static readonly Pen GoldThread = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(170, 0xF2, 0xD2, 0x7A)), 1.2));
    static readonly Pen Wire = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 0x1A, 0x2A, 0x22)), 2));
    static readonly Brush Shine = Solid(70, 255, 255, 255);
    static readonly Brush SnowBack = Solid(255, 0x9F, 0xB3, 0xD6);
    static readonly Brush SnowFront = Solid(255, 0xE6, 0xEE, 0xF9);
    static readonly Brush Pine = Solid(255, 0x0E, 0x1E, 0x33);
    static readonly Color[] BulbColors =
    {
        Color.FromRgb(0xFF, 0x5A, 0x5F), Color.FromRgb(0xF7, 0xD8, 0x88), Color.FromRgb(0xF3, 0xF6, 0xFB), Color.FromRgb(0x5E, 0xD1, 0x9A),
    };

    public override void Render(DrawingContext dc, in SceneFrame f)
    {
        double w = f.Width, h = f.Height, time = f.Time;
        double s = Math.Min(w, h), m = Math.Max(w, h);

        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x10, 0x1E, 0x48), Color.FromRgb(0x06, 0x0A, 0x16), 90), null, new Rect(0, 0, w, h));
        Glow(dc, new Point(w * 0.85, h * 0.10), m * 0.55, Color.FromArgb(70, 0x4A, 0x7F, 0xE0));
        Stars(dc, w, h, time);

        var t = Train(new Point(w * 0.40, h * 0.48), s * 0.33, f.Angle);
        Hills(dc, w, h, s, back: true);
        dc.PushOpacity(0.85);
        BaubleGear(dc, t.Low, LowTeeth, t.Module, t.LowAngle, Silver);
        dc.Pop();
        Hills(dc, w, h, s, back: false);

        // fils dorés : les boules pendent de la guirlande
        dc.DrawLine(GoldThread, new Point(t.Big.X, 0), t.Big);
        dc.DrawLine(GoldThread, new Point(t.Small.X, 0), t.Small);
        BaubleGear(dc, t.Big, BigTeeth, t.Module, t.BigAngle, Red);
        BaubleGear(dc, t.Small, SmallTeeth, t.Module, t.SmallAngle, Gold);

        Garland(dc, w, h, time);
    }

    /// <summary>Boule de Noël dentée : dégradé brillant, anneau et perles qui tournent, reflet fixe, attache dorée.</summary>
    static void BaubleGear(DrawingContext dc, Point c, int teeth, double module, double angleDeg, Bauble b)
    {
        var geo = GearGeometry(c, teeth, module, angleDeg, hollow: false);
        var fill = new RadialGradientBrush(b.Light, b.Dark) { GradientOrigin = new Point(0.32, 0.28), Center = new Point(0.4, 0.36), RadiusX = 0.78, RadiusY = 0.78 };
        dc.DrawGeometry(fill, new Pen(new SolidColorBrush(Color.FromArgb(200, b.Trim.R, b.Trim.G, b.Trim.B)), 1.6), geo);

        double pitch = PitchRadius(teeth, module), r = pitch + module;
        var trim = new SolidColorBrush(b.Trim);
        dc.DrawEllipse(null, new Pen(trim, Math.Max(1.5, r * 0.035)), c, pitch * 0.62, pitch * 0.62);
        dc.PushTransform(new RotateTransform(angleDeg, c.X, c.Y));
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            dc.DrawEllipse(trim, null, new Point(c.X + pitch * 0.78 * Math.Cos(a), c.Y + pitch * 0.78 * Math.Sin(a)), r * 0.035, r * 0.035);
        }
        dc.Pop();
        // reflet (ne tourne pas : c'est la lumière)
        dc.PushTransform(new RotateTransform(-35, c.X - r * 0.38, c.Y - r * 0.40));
        dc.DrawEllipse(Shine, null, new Point(c.X - r * 0.38, c.Y - r * 0.40), r * 0.22, r * 0.10);
        dc.Pop();
        // attache
        dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(0xFF, 0xEC, 0xB0), Color.FromRgb(0xB8, 0x8A, 0x2E)), null, c, r * 0.12, r * 0.12);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x64, 0x20)), 1), c, r * 0.12, r * 0.12);
    }

    /// <summary>Étoiles fixes qui scintillent, et trois étincelles à quatre branches.</summary>
    static void Stars(DrawingContext dc, double w, double h, double time)
    {
        for (int i = 0; i < 42; i++)
        {
            double x = w * Frac(Math.Sin(i * 51.3) * 9137.1), y = h * 0.62 * Frac(Math.Sin(i * 27.7) * 3571.9);
            double r = 0.6 + 1.2 * Frac(Math.Sin(i * 7.1) * 517.3);
            byte a = (byte)(90 + 140 * (0.5 + 0.5 * Math.Sin(time * (0.8 + Frac(i * 0.37)) + i)));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(a, 0xF3, 0xF6, 0xFB)), null, new Point(x, y), r, r);
        }
        foreach (var (x, y, size, phase) in new[] { (0.12, 0.14, 7.0, 0.0), (0.58, 0.20, 5.0, 2.0), (0.30, 0.06, 4.0, 4.0) })
        {
            var c = new Point(w * x, h * y);
            double k = size * (0.8 + 0.25 * Math.Sin(time * 1.5 + phase));
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(c.X, c.Y - k * 2), true, true);
                ctx.LineTo(new Point(c.X + k * 0.35, c.Y - k * 0.35), true, true);
                ctx.LineTo(new Point(c.X + k * 2, c.Y), true, true);
                ctx.LineTo(new Point(c.X + k * 0.35, c.Y + k * 0.35), true, true);
                ctx.LineTo(new Point(c.X, c.Y + k * 2), true, true);
                ctx.LineTo(new Point(c.X - k * 0.35, c.Y + k * 0.35), true, true);
                ctx.LineTo(new Point(c.X - k * 2, c.Y), true, true);
                ctx.LineTo(new Point(c.X - k * 0.35, c.Y - k * 0.35), true, true);
            }
            g.Freeze();
            Glow(dc, c, k * 3, Color.FromArgb(90, 0xF7, 0xD8, 0x88));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xD0)), null, g);
        }
    }

    /// <summary>Collines enneigées ; sapins en silhouette sur la colline du fond.</summary>
    static void Hills(DrawingContext dc, double w, double h, double s, bool back)
    {
        if (back)
        {
            double Top(double x) => h * (0.78 - 0.05 * Math.Sin(x / w * Math.PI * 1.3 + 0.3));
            for (int i = 0; i < 9; i++)
            {
                double x = w * (0.05 + i * 0.115 + 0.03 * Math.Sin(i * 4.1)), size = s * (0.06 + 0.03 * Frac(Math.Sin(i * 3.3) * 77.7));
                Tree(dc, new Point(x, Top(x) + 4), size);
            }
            dc.DrawGeometry(SnowBack, null, Ground(w, h, Top));
        }
        else
        {
            double Top(double x) => h * (0.87 - 0.035 * Math.Sin(x / w * Math.PI * 1.1 + 2.0));
            dc.DrawGeometry(SnowFront, null, Ground(w, h, Top));
        }
    }

    static void Tree(DrawingContext dc, Point foot, double size)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (int k = 0; k < 3; k++)
            {
                double y = foot.Y - size * (0.35 + k * 0.32), half = size * (0.42 - k * 0.10);
                ctx.BeginFigure(new Point(foot.X - half, y + size * 0.30), true, true);
                ctx.LineTo(new Point(foot.X, y - size * 0.28), true, true);
                ctx.LineTo(new Point(foot.X + half, y + size * 0.30), true, true);
            }
        }
        g.Freeze();
        dc.DrawGeometry(Pine, null, g);
    }

    /// <summary>Guirlande en deux festons le long du haut ; les ampoules s'allument tour à tour.</summary>
    static void Garland(DrawingContext dc, double w, double h, double time)
    {
        var swags = new[] { (new Point(-10, h * 0.025), new Point(w * 0.25, h * 0.13), new Point(w * 0.5, h * 0.03)),
                            (new Point(w * 0.5, h * 0.03), new Point(w * 0.75, h * 0.13), new Point(w + 10, h * 0.02)) };
        int n = 0;
        foreach (var (a, ctrl, b) in swags)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open()) { ctx.BeginFigure(a, false, false); ctx.QuadraticBezierTo(ctrl, b, true, true); }
            g.Freeze();
            dc.DrawGeometry(null, Wire, g);
            for (double u = 0.06; u < 1; u += 0.088, n++)
            {
                var p = new Point((1 - u) * (1 - u) * a.X + 2 * u * (1 - u) * ctrl.X + u * u * b.X,
                                  (1 - u) * (1 - u) * a.Y + 2 * u * (1 - u) * ctrl.Y + u * u * b.Y + 5);
                var c = BulbColors[n % BulbColors.Length];
                double lit = 0.55 + 0.45 * Math.Sin(time * 1.6 - n * 0.9);
                Glow(dc, p, 9 + 5 * lit, Color.FromArgb((byte)(110 * lit), c.R, c.G, c.B));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(150 + 105 * lit), c.R, c.G, c.B)), null, p, 3.2, 4.4);
            }
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
}
