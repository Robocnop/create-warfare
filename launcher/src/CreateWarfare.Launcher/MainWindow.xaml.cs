using System.Windows;
using System.Windows.Input;
using CreateWarfare.Launcher.ViewModels;

namespace CreateWarfare.Launcher;

public partial class MainWindow : Window
{
    MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => OnStateChanged();
        Loaded += async (_, _) =>
        {
            await Vm.RefreshAsync();
            // Essais et captures : CW_START_PAGE=news|gallery|settings|logs|about
            if (Environment.GetEnvironmentVariable("CW_START_PAGE") is { Length: > 0 } page) Vm.Navigate(page);
        };
        PreviewKeyDown += OnKey;
    }

    /// <summary>Raccourcis : Entrée = bouton principal (accueil), Échap = retour, Ctrl+, = paramètres, flèches = images.</summary>
    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Vm.GoBack(); e.Handled = true; return; }
        if (Vm.IsDialogOpen)
        {
            if (e.Key == Key.Enter) { Vm.DialogYesCommand.Execute(null); e.Handled = true; }
            return;
        }
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control) { Vm.Navigate("settings"); e.Handled = true; return; }
        if (Vm.IsLightboxOpen)
        {
            if (e.Key == Key.Left) { Vm.LightboxPrevCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Right) { Vm.LightboxNextCommand.Execute(null); e.Handled = true; }
            return;
        }
        if (Vm.IsHome && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.ButtonBase)
        {
            if (e.Key == Key.Enter && !Vm.IsOnboarding && Vm.PrimaryCommand.CanExecute(null)) { Vm.PrimaryCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Left) { Vm.PrevSlideCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Right) { Vm.NextSlideCommand.Execute(null); e.Handled = true; }
        }
        if (Vm.IsOnboarding && e.Key == Key.Enter) { Vm.FinishOnboardingCommand.Execute(null); e.Handled = true; }
    }

    void OnCarouselHover(object sender, MouseEventArgs e) => Vm.CarouselPaused = true;
    void OnCarouselLeave(object sender, MouseEventArgs e) => Vm.CarouselPaused = false;

    void OnStateChanged()
    {
        // Sans cadre Windows, une fenêtre agrandie déborde de l'écran de l'épaisseur de la bordure de redimensionnement.
        BorderThickness = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(1);
        MaxButton.Content = FindResource(WindowState == WindowState.Maximized ? "IcRestore" : "IcMaximize");
        MaxButton.ToolTip = WindowState == WindowState.Maximized ? "Restaurer" : "Agrandir";
    }

    void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void OnClose(object sender, RoutedEventArgs e) => Close();
}
