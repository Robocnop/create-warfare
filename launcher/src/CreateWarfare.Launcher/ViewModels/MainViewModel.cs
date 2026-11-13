using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.ViewModels;

public enum Screen { Home, Install, Review, Working, Changelog, Settings, Gallery, Logs, About }

enum PrimaryAction { None, Install, Update, Play, UpdateLauncher }

public sealed partial class MainViewModel : Observable
{
    readonly Downloader _dl = new();
    readonly LauncherSettings _settings = SettingsStore.Load();
    InstanceInfo? _instance;
    /// <summary>version.json tel que publié (version stable à la racine).</summary>
    VersionInfo? _manifest;
    /// <summary>Version choisie dans le sélecteur : celle qu'on installe.</summary>
    VersionInfo? _remote;
    VersionChoice? _choice;
    FilePlan? _plan;
    CancellationTokenSource? _cts;
    PrimaryAction _action;
    /// <summary>Relit version.json toutes les 10 min (30 s tant qu'il est injoignable) : un launcher resté ouvert voit la nouvelle version.</summary>
    readonly System.Windows.Threading.DispatcherTimer _manifestTimer = new() { Interval = TimeSpan.FromMinutes(10) };

    public MainViewModel()
    {
        PrimaryCommand = new Command(OnPrimaryAsync, () => _action != PrimaryAction.None);
        RepairCommand = new Command(() => AnalyseAsync(repair: true), () => _instance != null && _remote != null);
        ConfirmCommand = new Command(ConfirmAsync, () => _plan != null);
        BackHomeCommand = new Command(() => { _plan = null; Screen = Screen.Home; });
        CancelWorkCommand = new Command(() => _cts?.Cancel(), () => CanCancel);
        UpdateLauncherCommand = new Command(() => UpdateLauncherAsync(), () => LauncherUpdateAvailable);
        DownloadZipCommand = new Command(DownloadZipAsync, () => _remote != null);
        ShowZipCommand = new Command(() => Explorer("/select,\"" + InstallZipPath + "\""), () => InstallZipPath != null);
        OpenCurseForgeCommand = new Command(OpenCurseForge);
        RescanCommand = new Command(RescanAsync);
        ShowChangelogCommand = new Command(() => Screen = Screen.Changelog);
        ShowSettingsCommand = new Command(() => Screen = Screen.Settings);
        OpenInstanceCommand = new Command(() => Explorer("\"" + _instance!.Path + "\""), () => _instance != null);
        OpenLogsCommand = new Command(() => { Directory.CreateDirectory(Log.LogDir); Explorer("\"" + Log.LogDir + "\""); });
        BrowseCommand = new Command(Browse);
        AutoDetectCommand = new Command(async () => { _settings.InstancePath = null; SettingsStore.Save(_settings); await RefreshAsync(); });
        DismissToastCommand = new Command(() => ToastText = null);
        ToggleVersionMenuCommand = new Command(() => IsVersionMenuOpen = !IsVersionMenuOpen, () => VersionItems.Count > 0);
        UninstallCommand = new Command(UninstallAsync);
        _manifestTimer.Tick += async (_, _) => await RecheckManifestAsync();
        _manifestTimer.Start();
        InitShell();
        InitPlay();
        InitNative();
    }

    // ---- Commandes
    public Command PrimaryCommand { get; }
    public Command RepairCommand { get; }
    public Command ConfirmCommand { get; }
    public Command BackHomeCommand { get; }
    public Command CancelWorkCommand { get; }
    public Command UpdateLauncherCommand { get; }
    public Command DownloadZipCommand { get; }
    public Command ShowZipCommand { get; }
    public Command OpenCurseForgeCommand { get; }
    public Command RescanCommand { get; }
    public Command ShowChangelogCommand { get; }
    public Command ShowSettingsCommand { get; }
    public Command OpenInstanceCommand { get; }
    public Command OpenLogsCommand { get; }
    public Command BrowseCommand { get; }
    public Command AutoDetectCommand { get; }
    public Command DismissToastCommand { get; }
    public Command ToggleVersionMenuCommand { get; }
    public Command UninstallCommand { get; }

