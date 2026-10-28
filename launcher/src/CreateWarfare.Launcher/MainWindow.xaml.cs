using System.Windows;
using System.Windows.Media.Imaging;
using CreateWarfare.Launcher.ViewModels;

namespace CreateWarfare.Launcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadHeroImage();
        StateChanged += (_, _) => OnStateChanged();
        Loaded += async (_, _) => await ((MainViewModel)DataContext).RefreshAsync();
    }

    /// <summary>Assets\hero.jpg ou .png, s'il a été ajouté au projet ; sinon l'illustration dessinée reste.</summary>
    void LoadHeroImage()
    {
        foreach (var name in new[] { "hero.jpg", "hero.png" })
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/{name}");
                if (Application.GetResourceStream(uri) == null) continue;
                HeroImage.Source = new BitmapImage(uri);
                HeroImage.Visibility = Visibility.Visible;
                HeroFallback.Visibility = Visibility.Collapsed;
                return;
            }
            catch (System.IO.IOException) { }
        }
    }

    void OnStateChanged()
    {
        // Sans cadre Windows, une fenêtre agrandie déborde de l'écran de l'épaisseur de la bordure de redimensionnement.
        BorderThickness = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(1);
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void OnClose(object sender, RoutedEventArgs e) => Close();
}
