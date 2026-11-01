using System.Windows;
using System.Windows.Media;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>
/// Moment d'une illustration : taille, angle de la grande roue (degrés), temps écoulé (s) pour les décors animés,
/// titre affiché ou non, échelle DPI pour le texte.
/// </summary>
public readonly record struct SceneFrame(double Width, double Height, double Angle, double Time, bool ShowTitle, double PixelsPerDip);

/// <summary>
/// Illustration d'un thème, dessinée par HeroArt (accueil sans image, écran de travail, premier lancement, aperçus).
/// Une scène ne garde aucun état : tout ce qui bouge dépend de SceneFrame.Angle et SceneFrame.Time.
/// </summary>
public abstract class HeroScene
{
    public abstract void Render(DrawingContext dc, in SceneFrame f);
}

/// <summary>Outils de dessin partagés par les scènes : lueurs et engrenages qui s'engrènent vraiment.</summary>
public static class SceneKit
{
    public static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }

    public static SolidColorBrush Solid(byte a, byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromArgb(a, r, g, b)));

    /// <summary>Halo radial qui s'efface vers l'extérieur.</summary>
    public static void Glow(DrawingContext dc, Point c, double r, Color color)
    {
        var b = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B));
        dc.DrawEllipse(b, null, c, r, r);
    }

    // ------------------------------------------------------------------ engrenages

    /// <summary>Nombre de dents : grande roue, petite roue (en haut à droite), roue sombre (en bas à gauche).</summary>
    public const int BigTeeth = 16, SmallTeeth = 10, LowTeeth = 12;

    /// <summary>Direction (radians, y vers le bas) de la petite roue et de la roue basse par rapport à la grande.</summary>
    public const double SmallDirection = -0.75, LowDirection = 2.35;

    // Profil d'une dent sur un pas (fractions du pas) : montée, sommet, descente, puis creux en arc.
    // Symétrique autour de ToothCenter, creux centré sur ToothCenter + 0,5.
    const double RiseEnd = 0.12, TipEnd = 0.44, FallEnd = 0.56, ToothCenter = (0 + FallEnd) / 2;

    /// <summary>Module (taille de dent) qui donne à une roue de <paramref name="teeth"/> dents ce rayon extérieur.</summary>
    public static double ModuleFor(double outerRadius, int teeth) => outerRadius / (teeth / 2.0 + 1);

    /// <summary>Rayon primitif : deux roues de même module engrènent quand l'entraxe vaut la somme de ces rayons.</summary>
    public static double PitchRadius(int teeth, double module) => module * teeth / 2;

    /// <summary>Rayon extérieur (sommet des dents).</summary>
    public static double OuterRadius(int teeth, double module) => PitchRadius(teeth, module) + module;

    /// <summary>
    /// Place une roue menée contre une roue menante (direction theta, en radians) et calcule son angle (degrés)
    /// pour qu'une dent de la menante tombe toujours dans un creux de la menée : rapport −Nmenante/Nmenée,
    /// phase réglée sur la ligne des centres.
    /// </summary>
    public static (Point Center, double AngleDeg) Mesh(Point driver, int driverTeeth, double driverAngleDeg, int teeth, double module, double theta)
    {
        double d = PitchRadius(driverTeeth, module) + PitchRadius(teeth, module);
        var center = new Point(driver.X + d * Math.Cos(theta), driver.Y + d * Math.Sin(theta));
        double s1 = 2 * Math.PI / driverTeeth, s2 = 2 * Math.PI / teeth, a1 = driverAngleDeg * Math.PI / 180;
        // Phase de la menante sur la ligne des centres (en pas) + phase de la menée = constante : une dent face à un creux.
        double a2 = theta + Math.PI - s2 * (0.5 + 2 * ToothCenter) + s2 * (theta - a1) / s1;
        return (center, a2 * 180 / Math.PI);
    }

    /// <summary>Contour d'une roue dentée (remplissage pair-impair : le moyeu est évidé si hollow).</summary>
    public static StreamGeometry GearGeometry(Point c, int teeth, double module, double angleDeg, bool hollow)
    {
        var g = new StreamGeometry { FillRule = FillRule.EvenOdd };
        double pitch = PitchRadius(teeth, module), r = pitch + module, root = pitch - 1.15 * module;
        double step = 2 * Math.PI / teeth, a0 = angleDeg * Math.PI / 180;
        Point P(double rad, double a) => new(c.X + rad * Math.Cos(a), c.Y + rad * Math.Sin(a));
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(P(root, a0), true, true);
            for (int i = 0; i < teeth; i++)
            {
                double a = a0 + i * step;
                ctx.LineTo(P(r, a + step * RiseEnd), true, true);
                ctx.LineTo(P(r, a + step * TipEnd), true, true);
                ctx.LineTo(P(root, a + step * FallEnd), true, true);
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
        return g;
    }

    /// <summary>Roue dentée de module donné, avec un moyeu évidé et des rayons si hollow.</summary>
    public static void Gear(DrawingContext dc, Point c, int teeth, double module, double angleDeg, Brush fill, Pen? edge, bool hollow = true)
    {
        dc.DrawGeometry(fill, edge, GearGeometry(c, teeth, module, angleDeg, hollow));
        if (!hollow) return;
        // rayons
        double pitch = PitchRadius(teeth, module), r = pitch + module, root = pitch - 1.15 * module;
        double step = 2 * Math.PI / teeth, a0 = angleDeg * Math.PI / 180;
        Point P(double rad, double a) => new(c.X + rad * Math.Cos(a), c.Y + rad * Math.Sin(a));
        var spoke = new Pen(fill, r * 0.10);
        for (int i = 0; i < 4; i++)
        {
            double a = a0 + i * Math.PI / 2 + step / 2;
            dc.DrawLine(spoke, P(r * 0.30, a), P(root * 0.92, a));
        }
    }

    /// <summary>
    /// Le train d'engrenages commun à tous les thèmes : grande roue en <paramref name="big"/> (rayon extérieur
    /// <paramref name="bigOuter"/>), petite roue en haut à droite, roue basse en bas à gauche.
    /// </summary>
    public static GearTrain Train(Point big, double bigOuter, double angle)
    {
        double m = ModuleFor(bigOuter, BigTeeth);
        var (small, aSmall) = Mesh(big, BigTeeth, angle, SmallTeeth, m, SmallDirection);
        var (low, aLow) = Mesh(big, BigTeeth, angle, LowTeeth, m, LowDirection);
        return new GearTrain(m, big, angle, small, aSmall, low, aLow);
    }
}

/// <summary>Positions et angles des trois roues d'un train (voir SceneKit.Train).</summary>
public readonly record struct GearTrain(double Module, Point Big, double BigAngle, Point Small, double SmallAngle, Point Low, double LowAngle)
{
    public double BigPitch => SceneKit.PitchRadius(SceneKit.BigTeeth, Module);
    public double SmallPitch => SceneKit.PitchRadius(SceneKit.SmallTeeth, Module);
    public double LowPitch => SceneKit.PitchRadius(SceneKit.LowTeeth, Module);
}
