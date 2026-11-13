using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Core.Native;

/// <param name="Phase">« check » (vérification), « download », « neoforge » (installateur) ou « done ».</param>
public sealed record InstallProgress(string Phase, int Done, int Total, long Bytes, long BytesTotal, string Current);

/// <summary>Jeu prêt à lancer : profil fusionné (NeoForge + 1.21.1) et dossiers.</summary>
public sealed record InstalledGame(string GameRoot, GameVersion Version, string NativesDir);

/// <summary>
/// Un fichier du jeu : chemin relatif à la racine (avec des /), sha1, taille. <see cref="SizeOnly"/> : les assets
/// (≈ 4 000 fichiers) sont vérifiés par leur empreinte quand on les obtient, puis seulement par leur taille.
/// </summary>
public sealed record GameFile(string RelPath, string? Sha1, long Size, IReadOnlyList<string> Urls, bool SizeOnly = false);

/// <summary>
/// Installe Minecraft 1.21.1 + NeoForge 21.1.264 dans <see cref="Root"/> (libraries/, assets/, versions/) :
/// manifestes Mojang et profil NeoForge (tiré de l'installateur officiel, empreinte figée), bibliothèques, assets, client.
/// Chaque fichier déjà présent dans un miroir (installation CurseForge, launcher officiel) et dont l'empreinte correspond
/// y est repris par lien physique (copie si impossible) : rien n'est écrit chez CurseForge. Le client patché par NeoForge
/// est repris de CurseForge s'il a l'empreinte attendue, sinon l'installateur officiel est lancé (--install-client).
/// </summary>
public sealed class GameInstaller
{
    readonly Downloader _dl;
    /// <summary>Racine d'installation (chemin absolu).</summary>
    public string Root { get; }
    /// <summary>Dossiers où chercher des fichiers déjà présents (CurseForge, launcher officiel), hors <see cref="Root"/>.</summary>
    public IReadOnlyList<string> Mirrors { get; }
    /// <summary>Système pris en compte pour les règles des bibliothèques (remplaçable dans les tests).</summary>
    public OsInfo Os { get; init; } = OsInfo.Current;
    /// <summary>Nombre de fichiers vérifiés ou téléchargés en parallèle.</summary>
    public int Parallelism { get; init; } = 8;
    /// <summary>Lance l'installateur NeoForge (java, arguments, dossier de travail) ; renvoie le code de sortie et la fin de sa sortie.</summary>
    public Func<string, IReadOnlyList<string>, string, CancellationToken, Task<(int Code, string Output)>> RunProcess { get; init; } = RunAsync;

    /// <param name="root">Racine du jeu.</param>
    /// <param name="mirrors">Dossiers de fichiers à reprendre s'ils ont la bonne empreinte.</param>
    /// <param name="dl">Téléchargeur vérifiant les empreintes.</param>
    public GameInstaller(string root, IReadOnlyList<string> mirrors, Downloader dl)
    {
        Root = Path.GetFullPath(root);
        Mirrors = mirrors.Select(Path.GetFullPath).Where(m => !m.Equals(Root, StringComparison.OrdinalIgnoreCase)).ToList();
        _dl = dl;
    }

    string Abs(string rel) => PathRules.Resolve(Root, rel);

