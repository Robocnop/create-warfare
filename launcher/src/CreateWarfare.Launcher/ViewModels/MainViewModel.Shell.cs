using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;
using CreateWarfare.Launcher.Themes;
using Microsoft.Win32;

namespace CreateWarfare.Launcher.ViewModels;

/// <summary>Commande avec paramètre (navigation, liens).</summary>
public sealed class ParamCommand : System.Windows.Input.ICommand
{
    readonly Action<object?> _run;
    public ParamCommand(Action<object?> run) => _run = run;
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _run(parameter);
}

/// <summary>Choix d'une liste déroulante. ToString renvoie le libellé : sans ça, la zone fermée d'une ComboBox
/// pouvait afficher « Option { Key = …, Label = … } ».</summary>
public sealed record Option(string Key, string Label)
{
    public override string ToString() => Label;
}
/// <summary>Vignette d'aperçu d'un thème (Paramètres → Apparence).</summary>
public sealed class ThemePreview : Observable
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    bool _active;
    /// <summary>Thème affiché en ce moment.</summary>
    public bool IsActive { get => _active; set => Set(ref _active, value); }
}
public sealed class SlideDot : Observable
{
    public int Index { get; init; }
    bool _current;
    public bool IsCurrent { get => _current; set => Set(ref _current, value); }
    public required System.Windows.Input.ICommand Go { get; init; }
}
public sealed record GalleryCard(ImageSource Image, string Caption, System.Windows.Input.ICommand Open);
public sealed record NewsLine(string Category, string CategoryLabel, string Title, string Text);
public sealed record NewsCard(string Kicker, string Title, string Date, string Tag, string Body, ImageSource? Image, string? Url,
                              IReadOnlyList<NewsLine> Lines, bool IsRelease);

