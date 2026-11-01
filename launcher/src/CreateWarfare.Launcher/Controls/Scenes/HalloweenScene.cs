using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static CreateWarfare.Launcher.Controls.Scenes.SceneKit;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>Halloween : engrenages-citrouilles sous la lune, chauves-souris, cimetière et brume verte.</summary>
public sealed class HalloweenScene : HeroScene
{
    public static readonly FontFamily Creepster = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Creepster");

    static readonly Brush Night = Freeze(new SolidColorBrush(Color.FromRgb(0x0B, 0x07, 0x10)));
    static readonly Brush Pumpkin = Freeze(new RadialGradientBrush(Color.FromRgb(0xFF, 0x8C, 0x1A), Color.FromRgb(0xA8, 0x3A, 0x08)));
    static readonly Pen Moss = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(190, 0x7B, 0xD1, 0x3A)), 2));
    static readonly Brush Candle = Freeze(new RadialGradientBrush(Color.FromRgb(0xFF, 0xF0, 0x9A), Color.FromRgb(0xFF, 0xA5, 0x1F)));

    public override void Render(DrawingContext dc, in SceneFrame f)
    {
        double w = f.Width, h = f.Height, time = f.Time;
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

        Bat(dc, new Point(w * 0.56 + 24 * Math.Sin(time * 0.35), h * 0.10 + 6 * Math.Sin(time * 0.8)), s * 0.050, time * 9);
        Bat(dc, new Point(w * 0.86 + 16 * Math.Sin(time * 0.5 + 2), h * 0.36 + 5 * Math.Sin(time * 0.9)), s * 0.035, time * 11 + 1);
        Bat(dc, new Point(w * 0.10 + 12 * Math.Sin(time * 0.4 + 4), h * 0.40), s * 0.030, time * 10 + 2);

        Graveyard(dc, w, h);
        Glow(dc, new Point(w * 0.20, h * 1.02), m * 0.55, Color.FromArgb(120, 0x5F, 0xD4, 0x3A));
        Glow(dc, new Point(w * 0.90, h * 1.05), m * 0.45, Color.FromArgb(90, 0x5F, 0xD4, 0x3A));

        // engrenages-citrouilles : les dents tournent, le visage reste droit
        var t = Train(new Point(w * 0.40, h * 0.48), s * 0.33, f.Angle);
        Gear(dc, t.Big, BigTeeth, t.Module, t.BigAngle, Pumpkin, Moss, hollow: false);
        Face(dc, t.Big, t.BigPitch * 0.80);
        Gear(dc, t.Small, SmallTeeth, t.Module, t.SmallAngle, Pumpkin, Moss, hollow: false);
        Face(dc, t.Small, t.SmallPitch * 0.80);

        // nom du pack
        if (!f.ShowTitle) return;
        var dpi = f.PixelsPerDip;
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
}
