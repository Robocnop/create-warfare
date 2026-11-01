using System.Windows;

namespace CreateWarfare.Launcher.Themes;

/// <summary>
/// Textes de l'interface : Strings/fr.xaml (toujours chargé, référence) et Strings/en.xaml par-dessus si l'anglais
/// est choisi. Le XAML les lit en DynamicResource, le ViewModel par MainViewModel.T(clé, secours).
/// </summary>
public static class Strings
{
    const string Prefix = "Strings/";

    public static void Apply(string? language)
    {
        var dicts = Application.Current.Resources.MergedDictionaries;
        foreach (var d in dicts.Where(d => d.Source?.OriginalString.Contains(Prefix) == true).ToList()) dicts.Remove(d);
        dicts.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/{Prefix}fr.xaml") });
        if (language == "en")
            dicts.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/{Prefix}en.xaml") });
    }
}
