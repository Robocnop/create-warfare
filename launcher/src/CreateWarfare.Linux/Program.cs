using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Linux;

/// <summary>
/// Create Warfare pour Linux : crée l'instance « Create Warfare » dans Prism Launcher, y installe le pack, puis reste
/// dans l'instance comme commande de pré-lancement pour la mettre à jour avant chaque partie.
/// Même logique que le launcher Windows (CreateWarfare.Launcher.Core) : version.json, hash vérifiés, fichiers du joueur
/// protégés, sauvegarde et retour arrière. Rien n'est installé sur le système, tout reste dans le dossier du joueur.
/// </summary>
static class Program
{
    const string Help = """
        Create Warfare pour Linux (Prism Launcher)

        Utilisation : create-warfare [commande] [options]

        Commandes
          install      (par défaut) crée l'instance dans Prism Launcher, ou la met à jour si elle existe
          repair       remet tous les fichiers du pack comme à l'origine (tes mondes, touches et réglages restent)
          status       montre ce qui est installé, sans rien modifier
          prelaunch    mise à jour avant le jeu ; c'est Prism qui lance cette commande, pas toi

        Options
          --launch             lance le jeu dans Prism une fois l'instance prête
          --repair             comme la commande repair
          --prism-dir DOSSIER  dossier de données de Prism (installation portable ou inhabituelle)
          --memory MO          mémoire donnée au jeu, en Mo (ex. 8192) ; sinon réglée d'après la RAM du PC, une seule fois
          --manifest ADRESSE   autre version.json (essais)
          --version, --help

        Exemples
          ./create-warfare-linux-x86_64
          ./create-warfare-linux-x86_64 --launch
          ./create-warfare-linux-x86_64 repair
        """;

