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
        ThemeManager.Apply(SettingsStore.Load().Theme);
        base.OnStartup(e);
    }

    static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Erreur non gérée", e.Exception);
        MessageBox.Show($"Erreur inattendue : {e.Exception.Message}\n\nLe détail est dans les logs (%LOCALAPPDATA%\\CreateWarfareLauncher\\logs).",
            "Create Warfare Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