/// <summary>
/// Coquille du launcher 1.2 : navigation, accueil (carrousel, serveur, actualités), galerie, paramètres, logs,
/// à propos, boîte de dialogue et états du bouton Jouer. La logique d'installation reste dans MainViewModel.cs.
/// </summary>
public sealed partial class MainViewModel
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    readonly DispatcherTimer _serverTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    LauncherContent _content = new();

    /// <summary>Texte de l'interface (Strings/*.xaml), avec le français en secours.</summary>
    static string T(string key, string fr) => Application.Current?.TryFindResource(key) as string ?? fr;

    void InitShell()
    {
        NavCommand = new ParamCommand(p => Navigate(p as string ?? "home"));
        GoBackCommand = new Command(GoBack);
        OpenLinkCommand = new ParamCommand(p => OpenUrl(p as string));
        ReportBugCommand = new Command(ReportBug);
        RefreshServerCommand = new Command(RefreshServerAsync);
        CopyAddressCommand = new Command(() => CopyText(ServerAddress, T("s.toast.addressCopied", "Adresse copiée : ") + ServerAddress));
        ToggleHomePhotosCommand = new Command(() => HomePhotos = !HomePhotos);
        NextSlideCommand = new Command(() => { if (Slides.Count > 1) SlideIndex = (SlideIndex + 1) % Slides.Count; });
        PrevSlideCommand = new Command(() => { if (Slides.Count > 1) SlideIndex = (SlideIndex - 1 + Slides.Count) % Slides.Count; });
        CloseLightboxCommand = new Command(() => IsLightboxOpen = false);
        LightboxNextCommand = new Command(() => { if (Slides.Count > 0) LightboxIndex = (LightboxIndex + 1) % Slides.Count; });
        LightboxPrevCommand = new Command(() => { if (Slides.Count > 0) LightboxIndex = (LightboxIndex - 1 + Slides.Count) % Slides.Count; });
        RefreshLogsCommand = new Command(LoadLogs);
        CopyLogsCommand = new Command(() => CopyText(LogTail(400), T("s.toast.logsCopied", "Logs copiés dans le presse-papiers.")));
        OpenGameLogsCommand = new Command(() => Explorer("\"" + Path.Combine(_instance!.Path, "logs") + "\""), () => _instance != null && Directory.Exists(Path.Combine(_instance.Path, "logs")));
        ExportLogsCommand = new Command(ExportLogs);
        ApplyRamCommand = new Command(ApplyRam, () => _instance != null);
        ResetRamCommand = new Command(() => RamValue = RecommendedRamMb);
        ClearCacheCommand = new Command(ClearCache);
        ResetSettingsCommand = new Command(ResetSettingsAsync);
        DialogYesCommand = new Command(() => CloseDialog(true));
        DialogNoCommand = new Command(() => CloseDialog(false));
        FinishOnboardingCommand = new Command(FinishOnboarding);
        DismissToastActionCommand = new Command(() => { var a = _toastAction; ToastText = null; a?.Invoke(); });
        SettingsTabCommand = new ParamCommand(p => SettingsTab = p as string ?? "game");
        PreviewThemeCommand = new ParamCommand(p => { if (p is string key) SetTheme(key); });
        ThemeManager.Changed += RaiseThemeProps; // y compris le changement automatique à minuit

        _serverTimer.Tick += async (_, _) => await RefreshServerAsync();
        _gameTimer.Tick += async (_, _) => await CheckGameAsync();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); if (!ToastIsError) ToastText = null; };
        _serverTimer.Start();
        _gameTimer.Start();

        TotalRamMb = MemoryAdvisor.TotalPhysicalMb();
        _ramValue = RecommendedRamMb;
        IsOnboarding = !_settings.OnboardingDone;
        _ = LoadContentAsync();
    }

    // ================================================================ Navigation

    public System.Windows.Input.ICommand NavCommand { get; private set; } = null!;
    public Command GoBackCommand { get; private set; } = null!;
    public bool IsNews => Screen == Screen.Changelog;
    public bool IsGallery => Screen == Screen.Gallery;
    public bool IsLogs => Screen == Screen.Logs;
    public bool IsAbout => Screen == Screen.About;
    /// <summary>Pages de contenu (barre latérale active) ; les autres sont des étapes (installation, revue, travail).</summary>
    public bool IsStep => Screen is Screen.Install or Screen.Review or Screen.Working;
    public bool NavEnabled => Screen != Screen.Working;

    void RaiseShell()
    {
        foreach (var n in new[] { nameof(IsNews), nameof(IsGallery), nameof(IsLogs), nameof(IsAbout), nameof(IsStep), nameof(NavEnabled),
                                  nameof(NavHome), nameof(NavNews), nameof(NavGallery), nameof(NavSettings), nameof(NavLogs), nameof(NavAbout) })
            Raise(n);
    }

    // Élément actif de la barre latérale (liaisons TwoWay sur des RadioButton)
    public bool NavHome { get => Screen is Screen.Home or Screen.Install or Screen.Review or Screen.Working; set { if (value) Navigate("home"); } }
    public bool NavNews { get => Screen == Screen.Changelog; set { if (value) Navigate("news"); } }
    public bool NavGallery { get => Screen == Screen.Gallery; set { if (value) Navigate("gallery"); } }
    public bool NavSettings { get => Screen == Screen.Settings; set { if (value) Navigate("settings"); } }
    public bool NavLogs { get => Screen == Screen.Logs; set { if (value) Navigate("logs"); } }
    public bool NavAbout { get => Screen == Screen.About; set { if (value) Navigate("about"); } }

    public void Navigate(string page)
    {
        if (Screen == Screen.Working) return; // une opération en cours ne s'interrompt pas en changeant de page
        if (Screen == Screen.Review) _plan = null;
        IsLightboxOpen = false;
        Screen = page switch
        {
            "news" => Screen.Changelog,
            "gallery" => Screen.Gallery,
            "settings" => Screen.Settings,
            "logs" => Screen.Logs,
            "about" => Screen.About,
            _ => _instance == null && _remote != null && page == "install" ? Screen.Install : Screen.Home,
        };
        if (Screen == Screen.Logs) LoadLogs();
        if (Screen == Screen.Settings) RefreshRamInfo();
    }

    /// <summary>Retour / Échap : ferme d'abord ce qui est ouvert par-dessus, puis revient à l'accueil.</summary>
    public void GoBack()
    {
        if (IsDialogOpen) { CloseDialog(false); return; }
        if (IsLightboxOpen) { IsLightboxOpen = false; return; }
        if (IsVersionMenuOpen) { IsVersionMenuOpen = false; return; }
        if (Screen == Screen.Working) return;
        if (Screen != Screen.Home) { _plan = null; Screen = Screen.Home; }
    }

    // ================================================================ Bouton Jouer : icône et états

    string _primaryIcon = "";
    public string PrimaryIcon { get => _primaryIcon; set => Set(ref _primaryIcon, value); }
    bool _isLaunching;
    public bool IsLaunching { get => _isLaunching; set => Set(ref _isLaunching, value); }
    bool _gameRunning;
    public bool GameRunning { get => _gameRunning; set { if (Set(ref _gameRunning, value)) Raise(nameof(PlayHint)); } }
    public string PlayHint => GameRunning ? T("s.play.running", "Le jeu est lancé.") : T("s.play.enterHint", "Entrée pour lancer");

    void SetPrimaryIcon(string resourceKey) => PrimaryIcon = Application.Current?.TryFindResource(resourceKey) as string ?? "";

    /// <summary>Appelé quand Jouer ouvre CurseForge : état « ouverture », puis détection du jeu.</summary>
    void OnPlayStarted()
    {
        IsLaunching = true;
        var until = DateTime.Now.AddSeconds(45);
        _launchDeadline = until;
    }
    DateTime _launchDeadline;

    async Task CheckGameAsync()
    {
        if (_instance == null) return;
        var dir = _instance.Path;
        var running = await Task.Run(() => ProcessGuard.GameRunning(dir));
        if (running && !GameRunning)
        {
            GameRunning = true;
            IsLaunching = false;
            Log.Info("Jeu détecté");
            if (Application.Current?.MainWindow is { } w)
            {
                if (_settings.OnGameLaunch == "close") Application.Current.Shutdown();
                else if (_settings.OnGameLaunch == "minimize") w.WindowState = WindowState.Minimized;
            }
            UpdateHome();
        }
        else if (!running && GameRunning)
        {
            GameRunning = false;
            UpdateHome();
        }
        if (IsLaunching && DateTime.Now > _launchDeadline) IsLaunching = false;
    }

    // ================================================================ Contenu : carrousel, galerie, actualités, liens

    public ObservableCollection<ImageSource> Slides { get; } = new();
    readonly List<string> _captions = new();
    public ObservableCollection<SlideDot> SlideDots { get; } = new();
    public ObservableCollection<GalleryCard> GalleryCards { get; } = new();
    public ObservableCollection<NewsCard> NewsCards { get; } = new();
    public bool HasSlides => Slides.Count > 0;
    public bool HasGallery => GalleryCards.Count > 0;
    public bool HasNews => NewsCards.Count > 0;

    int _slideIndex;
    public int SlideIndex
    {
        get => _slideIndex;
        set
        {
            if (!Set(ref _slideIndex, value)) return;
            foreach (var d in SlideDots) d.IsCurrent = d.Index == value;
            Raise(nameof(SlideCaption));
        }
    }
    public string SlideCaption => HomePhotos && _slideIndex < _captions.Count && _captions[_slideIndex].Length > 0
        ? _captions[_slideIndex] : T("s.home.tagline", "Industrie, artillerie, aviation et dissuasion nucléaire, sur un serveur PvP entre amis.");
    /// <summary>Images de fond de l'accueil : celles du serveur, ou aucune (illustration du thème) si le joueur l'a choisi.</summary>
    public System.Collections.IEnumerable HomeSlides => HomePhotos ? Slides : Array.Empty<ImageSource>();

    /// <summary>Flèches et points du carrousel (images affichées et au moins une image).</summary>
    public bool ShowSlideNav => HomePhotos && HasSlides;

    /// <summary>Choix du joueur, sauvegardé : images qui défilent ou illustration du thème.</summary>
    public bool HomePhotos
    {
        get => _settings.HomePhotos;
        set
        {
            if (value == _settings.HomePhotos) return;
            _settings.HomePhotos = value;
            SettingsStore.Save(_settings);
            foreach (var n in new[] { nameof(HomePhotos), nameof(HomeSlides), nameof(ShowSlideNav), nameof(SlideCaption) }) Raise(n);
        }
    }
    public Command ToggleHomePhotosCommand { get; private set; } = null!;

    bool _carouselPaused;
    public bool CarouselPaused { get => _carouselPaused; set => Set(ref _carouselPaused, value); }
    public Command NextSlideCommand { get; private set; } = null!;
    public Command PrevSlideCommand { get; private set; } = null!;

    bool _lightboxOpen;
    public bool IsLightboxOpen { get => _lightboxOpen; set { if (Set(ref _lightboxOpen, value)) RaiseLightbox(); } }
    int _lightboxIndex;
    public int LightboxIndex { get => _lightboxIndex; set { if (Set(ref _lightboxIndex, value)) RaiseLightbox(); } }
    public ImageSource? LightboxImage => _lightboxIndex < Slides.Count ? Slides[_lightboxIndex] : null;
    public string LightboxCaption => _lightboxIndex < _captions.Count ? _captions[_lightboxIndex] : "";
    public string LightboxCounter => Slides.Count == 0 ? "" : $"{_lightboxIndex + 1} / {Slides.Count}";
    void RaiseLightbox() { Raise(nameof(LightboxImage)); Raise(nameof(LightboxCaption)); Raise(nameof(LightboxCounter)); }
    public Command CloseLightboxCommand { get; private set; } = null!;
    public Command LightboxNextCommand { get; private set; } = null!;
    public Command LightboxPrevCommand { get; private set; } = null!;

    // Liens
    public string GithubUrl => _content.Links.Github;
    public string? DiscordUrl => string.IsNullOrWhiteSpace(_content.Links.Discord) ? null : _content.Links.Discord;
    public string? WebsiteUrl => string.IsNullOrWhiteSpace(_content.Links.Website) ? null : _content.Links.Website;
    public bool HasDiscord => DiscordUrl != null;
    public bool HasWebsite => WebsiteUrl != null;
    public System.Windows.Input.ICommand OpenLinkCommand { get; private set; } = null!;
    public Command ReportBugCommand { get; private set; } = null!;

    async Task LoadContentAsync()
    {
        var url = Environment.GetEnvironmentVariable("CW_LAUNCHER_CONTENT") ?? LauncherContent.DefaultUrl;
        _content = await LauncherContent.LoadAsync(url, Path.Combine(Log.AppDataDir, "content.json"), Http, CancellationToken.None);
        foreach (var n in new[] { nameof(GithubUrl), nameof(DiscordUrl), nameof(WebsiteUrl), nameof(HasDiscord), nameof(HasWebsite) }) Raise(n);
        if (_content.Server != null) { ServerName = _content.Server.Name; ServerAddress = _content.Server.Address; }
        await RefreshServerAsync();
        await LoadSlidesAsync();
        BuildNews();
    }

    /// <summary>
    /// Images intégrées (Assets/screenshots, triées par nom) puis celles de launcher-content.json (https, mises en cache).
    /// Une image illisible est ignorée ; décodage en arrière-plan à 1920 px de large au plus.
    /// </summary>
    async Task LoadSlidesAsync()
    {
        var captions = _content.Gallery.Where(g => !g.Image.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(g => Path.GetFileName(g.Image).ToLowerInvariant(), g => g.Caption);
        var items = new List<(Func<Stream?> open, string caption)>();
        foreach (var name in EmbeddedScreenshots())
            items.Add((() => Application.GetResourceStream(new Uri("pack://application:,,,/" + name))?.Stream,
                       captions.GetValueOrDefault(Path.GetFileName(name).ToLowerInvariant(), "")));
        foreach (var g in _content.Gallery.Where(g => g.Image.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
        {
            var url = g.Image;
            items.Add((() => RemoteImage(url), g.Caption));
        }

        foreach (var (open, caption) in items)
        {
            var img = await Task.Run(() =>
            {
                try
                {
                    using var s = open();
                    if (s == null) return null;
                    var ms = new MemoryStream();
                    s.CopyTo(ms);
                    ms.Position = 0;
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.DecodePixelWidth = 1920;
                    b.StreamSource = ms;
                    b.EndInit();
                    b.Freeze();
                    return (ImageSource)b;
                }
                catch (Exception e) when (e is IOException or NotSupportedException or InvalidOperationException or ArgumentException or HttpRequestException or TaskCanceledException
                                          or System.Runtime.InteropServices.COMException or FileFormatException or InvalidDataException)
                {
                    Log.Info($"Image ignorée : {e.Message}");
                    return null;
                }
            });
            if (img == null) continue;
            var index = Slides.Count;
            Slides.Add(img);
            _captions.Add(caption);
            SlideDots.Add(new SlideDot { Index = index, IsCurrent = index == SlideIndex, Go = new Command(() => SlideIndex = index) });
            GalleryCards.Add(new GalleryCard(img, caption, new Command(() => { LightboxIndex = index; IsLightboxOpen = true; })));
        }
        // une image au hasard à chaque ouverture, pour ne pas toujours voir la même en premier
        if (Slides.Count > 1) SlideIndex = Random.Shared.Next(Slides.Count);
        Raise(nameof(HasSlides));
        Raise(nameof(ShowSlideNav));
        Raise(nameof(HasGallery));
        Raise(nameof(SlideCaption));
    }

    /// <summary>Noms des images de Assets/screenshots intégrées à l'exe (ressources WPF « g »).</summary>
    static IEnumerable<string> EmbeddedScreenshots()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(asm.GetName().Name + ".g.resources");
        if (s == null) return Array.Empty<string>();
        using var r = new ResourceReader(s);
        var ext = new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };
        return r.Cast<DictionaryEntry>().Select(e => (string)e.Key)
            .Where(k => k.StartsWith("assets/screenshots/") && ext.Contains(Path.GetExtension(k)))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Image distante, mise en cache par son adresse (fonctionne ensuite hors ligne).</summary>
    static Stream? RemoteImage(string url)
    {
        var dir = Path.Combine(Log.AppDataDir, "images");
        var file = Path.Combine(dir, Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant());
        if (!File.Exists(file))
        {
            UrlRules.EnsureAllowed(url); // https seulement
            var bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
            Directory.CreateDirectory(dir);
            // écriture en deux temps : une coupure ne laisse pas une image tronquée en cache pour toujours
            File.WriteAllBytes(file + ".tmp", bytes);
            File.Move(file + ".tmp", file, overwrite: true);
        }
        return File.OpenRead(file);
    }

    /// <summary>Cartes Actualités : annonces de launcher-content.json, puis les versions du changelog.</summary>
    void BuildNews()
    {
        NewsCards.Clear();
        foreach (var n in _content.News)
        {
            ImageSource? img = null;
            if (n.Image != null && Slides.Count > 0)
            {
                var i = _content.Gallery.FindIndex(g => g.Image == n.Image);
                if (i >= 0 && i < Slides.Count) img = Slides[i];
            }
            NewsCards.Add(new NewsCard(T("s.news.announce", "ANNONCE"), n.Title, n.Date, n.Tag, n.Body, img, n.Url, Array.Empty<NewsLine>(), false));
        }
        foreach (var e in ChangelogParser.Parse(_remote?.Changelog))
        {
            var lines = e.Items.Select(i => new NewsLine(i.Category, CategoryLabel(i.Category), i.Title, i.Text)).ToList();
            NewsCards.Add(new NewsCard(T("s.news.version", "VERSION"), "v" + e.Version, FormatDate(e.Date), "", "", null, null, lines, true));
        }
        Raise(nameof(HasNews));
        var latest = NewsCards.FirstOrDefault(c => c.IsRelease) ?? NewsCards.FirstOrDefault();
        LatestTitle = latest?.Title ?? "";
        LatestDate = latest?.Date ?? "";
        LatestLines = latest == null ? "" : string.Join("\n", latest.Lines.Take(3).Select(l => "•  " + (l.Title.Length > 0 ? l.Title : Shorten(l.Text, 70))));
        Raise(nameof(HasLatest));
    }

    string _latestTitle = "", _latestDate = "", _latestLines = "";
    public string LatestTitle { get => _latestTitle; set => Set(ref _latestTitle, value); }
    public string LatestDate { get => _latestDate; set => Set(ref _latestDate, value); }
    public string LatestLines { get => _latestLines; set => Set(ref _latestLines, value); }
    public bool HasLatest => LatestTitle.Length > 0;

    static string CategoryLabel(string c) => c switch
    {
        ChangelogParser.Added => T("s.cat.added", "Ajouté"),
        ChangelogParser.Fixed => T("s.cat.fixed", "Corrigé"),
        ChangelogParser.Removed => T("s.cat.removed", "Retiré"),
        _ => T("s.cat.changed", "Modifié"),
    };
    static string FormatDate(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("d MMMM yyyy", System.Globalization.CultureInfo.CurrentUICulture) : iso;
    static string Shorten(string s, int n) => s.Length <= n ? s : s[..n].TrimEnd() + "…";

    // ================================================================ Serveur

    string _serverName = "Serveur Create Warfare";
    public string ServerName { get => _serverName; set => Set(ref _serverName, value); }
    string _serverAddress = $"{ServerPing.DefaultHost}:{ServerPing.DefaultPort}";
    public string ServerAddress { get => _serverAddress; set => Set(ref _serverAddress, value); }
    string _serverState = "checking";
    /// <summary>« checking », « online » ou « offline ».</summary>
    public string ServerState { get => _serverState; set => Set(ref _serverState, value); }
    string _serverLine = "";
    public string ServerLine { get => _serverLine; set => Set(ref _serverLine, value); }
    string _serverPing = "";
    public string ServerPingText { get => _serverPing; set => Set(ref _serverPing, value); }
    public Command RefreshServerCommand { get; private set; } = null!;
    public Command CopyAddressCommand { get; private set; } = null!;

    async Task RefreshServerAsync()
    {
        var host = _content.Server?.Host ?? ServerPing.DefaultHost;
        var port = _content.Server?.Port ?? ServerPing.DefaultPort;
        if (ServerState != "online") ServerState = "checking";
        var st = await ServerPing.QueryAsync(host, port, TimeSpan.FromSeconds(5));
        ServerState = st.Online ? "online" : "offline";
        ServerLine = st.Online
            ? string.Format(T("s.server.players", "{0} / {1} joueurs"), st.Players, st.MaxPlayers)
            : T("s.server.offline", "Hors ligne ou injoignable");
        ServerPingText = st.Online ? $"{st.PingMs} ms" : "";
        if (_settings.DebugLog) Log.Info($"Ping {host}:{port} : {(st.Online ? $"{st.Players}/{st.MaxPlayers}, {st.PingMs} ms" : st.Error)}");
    }

    // ================================================================ Paramètres

    string _settingsTab = "game";
    public string SettingsTab
    {
        get => _settingsTab;
        set { if (Set(ref _settingsTab, value)) foreach (var n in new[] { nameof(TabGame), nameof(TabAppearance), nameof(TabLauncher), nameof(TabInstance), nameof(TabAdvanced) }) Raise(n); }
    }
    public bool TabGame { get => SettingsTab == "game"; set { if (value) SettingsTab = "game"; } }
    public bool TabAppearance { get => SettingsTab == "appearance"; set { if (value) SettingsTab = "appearance"; } }
    public bool TabLauncher { get => SettingsTab == "launcher"; set { if (value) SettingsTab = "launcher"; } }
    public bool TabInstance { get => SettingsTab == "instance"; set { if (value) SettingsTab = "instance"; } }
    public bool TabAdvanced { get => SettingsTab == "advanced"; set { if (value) SettingsTab = "advanced"; } }
    public System.Windows.Input.ICommand SettingsTabCommand { get; private set; } = null!;

    // ---------------------------------------------------------------- Apparence

    const string ModeAuto = "auto", ModeBase = "base", ModePick = "pick";

    /// <summary>Mode du thème : automatique (saisonnier), thème de base seul, ou un thème précis.</summary>
    public IReadOnlyList<Option> ThemeModeOptions { get; } = new[]
    {
        new Option(ModeAuto, T("s.ap.mode.auto", "Automatique (saisonnier)")),
        new Option(ModeBase, T("s.ap.mode.base", "Thème de base uniquement")),
        new Option(ModePick, T("s.ap.mode.pick", "Choisir un thème")),
    };

    string CurrentMode => _settings.Theme switch
    {
        SeasonalTheme.Default => ModeBase,
        var t when SeasonalTheme.All.Contains(t) => ModePick,
        _ => ModeAuto,
    };

    public Option? SelectedThemeMode
    {
        get => ThemeModeOptions.First(o => o.Key == CurrentMode);
        set
        {
            if (value == null || value.Key == CurrentMode) return;
            SetTheme(value.Key switch
            {
                ModeBase => SeasonalTheme.Default,
                // thème précis : on part du thème affiché (ou d'Halloween si c'est le thème de base)
                ModePick => ThemeManager.Current != SeasonalTheme.Default ? ThemeManager.Current : SeasonalTheme.Halloween,
                _ => SeasonalTheme.Auto,
            });
        }
    }

    /// <summary>Liste des thèmes du mode « Choisir un thème ».</summary>
    public bool IsThemePick => CurrentMode == ModePick;

    /// <summary>Thèmes au choix (ordre de SeasonalTheme.All : base, Halloween, Pâques, Noël, Été).</summary>
    public IReadOnlyList<Option> ThemeOptions { get; } =
        ThemeRegistry.All.Select(t => new Option(t.Key, T(t.NameKey, t.NameFr))).ToArray();

    public Option? SelectedTheme
    {
        get => ThemeOptions.FirstOrDefault(o => o.Key == _settings.Theme);
        set { if (value != null && value.Key != _settings.Theme) SetTheme(value.Key); }
    }

    /// <summary>Vignettes animées de chaque thème ; un clic l'applique (mode « Choisir un thème »).</summary>
    public IReadOnlyList<ThemePreview> ThemePreviews { get; } =
        ThemeRegistry.All.Select(t => new ThemePreview { Key = t.Key, Label = T(t.NameKey, t.NameFr), IsActive = t.Key == ThemeManager.Current }).ToArray();
    public System.Windows.Input.ICommand PreviewThemeCommand { get; private set; } = null!;

    /// <summary>« Affiché en ce moment : Noël ».</summary>
    public string ActiveThemeText
    {
        get
        {
            var t = ThemeRegistry.Get(ThemeManager.Current);
            return T("s.ap.active", "Affiché en ce moment : ") + T(t.NameKey, t.NameFr);
        }
    }

    /// <summary>Animer le décor (engrenages, neige, pétales…). Windows peut aussi le couper (moins d'animations).</summary>
    public bool DecorAnimations
    {
        get => _settings.DecorAnimations;
        set
        {
            if (value == _settings.DecorAnimations) return;
            _settings.DecorAnimations = value;
            SettingsStore.Save(_settings);
            Motion.DecorAnimations = value;
            Raise();
        }
    }

    /// <summary>Mode performance : désactive les mods visuels lourds de l'instance (Core.Services.PerformanceMode).</summary>
    public bool PerformanceMode
    {
        get => _settings.PerformanceMode;
        set
        {
            if (value == _settings.PerformanceMode) return;
            if (_instance == null) { Toast(T("s.perf.noInstance", "Aucune instance trouvée."), error: true); Raise(); return; }
            if (ProcessGuard.GameRunning(_instance.Path))
            {
                Toast(T("s.perf.closeGame", "Ferme Minecraft d'abord : ses mods sont verrouillés tant qu'il tourne."), error: true);
                Raise();
                return;
            }
            _settings.PerformanceMode = value;
            if (!ApplyPerformanceMode()) _settings.PerformanceMode = !value;
            SettingsStore.Save(_settings);
            Raise();
            if (_settings.PerformanceMode == value)
                Toast(value ? T("s.perf.on", "Mode performance activé : mods visuels lourds désactivés.")
                            : T("s.perf.off", "Mode performance désactivé : mods visuels réactivés."));
        }
    }

    /// <summary>Remet l'instance en accord avec le réglage (après une mise à jour, avant de jouer). Faux si un jar n'a pas pu être renommé.</summary>
    bool ApplyPerformanceMode()
    {
        if (_instance == null) return true;
        if (!_settings.PerformanceMode && _settings.PerformanceDisabled.Count == 0) return true;
        var ours = new HashSet<string>(_settings.PerformanceDisabled, StringComparer.OrdinalIgnoreCase);
        try
        {
            var n = global::CreateWarfare.Launcher.Core.Services.PerformanceMode.Apply(_instance.Path, _settings.PerformanceMode, ours);
            if (n > 0) Log.Info($"Mode performance {(_settings.PerformanceMode ? "activé" : "désactivé")} : {n} jar(s) renommé(s)");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error("Mode performance non appliqué", e);
            Toast(T("s.perf.failed", "Impossible de renommer les mods : ") + e.Message, error: true);
            return false;
        }
        finally
        {
            _settings.PerformanceDisabled = ours.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            SettingsStore.Save(_settings);
        }
    }

    /// <summary>Enregistre le réglage de thème (« auto », « default » ou un thème) et l'applique tout de suite.</summary>
    void SetTheme(string key)
    {
        _settings.Theme = key;
        SettingsStore.Save(_settings);
        ThemeManager.Apply(key);
        RaiseThemeProps();
    }

    void RaiseThemeProps()
    {
        foreach (var n in new[] { nameof(SelectedThemeMode), nameof(IsThemePick), nameof(SelectedTheme), nameof(ActiveThemeText) }) Raise(n);
        foreach (var p in ThemePreviews) p.IsActive = p.Key == ThemeManager.Current;
    }

    public IReadOnlyList<Option> LanguageOptions { get; } = new[] { new Option("fr", "Français"), new Option("en", "English") };
    public Option? SelectedLanguage
    {
        get => LanguageOptions.FirstOrDefault(o => o.Key == _settings.Language) ?? LanguageOptions[0];
        set
        {
            if (value == null || value.Key == _settings.Language) return;
            _settings.Language = value.Key;
            SettingsStore.Save(_settings);
            Strings.Apply(value.Key);
            Raise();
            Toast(T("s.toast.language", "Langue changée. Certains textes changeront à la prochaine ouverture."));
        }
    }

    public IReadOnlyList<Option> GameLaunchOptions { get; } = new[]
    {
        new Option("minimize", T("s.launch.minimize", "Réduire le launcher")),
        new Option("close", T("s.launch.close", "Fermer le launcher")),
        new Option("stay", T("s.launch.stay", "Laisser le launcher ouvert")),
    };
    public Option? SelectedGameLaunch
    {
        get => GameLaunchOptions.FirstOrDefault(o => o.Key == _settings.OnGameLaunch) ?? GameLaunchOptions[0];
        set { if (value == null) return; _settings.OnGameLaunch = value.Key; SettingsStore.Save(_settings); Raise(); }
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "CreateWarfareLauncher";
    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) key.SetValue(RunName, "\"" + Environment.ProcessPath + "\"");
                else key.DeleteValue(RunName, throwOnMissingValue: false);
                _settings.StartWithWindows = value;
                SettingsStore.Save(_settings);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                Toast(T("s.toast.startupFailed", "Impossible de modifier le démarrage de Windows : ") + e.Message, error: true);
            }
            Raise();
        }
    }
    public bool AutoUpdateLauncher
    {
        get => _settings.AutoUpdateLauncher;
        set { _settings.AutoUpdateLauncher = value; SettingsStore.Save(_settings); Raise(); }
    }
    public bool DebugLog
    {
        get => _settings.DebugLog;
        set { _settings.DebugLog = value; SettingsStore.Save(_settings); Log.Info($"Journal détaillé : {value}"); Raise(); }
    }

    // RAM
    public long TotalRamMb { get; private set; }
    public int RecommendedRamMb => MemoryAdvisor.RecommendedMb(TotalRamMb);
    public int RamMin => 4096;
    public int RamMax => MemoryAdvisor.MaxMb(TotalRamMb);
    double _ramValue;
    public double RamValue
    {
        get => _ramValue;
        set { if (Set(ref _ramValue, Math.Round(value / 1024) * 1024)) Raise(nameof(RamLabel)); }
    }
    public string RamLabel => $"{RamValue / 1024:0} " + T("s.unit.gb", "Go");
    public string RamAdvice => string.Format(T("s.ram.advice", "Conseillé pour ce PC : {0} Go ({1} Go installés). Le pack demande au moins 6 Go."),
        RecommendedRamMb / 1024, Math.Round(TotalRamMb / 1024.0));
    string _instanceRam = "";
    public string InstanceRam { get => _instanceRam; set => Set(ref _instanceRam, value); }
    public Command ApplyRamCommand { get; private set; } = null!;
    public Command ResetRamCommand { get; private set; } = null!;

    void RefreshRamInfo()
    {
        var current = _instance == null ? null : MemoryAdvisor.ReadAllocated(_instance.Path);
        InstanceRam = current == null ? T("s.ram.unknown", "RAM actuelle de l'instance : inconnue")
            : string.Format(T("s.ram.current", "RAM actuelle de l'instance CurseForge : {0} Go"), current.Value / 1024);
        if (current != null) RamValue = current.Value;
    }

    void ApplyRam()
    {
        if (_instance == null) return;
        if (ProcessGuard.CurseForgeRunning())
        {
            Toast(T("s.ram.closeCf", "Ferme CurseForge d'abord : il réécrit ce réglage tant qu'il est ouvert."), error: true);
            return;
        }
        try
        {
            MemoryAdvisor.WriteAllocated(_instance.Path, (int)RamValue);
            Log.Info($"RAM de l'instance : {RamValue} Mo");
            RefreshRamInfo();
            Toast(string.Format(T("s.ram.applied", "RAM réglée sur {0} : elle s'appliquera au prochain lancement depuis CurseForge."), RamLabel));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            Log.Error("RAM non appliquée", e);
            Toast(T("s.ram.failed", "Impossible d'écrire la RAM : ") + e.Message, error: true);
        }
    }

    // Avancé
    public string CacheSize
    {
        get
        {
            long size = 0;
            foreach (var dir in new[] { _dl.CacheDir, Path.Combine(Log.AppDataDir, "images") })
                if (Directory.Exists(dir))
                    size += new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return Format.Size(size);
        }
    }
    public Command ClearCacheCommand { get; private set; } = null!;
    public Command ResetSettingsCommand { get; private set; } = null!;

    void ClearCache()
    {
        foreach (var dir in new[] { _dl.CacheDir, Path.Combine(Log.AppDataDir, "images") })
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Error($"Cache non vidé : {dir}", e); }
        Raise(nameof(CacheSize));
        Toast(T("s.toast.cacheCleared", "Cache vidé. Les fichiers seront téléchargés à nouveau au besoin."));
    }

    async Task ResetSettingsAsync()
    {
        if (!await AskAsync(T("s.reset.title", "Réinitialiser les paramètres ?"),
                T("s.reset.text", "Thème, langue, démarrage avec Windows et choix de version reviennent à leurs valeurs par défaut. L'instance et tes fichiers de jeu ne sont pas touchés."),
                T("s.reset.yes", "Réinitialiser"), danger: true)) return;
        var keep = _settings.InstancePath;
        StartWithWindows = false;
        // Le mode performance suit l'état des jars de l'instance : on le garde, sinon les mods désactivés ne reviendraient plus.
        var fresh = new LauncherSettings { InstancePath = keep, OnboardingDone = true,
            PerformanceMode = _settings.PerformanceMode, PerformanceDisabled = _settings.PerformanceDisabled };
        foreach (var p in typeof(LauncherSettings).GetProperties()) p.SetValue(_settings, p.GetValue(fresh));
        SettingsStore.Save(_settings);
        ThemeManager.Apply(_settings.Theme);
        Motion.DecorAnimations = _settings.DecorAnimations;
        Strings.Apply(_settings.Language);
        RaiseThemeProps();
        foreach (var n in new[] { nameof(DecorAnimations), nameof(HomePhotos), nameof(HomeSlides), nameof(ShowSlideNav), nameof(SlideCaption), nameof(SelectedLanguage), nameof(SelectedGameLaunch), nameof(AutoUpdateLauncher), nameof(DebugLog), nameof(DevMode) }) Raise(n);
        ApplyChoice();
        Toast(T("s.toast.reset", "Paramètres réinitialisés."));
    }

    // ================================================================ Logs

    string _logText = "";
    public string LogText { get => _logText; set => Set(ref _logText, value); }
    public Command RefreshLogsCommand { get; private set; } = null!;
    public Command CopyLogsCommand { get; private set; } = null!;
    public Command OpenGameLogsCommand { get; private set; } = null!;
    public Command ExportLogsCommand { get; private set; } = null!;

    void LoadLogs() => LogText = LogTail(600);

    static string LogTail(int lines)
    {
        try
        {
            var file = Directory.Exists(Log.LogDir)
                ? new DirectoryInfo(Log.LogDir).GetFiles("launcher-*.log").OrderByDescending(f => f.LastWriteTime).FirstOrDefault() : null;
            if (file == null) return "";
            using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var all = new StreamReader(fs).ReadToEnd().Split('\n');
            return string.Join("\n", all.Skip(Math.Max(0, all.Length - lines))).TrimEnd();
        }
        catch (IOException e) { return e.Message; }
    }

    void ExportLogs()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var zip = Path.Combine(desktop, $"CreateWarfare-logs-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        try
        {
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                if (Directory.Exists(Log.LogDir))
                    foreach (var f in new DirectoryInfo(Log.LogDir).GetFiles().OrderByDescending(f => f.LastWriteTime).Take(5))
                        AddShared(archive, f.FullName, "launcher/" + f.Name);
                if (_instance != null)
                {
                    var game = Path.Combine(_instance.Path, "logs", "latest.log");
                    if (File.Exists(game)) AddShared(archive, game, "jeu/latest.log");
                    var crashes = Path.Combine(_instance.Path, "crash-reports");
                    if (Directory.Exists(crashes))
                        foreach (var f in new DirectoryInfo(crashes).GetFiles().OrderByDescending(f => f.LastWriteTime).Take(3))
                            AddShared(archive, f.FullName, "jeu/crash-reports/" + f.Name);
                }
            }
            Explorer("/select,\"" + zip + "\"");
            Toast(T("s.toast.logsExported", "Logs exportés sur le Bureau : ") + Path.GetFileName(zip));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Toast(T("s.toast.exportFailed", "Export impossible : ") + e.Message, error: true);
        }
    }

    static void AddShared(ZipArchive archive, string path, string name)
    {
        using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var dst = archive.CreateEntry(name).Open();
        src.CopyTo(dst);
    }

    /// <summary>Ouvre une issue GitHub préremplie (versions, système, fin du log) ; le log complet est aussi copié.</summary>
    void ReportBug()
    {
        var tail = LogTail(40);
        var body = $"**Que s'est-il passé ?**\n\n\n**Comment le reproduire ?**\n\n\n---\n" +
                   $"- Launcher : {LauncherVersion}\n- Pack : {PackVersion}\n- Windows : {Environment.OSVersion.VersionString}\n" +
                   $"- Thème : {ThemeManager.Current}\n\n<details><summary>Fin du log du launcher</summary>\n\n```\n{tail}\n```\n</details>";
        if (body.Length > 6000) body = body[..6000] + "\n```\n</details>";
        CopyText(LogTail(400), null);
        OpenUrl($"{GithubUrl}/issues/new?title={Uri.EscapeDataString("[Bug] ")}&body={Uri.EscapeDataString(body)}");
        Toast(T("s.toast.bugOpened", "Le formulaire GitHub s'ouvre, prérempli. Les logs complets sont aussi dans le presse-papiers."));
    }

    // ================================================================ À propos

    public string MinecraftVersion => _remote?.Minecraft is { Length: > 0 } m ? m : "1.21.1";
    public string NeoForgeVersion => _remote?.Neoforge is { Length: > 0 } n ? n : "21.1.264";

    // ================================================================ Boîte de dialogue (remplace MessageBox)

    TaskCompletionSource<bool>? _dialog;
    bool _dialogOpen;
    public bool IsDialogOpen { get => _dialogOpen; set => Set(ref _dialogOpen, value); }
    string _dialogTitle = "", _dialogText = "", _dialogYes = "", _dialogNo = "";
    public string DialogTitle { get => _dialogTitle; set => Set(ref _dialogTitle, value); }
    public string DialogText { get => _dialogText; set => Set(ref _dialogText, value); }
    public string DialogYes { get => _dialogYes; set => Set(ref _dialogYes, value); }
    public string DialogNo { get => _dialogNo; set { Set(ref _dialogNo, value); Raise(nameof(HasDialogNo)); } }
    public bool HasDialogNo => DialogNo.Length > 0;
    bool _dialogDanger;
    public bool DialogIsDanger { get => _dialogDanger; set => Set(ref _dialogDanger, value); }
    public Command DialogYesCommand { get; private set; } = null!;
    public Command DialogNoCommand { get; private set; } = null!;

    /// <summary>Question avec deux choix (no vide : simple information). Échap = non.</summary>
    public Task<bool> AskAsync(string title, string text, string yes, string? no = null, bool danger = false)
    {
        _dialog?.TrySetResult(false);
        _dialog = new TaskCompletionSource<bool>();
        DialogTitle = title;
        DialogText = text;
        DialogYes = yes;
        DialogNo = no ?? T("s.cancel", "Annuler");
        DialogIsDanger = danger;
        IsDialogOpen = true;
        return _dialog.Task;
    }

    /// <summary>Message d'erreur lisible avec une action (Réessayer, Copier les logs…).</summary>
    public async void ShowError(string title, string text)
    {
        if (await AskAsync(title, text, T("s.copyLogs", "Copier les logs"), T("s.close", "Fermer")))
            CopyText(LogTail(400), T("s.toast.logsCopied", "Logs copiés dans le presse-papiers."));
    }

    void CloseDialog(bool result)
    {
        IsDialogOpen = false;
        _dialog?.TrySetResult(result);
        _dialog = null;
    }

    // ================================================================ Premier lancement

    bool _onboarding;
    public bool IsOnboarding { get => _onboarding; set => Set(ref _onboarding, value); }
    public Command FinishOnboardingCommand { get; private set; } = null!;
    void FinishOnboarding()
    {
        IsOnboarding = false;
        _settings.OnboardingDone = true;
        SettingsStore.Save(_settings);
        if (_instance == null && _remote != null) Screen = Screen.Install;
    }

    // ================================================================ Toast avec action

    Action? _toastAction;
    string? _toastActionText;
    public string? ToastActionText { get => _toastActionText; set { Set(ref _toastActionText, value); Raise(nameof(HasToastAction)); } }
    public bool HasToastAction => ToastActionText != null;
    public Command DismissToastActionCommand { get; private set; } = null!;

    void ToastWithAction(string text, string action, Action run, bool error = false)
    {
        Toast(text, error);
        _toastAction = run;
        ToastActionText = action;
    }

    /// <summary>Appelé à chaque toast : les messages ordinaires disparaissent seuls, pas les erreurs.</summary>
    void OnToastShown()
    {
        _toastAction = null;
        ToastActionText = null;
        _toastTimer.Stop();
        if (!ToastIsError) _toastTimer.Start();
    }

    // ================================================================ Outils

    void CopyText(string text, string? toast)
    {
        try { Clipboard.SetText(text); if (toast != null) Toast(toast); }
        catch (System.Runtime.InteropServices.COMException) { Toast(T("s.toast.clipboard", "Presse-papiers occupé, réessaie."), error: true); }
    }

    void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { Toast(e.Message, error: true); }
    }
}
