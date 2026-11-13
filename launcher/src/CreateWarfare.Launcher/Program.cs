using Velopack;

namespace CreateWarfare.Launcher;

public static class Program
{
    /// <summary>
    /// Velopack d'abord : l'installeur relance l'exe avec des arguments spéciaux (--veloapp-install, --veloapp-updated,
    /// --veloapp-uninstall…) et il faut les traiter avant toute fenêtre. Hors installation (exe portable, dotnet run), ne fait rien.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