    static bool _headless; // lancé d'un double-clic sans terminal : le résultat est aussi montré dans une boîte de dialogue

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Options o;
        try { o = Options.Parse(args); }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Aide : create-warfare --help");
            return 64;
        }
        if (o.Help) { Console.WriteLine(Help); return 0; }
        if (o.ShowVersion) { Console.WriteLine(SelfUpdater.Display(SelfUpdater.CurrentVersion)); return 0; }

        if (args.Length == 0 && Desktop.NoTerminal())
        {
            if (Desktop.ReopenInTerminal()) return 0;
            _headless = true;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        int code;
        try
        {
            Log.Info($"create-warfare {SelfUpdater.Display(SelfUpdater.CurrentVersion)} : {o.Command}");
            Log.Prune();
            code = o.Command switch
            {
                "prelaunch" => await PrelaunchAsync(o, args, cts.Token),
                "status" => Status(o),
                _ => await InstallAsync(o, cts.Token),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("\nAnnulé. Rien n'a été modifié dans l'instance.");
            code = 130;
        }
        catch (Exception e)
        {
            Log.Error("Échec", e);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Erreur : " + Explain(e));
            Console.Error.WriteLine("Détail dans " + Log.LogDir);
            if (_headless) Desktop.Notify("Create Warfare : échec\n" + Explain(e));
            code = 1;
        }
        if (o.Pause)
        {
            Console.WriteLine();
            Console.Write("Appuie sur Entrée pour fermer cette fenêtre.");
            Console.ReadLine();
        }
        return code;
    }

    static string Explain(Exception e) => e switch
    {
        UnauthorizedAccessException => "accès refusé : " + e.Message + " (vérifie que le dossier de Prism t'appartient)",
        RollbackIncompleteException r => r.Message.Split('\n')[0] + $". {r.NotRestored.Count} fichier(s) n'ont pas pu être remis : relance avec « repair ». Copie de sécurité : {r.BackupDir}",
        _ => e.Message.Split('\n')[0],
    };

    // ------------------------------------------------------------------ install / repair

    static async Task<int> InstallAsync(Options o, CancellationToken ct)
    {
        var repair = o.Repair || o.Command == "repair";
        var prism = FindPrism(o, out var all);
        if (prism == null) return 2;

        Say($"Prism Launcher ({KindLabel(prism.Kind)}) : {prism.DataDir}");
        if (all.Count > 1)
            Say("  Autres installations trouvées : " + string.Join(", ", all.Where(p => p != prism).Select(p => p.DataDir)) +
                "\n  Pour en choisir une autre : --prism-dir DOSSIER");

        var dl = NewDownloader();
        VersionInfo v;
        try { v = await dl.FetchManifestAsync(ManifestUrl(o), ct); }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException)
        {
            throw new IOException(e is InvalidDataException
                ? "la liste des mises à jour est illisible (" + e.Message + "). Ce n'est pas ta connexion : réessaie dans quelques minutes"
                : "serveur de mises à jour injoignable (" + e.Message + "). Vérifie ta connexion, puis relance", e);
        }

        var instanceDir = Path.Combine(prism.InstancesDir, PrismInstance.FolderName);
        var created = PrismInstance.Ensure(instanceDir, v.Minecraft, v.Neoforge, o.MemoryMb, Machine.RecommendedMemoryMb(), resetCommand: repair);
        Say(created
            ? $"Instance créée : {instanceDir}\n  Minecraft {v.Minecraft}, NeoForge {v.Neoforge}, {IniLines.Get(Path.Combine(instanceDir, "instance.cfg"), "MaxMemAlloc")} Mo de mémoire"
            : $"Instance trouvée : {instanceDir}");
        if (!PrismInstance.HasOurCommand(instanceDir))
            Say("  Attention : la commande de pré-lancement de l'instance n'est pas celle de Create Warfare (tu l'as changée dans Prism).\n" +
                "  Les mises à jour automatiques sont donc coupées. Pour la remettre : create-warfare repair");
        if (!created && Machine.PrismRunning())
            Say("  Prism est ouvert : si un réglage de l'instance ne change pas, ferme Prism puis relance cette commande.");
        InstallSelf(instanceDir);

        var gameDir = PrismInstance.GameDir(instanceDir);
        await UpdateAsync(dl, gameDir, v, repair, ct);

        Say("");
        Say($"Create Warfare v{v.Version} est prêt.");
        Say(prism.Kind == "flatpak"
            ? "Pour jouer : ouvre Prism Launcher et lance « Create Warfare », ou tape\n  flatpak run " + PrismLocator.FlatpakId + " --launch " + PrismInstance.FolderName
            : "Pour jouer : ouvre Prism Launcher et lance « Create Warfare ».");
        if (created)
            Say("Première fois : connecte ton compte Microsoft dans Prism (en haut à droite). Prism télécharge Java 21 tout seul\n" +
                "au premier lancement ; s'il le propose, accepte le téléchargement automatique de Java.");
        Say("Le pack se met ensuite à jour tout seul à chaque lancement.");
        if (_headless) Desktop.Notify($"Create Warfare v{v.Version} est prêt.\nOuvre Prism Launcher et lance « Create Warfare ».");

        return o.Launch ? LaunchPrism(prism) : 0;
    }

    static PrismInstall? FindPrism(Options o, out List<PrismInstall> all)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        all = PrismLocator.Find(home, Environment.GetEnvironmentVariable("XDG_DATA_HOME"), o.PrismDir);
        if (all.Count == 0 && o.PrismDir == null && Machine.FlatpakPrismInstalled(home))
        {
            // Flatpak installé mais jamais lancé : son dossier de données n'existe pas encore, on le prépare
            var dir = Path.Combine(home, ".var", "app", PrismLocator.FlatpakId, "data", "PrismLauncher");
            Directory.CreateDirectory(dir);
            all.Add(new PrismInstall(dir, "flatpak"));
            Say("Prism Launcher (Flatpak) est installé mais n'a jamais été ouvert : je prépare son dossier.");
        }
        var prism = PrismLocator.Choose(all);
        if (prism != null) return prism;

        if (o.PrismDir != null)
        {
            Console.Error.WriteLine($"Dossier introuvable : {o.PrismDir}");
            Console.Error.WriteLine("Donne le dossier de données de Prism : celui qui contient « instances » et « prismlauncher.cfg ».");
            return null;
        }
        Console.Error.WriteLine($"""
            Prism Launcher est introuvable sur ce compte.

            Installe-le, ouvre-le une fois pour connecter ton compte Microsoft, puis relance cette commande.

              Recommandé (toutes les distributions, Steam Deck compris) :
                  flatpak install flathub {PrismLocator.FlatpakId}

              Autres méthodes (paquet de la distribution, AppImage) : https://prismlauncher.org/download/

            Prism est installé ailleurs (version portable) ? Indique son dossier :
                  create-warfare --prism-dir /chemin/vers/PrismLauncher

            Dossiers vérifiés :
              {Path.Combine(home, ".var", "app", PrismLocator.FlatpakId, "data", "PrismLauncher")}
              {Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".local", "share"), "PrismLauncher")}
            """);
        if (_headless) Desktop.Notify("Create Warfare : Prism Launcher est introuvable.\nInstalle-le (Flathub : org.prismlauncher.PrismLauncher), ouvre-le une fois, puis relance.");
        return null;
    }

    static string KindLabel(string kind) => kind switch { "flatpak" => "Flatpak", "native" => "natif ou AppImage", _ => "dossier indiqué" };

    /// <summary>Copie ce binaire dans l'instance : Prism le lance avant chaque partie (commande de pré-lancement).</summary>
    static void InstallSelf(string instanceDir)
    {
        var self = Environment.ProcessPath;
        var dest = Path.Combine(instanceDir, PrismInstance.UpdaterName);
        if (self == null || Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            Say("  (lancé par « dotnet » : le binaire n'est pas copié dans l'instance)");
            return;
        }
        if (Path.GetFullPath(self) == Path.GetFullPath(dest)) return;
        File.Copy(self, dest + ".new", overwrite: true);
        Machine.MakeExecutable(dest + ".new");
        File.Move(dest + ".new", dest, overwrite: true);
    }

    // ------------------------------------------------------------------ mise à jour des fichiers

    static Downloader NewDownloader()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".cache");
        // pas /tmp : c'est souvent de la mémoire vive, et le pack pèse 700 Mo
        return new Downloader(Path.Combine(cache, "create-warfare"));
    }

    static string ManifestUrl(Options o) =>
        o.Manifest ?? (Environment.GetEnvironmentVariable("CW_LAUNCHER_MANIFEST") is { Length: > 0 } env ? env : SettingsStore.DefaultManifestUrl);

    /// <summary>Renvoie vrai si des fichiers ont changé.</summary>
    static async Task<bool> UpdateAsync(Downloader dl, string gameDir, VersionInfo v, bool repair, CancellationToken ct)
    {
        Directory.CreateDirectory(gameDir);
        var meter = new Meter();
        var overrides = await dl.GetOverridesAsync(v, new Direct<long>(n => meter.AddBytes("Fichiers du pack", n, v.Zip.Size)), ct);
        meter.Finish();

        var state = UpdatePlanner.ReadState(gameDir);
        var plan = UpdatePlanner.Plan(gameDir, v, overrides, state, null, repair);
        if (plan.IsEmpty && state?.Version == v.Version)
        {
            Say($"Le pack est à jour (v{v.Version}).");
            return false;
        }

        var parts = new List<string>();
        void Count(FileActionKind k, string label) { if (plan.Count(k) > 0) parts.Add($"{plan.Count(k)} {label}"); }
        Count(FileActionKind.Add, "à ajouter");
        Count(FileActionKind.Replace, "à remplacer");
        Count(FileActionKind.Remove, "à supprimer");
        Count(FileActionKind.Kept, "modifiés par toi, gardés");
        Count(FileActionKind.Protected, "protégés");
        Say((string.IsNullOrEmpty(plan.FromVersion) ? $"Installation de v{plan.ToVersion}" : $"Mise à jour v{plan.FromVersion} → v{plan.ToVersion}")
            + (parts.Count > 0 ? " : " + string.Join(", ", parts) : "")
            + (plan.DownloadSize > 0 ? $" ({DiskSpace.Format(plan.DownloadSize)} à télécharger)" : ""));

        var progress = new Direct<ApplyProgress>(p =>
        {
            if (p.Phase == "download") meter.Set("Téléchargement des mods", p.Bytes, p.BytesTotal, p.Done, p.Total);
            else meter.Set("Écriture des fichiers", 0, 0, p.Done, p.Total);
        });
        try { await new UpdateApplier(dl).ApplyAsync(gameDir, plan, progress, ct); }
        finally { meter.Finish(); }
        dl.Prune(v);
        foreach (var s in plan.ServersToAdd) Say($"Serveur ajouté à ta liste multijoueur : {s.Name} ({s.Ip})");
        return true;
    }

    // ------------------------------------------------------------------ prelaunch (lancé par Prism)

    /// <summary>
    /// Avant chaque partie. Règle : ne jamais empêcher de jouer une instance déjà installée parce que le réseau est
    /// coupé ou qu'une mise à jour échoue (elle est alors annulée proprement). On ne bloque que si rien n'est installé,
    /// ou si l'instance est dans un état douteux.
    /// </summary>
    static async Task<int> PrelaunchAsync(Options o, string[] args, CancellationToken ct)
    {
        var self = Environment.ProcessPath;
        var instanceDir = Environment.GetEnvironmentVariable("INST_DIR") is { Length: > 0 } d ? d
            : self != null ? Path.GetDirectoryName(self)! : throw new InvalidOperationException("dossier de l'instance inconnu");
        var gameDir = Environment.GetEnvironmentVariable("INST_MC_DIR") is { Length: > 0 } g ? g : PrismInstance.GameDir(instanceDir);
        var installed = UpdatePlanner.ReadState(gameDir)?.Version;

        var dl = NewDownloader();
        VersionInfo v;
        try
        {
            dl.IdleTimeout = TimeSpan.FromSeconds(10); // hors ligne, ne pas faire attendre le joueur
            v = await dl.FetchManifestAsync(ManifestUrl(o), ct);
            dl.IdleTimeout = TimeSpan.FromSeconds(30);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException)
        {
            Log.Error("version.json indisponible", e);
            if (installed != null)
            {
                Say($"Create Warfare : mises à jour injoignables ({e.Message.Split('\n')[0]}). Lancement de la v{installed} déjà installée.");
                return 0;
            }
            Console.Error.WriteLine("Create Warfare : le pack n'est pas encore installé et le serveur de mises à jour est injoignable. Vérifie ta connexion, puis relance.");
            return 1;
        }

        // 1. L'outil lui-même (prend effet au lancement suivant, ou tout de suite si le pack l'exige)
        var selfUpdated = self != null && await SelfUpdateAsync(dl, v, self, ct);
        if (SelfUpdater.PackRequiresUpdate(v))
        {
            if (selfUpdated && Environment.GetEnvironmentVariable("CW_REEXEC") != "1")
            {
                var psi = new ProcessStartInfo(self!) { UseShellExecute = false };
                foreach (var a in args) psi.ArgumentList.Add(a);
                psi.Environment["CW_REEXEC"] = "1";
                using var p = Process.Start(psi)!;
                await p.WaitForExitAsync(ct);
                return p.ExitCode;
            }
            Say($"Create Warfare : le pack v{v.Version} demande un outil plus récent (v{v.MinLauncherVersion}), pas encore disponible pour Linux.");
            if (installed != null) { Say($"Lancement de la v{installed} déjà installée."); return 0; }
            return 1;
        }

        // 2. Versions de Minecraft et de NeoForge : Prism les a déjà lues pour ce lancement
        var loaderChanged = PrismInstance.SyncComponents(instanceDir, v.Minecraft, v.Neoforge);

        // 3. Les fichiers
        try { await UpdateAsync(dl, gameDir, v, repair: false, ct); }
        catch (RollbackIncompleteException) { throw; }
        catch (Exception e) when (e is IOException or HttpRequestException or UnauthorizedAccessException)
        {
            Log.Error("Mise à jour avant lancement échouée", e);
            if (installed == null) throw;
            Say($"Create Warfare : mise à jour impossible ({Explain(e)}). Rien n'a été changé : lancement de la v{installed}.");
            return 0;
        }

        if (loaderChanged)
        {
            Console.Error.WriteLine($"Create Warfare : le pack passe à NeoForge {v.Neoforge}. Prism doit recharger l'instance : relance-la une fois.");
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// Nouvelle version de l'outil : même release GitHub que le launcher Windows (« launcher-v… »), fichier
    /// create-warfare-linux-&lt;arch&gt; et son .sha256, posés par l'intégration continue. Absent ou illisible : on ne fait rien.
    /// </summary>
    static async Task<bool> SelfUpdateAsync(Downloader dl, VersionInfo v, string self, CancellationToken ct)
    {
        try
        {
            if (v.Launcher is not { Version.Length: > 0, Url.Length: > 0 } l || SelfUpdater.ParseVersion(l.Version) <= SelfUpdater.CurrentVersion) return false;
            if (Machine.Arch is not { } arch) return false;
            var slash = l.Url.Replace('\\', '/').LastIndexOf('/');
            if (slash < 0) return false;
            var asset = l.Url[..(slash + 1)] + Machine.AssetName(arch);
            var shaText = UrlRules.IsRemote(asset) ? await dl.GetStringAsync(asset + ".sha256", ct) : await File.ReadAllTextAsync(asset + ".sha256", ct);
            var sha = shaText.Trim().Split(' ', '\t', '\n')[0];
            if (sha.Length != 64) return false;

            var next = self + ".new";
            await dl.DownloadVerifiedAsync(new[] { asset }, next, sha, sha1: false, null, ct);
            Machine.MakeExecutable(next);
            File.Move(next, self, overwrite: true); // remplacer un binaire en cours d'exécution est permis sous Linux
            Say($"Create Warfare : outil de mise à jour passé en v{l.Version}.");
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Info($"Pas de mise à jour de l'outil : {e.Message.Split('\n')[0]}");
            return false;
        }
    }

    // ------------------------------------------------------------------ status, launch

    static int Status(Options o)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var all = PrismLocator.Find(home, Environment.GetEnvironmentVariable("XDG_DATA_HOME"), o.PrismDir);
        Say($"create-warfare {SelfUpdater.Display(SelfUpdater.CurrentVersion)} ({Machine.Arch ?? RuntimeInformation.ProcessArchitecture.ToString()})");
        if (all.Count == 0)
        {
            Say("Prism Launcher : introuvable" + (Machine.FlatpakPrismInstalled(home) ? " (Flatpak installé, jamais ouvert)" : ""));
            return 2;
        }
        foreach (var p in all) Say($"Prism Launcher ({KindLabel(p.Kind)}) : {p.DataDir}");
        var prism = PrismLocator.Choose(all)!;
        var instanceDir = Path.Combine(prism.InstancesDir, PrismInstance.FolderName);
        if (!File.Exists(Path.Combine(instanceDir, "instance.cfg")))
        {
            Say($"Instance : absente ({instanceDir})");
            return 3;
        }
        var (mc, nf) = PrismInstance.ReadComponents(instanceDir);
        var state = UpdatePlanner.ReadState(PrismInstance.GameDir(instanceDir));
        Say($"Instance : {instanceDir}");
        Say($"  Minecraft {mc ?? "?"}, NeoForge {nf ?? "?"}, mémoire {IniLines.Get(Path.Combine(instanceDir, "instance.cfg"), "MaxMemAlloc") ?? "?"} Mo");
        Say($"  Pack installé : {(state == null ? "non" : "v" + state.Version + $" ({state.Mods.Count} mods)")}");
        Say($"  Mise à jour avant chaque partie : {(PrismInstance.HasOurCommand(instanceDir) && File.Exists(Path.Combine(instanceDir, PrismInstance.UpdaterName)) ? "active" : "coupée (relance « create-warfare repair »)")}");
        return 0;
    }

    static int LaunchPrism(PrismInstall prism)
    {
        ProcessStartInfo? psi = null;
        void Use(string exe, params string[] a)
        {
            psi = new ProcessStartInfo(exe) { UseShellExecute = false };
            foreach (var x in a) psi.ArgumentList.Add(x);
        }
        if (prism.Kind == "flatpak" && Machine.Which("flatpak") is { } flatpak)
            Use(flatpak, "run", PrismLocator.FlatpakId, "--launch", PrismInstance.FolderName);
        else if (prism.Kind == "custom" && new[] { "PrismLauncher", "prismlauncher" }.Select(n => Path.Combine(prism.DataDir, n)).FirstOrDefault(File.Exists) is { } portable)
            Use(portable, "--launch", PrismInstance.FolderName);
        else if (Machine.Which("prismlauncher") is { } native)
        {
            if (prism.Kind == "custom") Use(native, "--dir", prism.DataDir, "--launch", PrismInstance.FolderName);
            else Use(native, "--launch", PrismInstance.FolderName);
        }
        if (psi == null)
        {
            Say("Je ne trouve pas la commande de Prism (AppImage ?). Ouvre Prism Launcher et lance « Create Warfare ».");
            return 0;
        }
        Say("Lancement : " + psi.FileName + " " + string.Join(' ', psi.ArgumentList));
        Process.Start(psi); // Prism garde la main ; on ne capture pas sa sortie (un tube fermé à notre sortie le tuerait)
        return 0;
    }

    static void Say(string text) => Console.WriteLine(text);
}

