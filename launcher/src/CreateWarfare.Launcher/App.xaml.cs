using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CreateWarfare.Launcher.Controls;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Log.Info($"Démarrage du launcher {SelfUpdater.Display(SelfUpdater.CurrentVersion)}");
        SelfUpdater.CleanupOld();
        DispatcherUnhandledException += OnUnhandled;
        if (SeasonalTheme.IsHalloweenNow()) ApplyHalloween();
        base.OnStartup(e);
    }

    /// <summary>
    /// Thème Halloween : noir violacé, orange citrouille, titres en Creepster, décors (toiles, citrouille).
    /// Appelé avant l'ouverture de la fenêtre : ses StaticResource prennent les nouveaux pinceaux, et les styles
    /// de App.xaml les lisent en DynamicResource (les pinceaux d'un style sont figés, on les remplace donc).
    /// </summary>
    void ApplyHalloween()
    {
        Log.Info("Thème Halloween");
        foreach (var (key, hex) in new[]
                 {
                     ("Bg", "#0D0912"), ("Panel", "#140D1C"), ("Row", "#180F22"), ("Line", "#2A1C35"),
                     ("LineStrong", "#45294F"), ("Text", "#F2E8E1"), ("Muted", "#A897AB"), ("Dim", "#6F5F74"),
                     ("Accent", "#FF8A1F"), ("Yellow", "#FF7A0F"), ("YellowHover", "#FF9B3D"),
                     ("Hover", "#21142C"), ("CaptionHover", "#2B1A38"), ("PrimaryText", "#1A0B05"),
                 })
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        Resources["Display"] = HeroArt.Creepster;
        Resources["PrimaryFontSize"] = 26.0;
        Resources["HalloweenVis"] = Visibility.Visible;
        HeroArt.Halloween = true;
    }

    static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Erreur non gérée", e.Exception);
        MessageBox.Show($"Erreur inattendue : {e.Exception.Message}\n\nLe détail est dans les logs (%LOCALAPPDATA%\\CreateWarfareLauncher\\logs).",
            "Create Warfare Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