    /// <summary>
    /// Rend le jeu prêt : profil vanilla, installateur NeoForge, client, bibliothèques, assets, client patché, natives.
    /// Ne retélécharge que ce qui manque ou diffère. <paramref name="javaExe"/> sert à lancer l'installateur NeoForge si nécessaire.
    /// </summary>
    public async Task<InstalledGame> EnsureAsync(string javaExe, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var mc = NativeConfig.MinecraftVersion;
        progress?.Report(new InstallProgress("check", 0, 0, 0, 0, $"versions/{mc}/{mc}.json"));

        // 1. Profil vanilla (empreinte figée)
        var vanillaRel = $"versions/{mc}/{mc}.json";
        await AcquireAllAsync(new[] { new GameFile(vanillaRel, NativeConfig.VanillaJsonSha1, -1, new[] { NativeConfig.VanillaJsonUrl }) }, null, ct);
        var vanilla = VersionParser.Parse(await File.ReadAllTextAsync(Abs(vanillaRel), ct));

        // 2. Installateur NeoForge (empreinte figée) : profil et liste des fichiers produits
        var installer = Abs($"cache/neoforge-{NativeConfig.NeoForgeVersion}-installer.jar");
        await _dl.DownloadVerifiedAsync(new[] { NativeConfig.InstallerUrl }, installer, NativeConfig.InstallerSha256, sha1: false, null, ct);
        string profileJson, installProfileJson;
        using (var zip = ZipFile.OpenRead(installer))
        {
            profileJson = ReadEntry(zip, "version.json");
            installProfileJson = ReadEntry(zip, "install_profile.json");
        }
        var id = NativeConfig.VersionId;
        var profileRel = $"versions/{id}/{id}.json";
        WriteIfChanged(Abs(profileRel), profileJson);
        var neo = VersionParser.Parse(profileJson);
        if (neo.InheritsFrom != mc) throw new InvalidDataException($"Profil {neo.Id} prévu pour {neo.InheritsFrom}, pas {mc}");
        var merged = VersionParser.Merge(neo, vanilla);

        // 3. Client, bibliothèques, jar universal de NeoForge, index des assets
        var files = new List<GameFile>();
        if (vanilla.Client is { Sha1: { } clientSha } client)
        {
            files.Add(new GameFile($"versions/{mc}/{mc}.jar", clientSha, client.Size, Urls(client)));
            files.Add(new GameFile($"versions/{id}/{id}.jar", clientSha, client.Size, Urls(client)));
        }
        foreach (var lib in LibraryResolver.Resolve(merged, Os))
            files.Add(new GameFile("libraries/" + lib.File.Path, lib.File.Sha1, lib.File.Size, Urls(lib.File)));
        foreach (var lib in InstallProfileRuntimeLibraries(installProfileJson))
            files.Add(new GameFile("libraries/" + lib.Path, lib.Sha1, lib.Size, Urls(lib)));
        var index = merged.AssetIndex ?? throw new InvalidDataException("Profil sans assetIndex");
        var indexRel = $"assets/indexes/{index.Id}.json";
        files.Add(new GameFile(indexRel, index.Sha1, index.Size, new[] { index.Url }));
        await AcquireAllAsync(Dedupe(files), progress, ct);

        // 4. Assets
        var assets = ParseAssetIndex(await File.ReadAllTextAsync(Abs(indexRel), ct));
        await AcquireAllAsync(assets, progress, ct);

        // 5. Client patché par NeoForge : repris d'un miroir s'il a l'empreinte attendue, sinon installateur officiel
        var outputs = NativeConfig.InstallerOutputs.Select(o => new GameFile("libraries/" + o.Path, o.Sha1, -1, Array.Empty<string>())).ToList();
        var missing = await AcquireAllAsync(outputs, null, ct, allowMissing: true);
        if (missing.Count > 0)
        {
            progress?.Report(new InstallProgress("neoforge", 0, 1, 0, 0, $"NeoForge {NativeConfig.NeoForgeVersion}"));
            await RunInstallerAsync(javaExe, installer, ct);
            foreach (var o in outputs)
                if (!Hashing.Matches(Abs(o.RelPath), o.Sha1!, sha1: true))
                    throw new InvalidDataException($"Installation de NeoForge incorrecte : {o.RelPath} n'a pas l'empreinte attendue.");
        }

        var natives = Abs($"natives/{id}");
        Directory.CreateDirectory(natives);
        ExtractLegacyNatives(merged, natives);
        progress?.Report(new InstallProgress("done", 1, 1, 0, 0, ""));
        return new InstalledGame(Root, merged, natives);
    }

    static IReadOnlyList<string> Urls(Artifact a) => a.Url is { } u ? new[] { u } : Array.Empty<string>();

    static List<GameFile> Dedupe(IEnumerable<GameFile> files) =>
        files.GroupBy(f => f.RelPath, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

    static string ReadEntry(ZipArchive zip, string name)
    {
        var e = zip.GetEntry(name) ?? throw new InvalidDataException($"{name} absent de l'installateur NeoForge");
        using var r = new StreamReader(e.Open());
        return r.ReadToEnd();
    }

    static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteAllText(path, text);
    }

