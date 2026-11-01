using CreateWarfare.Launcher.Controls.Scenes;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Themes;

/// <summary>
/// Un thème = une surcouche du design system : palette (Themes/Palettes/&lt;Palette&gt;.xaml, mêmes clés partout),
/// illustration (HeroScene) et décor d'ambiance facultatif. Les composants ne connaissent aucun thème.
/// </summary>
/// <param name="Key">Clé stockée dans les réglages (SeasonalTheme.*).</param>
/// <param name="Palette">Nom du fichier de palette, sans extension.</param>
/// <param name="NameKey">Clé du nom dans Strings/*.xaml ; NameFr sert de secours.</param>
/// <param name="Ambient">Décor animé posé sur l'accueil (neige, pétales…), ou null.</param>
public sealed record ThemeDefinition(string Key, string Palette, string NameKey, string NameFr, HeroScene Scene, AmbientDecor? Ambient = null);

/// <summary>
/// Liste des thèmes du launcher. Ajouter un thème : une clé dans SeasonalTheme, une période dans ThemeSchedule
/// (si saisonnier), une palette, une scène, une ligne ici et son nom dans Strings/fr.xaml et en.xaml.
/// </summary>
public static class ThemeRegistry
{
    public static readonly IReadOnlyList<ThemeDefinition> All = new ThemeDefinition[]
    {
        new(SeasonalTheme.Default, "Default", "s.theme.default", "Create Warfare", new DefaultScene()),
        new(SeasonalTheme.Halloween, "Halloween", "s.theme.halloween", "Halloween", new HalloweenScene()),
        new(SeasonalTheme.Easter, "Easter", "s.theme.easter", "Pâques", new EasterScene(), new PetalDrift()),
        new(SeasonalTheme.Christmas, "Christmas", "s.theme.christmas", "Noël", new ChristmasScene(), new Snowfall()),
        new(SeasonalTheme.Summer, "Summer", "s.theme.summer", "Été", new SummerScene(), new SunMotes()),
    };

    /// <summary>Thème de cette clé, ou le thème de base si elle est inconnue.</summary>
    public static ThemeDefinition Get(string? key) => All.FirstOrDefault(t => t.Key == key) ?? All[0];
}
