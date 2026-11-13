using CreateWarfare.Launcher.Core.Services;
using Velopack.Sources;

namespace CreateWarfare.Launcher.Tests;

/// <summary>Installeur Velopack : choix du parcours de mise à jour, dossier d'installation, désinstallation, cohérence avec la publication.</summary>
public class InstalledUpdaterTests : IDisposable
{
    readonly string _tmp = Path.Combine(Path.GetTempPath(), "cw-velo-" + Guid.NewGuid().ToString("N")[..8]);
    public InstalledUpdaterTests() => Directory.CreateDirectory(_tmp);
    public void Dispose() { try { Directory.Delete(_tmp, true); } catch { } }

    [Fact]
    public void Installe_passe_par_Velopack_portable_garde_l_ancien_canal()
    {
        Assert.Equal(UpdateRoute.Installed, InstalledUpdater.ChooseRoute(true));
        Assert.Equal(UpdateRoute.Portable, InstalledUpdater.ChooseRoute(false));
    }

    [Fact]
    public void Hors_installation_Velopack_le_launcher_est_portable()
    {
        // L'hôte de test n'est pas installé par Setup.exe : c'est aussi le cas d'un exe 1.x ou de « dotnet run ».
        Assert.False(InstalledUpdater.IsInstalled);
        Assert.Equal(UpdateRoute.Portable, InstalledUpdater.Route);
        Assert.Null(InstalledUpdater.InstallRoot);
    }

    [Fact]
    public void La_racine_d_installation_est_le_dossier_de_Update_exe_au_dessus_de_current()
    {
        var current = Path.Combine(_tmp, "current");
        Directory.CreateDirectory(current);
        var exe = Path.Combine(current, "CreateWarfareLauncher.exe");
        Assert.Null(InstalledUpdater.RootFromExe(exe)); // pas de Update.exe : pas une installation Velopack
        File.WriteAllText(Path.Combine(_tmp, "Update.exe"), "");
        Assert.Equal(_tmp, InstalledUpdater.RootFromExe(exe));
        Assert.Null(InstalledUpdater.RootFromExe(Path.Combine(_tmp, "CreateWarfareLauncher.exe"))); // exe portable à côté de Update.exe
        Assert.Null(InstalledUpdater.RootFromExe(null));
    }

    [Fact]
    public void La_source_est_un_dossier_local_pour_les_essais_sinon_GitHub()
    {
        Assert.IsType<SimpleFileSource>(InstalledUpdater.CreateSource(_tmp));
        Assert.IsType<GithubSource>(InstalledUpdater.CreateSource(null));
        Assert.IsType<GithubSource>(InstalledUpdater.CreateSource("  "));
    }

    [Fact]
    public void Le_dossier_d_installation_ne_se_confond_pas_avec_les_reglages()
    {
        // Velopack installe dans %LOCALAPPDATA%\<packId> ; les réglages et logs sont dans %LOCALAPPDATA%\CreateWarfareLauncher.
        Assert.NotEqual(Path.GetFileName(Log.AppDataDir), InstalledUpdater.PackId);
        Assert.Equal("CreateWarfare-win-Setup.exe", InstalledUpdater.SetupFileName);
    }

    [Fact]
    public void Le_nom_du_Setup_est_le_meme_dans_publish_launcher_py()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "tools", "publish_launcher.py"))) dir = Path.GetDirectoryName(dir);
        if (dir == null) return; // tests lancés hors du dépôt
        var py = File.ReadAllText(Path.Combine(dir, "tools", "publish_launcher.py"));
        Assert.Contains($"SETUP = \"{InstalledUpdater.SetupFileName}\"", py);
    }

    [Fact]
    public void Script_de_desinstallation_installe_appelle_Update_exe_puis_efface_les_reglages()
    {
        var script = Uninstaller.SelfRemovalScript(1234, @"C:\Users\x\AppData\Local\CreateWarfare\current\CreateWarfareLauncher.exe",
            @"C:\Users\x\AppData\Local\CreateWarfareLauncher", @"C:\Users\x\AppData\Local\CreateWarfare");
        Assert.Contains("PID eq 1234", script);
        Assert.Contains(@"""C:\Users\x\AppData\Local\CreateWarfare\Update.exe"" uninstall --silent", script);
        Assert.DoesNotContain("del /f /q \"C:", script.Replace("del /f /q \"%~f0\"", "")); // l'exe est retiré par Velopack, pas par del
        Assert.Contains(@"rmdir /s /q ""C:\Users\x\AppData\Local\CreateWarfareLauncher""", script);
        Assert.True(script.IndexOf("uninstall --silent") < script.IndexOf("rmdir"));
        Assert.EndsWith("del /f /q \"%~f0\"\r\n", script);
    }
}
