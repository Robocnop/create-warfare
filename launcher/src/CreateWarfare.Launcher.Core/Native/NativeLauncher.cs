using System.Diagnostics;
using System.Text.Json.Nodes;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Core.Native;

/// <summary>
/// Lancement natif de bout en bout, sans interface : Java 21 (trouvé ou téléchargé), installation du jeu, ligne de commande,
/// démarrage du processus. Windows seulement : la version Linux passe par Prism Launcher (Prism.cs).
/// </summary>
public sealed class NativeLauncher
{
    readonly Downloader _dl;
    readonly string? _curseForgeRoot;
    /// <summary>Racine du jeu installé par le launcher (libraries/, assets/, versions/, natives/).</summary>
    public string GameRoot { get; }

    /// <param name="dl">Téléchargeur (hachage, reprise, liste blanche d'adresses).</param>
    /// <param name="curseForgeMinecraftRoot">Dossier minecraft de CurseForge (Java et fichiers à réutiliser), ou null.</param>
    /// <param name="gameRoot">Autre racine du jeu (essais) ; sinon <see cref="NativeConfig.GameRoot"/>.</param>
    public NativeLauncher(Downloader dl, string? curseForgeMinecraftRoot, string? gameRoot = null)
    {
        _dl = dl;
        _curseForgeRoot = curseForgeMinecraftRoot;
        GameRoot = gameRoot ?? NativeConfig.GameRoot;
    }

    /// <summary>Vrai sous Windows seulement : le lancement natif n'existe pas dans le build Linux.</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>Java 21, téléchargé chez Adoptium s'il n'y en a aucun sur le PC.</summary>
    public async Task<JavaInstall> EnsureJavaAsync(IProgress<long>? bytes, CancellationToken ct)
    {
        if (JavaLocator.Find(_curseForgeRoot) is { } j) return j;
        Log.Info("Aucun Java 21 trouvé : téléchargement depuis Adoptium");
        return await JavaLocator.DownloadAsync(_dl, NativeConfig.JavaRoot, bytes, ct);
    }

    /// <summary>Installe ou vérifie Minecraft + NeoForge dans <see cref="GameRoot"/> (voir <see cref="GameInstaller"/>).</summary>
    public Task<InstalledGame> EnsureGameAsync(JavaInstall java, IProgress<InstallProgress>? progress, CancellationToken ct) =>
        new GameInstaller(GameRoot, NativeConfig.MirrorRoots(_curseForgeRoot), _dl).EnsureAsync(java.Executable, progress, ct);

    /// <summary>Arguments Java propres à l'instance CurseForge (javaArgsOverride), s'il y en a.</summary>
    public static IReadOnlyList<string> InstanceJvmArgs(string instanceDir)
    {
        try
        {
            var file = Path.Combine(instanceDir, "minecraftinstance.json");
            if (!File.Exists(file)) return Array.Empty<string>();
            var node = JsonNode.Parse(File.ReadAllText(file));
            return LaunchCommandBuilder.SplitArgs(node?["javaArgsOverride"]?.GetValue<string>());
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Construit la ligne de commande du jeu (fonction pure, rien n'est lancé). RAM en Mo, <paramref name="clientId"/> = identifiant d'installation (--clientId).</summary>
    /// <param name="server">« hôte:port » pour rejoindre le serveur dès le chargement, ou null.</param>
    public static LaunchCommand BuildCommand(InstalledGame game, JavaInstall java, string instanceDir, GameSession session, int ramMb,
        string clientId, string? server)
    {
        return LaunchCommandBuilder.Build(game.Version, new LaunchOptions
        {
            JavaExe = java.Executable,
            GameDir = Path.GetFullPath(instanceDir).TrimEnd(Path.DirectorySeparatorChar),
            GameRoot = game.GameRoot,
            NativesDir = game.NativesDir,
            Session = session,
            RamMb = ramMb,
            ClientId = clientId,
            // jamais de connexion directe en mode test hors ligne : le serveur du pack refuserait ce joueur
            QuickPlayServer = session.Offline || string.IsNullOrWhiteSpace(server) ? null : server.Trim(),
            LauncherVersion = SelfUpdater.Display(SelfUpdater.CurrentVersion),
            ExtraJvmArgs = InstanceJvmArgs(instanceDir),
        });
    }

    /// <summary>
    /// Démarre le jeu (javaw, sans console ni redirection : le jeu écrit logs/latest.log dans l'instance).
    /// Les variables propres au launcher ne passent pas au jeu.
    /// </summary>
    public static Process Start(LaunchCommand cmd)
    {
        var psi = new ProcessStartInfo(cmd.JavaExe) { WorkingDirectory = cmd.WorkingDirectory, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in cmd.Arguments) psi.ArgumentList.Add(a);
        foreach (var v in new[] { OfflineTest.Variable, AuthConfig.Variable, "CW_GAME_ROOT", "CW_JAVA_HOME" }) psi.Environment.Remove(v);
        Log.Info("Lancement natif : " + cmd.Redacted());
        return Process.Start(psi) ?? throw new IOException("Impossible de démarrer Java : " + cmd.JavaExe);
    }
}