sealed class Direct<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

sealed class Options
{
    public string Command = "install";
    public bool Launch, Repair, Help, ShowVersion, Pause;
    public string? PrismDir, Manifest;
    public int? MemoryMb;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Il manque une valeur après {args[i]}");
            switch (args[i])
            {
                case "install" or "repair" or "status" or "prelaunch": o.Command = args[i]; break;
                case "update": o.Command = "install"; break;
                case "--launch": o.Launch = true; break;
                case "--repair" or "--reinstall": o.Repair = true; break;
                case "--pause": o.Pause = true; break;
                case "--prism-dir": o.PrismDir = Value(); break;
                case "--manifest": o.Manifest = Value(); break;
                case "--memory":
                    var raw = Value();
                    if (!int.TryParse(raw, out var mb) || mb < 2048 || mb > 131072)
                        throw new ArgumentException($"--memory attend un nombre de Mo entre 2048 et 131072 (reçu : {raw})");
                    o.MemoryMb = mb;
                    break;
                case "-h" or "--help" or "help": o.Help = true; break;
                case "-V" or "--version": o.ShowVersion = true; break;
                default: throw new ArgumentException($"Option inconnue : {args[i]}");
            }
        }
        return o;
    }
}

/// <summary>Progression : une ligne réécrite dans un terminal, une ligne toutes les 3 s sinon (journal de Prism, fichier).</summary>
sealed class Meter
{
    readonly bool _tty = !Console.IsOutputRedirected;
    readonly Stopwatch _clock = new();
    readonly object _gate = new();
    string _label = "";
    long _bytes, _firstBytes = -1, _lastPrint = -10_000;
    bool _dirty;

