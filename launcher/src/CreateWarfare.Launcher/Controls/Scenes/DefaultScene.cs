using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static CreateWarfare.Launcher.Controls.Scenes.SceneKit;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>Thème de base : engrenages de laiton devant une lueur d'explosion, bande de signalisation, nom du pack.</summary>
public sealed class DefaultScene : HeroScene
{
    static readonly Brush Brass = Freeze(new SolidColorBrush(Color.FromArgb(150, 0xC8, 0x93, 0x3A)));
    static readonly Brush BrassDark = Freeze(new SolidColorBrush(Color.FromArgb(90, 0x8A, 0x63, 0x24)));
    static readonly Pen BrassEdge = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 0xE8, 0xB4, 0x5A)), 1.5));

    public override void Render(DrawingContext dc, in SceneFrame f)
    {
        double w = f.Width, h = f.Height;
        var area = new Rect(0, 0, w, h);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x1C, 0x20, 0x29), Color.FromRgb(0x0E, 0x10, 0x14), 60), null, area);
        Glow(dc, new Point(w * 0.30, h * 0.72), Math.Max(w, h) * 0.75, Color.FromArgb(150, 0xFF, 0x7A, 0x1A));
        Glow(dc, new Point(w * 0.42, h * 0.62), Math.Max(w, h) * 0.30, Color.FromArgb(120, 0xFF, 0xD0, 0x60));
        Glow(dc, new Point(w * 0.95, h * 0.05), Math.Max(w, h) * 0.55, Color.FromArgb(70, 0x58, 0xA6, 0xFF));

        var s = Math.Min(w, h);
        var big = f.ShowTitle ? new Point(w * 0.40, h * 0.50) : new Point(w * 0.50, h * 0.46);
        var t = Train(big, s * 0.34, f.Angle);
        Gear(dc, t.Low, LowTeeth, t.Module, t.LowAngle, BrassDark, null);
        Gear(dc, t.Big, BigTeeth, t.Module, t.BigAngle, Brass, BrassEdge);
        Gear(dc, t.Small, SmallTeeth, t.Module, t.SmallAngle, Brass, BrassEdge);

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
        if (!f.ShowTitle) return;
        var dpi = f.PixelsPerDip;
        var black = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
        var mono = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        double size = Math.Clamp(w * 0.11, 30, 64);
        var t1 = new FormattedText("CREATE", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, black, size, new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), dpi);
        var t2 = new FormattedText("WARFARE", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, black, size, new SolidColorBrush(Color.FromRgb(0xF2, 0x8C, 0x28)), dpi);
        var t3 = new FormattedText("MINECRAFT 1.21.1 · NEOFORGE · CREATE 6", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, mono, 11, new SolidColorBrush(Color.FromArgb(170, 0xE8, 0xEB, 0xF4)), dpi);
        double x0 = 32, y3 = h - 26 - 22 - t3.Height;
        dc.DrawText(t3, new Point(x0 + 2, y3));
        dc.DrawText(t2, new Point(x0, y3 - t2.Height + size * 0.12));
        dc.DrawText(t1, new Point(x0, y3 - t2.Height - t1.Height + size * 0.30));
    }
}
