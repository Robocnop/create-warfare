using System.Windows;
using System.Windows.Threading;
using CreateWarfare.Launcher.Controls;
using CreateWarfare.Launcher.Core.Services;
using CreateWarfare.Launcher.Themes;

namespace CreateWarfare.Launcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Log.Info($"Démarrage du launcher {SelfUpdater.Display(SelfUpdater.CurrentVersion)}");
        SelfUpdater.CleanupOld();
        DispatcherUnhandledException += OnUnhandled;
        Motion.Apply(Resources);
        ThemeManager.Changed += () => HeroArt.Halloween = ThemeManager.Current == SeasonalTheme.Halloween;
        var settings = SettingsStore.Load();
        Strings.Apply(settings.Language);
        ThemeManager.Apply(settings.Theme);
        base.OnStartup(e);
    }

    static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Erreur non gérée", e.Exception);
        e.Handled = true;
        // Message lisible dans la fenêtre (avec « Copier les logs ») ; MessageBox seulement si elle n'existe pas encore.
        if (Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm && Current.MainWindow.IsLoaded)
            vm.ShowError("Une erreur inattendue s'est produite",
                e.Exception.Message + "\n\nLe launcher continue de fonctionner. Le détail est dans les logs : copie-les pour signaler le problème.");
        else
            MessageBox.Show("Erreur inattendue : " + e.Exception.Message + "\n\nLe détail est dans les logs (%LOCALAPPDATA%\\CreateWarfareLauncher\\logs).",
                "Create Warfare Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