    public void AddBytes(string label, long delta, long total) { lock (_gate) Set(label, (_label == label ? _bytes : 0) + delta, total, 0, 0); }

    public void Set(string label, long bytes, long bytesTotal, int done, int total)
    {
        lock (_gate)
        {
            if (label != _label)
            {
                FinishLine();
                _label = label;
                _firstBytes = -1;
                _clock.Restart();
                _lastPrint = -10_000;
            }
            _bytes = bytes;
            if (_firstBytes < 0) _firstBytes = bytes;
            var now = _clock.ElapsedMilliseconds;
            var last = total > 0 ? done >= total && (bytesTotal == 0 || bytes >= bytesTotal) : bytesTotal > 0 && bytes >= bytesTotal;
            if (!last && now - _lastPrint < (_tty ? 150 : 3000)) return;
            _lastPrint = now;

            var sb = new StringBuilder(label);
            if (total > 0) sb.Append($"  {done}/{total}");
            if (bytesTotal > 0)
            {
                sb.Append($"  {DiskSpace.Format(bytes)} / {DiskSpace.Format(bytesTotal)}");
                var secs = _clock.Elapsed.TotalSeconds;
                var speed = secs > 1 ? (bytes - _firstBytes) / secs : 0;
                if (speed > 0)
                {
                    sb.Append($"  {speed / (1 << 20):0.0} Mo/s");
                    var left = (bytesTotal - bytes) / speed;
                    if (left >= 1) sb.Append(left >= 60 ? $"  reste {(int)(left / 60)} min {(int)(left % 60):00} s" : $"  reste {(int)left} s");
                }
            }
            if (_tty)
            {
                var width = Math.Max(20, SafeWidth() - 1);
                var text = sb.Length > width ? sb.ToString(0, width) : sb.ToString().PadRight(width);
                Console.Write("\r" + text);
                _dirty = true;
            }
            else Console.WriteLine(sb.ToString());
        }
    }

