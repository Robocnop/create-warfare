using System.Windows;
using System.Windows.Media;
using static CreateWarfare.Launcher.Controls.Scenes.SceneKit;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>
/// Été : coucher de soleil sur la mer. La grande roue est le soleil (ses dents sont les rayons), la petite une bouée
/// rayée de corail, la roue basse tourne sous l'eau ; reflets dorés, vagues légères et deux mouettes.
/// </summary>
public sealed class SummerScene : HeroScene
{
    const double Horizon = 0.70;

    static readonly Brush Sun = Freeze(new RadialGradientBrush(Color.FromRgb(0xFF, 0xF0, 0xA8), Color.FromRgb(0xFF, 0xA8, 0x2E))
    { GradientOrigin = new Point(0.45, 0.4), RadiusX = 0.6, RadiusY = 0.6 });
    static readonly Pen SunEdge = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(160, 0xFF, 0xE3, 0x8A)), 1.5));
    static readonly Brush BuoyWhite = Solid(255, 0xFF, 0xF6, 0xEC);
    static readonly Brush BuoyCoral = Solid(255, 0xFF, 0x6F, 0x59);
    static readonly Pen BuoyEdge = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 0xB8, 0x3E, 0x2E)), 1.5));
    static readonly Pen BuoyRope = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(220, 0xE8, 0xD9, 0xB8)), 2) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) });
    static readonly Brush DeepGear = Solid(255, 0x2E, 0xD3, 0xC6);
    static readonly Pen WaveLine = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), 1.4));
    static readonly Pen Gull = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 0x2A, 0x1C, 0x3A)), 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });

    public override void Render(DrawingContext dc, in SceneFrame f)
    {
        double w = f.Width, h = f.Height, time = f.Time;
        double s = Math.Min(w, h), m = Math.Max(w, h), horizon = h * Horizon;

        var sky = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        sky.GradientStops.Add(new GradientStop(Color.FromRgb(0x1A, 0x1A, 0x46), 0));
        sky.GradientStops.Add(new GradientStop(Color.FromRgb(0x4A, 0x2C, 0x6A), 0.28));
        sky.GradientStops.Add(new GradientStop(Color.FromRgb(0xC2, 0x50, 0x6E), 0.52));
        sky.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0x8A, 0x5B), 0.63));
        sky.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xCF, 0x7A), Horizon));
        sky.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xCF, 0x7A), 1));
        dc.DrawRectangle(sky, null, new Rect(0, 0, w, h));

        var t = Train(new Point(w * 0.40, h * 0.48), s * 0.33, f.Angle);
        Glow(dc, t.Big, m * 0.55, Color.FromArgb(150, 0xFF, 0xB3, 0x47));

        Gulls(dc, w, h, s, time);

        // soleil-engrenage : ses dents sont les rayons
        Gear(dc, t.Big, BigTeeth, t.Module, t.BigAngle, Sun, SunEdge, hollow: false);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 0xFF, 0xFF, 0xFF)), 2), t.Big, t.BigPitch * 0.7, t.BigPitch * 0.7);
        // roue basse : sous l'eau, entraînée par le soleil
        Gear(dc, t.Low, LowTeeth, t.Module, t.LowAngle, DeepGear, null, hollow: true);
        Buoy(dc, t.Small, SmallTeeth, t.Module, t.SmallAngle);

        Sea(dc, w, h, horizon, t.Big.X, OuterRadius(BigTeeth, t.Module), time);
    }

    /// <summary>Bouée dentée : anneau blanc, quatre secteurs corail et corde, trou central qui laisse voir le ciel.</summary>
    static void Buoy(DrawingContext dc, Point c, int teeth, double module, double angleDeg)
    {
        double pitch = PitchRadius(teeth, module), r = pitch + module, hole = pitch * 0.48;
        var ring = Geometry.Combine(GearGeometry(c, teeth, module, angleDeg, hollow: false), new EllipseGeometry(c, hole, hole), GeometryCombineMode.Exclude, null);
        ring.Freeze();
        dc.DrawGeometry(BuoyWhite, null, ring);
        dc.PushClip(ring);
        for (int i = 0; i < 4; i++)
        {
            double a0 = (angleDeg + i * 90) * Math.PI / 180, a1 = a0 + Math.PI / 4;
            var wedge = new StreamGeometry();
            using (var ctx = wedge.Open())
            {
                ctx.BeginFigure(c, true, true);
                ctx.LineTo(new Point(c.X + r * 1.2 * Math.Cos(a0), c.Y + r * 1.2 * Math.Sin(a0)), true, true);
                ctx.ArcTo(new Point(c.X + r * 1.2 * Math.Cos(a1), c.Y + r * 1.2 * Math.Sin(a1)), new Size(r * 1.2, r * 1.2), 0, false, SweepDirection.Clockwise, true, true);
            }
            wedge.Freeze();
            dc.DrawGeometry(BuoyCoral, null, wedge);
        }
        dc.Pop();
        dc.DrawGeometry(null, BuoyEdge, ring);
        dc.DrawEllipse(null, BuoyRope, c, (hole + pitch * 0.95) / 2, (hole + pitch * 0.95) / 2);
    }

    /// <summary>Mer sous l'horizon (légèrement transparente : la roue basse se devine dessous), reflet du soleil, vagues.</summary>
    static void Sea(DrawingContext dc, double w, double h, double horizon, double sunX, double sunR, double time)
    {
        var sea = new LinearGradientBrush(Color.FromArgb(235, 0x1E, 0x6F, 0x8A), Color.FromArgb(250, 0x0A, 0x22, 0x38), 90);
        dc.DrawRectangle(sea, null, new Rect(0, horizon, w, h - horizon));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 0xFF, 0xE1, 0x9A)), 1.5), new Point(0, horizon), new Point(w, horizon));

        // reflet : traits dorés qui rétrécissent et scintillent
        int k = 0;
        for (double y = horizon + 5; y < h; y += 7, k++)
        {
            double width = sunR * 1.6 * Math.Max(0.15, 1 - k * 0.035) * (0.65 + 0.35 * Math.Sin(time * 2.1 + k * 1.3));
            byte a = (byte)Math.Max(30, 190 - k * 6);
            double dx = 6 * Math.Sin(time * 0.9 + k * 0.7);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(a, 0xFF, 0xD2, 0x7A)), 2.2), new Point(sunX - width / 2 + dx, y), new Point(sunX + width / 2 + dx, y));
        }
        // vagues
        for (int row = 0; row < 6; row++)
        {
            double y0 = horizon + 16 + row * (h - horizon) / 6.5;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(0, y0), false, false);
                for (double x = 8; x <= w; x += 8)
                    ctx.LineTo(new Point(x, y0 + (1.5 + row * 0.5) * Math.Sin(x / (26 + row * 6) + time * (0.9 + row * 0.15) + row)), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(null, WaveLine, g);
        }
    }

    /// <summary>Deux mouettes en « v » qui planent lentement.</summary>
    static void Gulls(DrawingContext dc, double w, double h, double s, double time)
    {
        foreach (var (x0, y0, speed, size) in new[] { (0.62, 0.16, 9.0, 0.022), (0.72, 0.22, 7.0, 0.016) })
        {
            double u = s * size, span = w + 4 * u;
            double x = (w * x0 + time * speed) % span - 2 * u, y = h * y0 + 4 * Math.Sin(time * 0.7 + x0 * 10); // reboucle hors cadre
            double flap = 0.35 + 0.25 * Math.Sin(time * 3 + x0 * 7);
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(x - u, y - u * flap), false, false);
                ctx.QuadraticBezierTo(new Point(x - u * 0.45, y - u * 0.55), new Point(x, y), true, true);
                ctx.QuadraticBezierTo(new Point(x + u * 0.45, y - u * 0.55), new Point(x + u, y - u * flap), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(null, Gull, g);
        }
    }
}