    // ---- Écran courant
    Screen _screen = Screen.Home;
    public Screen Screen
    {
        get => _screen;
        set
        {
            if (!Set(ref _screen, value)) return;
            foreach (var n in new[] { nameof(IsHome), nameof(IsInstall), nameof(IsReview), nameof(IsWorking), nameof(IsChangelog), nameof(IsSettings), nameof(ShowHero) })
                Raise(n);
            RaiseShell();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool IsHome => Screen == Screen.Home;
    public bool IsInstall => Screen == Screen.Install;
    public bool IsReview => Screen == Screen.Review;
    public bool IsWorking => Screen == Screen.Working;
    public bool IsChangelog => Screen == Screen.Changelog;
    public bool IsSettings => Screen == Screen.Settings;
    /// <summary>L'image de gauche est masquée sur les écrans en pleine largeur.</summary>
    public bool ShowHero => Screen is Screen.Home or Screen.Install or Screen.Working;

    // ---- Accueil
    string _statusTitle = "Recherche de l'instance…";
    public string StatusTitle { get => _statusTitle; set => Set(ref _statusTitle, value); }
    string _packVersion = "—";
    public string PackVersion { get => _packVersion; set => Set(ref _packVersion, value); }
    string _packSub = "";
    public string PackSub { get => _packSub; set => Set(ref _packSub, value); }
    public string LauncherVersion => "v" + SelfUpdater.Display(SelfUpdater.CurrentVersion);
    string _launcherSub = "";
    public string LauncherSub { get => _launcherSub; set => Set(ref _launcherSub, value); }
    public bool LauncherUpdateAvailable => _manifest != null && SelfUpdater.UpdateAvailable(_manifest);
    public bool LauncherUpToDate => !LauncherUpdateAvailable;
    string _primaryText = "…";
    public string PrimaryText { get => _primaryText; set => Set(ref _primaryText, value); }
    public string? Notice => string.IsNullOrWhiteSpace(_remote?.Notice) ? null : _remote!.Notice;
    public bool HasNotice => Notice != null;
    public string Changelog => string.IsNullOrWhiteSpace(_remote?.Changelog) ? "Aucune note de version." : _remote!.Changelog;
    public string RemoteVersion => _remote == null ? "" : "v" + _remote.Version;
    public string InstancePath => _instance?.Path ?? "Aucune instance trouvée";
    public bool HasInstance => _instance != null;

    // ---- Sélecteur de version
    public ObservableCollection<VersionItem> VersionItems { get; } = new();
    public string ChoiceLabel => _choice?.Label ?? "Version";
    public string ChoiceVersion => _choice?.Info.Version ?? "—";
    public bool ChoiceIsPrerelease => _choice?.IsPrerelease == true;
    public bool ChoiceIsRelease => !ChoiceIsPrerelease;
    /// <summary>Le sélecteur choisit un canal (stable / pré-release) : inutile, donc masqué, quand il n'y a qu'un choix.</summary>
    public bool HasVersionChoice => VersionItems.Count > 1;
    bool _versionMenuOpen;
    public bool IsVersionMenuOpen { get => _versionMenuOpen; set => Set(ref _versionMenuOpen, value); }

    // ---- Réglages
    public bool DevMode
    {
        get => _settings.DevMode;
        set
        {
            if (_settings.DevMode == value) return;
            _settings.DevMode = value;
            SettingsStore.Save(_settings);
            Raise();
            ApplyChoice();
        }
    }
    bool _keepPlayerData = true;
    public bool KeepPlayerData { get => _keepPlayerData; set => Set(ref _keepPlayerData, value); }
    public string BackupRoot => Uninstaller.DefaultBackupRoot;

    // ---- Installation
    string? _installZipPath;
    public string? InstallZipPath { get => _installZipPath; set { Set(ref _installZipPath, value); Raise(nameof(HasInstallZip)); } }
    public bool HasInstallZip => InstallZipPath != null;

    // ---- Revue
    public ObservableCollection<PlanRow> Rows { get; } = new();
    string _reviewKicker = "";
    public string ReviewKicker { get => _reviewKicker; set => Set(ref _reviewKicker, value); }
    string _reviewTitle = "";
    public string ReviewTitle { get => _reviewTitle; set => Set(ref _reviewTitle, value); }
    string _reviewText = "";
    public string ReviewText { get => _reviewText; set => Set(ref _reviewText, value); }
    public int CountReplace => _plan?.Count(FileActionKind.Replace) ?? 0;
    public int CountAdd => _plan?.Count(FileActionKind.Add) ?? 0;
    public int CountRemove => _plan?.Count(FileActionKind.Remove) ?? 0;
    public int CountSame => _plan?.Count(FileActionKind.Same) ?? 0;
    public int CountProtected => _plan?.Count(FileActionKind.Protected) ?? 0;
    public int CountKept => _plan?.Count(FileActionKind.Kept) ?? 0;
    string _confirmText = "Mettre à jour";
    public string ConfirmText { get => _confirmText; set => Set(ref _confirmText, value); }

    // ---- Travail en cours
    string _workKicker = "";
    public string WorkKicker { get => _workKicker; set => Set(ref _workKicker, value); }
    string _workTitle = "";
    public string WorkTitle { get => _workTitle; set => Set(ref _workTitle, value); }
    string _workCount = "";
    public string WorkCount { get => _workCount; set => Set(ref _workCount, value); }
    string _workTotal = "";
    public string WorkTotal { get => _workTotal; set => Set(ref _workTotal, value); }
    double _workProgress;
    public double WorkProgress { get => _workProgress; set { if (Set(ref _workProgress, value)) Raise(nameof(WorkPercent)); } }
    string _workDetail = "";
    public string WorkDetail { get => _workDetail; set => Set(ref _workDetail, value); }
    public int WorkPercent => (int)Math.Round(WorkProgress * 100);
    string _workCurrent = "";
    public string WorkCurrent { get => _workCurrent; set => Set(ref _workCurrent, value); }
    bool _canCancel;
    public bool CanCancel { get => _canCancel; set => Set(ref _canCancel, value); }

    // ---- Toast
    string? _toastText;
    public string? ToastText { get => _toastText; set { Set(ref _toastText, value); Raise(nameof(HasToast)); } }
    public bool HasToast => ToastText != null;
    bool _toastIsError;
    public bool ToastIsError { get => _toastIsError; set => Set(ref _toastIsError, value); }

    void Toast(string text, bool error = false)
    {
        ToastIsError = error;
        ToastText = text;
        OnToastShown();
        if (error) Log.Error(text); else Log.Info(text);
    }

    // ================================================================ Logique

    public async Task RefreshAsync()
    {
        _instance = await Task.Run(() =>
            _settings.InstancePath is { } p && InstanceFinder.Read(p) is { } chosen
                ? chosen
                : InstanceFinder.Find(CurseForgeLocator.FindMinecraftRoot(), null));
        if (_instance != null && _settings.InstancePath != _instance.Path)
        {
            _settings.InstancePath = _instance.Path;
            SettingsStore.Save(_settings);
        }

        try
        {
            _manifest = await _dl.FetchManifestAsync(SettingsStore.ManifestUrl(_settings), CancellationToken.None);
            _manifestTimer.Interval = TimeSpan.FromMinutes(10);
        }
        catch (Exception e)
        {
            _manifest = null;
            _manifestTimer.Interval = TimeSpan.FromSeconds(30);
            Log.Error("version.json indisponible", e);
            Toast(e is InvalidDataException
                ? T("s.manifest.invalid", "La liste des mises à jour est illisible. Ce n'est pas ta connexion : nouvel essai automatique dans 30 s.")
                : T("s.manifest.offline", "Serveur de mises à jour injoignable. Vérifie ta connexion : nouvel essai automatique dans 30 s."), error: true);
        }
        ApplyChoice();
        // après un échec de la mise à jour automatique, on ne réessaie qu'une fois par jour (le bouton reste disponible)
        var recentFailure = _settings.AutoUpdateFailedUtc is { } t && DateTime.UtcNow - t < TimeSpan.FromDays(1);
        if (_settings.AutoUpdateLauncher && !recentFailure && LauncherUpdateAvailable && _manifest?.Launcher != null)
        {
            Log.Info("Mise à jour automatique du launcher");
            _ = UpdateLauncherAsync(auto: true);
        }
    }

    /// <summary>Relecture silencieuse de version.json : ne dérange pas une installation en cours, n'affiche rien en cas d'échec.</summary>
    async Task RecheckManifestAsync()
    {
        if (IsStep) return;
        VersionInfo m;
        try { m = await _dl.FetchManifestAsync(SettingsStore.ManifestUrl(_settings), CancellationToken.None); }
        catch (Exception e)
        {
            Log.Info($"Relecture de version.json impossible : {e.Message}");
            return;
        }
        if (IsStep) return;
        _manifestTimer.Interval = TimeSpan.FromMinutes(10);
        var wasOffline = _manifest == null;
        var changed = wasOffline || m.Version != _manifest!.Version || m.Launcher?.Version != _manifest.Launcher?.Version
                      || m.Prerelease?.Version != _manifest.Prerelease?.Version || m.Notice != _manifest.Notice;
        if (!changed) return;
        var newPack = !wasOffline && m.Version != _manifest!.Version;
        _manifest = m;
        ApplyChoice();
        if (wasOffline) Toast(T("s.manifest.back", "Connexion rétablie."));
        else if (newPack) Toast(string.Format(T("s.manifest.newVersion", "Nouvelle version du pack disponible : v{0}."), m.Version));
    }

    /// <summary>Reconstruit le sélecteur et la version visée à partir du canal mémorisé.</summary>
    void ApplyChoice()
    {
        VersionItems.Clear();
        if (_manifest == null)
        {
            _choice = null;
            _remote = null;
        }
        else
        {
            _choice = VersionPicker.Pick(_manifest, _settings.DevMode, _settings.Channel);
            _remote = _choice.Info;
            foreach (var c in VersionPicker.Choices(_manifest, _settings.DevMode))
                VersionItems.Add(new VersionItem(c, Select));
        }
        foreach (var n in new[] { nameof(ChoiceLabel), nameof(ChoiceVersion), nameof(ChoiceIsPrerelease), nameof(ChoiceIsRelease), nameof(HasVersionChoice) })
            Raise(n);
        UpdateHome();
        RefreshBdcraft(force: true);
        BuildNews();
        Raise(nameof(MinecraftVersion));
        Raise(nameof(NeoForgeVersion));
    }

    void Select(VersionChoice c)
    {
        IsVersionMenuOpen = false;
        if (_settings.Channel != c.Channel)
        {
            _settings.Channel = c.Channel;
            SettingsStore.Save(_settings);
        }
        ApplyChoice();
    }

    async Task RescanAsync()
    {
        await RefreshAsync();
        if (_instance == null)
        {
            Toast("Toujours pas d'instance « Create Warfare » dans CurseForge.", error: true);
            return;
        }
        Screen = Screen.Home;
        Toast("Instance Create Warfare trouvée.");
    }

    void UpdateHome()
    {
        var installed = _instance == null ? null : UpdatePlanner.InstalledVersion(_instance.Path, _instance);
        // une instance à la bonne version mais dont les packs de ressources n'ont jamais été proposés reste à mettre à jour
        var upToDate = installed != null && _remote != null && installed == _remote.Version
                       && UpdatePlanner.PendingPacks(_remote, UpdatePlanner.ReadState(_instance!.Path)).Count == 0;

        PackVersion = installed == null ? "—" : "v" + installed;
        PackSub = _instance == null ? "non installé"
            : _remote == null ? "installé"
            : upToDate ? "à jour" : $"{RemoteVersion} disponible";
        LauncherSub = LauncherUpdateAvailable ? $"v{_manifest!.Launcher!.Version} disponible" : "à jour";

        (StatusTitle, PrimaryText, _action) = (_instance, _remote) switch
        {
            (null, null) => ("Pack non installé", "Installer", PrimaryAction.None),
            (null, _) => ("Pack non installé", "Installer", PrimaryAction.Install),
            (_, null) => ("Hors ligne", "Jouer", PrimaryAction.Play),
            _ when SelfUpdater.PackRequiresUpdate(_remote!) => ("Launcher trop ancien", "Mettre à jour le launcher", PrimaryAction.UpdateLauncher),
            _ when !upToDate && IsDowngrade(installed, _remote!.Version) => ($"Retour à v{_remote!.Version}", "Installer cette version", PrimaryAction.Update),
            _ when !upToDate => ("Mise à jour disponible", "Mettre à jour", PrimaryAction.Update),
            _ => ("Prêt à jouer", "Jouer", PrimaryAction.Play),
        };
        SetPrimaryIcon(_action switch
        {
            PrimaryAction.Install => "IcDownload",
            PrimaryAction.Update => "IcSync",
            PrimaryAction.UpdateLauncher => "IcSync",
            _ => "IcPlay",
        });
        if (GameRunning && _action == PrimaryAction.Play) (StatusTitle, PrimaryText) = (T("s.status.inGame", "En jeu"), T("s.play.inGame", "Jeu lancé"));
        foreach (var n in new[] { nameof(Notice), nameof(HasNotice), nameof(Changelog), nameof(RemoteVersion), nameof(InstancePath), nameof(HasInstance), nameof(LauncherUpdateAvailable), nameof(LauncherUpToDate) })
            Raise(n);
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    Task OnPrimaryAsync()
    {
        switch (_action)
        {
            case PrimaryAction.Install: Screen = Screen.Install; return Task.CompletedTask;
            case PrimaryAction.Update: return AnalyseAsync(repair: false);
            case PrimaryAction.UpdateLauncher: return UpdateLauncherAsync();
            case PrimaryAction.Play: return PlayAsync();
            default: return Task.CompletedTask;
        }
    }

    /// <summary>Télécharge le zip (petit), calcule le plan et ouvre l'écran de revue. Rien n'est écrit.</summary>
    async Task AnalyseAsync(bool repair)
    {
        if (_instance == null || _remote == null) return;
        var instance = _instance;
        var remote = _remote;
        StartWork("ANALYSE", $"Préparation de v{remote.Version}", cancellable: true);
        try
        {
            var overrides = await _dl.GetOverridesAsync(remote, BytesProgress(remote.Zip.Size, "Pack " + RemoteVersion), _cts!.Token);
            WorkCurrent = "Comparaison des fichiers de l'instance…";
            var state = UpdatePlanner.ReadState(instance.Path);
            var plan = await Task.Run(() => UpdatePlanner.Plan(instance.Path, remote, overrides, state, instance, repair));

            if (plan.IsEmpty && state?.Version == remote.Version)
            {
                Screen = Screen.Home;
                Toast("Tous les fichiers sont à jour.");
                return;
            }
            ShowReview(plan, repair);
        }
        catch (OperationCanceledException) { Screen = Screen.Home; }
        catch (Exception e)
        {
            Screen = Screen.Home;
            Log.Error("Analyse impossible", e);
            Toast($"Analyse impossible : {FirstLine(e.Message)}", error: true);
        }
    }

    void ShowReview(FilePlan plan, bool repair)
    {
        _plan = plan;
        Rows.Clear();
        foreach (var a in plan.Actions) Rows.Add(new PlanRow(a));

        ReviewKicker = repair ? "VÉRIFICATION / RÉPARATION" : "REVUE / MISE À JOUR";
        ReviewTitle = repair ? $"Vérification de v{plan.ToVersion}"
            : string.IsNullOrEmpty(plan.FromVersion) ? $"Installation de v{plan.ToVersion}"
            : $"v{plan.FromVersion} → v{plan.ToVersion}";
        var dl = plan.DownloadSize > 0 ? $" {Format.Size(plan.DownloadSize)} à télécharger d'abord." : "";
        var servers = plan.ServersToAdd.Count == 0 ? ""
            : "\nAjouté à ta liste de serveurs : " + string.Join(", ", plan.ServersToAdd.Select(s => $"{s.Name} ({s.Ip})")) + ".";
        var packs = plan.PacksToEnable.Count == 0 ? ""
            : "\nActivé dans tes packs de ressources (une seule fois, tu peux le désactiver ensuite) : "
              + string.Join(", ", plan.PacksToEnable.Select(p => Path.GetFileNameWithoutExtension(p.File))) + ".";
        ReviewText = "Rien n'est écrit avant ta confirmation. Chaque fichier remplacé ou supprimé est d'abord sauvegardé, " +
                     $"et tout est remis en place en cas d'erreur.{dl}\nTes touches, réglages, mondes et mods ajoutés ne sont pas touchés.{servers}{packs}";
        if (plan.Count(FileActionKind.Kept) > 0)
            ReviewText += $"\n{plan.Count(FileActionKind.Kept)} fichier(s) de configuration que tu as modifiés sont gardés : le pack ne les a pas changés. « Réparer » les remet comme dans le pack.";
        if (IsDowngrade(plan.FromVersion, plan.ToVersion))
            ReviewText += $"\n\n⚠ Retour d'une version plus récente (v{plan.FromVersion}) : un monde solo ouvert avec v{plan.FromVersion} " +
                          "peut perdre des blocs ou des objets. Fais une copie de tes mondes avant.";
        ConfirmText = plan.IsEmpty ? "Valider" : repair ? "Réparer" : "Mettre à jour";
        foreach (var n in new[] { nameof(CountReplace), nameof(CountAdd), nameof(CountRemove), nameof(CountSame), nameof(CountProtected), nameof(CountKept) })
            Raise(n);
        Screen = Screen.Review;
    }

    async Task ConfirmAsync()
    {
        if (_plan == null || _instance == null) return;
        if (ProcessGuard.CurseForgeRunning())
        {
            Toast("Ferme CurseForge avant de mettre à jour : il réécrit l'instance pendant qu'il tourne.", error: true);
            return;
        }
        var dir = _instance.Path;
        if (await Task.Run(() => ProcessGuard.GameRunning(dir)))
        {
            Toast("Minecraft tourne encore sur cette instance : ferme le jeu d'abord.", error: true);
            return;
        }

        var plan = _plan;
        StartWork("TÉLÉCHARGEMENT", "Téléchargement des mods", cancellable: true);
        var progress = MakeApplyProgress(plan.ToVersion);
        try
        {
            var ct = _cts!.Token;
            await Task.Run(() => new UpdateApplier(_dl).ApplyAsync(dir, plan, progress, ct));
            ApplyPerformanceMode(); // une mise à jour d'un mod lourd repose un jar actif
            if (_remote is { } installed && installed.Version == plan.ToVersion)
                _ = Task.Run(() => _dl.Prune(installed)); // le cache ne garde que la version en place
            _plan = null;
            UpdateHome();
            Screen = Screen.Home;
            Toast($"v{plan.ToVersion} est installée.");
        }
        catch (OperationCanceledException)
        {
            Screen = Screen.Review;
            Toast("Téléchargement annulé, rien n'a été modifié.");
        }
        catch (InsufficientSpaceException e)
        {
            Screen = Screen.Review;
            Log.Error("Mise à jour refusée : espace disque", e);
            ShowError("Pas assez de place sur le disque", e.Message + ".\n\nRien n'a été téléchargé ni modifié.");
        }
        catch (RollbackIncompleteException e)
        {
            Screen = Screen.Home;
            Log.Error("Mise à jour échouée, retour arrière incomplet", e);
            ShowError("Mise à jour interrompue : instance à réparer",
                $"{FirstLine(e.Message)}.\n\n{e.NotRestored.Count} fichier(s) n'ont pas pu être remis en place, par exemple {e.NotRestored[0].Replace('/', '\\')}. " +
                "Ferme Minecraft et CurseForge, puis clique sur « Réparer ».\n\nCopie de sécurité : " + e.BackupDir);
        }
        catch (Exception e)
        {
            Screen = Screen.Home;
            Log.Error("Mise à jour échouée", e);
            var why = e is UnauthorizedAccessException ? "accès refusé à un fichier de l'instance (ferme le jeu et CurseForge, vérifie l'antivirus)" : FirstLine(e.Message);
            Toast($"Échec : {why}. L'instance a été remise comme avant.", error: true);
        }
    }

    /// <summary>Progression d'une application (téléchargement des jars, puis écriture) sur l'écran de travail.</summary>
    Progress<ApplyProgress> MakeApplyProgress(string toVersion)
    {
        var clock = Stopwatch.StartNew();
        long firstBytes = -1;
        return new Progress<ApplyProgress>(p =>
        {
            if (p.Phase == "apply" && CanCancel)
            {
                WorkKicker = "APPLICATION";
                WorkTitle = $"Écriture de v{toVersion}";
                CanCancel = false;
            }
            WorkCount = p.Done.ToString();
            WorkTotal = $"/ {p.Total}";
            WorkProgress = p.Total == 0 ? 1 : (double)p.Done / p.Total;
            WorkCurrent = p.Current.Length == 0 ? "" : (p.Phase == "apply" ? "⇄  " : "↓  ") + p.Current.Replace('/', '\\');
            if (p.Phase == "download" && p.BytesTotal > 0)
            {
                // la barre suit les octets : un gros jar ne donne plus l'impression que tout est figé
                WorkProgress = Math.Min(1, (double)p.Bytes / p.BytesTotal);
                if (firstBytes < 0) { firstBytes = p.Bytes; clock.Restart(); }
                var secs = clock.Elapsed.TotalSeconds;
                var speed = secs > 0.5 ? (p.Bytes - firstBytes) / secs : 0;
                var left = speed > 0 && p.BytesTotal > p.Bytes ? TimeSpan.FromSeconds((p.BytesTotal - p.Bytes) / speed) : TimeSpan.Zero;
                WorkDetail = $"{Format.Size(p.Bytes)} / {Format.Size(p.BytesTotal)}"
                    + (speed > 0 ? $"  ·  {speed / (1024 * 1024):0.0} Mo/s" : "")
                    + (left > TimeSpan.Zero ? "  ·  " + (left.TotalMinutes >= 1 ? $"{(int)left.TotalMinutes} min {left.Seconds:00} s" : $"{left.Seconds} s") : "");
            }
            else if (p.Phase == "apply") WorkDetail = "";
        });
    }

    async Task UpdateLauncherAsync(bool auto = false)
    {
        if (_manifest?.Launcher == null) return;
        var remote = _manifest;
        StartWork("LAUNCHER", $"Launcher v{remote.Launcher!.Version}", cancellable: true);
        try
        {
            if (InstalledUpdater.Route == UpdateRoute.Installed)
            {
                // Installé par Setup.exe : Velopack télécharge les deltas, remplace les fichiers et relance.
                WorkCurrent = "Téléchargement de la mise à jour…";
                var percent = new Progress<int>(pct => WorkProgress = Math.Clamp(pct, 0, 100) / 100.0);
                await InstalledUpdater.UpdateAndRestartAsync(percent, _cts!.Token);
            }
            else
            {
                // Exe portable : remplacé par celui de version.json (pour la 2.0.0, le Setup.exe : migration vers l'installeur).
                await SelfUpdater.UpdateAndRestartAsync(remote, _dl, BytesProgress(remote.Launcher.Size, Path.GetFileName(remote.Launcher.Url)), _cts!.Token);
            }
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) { Screen = Screen.Home; }
        catch (Exception e)
        {
            Screen = Screen.Home;
            Log.Error("Auto-update échoué", e);
            if (auto)
            {
                _settings.AutoUpdateFailedUtc = DateTime.UtcNow;
                SettingsStore.Save(_settings);
            }
            Toast($"Mise à jour du launcher impossible : {FirstLine(e.Message)}", error: true);
        }
    }

    async Task DownloadZipAsync()
    {
        if (_remote == null) return;
        var remote = _remote;
        StartWork("TÉLÉCHARGEMENT", $"Pack v{remote.Version}", cancellable: true);
        try
        {
            var cached = await _dl.GetZipAsync(remote, BytesProgress(remote.Zip.Size, $"CreateWarfare-{remote.Version}.zip"), _cts!.Token);
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloads);
            var dest = Path.Combine(downloads, $"CreateWarfare-{remote.Version}.zip");
            File.Copy(cached, dest, overwrite: true);
            InstallZipPath = dest;
            Toast($"Zip enregistré dans Téléchargements : {Path.GetFileName(dest)}");
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Toast($"Téléchargement impossible : {FirstLine(e.Message)}", error: true); }
        Screen = Screen.Install;
    }

    /// <summary>Supprime l'instance CurseForge, puis le launcher et ses données après sa fermeture.</summary>
    async Task UninstallAsync()
    {
        var keep = KeepPlayerData;
        var question = "Désinstaller Create Warfare ?\n\n" +
            (_instance == null ? "" : $"• Le profil CurseForge sera supprimé :\n  {_instance.Path}\n") +
            (keep ? $"• Tes mondes, captures et schémas seront d'abord copiés dans :\n  {BackupRoot}\n" : "• Tes mondes, captures et schémas seront PERDUS.\n") +
            "• Le launcher et ses réglages seront supprimés à sa fermeture.";
        if (!await AskAsync(T("s.uninstall.title", "Désinstaller Create Warfare ?"), question[(question.IndexOf('\n') + 2)..],
                T("s.uninstall.yes", "Désinstaller"), danger: true))
            return;
        if (ProcessGuard.CurseForgeRunning())
        {
            Toast("Ferme CurseForge avant de désinstaller : il garde l'instance ouverte.", error: true);
            return;
        }
        var instance = _instance;
        if (instance != null && await Task.Run(() => ProcessGuard.GameRunning(instance.Path)))
        {
            Toast("Minecraft tourne encore sur cette instance : ferme le jeu d'abord.", error: true);
            return;
        }

        StartWork("DÉSINSTALLATION", "Suppression de Create Warfare", cancellable: false);
        string? saved = null;
        try
        {
            if (instance != null)
            {
                if (keep)
                {
                    WorkCurrent = "Copie des mondes, captures et schémas…";
                    saved = await Task.Run(() => Uninstaller.BackupPlayerData(instance.Path, BackupRoot, DateTime.Now));
                }
                WorkCurrent = "Suppression du profil CurseForge…";
                await Task.Run(() => Uninstaller.DeleteInstance(instance.Path));
            }
            Uninstaller.ScheduleSelfRemoval(InstalledUpdater.InstallRoot);
        }
        catch (Exception e)
        {
            Screen = Screen.Settings;
            Log.Error("Désinstallation échouée", e);
            Toast($"Désinstallation interrompue : {FirstLine(e.Message)}", error: true);
            return;
        }
        await AskAsync(T("s.uninstall.done", "Create Warfare est désinstallé"),
            saved == null ? T("s.uninstall.bye", "Le launcher va se fermer.") : T("s.uninstall.saved", "Tes fichiers sont dans :") + "\n" + saved,
            T("s.close", "Fermer"), no: "");
        Application.Current.Shutdown();
    }

    /// <summary>Vrai si on installe une version plus ancienne que celle en place (pré-release → stable).</summary>
    static bool IsDowngrade(string? from, string to) =>
        !string.IsNullOrEmpty(from) && SelfUpdater.ParseVersion(to) < SelfUpdater.ParseVersion(from);

    void OpenCurseForge()
    {
        var exe = CurseForgeLocator.FindExe();
        if (exe == null)
        {
            Toast("CurseForge introuvable. Installe-le depuis curseforge.com/download/app.", error: true);
            return;
        }
        if (_action == PrimaryAction.Play) ApplyPerformanceMode();
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        if (_action == PrimaryAction.Play) OnPlayStarted();
        if (_action == PrimaryAction.Play)
            Toast("CurseForge s'ouvre : lance « Create Warfare » depuis Mes modpacks.");
    }

    void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Dossier de l'instance Create Warfare" };
        if (_instance != null) dlg.InitialDirectory = _instance.Path;
        if (dlg.ShowDialog() != true) return;
        if (InstanceFinder.Read(dlg.FolderName) == null)
        {
            Toast("Ce dossier n'est pas une instance CurseForge (minecraftinstance.json absent).", error: true);
            return;
        }
        _settings.InstancePath = dlg.FolderName;
        SettingsStore.Save(_settings);
        _ = RefreshAsync();
    }

    void StartWork(string kicker, string title, bool cancellable)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        WorkKicker = kicker;
        WorkTitle = title;
        WorkCount = "";
        WorkTotal = "";
        WorkProgress = 0;
        WorkCurrent = "";
        WorkDetail = "";
        CanCancel = cancellable;
        Screen = Screen.Working;
    }

