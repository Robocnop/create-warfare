using System.Diagnostics;
using System.IO;
using System.Net.Http;
using CreateWarfare.Launcher.Core.Native;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.ViewModels;

/// <summary>
/// Launcher 2.0, étape 2 : lancement natif (Minecraft 1.21.1 + NeoForge lancés par le launcher, sans CurseForge),
/// connexion Microsoft par code d'appareil, compte affiché, repli « Ouvrir CurseForge ».
/// </summary>
public sealed partial class MainViewModel
{
    static readonly HttpClient AuthHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    /// <summary>Jeu lancé par ce launcher (null s'il a été lancé ailleurs ou s'il est fermé).</summary>
    Process? _gameProcess;
    CancellationTokenSource? _loginCts;

    void InitNative()
    {
        SignInCommand = new Command(async () => await SignInAsync(), () => !IsLoginOpen);
        SignOutCommand = new Command(SignOut, () => _settings.Account != null);
        LoginCopyOpenCommand = new Command(CopyCodeAndOpen, () => LoginCode.Length > 0);
        LoginCancelCommand = new Command(() => _loginCts?.Cancel());
        if (string.IsNullOrEmpty(_settings.GameClientId))
        {
            _settings.GameClientId = Guid.NewGuid().ToString("N");
            SettingsStore.Save(_settings);
        }
        if (OfflineTest.FromEnvironment() is { } off)
            Log.Info($"Mode test hors ligne actif ({OfflineTest.Variable}) : {off.Name}. Jamais proposé dans l'interface.");
    }

    // ================================================================ Réglages

    /// <summary>
    /// Lancement natif actif : Windows (le build Linux passe par Prism), repli CurseForge non choisi, et connexion possible
    /// (Client ID Azure présent, ou mode test hors ligne). Sans Client ID, le bouton Jouer garde le comportement d'avant : CurseForge.
    /// </summary>
    bool NativeLaunch => NativeLauncher.IsSupported && !_settings.UseCurseForgeLaunch
                         && (AuthConfig.IsConfigured || OfflineTest.FromEnvironment() != null);
    public bool IsNativeLaunch => NativeLaunch;

    public bool UseCurseForgeLaunch
    {
        get => _settings.UseCurseForgeLaunch;
        set
        {
            if (_settings.UseCurseForgeLaunch == value) return;
            _settings.UseCurseForgeLaunch = value;
            SettingsStore.Save(_settings);
            Raise();
            RaiseAccount();
        }
    }

    public bool AutoConnectServer
    {
        get => _settings.AutoConnectServer;
        set
        {
            if (_settings.AutoConnectServer == value) return;
            _settings.AutoConnectServer = value;
            SettingsStore.Save(_settings);
            Raise();
        }
    }

    // ================================================================ Compte

    public Command SignInCommand { get; private set; } = null!;
    public Command SignOutCommand { get; private set; } = null!;
    public bool IsSignedIn => _settings.Account != null;
    public bool IsSignedOut => !IsSignedIn;
    public bool AuthAvailable => AuthConfig.IsConfigured;
    public bool AuthUnavailable => !AuthConfig.IsConfigured;

    /// <summary>Ligne du compte (accueil et paramètres).</summary>
    public string AccountLine =>
        !NativeLaunch ? T("s.auth.viaCf", "Lancement par CurseForge")
        : OfflineTest.FromEnvironment() is { } off ? string.Format(T("s.auth.offline", "{0} (test hors ligne)"), off.Name)
        : _settings.Account is { } a ? string.Format(T("s.auth.account", "Compte : {0}"), a.Name)
        : T("s.auth.none", "Aucun compte connecté");

