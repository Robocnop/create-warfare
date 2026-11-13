using Velopack;
using Velopack.Sources;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Comment le launcher se met à jour.</summary>
public enum UpdateRoute
{
    /// <summary>Installé par Setup.exe (Velopack) : téléchargement des deltas, relance par Update.exe.</summary>
    Installed,
    /// <summary>Exe portable (1.x, ou lancé hors installation) : l'exe annoncé dans version.json remplace l'exe en cours.
    /// Pour la 2.0.0, cet exe est le Setup.exe : c'est la migration vers l'installeur.</summary>
    Portable,
}

/// <summary>
/// Mises à jour du launcher installé (Velopack). Le parcours côté interface reste celui de SelfUpdater :
/// « vX disponible » (annoncé par version.json), clic, téléchargement, relance.
/// </summary>
public static class InstalledUpdater
{
    /// <summary>Dépôt public dont les releases launcher-v* portent releases.win.json, les .nupkg et Setup.exe.</summary>
    public const string RepoUrl = "https://github.com/Robocnop/create-warfare";

    /// <summary>Essais : dossier local (sortie de vpk pack) utilisé à la place de GitHub.</summary>
    public const string SourceEnv = "CW_VELOPACK_SOURCE";

    /// <summary>Identifiant du paquet : il donne le dossier d'installation (%LOCALAPPDATA%\CreateWarfare), distinct de
    /// celui des réglages et des logs (%LOCALAPPDATA%\CreateWarfareLauncher) pour qu'une mise à jour ou une désinstallation n'y touche pas.</summary>
    public const string PackId = "CreateWarfare";

    /// <summary>Nom du Setup produit par vpk et joint à la release (nom annoncé dans version.json).</summary>
    public const string SetupFileName = PackId + "-win-Setup.exe";

    public static UpdateRoute ChooseRoute(bool installed) => installed ? UpdateRoute.Installed : UpdateRoute.Portable;

    /// <summary>Source des mises à jour : le dossier de CW_VELOPACK_SOURCE s'il existe, sinon les releases GitHub.</summary>
    public static IUpdateSource CreateSource(string? localDir) =>
        !string.IsNullOrWhiteSpace(localDir) ? new SimpleFileSource(new DirectoryInfo(localDir)) : new GithubSource(RepoUrl, null, false);

    public static UpdateManager CreateManager() =>
        new(CreateSource(Environment.GetEnvironmentVariable(SourceEnv)));

    public static bool IsInstalled
    {
        get
        {
            try { return CreateManager().IsInstalled; }
            catch (Exception e) { Log.Info($"Velopack indisponible : {e.Message}"); return false; }
        }
    }

    public static UpdateRoute Route => ChooseRoute(IsInstalled);

    /// <summary>Dossier racine de l'installation (celui de Update.exe), ou null hors installation.</summary>
    public static string? InstallRoot => RootFromExe(Environment.ProcessPath);

    /// <summary>L'exe vit dans &lt;racine&gt;\current\ ; Update.exe est dans &lt;racine&gt;.</summary>
    public static string? RootFromExe(string? exePath)
    {
        var current = Path.GetDirectoryName(exePath);
        if (current == null || !string.Equals(Path.GetFileName(current), "current", StringComparison.OrdinalIgnoreCase)) return null;
        var root = Path.GetDirectoryName(current);
        return root != null && File.Exists(Path.Combine(root, "Update.exe")) ? root : null;
    }

    /// <summary>Télécharge (delta si possible), applique et relance. Ne rend la main qu'en cas d'échec.</summary>
    public static async Task UpdateAndRestartAsync(IProgress<int>? percent, CancellationToken ct)
    {
        var mgr = CreateManager();
        var info = await mgr.CheckForUpdatesAsync() ?? throw new InvalidOperationException("Aucune mise à jour trouvée dans la release du launcher");
        Log.Info($"Mise à jour Velopack vers {info.TargetFullRelease.Version} ({(info.DeltasToTarget.Length > 0 ? info.DeltasToTarget.Length + " deltas" : "version complète")})");
        await mgr.DownloadUpdatesAsync(info, p => percent?.Report(p), cancelToken: ct);
        mgr.ApplyUpdatesAndRestart(info.TargetFullRelease);
    }
}