    /// <summary>Progression en octets, affichée en Mo.</summary>
    IProgress<long> BytesProgress(long total, string label)
    {
        long done = 0;
        var sw = Stopwatch.StartNew();
        WorkCurrent = "↓  " + label;
        WorkDetail = "";
        return new Progress<long>(n =>
        {
            done += n;
            var secs = sw.Elapsed.TotalSeconds;
            if (secs > 0.5)
            {
                var speed = done / secs;
                var left = total > done && speed > 0 ? TimeSpan.FromSeconds((total - done) / speed) : TimeSpan.Zero;
                WorkDetail = $"{speed / (1024 * 1024):0.0} Mo/s" + (left > TimeSpan.Zero
                    ? "  ·  " + (left.TotalMinutes >= 1 ? $"{(int)left.TotalMinutes} min {left.Seconds:00} s" : $"{left.Seconds} s") + " " + T("s.work.left", "restantes") : "");
            }
            WorkCount = (done / (1024.0 * 1024)).ToString("0.0");
            WorkTotal = total > 0 ? $"/ {total / (1024.0 * 1024):0.0} Mo" : "Mo";
            WorkProgress = total > 0 ? Math.Min(1, (double)done / total) : 0;
        });
    }

    static void Explorer(string args) => Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
    static string FirstLine(string s) => s.Split('\n')[0].TrimEnd('.', ' ');
}
