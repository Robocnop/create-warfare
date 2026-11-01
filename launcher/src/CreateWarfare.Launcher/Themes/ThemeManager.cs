using System.Windows;
using System.Windows.Threading;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Themes;

/// <summary>
/// Applique un thème en remplaçant la palette (Themes/Palettes/*.xaml) dans les ressources de l'application.
/// Les composants lisent les couleurs en DynamicResource : le changement est immédiat, sans redémarrer.
/// En mode automatique, la date est revérifiée chaque minute : le thème change seul au passage de minuit.
/// </summary>
public static class ThemeManager
{
    const string PalettePrefix = "Themes/Palettes/";

    static string? _setting;
    static DispatcherTimer? _clock;
    static bool _announced;

    /// <summary>Thème appliqué (clé SeasonalTheme.*).</summary>
    public static string Current { get; private set; } = SeasonalTheme.Default;

    /// <summary>Juste avant de remplacer la palette (la fenêtre en profite pour préparer le fondu).</summary>
    public static event Action? Changing;
    public static event Action? Changed;

    /// <summary>Applique le thème correspondant au réglage (« auto » = selon la date ; CW_THEME prime pour les essais).</summary>
    public static void Apply(string? setting)
    {
        _setting = setting;
        StartClock();
        var theme = SeasonalTheme.ResolveNow(setting);
        var dicts = Application.Current.Resources.MergedDictionaries;
        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains(PalettePrefix) == true);
        if (old != null && theme == Current) // rien à changer
        {
            if (!_announced) Log.Info($"Thème : {theme}");
            _announced = true;
            return;
        }
        _announced = true;
        if (old != null) Changing?.Invoke();
        var palette = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{PalettePrefix}{ThemeRegistry.Get(theme).Palette}.xaml") };
        if (old != null) dicts[dicts.IndexOf(old)] = palette; else dicts.Insert(0, palette);
        Log.Info($"Thème : {theme}");
        Current = theme;
        Changed?.Invoke();
    }

    /// <summary>Revérifie la date chaque minute (passage de minuit, changement d'heure système, mise en veille).</summary>
    static void StartClock()
    {
        if (_clock != null) return;
        _clock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
        _clock.Tick += (_, _) =>
        {
            if (SeasonalTheme.ResolveNow(_setting) != Current) Apply(_setting);
        };
        _clock.Start();
    }
}
