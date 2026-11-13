using System.IO;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.ViewModels;

/// <summary>Launcher 2.0, étape 1 : vérification complète avant de jouer, carte du pack de textures recommandé.</summary>
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
    /// </summary>
    async Task<bool> VerifyBeforePlayAsync()
    {
        if (_instance == null || _remote == null) return true;
        var instance = _instance;
        var remote = _remote;
        // CurseForge ouvert ou jeu lancé : il réécrit l'instance, on ne la touche pas (comme pour une mise à jour)
        if (ProcessGuard.CurseForgeRunning() || await Task.Run(() => ProcessGuard.GameRunning(instance.Path)))
        {
            Log.Info("Vérification avant le jeu ignorée : CurseForge ou le jeu est déjà ouvert");
            return true;
        }

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

    /// <summary>Jouer : vérification complète, puis lancement natif (ou CurseForge si le repli est choisi).</summary>
    async Task PlayAsync()
    {
        if (!await VerifyBeforePlayAsync()) return;
        if (!NativeLaunch)
        {
            OpenCurseForge();
            return;
        }
        await LaunchNativeAsync();
    }
}