    void RaiseAccount()
    {
        foreach (var n in new[] { nameof(IsSignedIn), nameof(IsSignedOut), nameof(AccountLine), nameof(IsNativeLaunch), nameof(AuthAvailable), nameof(AuthUnavailable) })
            Raise(n);
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    void SignOut()
    {
        _settings.Account = null;
        SettingsStore.Save(_settings);
        RaiseAccount();
        Toast(T("s.auth.signedOut", "Déconnecté du compte Microsoft. La session enregistrée sur ce PC est effacée."));
    }

    // ================================================================ Fenêtre de connexion (code d'appareil)

    bool _loginOpen;
    public bool IsLoginOpen { get => _loginOpen; set { if (Set(ref _loginOpen, value)) System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
    string _loginCode = "";
    public string LoginCode { get => _loginCode; set { Set(ref _loginCode, value); Raise(nameof(HasLoginCode)); } }
    public bool HasLoginCode => LoginCode.Length > 0;
    string _loginUrl = "";
    public string LoginUrl { get => _loginUrl; set => Set(ref _loginUrl, value); }
    string _loginStatus = "";
    public string LoginStatus { get => _loginStatus; set => Set(ref _loginStatus, value); }
    public Command LoginCopyOpenCommand { get; private set; } = null!;
    public Command LoginCancelCommand { get; private set; } = null!;

    void CopyCodeAndOpen()
    {
        CopyText(LoginCode, T("s.auth.codeCopied", "Code copié : colle-le dans la page Microsoft."));
        OpenUrl(LoginUrl);
    }

    /// <summary>Connexion complète : affiche le code, attend que le joueur le saisisse, vérifie la possession du jeu.</summary>
    async Task<GameSession?> SignInAsync()
    {
        if (!AuthConfig.IsConfigured)
        {
            await AskAsync(T("s.auth.notConfigured.title", "Connexion Microsoft pas encore disponible"),
                T("s.auth.notConfigured.text", "Le lancement direct a besoin d'une application Azure autorisée par Mojang, pas encore en place pour ce launcher. En attendant, active « Ouvrir CurseForge au lieu du lancement direct » dans Paramètres → Jeu."),
                T("s.close", "Fermer"), no: "");
            return null;
        }
        _loginCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _loginCts = cts;
        LoginCode = "";
        LoginUrl = "";
        LoginStatus = T("s.auth.starting", "Demande d'un code à Microsoft…");
        IsLoginOpen = true;
        try
        {
            var auth = new MicrosoftAuth(AuthHttp, AuthConfig.ClientId);
            var (session, account) = await AccountStore.LoginAsync(auth, code =>
            {
                LoginCode = code.UserCode;
                LoginUrl = code.VerificationUri;
                LoginStatus = T("s.auth.waiting", "En attente de la connexion sur la page Microsoft…");
            }, cts.Token);
            _settings.Account = account;
            SettingsStore.Save(_settings);
            RaiseAccount();
            Log.Info($"Connexion Microsoft réussie : {session.Name}");
            Toast(string.Format(T("s.auth.done", "Connecté : {0}"), session.Name));
            return session;
        }
        catch (AuthException e) when (e.Error == AuthError.Cancelled) { return null; }
        catch (AuthException e)
        {
            Log.Error($"Connexion Microsoft impossible ({e.Error}) : {e.Message}");
            IsLoginOpen = false;
            await AskAsync(T("s.auth.failTitle", "Connexion impossible"), AuthMessage(e), T("s.close", "Fermer"), no: "");
            return null;
        }
        finally
        {
            if (_loginCts == cts) _loginCts = null;
            IsLoginOpen = false;
            LoginCode = "";
        }
    }

    /// <summary>Message pour le joueur, traduit pour les cas courants (les messages du Core sont en français).</summary>
    string AuthMessage(AuthException e) => e.Error switch
    {
        AuthError.Network => T("s.auth.err.network", "Serveurs Microsoft ou Minecraft injoignables. Vérifie ta connexion Internet, puis réessaie."),
        AuthError.NoMinecraft => T("s.auth.err.noGame", "Ce compte Microsoft ne possède pas Minecraft: Java Edition (ou n'a pas encore choisi de pseudo sur minecraft.net)."),
        AuthError.NoXboxAccount => T("s.auth.err.noXbox", "Ce compte n'a pas encore de profil Xbox. Connecte-toi une fois sur minecraft.net ou xbox.com pour le créer, puis réessaie."),
        AuthError.ChildAccount => T("s.auth.err.child", "Compte enfant : un adulte doit l'ajouter à une famille Microsoft et autoriser Minecraft."),
        AuthError.AppNotApproved => T("s.auth.err.app", "Les services Minecraft refusent encore ce launcher (application Azure pas encore validée par Mojang). Utilise CurseForge en attendant."),
        AuthError.CodeExpired => T("s.auth.err.expired", "Le code a expiré avant d'être saisi. Relance la connexion pour en obtenir un nouveau."),
        AuthError.Declined => T("s.auth.err.declined", "La connexion a été refusée sur la page Microsoft."),
        AuthError.ReloginRequired => T("s.auth.err.relogin", "Ta session Microsoft a expiré : reconnecte-toi."),
        AuthError.NotConfigured => T("s.auth.err.client", "Microsoft refuse le Client ID Azure de ce launcher (application mal réglée : comptes personnels et flux public requis).") + "\n\n" + e.Message,
        AuthError.RateLimited => T("s.auth.err.rate", "Trop de tentatives de connexion. Réessaie dans quelques minutes."),
        _ => e.Message,
    };

    /// <summary>
    /// Session pour lancer le jeu : mode test hors ligne (variable d'environnement seulement), jeton Minecraft encore valide,
    /// rafraîchissement silencieux, sinon connexion par code. null si le joueur annule ou si c'est impossible.
    /// </summary>
    async Task<GameSession?> GetSessionAsync()
    {
        if (OfflineTest.FromEnvironment() is { } off) return off;
        if (!AuthConfig.IsConfigured)
        {
            if (await AskAsync(T("s.auth.notConfigured.title", "Connexion Microsoft pas encore disponible"),
                    T("s.auth.notConfigured.play", "Le lancement direct a besoin d'une application Azure autorisée par Mojang, pas encore en place pour ce launcher. Ouvre CurseForge pour jouer en attendant (réglable dans Paramètres → Jeu)."),
                    T("s.launch.openCf", "Ouvrir CurseForge"), T("s.cancel", "Annuler")))
                OpenCurseForge();
            return null;
        }
        var acc = _settings.Account;
        if (AccountStore.Cached(acc, DateTime.UtcNow) is { } cached) return cached;
        if (acc != null)
        {
            try
            {
                var (session, account) = await AccountStore.RefreshAsync(acc, new MicrosoftAuth(AuthHttp, AuthConfig.ClientId), CancellationToken.None);
                _settings.Account = account;
                SettingsStore.Save(_settings);
                RaiseAccount();
                return session;
            }
            catch (AuthException e) when (e.Error == AuthError.ReloginRequired)
            {
                Log.Info("Session Microsoft expirée : nouvelle connexion");
            }
            catch (AuthException e)
            {
                Log.Error($"Session Microsoft non renouvelée ({e.Error}) : {e.Message}");
                if (await AskAsync(T("s.auth.failTitle", "Connexion impossible"), AuthMessage(e), T("s.launch.openCf", "Ouvrir CurseForge"), T("s.cancel", "Annuler")))
                    OpenCurseForge();
                return null;
            }
        }
        return await SignInAsync();
    }

    // ================================================================ Lancement

    /// <summary>Jeu en cours : celui que ce launcher a lancé, ou un java de l'instance (lancé par CurseForge, par exemple).</summary>
    bool IsGameRunning(string instanceDir) => _gameProcess is { HasExited: false } || ProcessGuard.GameRunning(instanceDir);

    async Task LaunchNativeAsync()
    {
        var instance = _instance;
        if (instance == null) return;
        if (await Task.Run(() => IsGameRunning(instance.Path)))
        {
            Toast(T("s.launch.already", "Minecraft est déjà lancé."));
            return;
        }
        IsLaunching = true;
        GameSession? session;
        try { session = await GetSessionAsync(); }
        finally { IsLaunching = false; }
        if (session == null) return;

        StartWork(T("s.launch.kicker", "LANCEMENT"), string.Format(T("s.launch.title", "Préparation de Minecraft {0}"), NativeConfig.MinecraftVersion), cancellable: true);
        try
        {
            var ct = _cts!.Token;
            var cfRoot = CurseForgeLocator.FindMinecraftRoot();
            var native = new NativeLauncher(_dl, cfRoot);
            WorkCurrent = T("s.launch.java", "Recherche de Java 21…");
            var java = await Task.Run(() => JavaLocator.Find(cfRoot), ct)
                       ?? await JavaLocator.DownloadAsync(_dl, NativeConfig.JavaRoot, BytesProgress(50L << 20, T("s.launch.javaDownload", "Java 21 (Adoptium)")), ct);
            WorkCurrent = T("s.launch.check", "Vérification des fichiers du jeu…");
            WorkDetail = "";
            var progress = new Progress<InstallProgress>(ShowInstallProgress); // créé ici : il capture le contexte de l'interface
            var game = await Task.Run(() => native.EnsureGameAsync(java, progress, ct), ct);

            WorkCurrent = T("s.launch.starting", "Démarrage de Minecraft…");
            WorkDetail = "";
            ApplyPerformanceMode();
            var ram = MemoryAdvisor.ReadAllocated(instance.Path) ?? RecommendedRamMb;
            var cmd = NativeLauncher.BuildCommand(game, java, instance.Path, session, ram, _settings.GameClientId!,
                _settings.AutoConnectServer ? ServerAddress : null);
            var proc = NativeLauncher.Start(cmd);
            _gameProcess = proc;
            GameMarker.Write(instance.Path, proc);
            Screen = Screen.Home;
            OnPlayStarted();
            Toast(string.Format(T("s.launch.started", "Minecraft démarre pour {0}. Le chargement des mods prend une à deux minutes."), session.Name));
            _ = WatchGameExitAsync(proc, instance.Path);
        }
        catch (OperationCanceledException)
        {
            Screen = Screen.Home;
        }
        catch (Exception e)
        {
            Screen = Screen.Home;
            Log.Error("Lancement natif impossible", e);
            var why = e is UnauthorizedAccessException ? T("s.verify.denied", "accès refusé à un fichier de l'instance") : FirstLine(e.Message);
            if (await AskAsync(T("s.launch.failTitle", "Lancement impossible"),
                    string.Format(T("s.launch.failText", "{0}.\n\nTu peux ouvrir CurseForge et lancer le pack depuis Mes modpacks."), why),
                    T("s.launch.openCf", "Ouvrir CurseForge"), T("s.close", "Fermer")))
                OpenCurseForge();
        }
    }

    void ShowInstallProgress(InstallProgress p)
    {
        switch (p.Phase)
        {
            case "check":
                WorkCurrent = T("s.launch.check", "Vérification des fichiers du jeu…");
                if (p.Total > 0)
                {
                    WorkCount = p.Done.ToString();
                    WorkTotal = "/ " + p.Total;
                    WorkProgress = (double)p.Done / p.Total;
                }
                break;
            case "download":
                if (p.Current.Length > 0) WorkCurrent = "↓  " + p.Current.Replace('/', '\\');
                WorkCount = (p.Bytes / 1048576.0).ToString("0.0");
                WorkTotal = $"/ {p.BytesTotal / 1048576.0:0.0} Mo";
                WorkDetail = string.Format(T("s.launch.files", "{0} / {1} fichiers"), p.Done, p.Total);
                WorkProgress = p.BytesTotal > 0 ? Math.Min(1, (double)p.Bytes / p.BytesTotal) : 0;
                break;
            case "neoforge":
                WorkCurrent = string.Format(T("s.launch.neoforge", "Installation de NeoForge {0}…"), NativeConfig.NeoForgeVersion);
                WorkDetail = T("s.launch.neoforgeHint", "Une seule fois : environ 30 secondes.");
                WorkCount = "";
                WorkTotal = "";
                WorkProgress = 0;
                break;
        }
    }

    /// <summary>Fin du jeu : met l'accueil à jour ; un arrêt avec erreur dans les premières minutes est signalé.</summary>
    async Task WatchGameExitAsync(Process p, string instanceDir)
    {
        var started = DateTime.Now;
        try { await p.WaitForExitAsync(); }
        catch (InvalidOperationException) { return; }
        var code = p.ExitCode;
        Log.Info($"Minecraft fermé (code {code}, {(DateTime.Now - started).TotalMinutes:0.0} min)");
        if (_gameProcess == p) _gameProcess = null;
        GameMarker.Clear(instanceDir, p.Id);
        if (code != 0 && DateTime.Now - started < TimeSpan.FromMinutes(5))
            ToastWithAction(string.Format(T("s.launch.crashed", "Minecraft s'est fermé au démarrage (code {0}). Les logs du jeu disent pourquoi."), code),
                T("s.launch.openLogs", "Logs du jeu"), () => Explorer("\"" + Path.Combine(instanceDir, "logs") + "\""), error: true);
        await CheckGameAsync();
    }
}