    /// <summary>
    /// Bibliothèques de install_profile.json utiles au jeu lui-même (et pas seulement aux outils de l'installateur) :
    /// le jar universal de NeoForge, que FML cherche dans libraries/.
    /// </summary>
    public static List<Artifact> InstallProfileRuntimeLibraries(string installProfileJson)
    {
        using var doc = JsonDocument.Parse(installProfileJson);
        var list = new List<Artifact>();
        if (!doc.RootElement.TryGetProperty("libraries", out var libs)) return list;
        foreach (var l in libs.EnumerateArray())
        {
            var name = l.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name == null || !name.StartsWith("net.neoforged:neoforge:", StringComparison.Ordinal) || !name.EndsWith(":universal", StringComparison.Ordinal)) continue;
            var art = l.GetProperty("downloads").GetProperty("artifact");
            list.Add(new Artifact(art.GetProperty("path").GetString()!, art.GetProperty("sha1").GetString(),
                art.TryGetProperty("size", out var s) ? s.GetInt64() : -1, art.TryGetProperty("url", out var u) ? u.GetString() : null));
        }
        return list;
    }

    /// <summary>Index des assets → un fichier par objet (objects/&lt;2 premiers caractères&gt;/&lt;sha1&gt;), sans doublon.</summary>
    public static List<GameFile> ParseAssetIndex(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<GameFile>();
        foreach (var o in doc.RootElement.GetProperty("objects").EnumerateObject())
        {
            var hash = o.Value.GetProperty("hash").GetString()!.ToLowerInvariant();
            if (hash.Length != 40 || !hash.All(Uri.IsHexDigit) || !seen.Add(hash)) continue;
            var size = o.Value.TryGetProperty("size", out var s) ? s.GetInt64() : -1;
            var rel = $"{hash[..2]}/{hash}";
            list.Add(new GameFile("assets/objects/" + rel, hash, size, new[] { NativeConfig.AssetsBaseUrl + rel }, SizeOnly: true));
        }
        return list;
    }

    // ---------------------------------------------------------------- Obtention des fichiers

    enum Need { Present, Mirror, Download, Missing }

    /// <summary>Obtient ces fichiers (déjà là, miroir ou téléchargement) ; renvoie ceux introuvables sans adresse.</summary>
    public Task<List<GameFile>> EnsureFilesAsync(IReadOnlyList<GameFile> files, IProgress<InstallProgress>? progress, CancellationToken ct) =>
        AcquireAllAsync(files, progress, ct, allowMissing: true);

    /// <summary>
    /// Vérifie puis obtient chaque fichier (miroir ou téléchargement), en parallèle. Renvoie ceux introuvables quand
    /// <paramref name="allowMissing"/> (sinon : exception).
    /// </summary>
    async Task<List<GameFile>> AcquireAllAsync(IReadOnlyList<GameFile> files, IProgress<InstallProgress>? progress, CancellationToken ct, bool allowMissing = false)
    {
        // Vérification (lecture seule, en parallèle : surtout du hachage)
        var plan = new (GameFile File, Need Need, string? Source)[files.Count];
        var checkedCount = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, files.Count), new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct }, (i, _) =>
        {
            var f = files[i];
            plan[i] = Classify(f);
            var n = Interlocked.Increment(ref checkedCount);
            if (progress != null && (n % 64 == 0 || n == files.Count))
                progress.Report(new InstallProgress("check", n, files.Count, 0, 0, f.RelPath));
            return ValueTask.CompletedTask;
        });

        var missing = plan.Where(p => p.Need == Need.Missing).Select(p => p.File).ToList();
        if (missing.Count > 0 && !allowMissing)
            throw new InvalidDataException($"Fichier introuvable et sans adresse de téléchargement : {missing[0].RelPath}");

        foreach (var p in plan.Where(p => p.Need == Need.Mirror)) LinkOrCopy(p.Source!, Abs(p.File.RelPath));

        var todo = plan.Where(p => p.Need == Need.Download).Select(p => p.File).ToList();
        if (todo.Count == 0) return missing;
        long total = todo.Sum(f => Math.Max(0, f.Size)), bytes = 0;
        var done = 0;
        Log.Info($"Lancement natif : {todo.Count} fichier(s) à télécharger ({total / 1048576.0:0.0} Mo)");
        var bytesProgress = new SyncProgress<long>(n =>
        {
            var b = Interlocked.Add(ref bytes, n);
            progress?.Report(new InstallProgress("download", Volatile.Read(ref done), todo.Count, b, total, ""));
        });
        await Parallel.ForEachAsync(todo, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct }, async (f, token) =>
        {
            if (f.Sha1 == null) throw new InvalidDataException($"Pas d'empreinte pour {f.RelPath} : téléchargement refusé.");
            await _dl.DownloadVerifiedAsync(f.Urls, Abs(f.RelPath), f.Sha1, sha1: true, bytesProgress, token);
            var d = Interlocked.Increment(ref done);
            progress?.Report(new InstallProgress("download", d, todo.Count, Interlocked.Read(ref bytes), total, f.RelPath));
        });
        return missing;
    }

    (GameFile, Need, string?) Classify(GameFile f)
    {
        var dest = Abs(f.RelPath);
        if (IsValid(dest, f, trustSize: f.SizeOnly)) return (f, Need.Present, null);
        foreach (var m in Mirrors)
        {
            var src = Path.Combine(m, f.RelPath.Replace('/', Path.DirectorySeparatorChar));
            if (IsValid(src, f, trustSize: false)) return (f, Need.Mirror, src);
        }
        return (f, f.Urls.Count > 0 ? Need.Download : Need.Missing, null);
    }

    /// <summary>Taille puis empreinte ; <paramref name="trustSize"/> : la taille suffit (assets déjà vérifiés à leur arrivée).</summary>
    static bool IsValid(string path, GameFile f, bool trustSize)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return false;
            if (f.Size >= 0 && fi.Length != f.Size) return false;
            if (f.Sha1 == null) return true;
            if (trustSize && f.Size >= 0) return true;
            return Hashing.Matches(path, f.Sha1, sha1: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Lien physique vers le fichier du miroir (même disque), sinon copie via un .tmp.</summary>
    public static void LinkOrCopy(string src, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (File.Exists(dest)) File.Delete(dest);
        if (OperatingSystem.IsWindows() && CreateHardLink(dest, src, IntPtr.Zero)) return;
        var tmp = dest + ".tmp";
        File.Copy(src, tmp, overwrite: true);
        File.Move(tmp, dest, overwrite: true);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    /// <summary>Progress synchrone : appelé sur le fil du téléchargement (Progress&lt;T&gt; passerait par le contexte de l'interface).</summary>
    sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    // ---------------------------------------------------------------- Installateur NeoForge, natives

    async Task RunInstallerAsync(string javaExe, string installer, CancellationToken ct)
    {
        // L'installateur client exige un dossier de launcher : un launcher_profiles.json minimal suffit
        var profiles = Abs("launcher_profiles.json");
        if (!File.Exists(profiles)) File.WriteAllText(profiles, "{\"profiles\":{}}");
        // javaw n'a pas de sortie : java.exe à côté
        var java = Path.Combine(Path.GetDirectoryName(javaExe)!, OperatingSystem.IsWindows() ? "java.exe" : "java");
        if (!File.Exists(java)) java = javaExe;
        Log.Info("Installation de NeoForge " + NativeConfig.NeoForgeVersion + " (installateur officiel, --install-client)");
        var (code, output) = await RunProcess(java, new[] { "-jar", installer, "--install-client", Root }, Path.GetDirectoryName(installer)!, ct);
        if (code != 0)
            throw new InvalidDataException($"L'installateur NeoForge a échoué (code {code}).\n{output}");
    }

    static async Task<(int, string)> RunAsync(string exe, IReadOnlyList<string> args, string workDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = workDir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new IOException("Impossible de lancer " + exe);
        var tail = new Queue<string>();
        void Keep(string? line)
        {
            if (line == null) return;
            lock (tail) { tail.Enqueue(line); while (tail.Count > 15) tail.Dequeue(); }
        }
        p.OutputDataReceived += (_, e) => Keep(e.Data);
        p.ErrorDataReceived += (_, e) => Keep(e.Data);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
        lock (tail) return (p.ExitCode, string.Join("\n", tail));
    }

    /// <summary>Anciens profils (natives à extraire) ; 1.21.1 n'en a pas : LWJGL extrait lui-même ses natives.</summary>
    void ExtractLegacyNatives(GameVersion v, string nativesDir)
    {
        foreach (var lib in LibraryResolver.Resolve(v, Os).Where(l => l.ExtractNatives))
        {
            using var zip = ZipFile.OpenRead(Abs("libraries/" + lib.File.Path));
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith('/') || e.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) continue;
                var dest = PathRules.Resolve(nativesDir, e.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                e.ExtractToFile(dest, overwrite: true);
            }
        }
    }
}
