using System.Windows;

namespace CreateWarfare.Launcher.Themes;

/// <summary>
/// Animations du launcher. Coupées si Windows le demande (Paramètres → Accessibilité → Effets d'animation,
/// équivalent de prefers-reduced-motion) ou en contraste élevé.
/// </summary>
public static class Motion
{
    public static bool Enabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    /// <summary>Durée réelle d'une animation : la durée voulue, ou 0 si les animations sont coupées.</summary>
    public static Duration Of(double ms) => new(TimeSpan.FromMilliseconds(Enabled ? ms : 0));

    /// <summary>Remet les durées de Tokens.xaml à zéro quand les animations sont coupées.</summary>
    public static void Apply(ResourceDictionary resources)
    {
        if (Enabled) return;
        foreach (var key in new[] { "DurFast", "DurBase", "DurSlow" })
            resources[key] = new Duration(TimeSpan.Zero);
    }
}
