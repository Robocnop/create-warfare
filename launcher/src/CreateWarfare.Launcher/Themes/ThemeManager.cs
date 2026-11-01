using System.Windows;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Themes;

/// <summary>
/// Applique un thème en remplaçant la palette (Themes/Palettes/*.xaml) dans les ressources de l'application.
/// Les composants lisent les couleurs en DynamicResource : le changement est immédiat, sans redémarrer.
/// </summary>
public static class ThemeManager
{
    const string PalettePrefix = "Themes/Palettes/";

    /// <summary>Thème appliqué (default, halloween, christmas, summer).</summary>
    public static string Current { get; private set; } = SeasonalTheme.Default;
    public static event Action? Changed;

    /// <summary>Applique le thème correspondant au réglage (« auto » = selon la date ; CW_THEME prime pour les essais).</summary>
    public static void Apply(string? setting)
    {
        var theme = SeasonalTheme.ResolveNow(setting);
        var file = ThemeRegistry.Get(theme).Palette;
        var dicts = Application.Current.Resources.MergedDictionaries;
        var palette = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{PalettePrefix}{file}.xaml") };
        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains(PalettePrefix) == true);
        if (old != null) dicts[dicts.IndexOf(old)] = palette; else dicts.Insert(0, palette);
        if (Current != theme) Log.Info($"Thème : {theme}");
        Current = theme;
        Changed?.Invoke();
    }
}
