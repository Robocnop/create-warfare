using System.Diagnostics;
using System.IO;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.ViewModels;

/// <summary>Launcher 2.0 : vérification complète avant de jouer, lancement automatique par CurseForge, carte du pack de textures recommandé.</summary>
public sealed partial class MainViewModel
{
    // ---- Pack de textures recommandé (PureBDcraft 256x) : jamais téléchargé par le launcher, seulement détecté
    bool _bdInstalled;
    public bool BdcraftInstalled => _bdInstalled;
    public bool BdcraftMissing => !_bdInstalled;
    public string BdcraftUrl => RecommendedPack.Url;
    public Command OpenResourcePacksCommand { get; private set; } = null!;
    public string BdcraftFile => RecommendedPack.FileName;

    void InitPlay() => OpenResourcePacksCommand = new Command(() =>
    {
        var dir = Path.Combine(_instance!.Path, "resourcepacks");
        Directory.CreateDirectory(dir);
        Explorer("\"" + dir + "\"");
    }, () => _instance != null);

    /// <summary>Relit la présence du pack dans resourcepacks/ ; ne notifie que si elle a changé.</summary>
    void RefreshBdcraft(bool force = false)
    {
        var now = RecommendedPack.IsInstalled(_instance?.Path);
        if (now == _bdInstalled && !force) return;
        _bdInstalled = now;
        Raise(nameof(BdcraftInstalled));
        Raise(nameof(BdcraftMissing));
    }

    /// <summary>
    /// Compare l'instance à l'index du pack et répare ce qui manque ou diffère (mêmes règles qu'une mise à jour :
    /// options, saves et jars du joueur intacts). Renvoie false si l'utilisateur annule ou refuse de lancer malgré un échec.
    /// L'appelant a déjà vérifié que le jeu ne tourne pas. CurseForge ouvert n'empêche pas la vérification : depuis que Jouer
    /// passe par lui, il reste presque toujours dans la zone de notification, et au repos il n'écrit que minecraftinstance.json,
    /// que la vérification ne touche jamais (contrairement à la RAM ou à une mise à jour, refusées tant qu'il tourne).
    /// </summary>
    async Task<bool> VerifyBeforePlayAsync(InstanceInfo instance)
    {
        if (_remote == null) return true;
        var remote = _remote;

        StartWork(T("s.verify.kicker", "VÉRIFICATION"), string.Format(T("s.verify.title", "Vérification de v{0}"), remote.Version), cancellable: true);
        try
        {
            var ct = _cts!.Token;
            var overrides = await _dl.GetOverridesAsync(remote, BytesProgress(remote.Zip.Size, "Pack " + RemoteVersion), ct);
            WorkCurrent = T("s.verify.compare", "Comparaison des fichiers de l'instance…");
            WorkDetail = "";
            var progress = MakeApplyProgress(remote.Version); // créé ici : il capture le contexte de l'interface
            var result = await Task.Run(() => PackVerifier.VerifyAndRepairAsync(instance.Path, remote, overrides, instance, _dl, progress, ct));
            UpdateHome();
            Screen = Screen.Home;
            if (result.Total > 0)
                Toast(string.Format(T("s.verify.repaired", "{0} fichier(s) réparé(s) avant le lancement."), result.Total));
            return true;
        }
        catch (OperationCanceledException)
        {
            Screen = Screen.Home;
            return false;
        }
        catch (Exception e)
        {
            Screen = Screen.Home;
            Log.Error("Vérification avant le jeu impossible", e);
            var why = e is UnauthorizedAccessException ? T("s.verify.denied", "accès refusé à un fichier de l'instance") : FirstLine(e.Message);
            return await AskAsync(T("s.verify.failTitle", "Vérification impossible"),
                string.Format(T("s.verify.failText", "{0}.\n\nTu peux lancer le jeu quand même : si un mod manque, utilise « Réparer » une fois la connexion rétablie."), why),
                T("s.verify.playAnyway", "Lancer quand même"), T("s.cancel", "Annuler"));
        }
    }

    /// <summary>
    /// Jouer : jeu déjà lancé ? vérification complète, puis CurseForge lance l'instance tout seul (lien curseforge://launch-game).
    /// Le contrôle du jeu en cours passe avant tout : CurseForge ouvre une seconde copie du jeu si on lui renvoie le lien (essayé le 13/11).
    /// </summary>
    async Task PlayAsync()
    {
        var instance = _instance;
        if (instance == null) return;
        // Lancement envoyé, jeu pas encore détecté : un double clic ne renvoie rien ; relancer reste possible après une erreur de CurseForge
        if (IsLaunching)
        {
            if (!await AskAsync(T("s.launch.pendingTitle", "Lancement déjà en cours"),
                    T("s.launch.pendingText", "CurseForge est en train de lancer Minecraft. Ne relance que si CurseForge a affiché une erreur (compte Microsoft, instance introuvable) et que tu l'as corrigée."),
                    T("s.launch.retry", "Relancer"), T("s.cancel", "Annuler")))
                return;
            IsLaunching = false;
        }
        if (await Task.Run(() => ProcessGuard.GameRunning(instance.Path)))
        {
            Toast(T("s.launch.already", "Minecraft est déjà lancé."));
            return;
        }
        if (!await VerifyBeforePlayAsync(instance)) return;
        await LaunchViaCurseForgeAsync(instance);
    }

    /// <summary>
    /// Demande à CurseForge de lancer l'instance (même lien que ses raccourcis Bureau) : il démarre s'il est fermé, puis lance le jeu
    /// avec son compte Microsoft, sans clic sur Play. Sans guid d'instance, repli : CurseForge s'ouvre et le joueur clique sur Play.
    /// </summary>
    async Task LaunchViaCurseForgeAsync(InstanceInfo instance)
    {
        var exe = CurseForgeLocator.FindExe();
        if (exe == null)
        {
            Toast(T("s.launch.noCf", "CurseForge introuvable. Installe-le depuis curseforge.com/download/app, importe le pack, puis réessaie."), error: true);
            return;
        }
        if (!Guid.TryParse(instance.Guid, out _))
        {
            Log.Info($"Instance sans guid ({instance.Path}) : CurseForge est seulement ouvert");
            OpenCurseForge();
            return;
        }
        if (CurseForgeLocator.ReadLaunchMethod() == CurseForgeLaunchMethod.MojangLauncher)
        {
            // CurseForge refuserait (« Cannot launch modpack with Mojang launcher ») : on explique avant
            if (await AskAsync(T("s.launch.mojangTitle", "CurseForge passe par le launcher Mojang"),
                    T("s.launch.mojangText", "CurseForge ne lance un pack tout seul qu'avec son propre lanceur. Dans CurseForge : Paramètres → Minecraft, choisis le lancement par CurseForge au lieu du launcher Mojang, puis reclique sur Jouer."),
                    T("s.launch.openCf", "Ouvrir CurseForge"), T("s.cancel", "Annuler")))
                OpenCurseForge();
            return;
        }

        ApplyPerformanceMode();
        try
        {
            using var cf = Process.Start(CurseForgeLocator.LaunchStartInfo(exe, instance.Guid));
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error("CurseForge n'a pas pu être lancé", e);
            Toast(T("s.launch.cfFailed", "CurseForge ne démarre pas : ") + FirstLine(e.Message), error: true);
            return;
        }
        Log.Info($"Lancement demandé à CurseForge : instance {instance.Guid}");
        OnPlayStarted(expectGame: true);
        Toast(T("s.launch.started", "CurseForge lance Create Warfare. Minecraft s'ouvre dans quelques secondes ; le chargement des mods prend une à deux minutes."));
    }
}