    public void Finish() { lock (_gate) { FinishLine(); _label = ""; } }

    void FinishLine()
    {
        if (_tty && _dirty) Console.WriteLine();
        _dirty = false;
    }

    static int SafeWidth()
    {
        try { return Console.WindowWidth > 0 ? Console.WindowWidth : 100; }
        catch (IOException) { return 100; }
    }
}

static class Machine
{
    public static string? Arch => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x86_64",
        Architecture.Arm64 => "aarch64",
        _ => null,
    };

    public static string AssetName(string arch) => $"create-warfare-linux-{arch}";

    /// <summary>La valeur conseillée par le pack, bornée par la RAM du PC (lue dans /proc/meminfo).</summary>
    public static int RecommendedMemoryMb()
    {
        long totalMb = 0;
        try
        {
            var line = File.ReadLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemTotal:", StringComparison.Ordinal));
            if (line != null && long.TryParse(new string(line.Where(char.IsDigit).ToArray()), out var kb)) totalMb = kb / 1024;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return MemoryAdvisor.RecommendedMb(totalMb);
    }

    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                   UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    public static string? Which(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, name)).FirstOrDefault(File.Exists);

    public static bool PrismRunning()
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                if (!char.IsDigit(Path.GetFileName(dir)[0])) continue;
                try
                {
                    if (File.ReadAllText(Path.Combine(dir, "comm")).Contains("prismlauncher", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return false;
    }

    /// <summary>Le Flatpak de Prism est installé (pour ce compte ou pour tout le système), même s'il n'a jamais été ouvert.</summary>
    public static bool FlatpakPrismInstalled(string home) =>
        Directory.Exists(Path.Combine(home, ".local", "share", "flatpak", "app", PrismLocator.FlatpakId)) ||
        Directory.Exists(Path.Combine("/var/lib/flatpak/app", PrismLocator.FlatpakId));
}

/// <summary>Double-clic depuis le gestionnaire de fichiers : il n'y a pas de terminal pour montrer ce qui se passe.</summary>
static class Desktop
{
    static bool Graphical => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                             !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

    public static bool NoTerminal() =>
        Console.IsInputRedirected && Console.IsOutputRedirected && Graphical && Environment.GetEnvironmentVariable("CW_IN_TERMINAL") != "1";

    /// <summary>Se relance dans le premier émulateur de terminal trouvé. Faux s'il n'y en a aucun.</summary>
    public static bool ReopenInTerminal()
    {
        var self = Environment.ProcessPath;
        if (self == null) return false;
        (string exe, string[] before)[] terminals =
        {
            ("x-terminal-emulator", new[] { "-e" }), ("konsole", new[] { "-e" }), ("gnome-terminal", new[] { "--" }), ("kgx", new[] { "-e" }),
            ("ptyxis", new[] { "--" }), ("xfce4-terminal", new[] { "-x" }), ("mate-terminal", new[] { "-x" }), ("lxterminal", new[] { "-e" }),
            ("alacritty", new[] { "-e" }), ("kitty", Array.Empty<string>()), ("foot", Array.Empty<string>()), ("xterm", new[] { "-e" }),
        };
        foreach (var (exe, before) in terminals)
        {
            if (Machine.Which(exe) is not { } path) continue;
            try
            {
                var psi = new ProcessStartInfo(path) { UseShellExecute = false };
                foreach (var a in before) psi.ArgumentList.Add(a);
                psi.ArgumentList.Add(self);
                psi.ArgumentList.Add("install");
                psi.ArgumentList.Add("--pause");
                psi.Environment["CW_IN_TERMINAL"] = "1";
                Process.Start(psi);
                return true;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Info($"{exe} : {e.Message}"); }
        }
        return false;
    }

    /// <summary>Message dans une boîte de dialogue du bureau, si un outil courant est là. Sans effet sinon.</summary>
    public static void Notify(string text)
    {
        (string exe, string[] args)[] tools =
        {
            ("zenity", new[] { "--info", "--title=Create Warfare", "--text=" + text }),
            ("kdialog", new[] { "--title", "Create Warfare", "--msgbox", text }),
            ("notify-send", new[] { "Create Warfare", text }),
        };
        foreach (var (exe, args) in tools)
        {
            if (Machine.Which(exe) is not { } path) continue;
            try
            {
                var psi = new ProcessStartInfo(path) { UseShellExecute = false };
                foreach (var a in args) psi.ArgumentList.Add(a);
                Process.Start(psi);
                return;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
    }
}
