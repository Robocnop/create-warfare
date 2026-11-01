using System.Windows;
using System.Windows.Media;
using static CreateWarfare.Launcher.Controls.Scenes.SceneKit;

namespace CreateWarfare.Launcher.Controls.Scenes;

/// <summary>
/// Décor d'ambiance d'un thème, dessiné par-dessus l'accueil (images du carrousel comprises) par AmbientLayer.
/// Sans état : la position de chaque particule ne dépend que de son numéro et du temps. Volontairement sobre
/// (quelques dizaines de formes simples).
/// </summary>
public abstract class AmbientDecor
{
    public abstract void Render(DrawingContext dc, double w, double h, double time);

    /// <summary>Pseudo-aléatoire stable dans [0, 1[ pour la particule i, tirage k.</summary>
    protected static double R(int i, int k)
    {
        var v = Math.Sin(i * 12.9898 + k * 78.233) * 43758.5453;
        return v - Math.Floor(v);
    }

    /// <summary>Position qui défile en boucle sur [−margin, size + margin[.</summary>
    protected static double Wrap(double v, double size, double margin)
    {
        double span = size + 2 * margin;
        return ((v % span) + span) % span - margin;
    }
}

/// <summary>Noël : neige fine qui tombe doucement en ondulant.</summary>
public sealed class Snowfall : AmbientDecor
{
    const int Count = 46;
    static readonly Brush[] Flakes = { Solid(110, 255, 255, 255), Solid(160, 255, 255, 255), Solid(210, 240, 246, 255) };

    public override void Render(DrawingContext dc, double w, double h, double time)
    {
        for (int i = 0; i < Count; i++)
        {
            double speed = 14 + 22 * R(i, 1), size = 1.1 + 2.2 * R(i, 2);
            double y = Wrap(R(i, 4) * h + time * speed, h, 6);
            double x = Wrap(R(i, 3) * w + 14 * Math.Sin(time * (0.3 + 0.5 * R(i, 5)) + i), w, 6);
            dc.DrawEllipse(Flakes[i % Flakes.Length], null, new Point(x, y), size, size);
        }
    }
}

/// <summary>Pâques : quelques pétales pastel qui tombent en tournoyant, poussés par une brise légère.</summary>
public sealed class PetalDrift : AmbientDecor
{
    const int Count = 16;
    static readonly Brush[] Petals =
    {
        Solid(190, 0xF3, 0xA9, 0xC4), Solid(170, 0xF4, 0xF1, 0xEA), Solid(170, 0xF6, 0xCF, 0x6A), Solid(150, 0x9A, 0xDB, 0x8F),
    };

    public override void Render(DrawingContext dc, double w, double h, double time)
    {
        for (int i = 0; i < Count; i++)
        {
            double speed = 16 + 18 * R(i, 1), size = 3.5 + 3 * R(i, 2);
            double y = Wrap(R(i, 4) * h + time * speed, h, 10);
            double x = Wrap(R(i, 3) * w + time * (8 + 10 * R(i, 6)) + 18 * Math.Sin(time * 0.7 + i), w, 10);
            dc.PushTransform(new RotateTransform(time * (40 + 50 * R(i, 5)) + i * 37, x, y));
            dc.DrawEllipse(Petals[i % Petals.Length], null, new Point(x, y), size, size * 0.55);
            dc.Pop();
        }
    }
}

/// <summary>Été : poussières de lumière dorée qui montent lentement, comme au-dessus de l'eau au coucher du soleil.</summary>
public sealed class SunMotes : AmbientDecor
{
    const int Count = 18;
    static readonly Brush Mote = Freeze(new RadialGradientBrush(Color.FromArgb(200, 0xFF, 0xE1, 0x9A), Color.FromArgb(0, 0xFF, 0xC9, 0x3C)));

    public override void Render(DrawingContext dc, double w, double h, double time)
    {
        for (int i = 0; i < Count; i++)
        {
            double speed = 6 + 10 * R(i, 1), size = 2.5 + 3.5 * R(i, 2);
            double y = h - Wrap(R(i, 4) * h + time * speed, h, 10);
            double x = Wrap(R(i, 3) * w + 10 * Math.Sin(time * 0.4 + i * 1.7), w, 10);
            dc.PushOpacity(0.35 + 0.45 * (0.5 + 0.5 * Math.Sin(time * (0.8 + R(i, 5)) + i)));
            dc.DrawEllipse(Mote, null, new Point(x, y), size, size);
            dc.Pop();
        }
    }
}
