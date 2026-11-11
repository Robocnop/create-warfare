using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Fournit un jar vérifié sur le disque local.</summary>
public interface IJarProvider
{
    Task<string> GetJarAsync(ModEntry mod, IProgress<long>? bytes, CancellationToken ct);
    /// <summary>Vrai si le jar est déjà en cache (sert au calcul de l'espace disque nécessaire).</summary>
    bool IsCached(ModEntry mod) => false;
}

/// <summary>Adresses acceptées : https, ou http vers la machine locale (tests). Tout le reste est refusé.</summary>
public static class UrlRules
{
    public static bool IsRemote(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowedRemote(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || (u.Scheme == Uri.UriSchemeHttp && u.IsLoopback));

    public static void EnsureAllowed(string url)
    {
        if (IsRemote(url) && !IsAllowedRemote(url))
            throw new InvalidDataException($"Adresse non sécurisée refusée (https obligatoire) : {url}");
    }
}

/// <summary>
/// Téléchargements HTTP (ou copie si l'URL est un chemin local : tests et essais hors ligne),
/// reprise des .part, vérification du hash avant tout usage. Cache : %TEMP%\cw-launcher.
/// Une connexion muette est coupée après <see cref="IdleTimeout"/> ; chaque adresse est essayée <see cref="Attempts"/> fois.
/// </summary>
public sealed class Downloader : IJarProvider
{
    readonly HttpClient _http;
    public string CacheDir { get; }
    /// <summary>Délai sans recevoir un seul octet (en-têtes ou corps) avant d'abandonner la tentative.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Tentatives par adresse (erreurs réseau et 5xx ; un 404 passe tout de suite à l'adresse suivante).</summary>
    public int Attempts { get; set; } = 3;
    /// <summary>Attente avant de réessayer, selon le numéro de l'essai raté (1, 2…) : 1 s, 3 s, puis 8 s.</summary>
    public Func<int, TimeSpan> RetryDelay { get; set; } = n => TimeSpan.FromSeconds(n switch { 1 => 1, 2 => 3, _ => 8 });

    public Downloader(string? cacheDir = null, HttpClient? http = null)
    {
        CacheDir = cacheDir ?? Path.Combine(Path.GetTempPath(), "cw-launcher");
        _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan }; // délais gérés ici (IdleTimeout)
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"CreateWarfareLauncher/{SelfUpdater.CurrentVersion}");
    }

    static bool IsLocal(string url, out string path)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.IsFile) { path = u.LocalPath; return true; }
        path = url;
        return !UrlRules.IsRemote(url);
    }

    public async Task<VersionInfo> FetchManifestAsync(string url, CancellationToken ct)
    {
        string json;
        if (IsLocal(url, out var p)) json = await File.ReadAllTextAsync(p, ct);
        else json = await GetStringAsync(url, ct);
        try
        {
            return JsonSerializer.Deserialize(json, JsonCtx.Default.VersionInfo)
                ?? throw new InvalidDataException("version.json vide");
        }
        catch (JsonException e) { throw new InvalidDataException("version.json illisible : " + e.Message, e); }
    }

    /// <summary>Petit texte distant (version.json, .sha256), sans cache, borné par <see cref="IdleTimeout"/>.</summary>
    public async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        UrlRules.EnsureAllowed(url);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(IdleTimeout);
        try
        {
            using var res = await _http.SendAsync(req, idle.Token);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync(idle.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException($"pas de réponse depuis {IdleTimeout.TotalSeconds:0} s");
        }
    }

    public async Task<string> GetJarAsync(ModEntry mod, IProgress<long>? bytes, CancellationToken ct)
    {
        var dest = JarPath(mod);
        await DownloadVerifiedAsync(mod.Urls, dest, mod.Sha1, sha1: true, bytes, ct);
        return dest;
    }

    string JarPath(ModEntry mod) => Path.Combine(CacheDir, "jars", mod.Sha1.ToLowerInvariant() + ".jar");

    public bool IsCached(ModEntry mod) => File.Exists(JarPath(mod));

    /// <summary>Télécharge le zip du pack et extrait son dossier overrides/ ; renvoie ce dossier.</summary>
    public async Task<string> GetOverridesAsync(VersionInfo v, IProgress<long>? bytes, CancellationToken ct)
    {
        var zip = await GetZipAsync(v, bytes, ct);
        var outDir = Path.Combine(CacheDir, "overrides", v.Zip.Sha256.ToLowerInvariant());
        var marker = Path.Combine(outDir, ".complete");
        if (File.Exists(marker)) return Path.Combine(outDir, "overrides");
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);
        using (var archive = ZipFile.OpenRead(zip))
            foreach (var e in archive.Entries)
            {
                if (!e.FullName.StartsWith("overrides/", StringComparison.Ordinal) || e.FullName.EndsWith('/')) continue;
                var target = PathRules.Resolve(outDir, e.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                e.ExtractToFile(target, overwrite: true);
            }
        Directory.CreateDirectory(Path.Combine(outDir, "overrides"));
        File.WriteAllText(marker, v.Version);
        return Path.Combine(outDir, "overrides");
    }

    public async Task<string> GetZipAsync(VersionInfo v, IProgress<long>? bytes, CancellationToken ct)
    {
        var dest = Path.Combine(CacheDir, "zips", $"CreateWarfare-{v.Version}.zip");
        await DownloadVerifiedAsync(new[] { v.Zip.Url }, dest, v.Zip.Sha256, sha1: false, bytes, ct);
        return dest;
    }

    /// <summary>
    /// Après une mise à jour réussie : ne garde en cache que les jars de <paramref name="current"/> et les overrides de son zip.
    /// Ne lève jamais d'exception (un fichier verrouillé reste jusqu'à la prochaine fois).
    /// </summary>
    public void Prune(VersionInfo current)
    {
        var keepJars = current.Mods.Select(m => m.Sha1).Concat(current.ResourcePacks.Select(r => r.Sha1))
            .Select(h => h.ToLowerInvariant() + ".jar").ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var jars = Path.Combine(CacheDir, "jars");
            if (Directory.Exists(jars))
                foreach (var f in Directory.EnumerateFiles(jars))
                    if (!keepJars.Contains(Path.GetFileName(f))) TryDelete(() => File.Delete(f));
            var zips = Path.Combine(CacheDir, "zips");
            if (Directory.Exists(zips))
                foreach (var f in Directory.EnumerateFiles(zips))
                    if (!Path.GetFileName(f).Equals($"CreateWarfare-{current.Version}.zip", StringComparison.OrdinalIgnoreCase)) TryDelete(() => File.Delete(f));
            var ov = Path.Combine(CacheDir, "overrides");
            if (Directory.Exists(ov))
                foreach (var d in Directory.EnumerateDirectories(ov))
                    if (!Path.GetFileName(d).Equals(current.Zip.Sha256, StringComparison.OrdinalIgnoreCase)) TryDelete(() => Directory.Delete(d, true));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Info($"Cache non nettoyé : {e.Message}"); }

        static void TryDelete(Action a)
        {
            try { a(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Essaie chaque URL dans l'ordre, plusieurs fois en cas d'erreur passagère ; le fichier n'apparaît à
    /// <paramref name="dest"/> que si le hash correspond.
    /// </summary>
    public async Task DownloadVerifiedAsync(IEnumerable<string> urls, string dest, string hash, bool sha1,
        IProgress<long>? bytes, CancellationToken ct)
    {
        if (Hashing.Matches(dest, hash, sha1)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var part = dest + ".part";
        var errors = new List<string>();
        foreach (var url in urls)
        {
            for (int attempt = 1; attempt <= Math.Max(1, Attempts); attempt++)
            {
                ct.ThrowIfCancellationRequested();
                if (attempt > 1) await Task.Delay(RetryDelay(attempt - 1), ct);
                var local = IsLocal(url, out var localPath);
                var resumed = !local && File.Exists(part);
                try
                {
                    if (local) File.Copy(localPath, part, overwrite: true);
                    else
                    {
                        UrlRules.EnsureAllowed(url);
                        await HttpToPartAsync(url, part, bytes, ct);
                    }
                    if (Hashing.Matches(part, hash, sha1))
                    {
                        File.Move(part, dest, overwrite: true);
                        return;
                    }
                    File.Delete(part);
                    errors.Add($"{url} : hash différent");
                    // un .part repris d'un essai précédent peut être faux : on repart de zéro une fois, sinon adresse suivante
                    if (!resumed) break;
                }
                catch (InvalidDataException e) { errors.Add(e.Message); break; }
                catch (HttpRequestException e) when (e.StatusCode is { } code && (int)code is >= 400 and < 500 and not 408 and not 429)
                {
                    errors.Add($"{url} : erreur {(int)code}");
                    break; // 404, 403… : réessayer ne changera rien
                }
                catch (IOException e) when (IsDiskFull(e))
                {
                    throw new IOException("Disque plein pendant le téléchargement de " + Path.GetFileName(dest), e);
                }
                catch (FileNotFoundException e) { errors.Add($"{url} : {e.Message}"); break; }
                catch (Exception e) when (e is HttpRequestException or IOException || (e is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    errors.Add($"{url} (essai {attempt}) : {e.Message}");
                    Log.Info($"Téléchargement interrompu, essai {attempt}/{Attempts} : {url} : {e.Message}");
                    if (local) break;
                }
            }
        }
        throw new IOException($"Téléchargement impossible de {Path.GetFileName(dest)}\n" + string.Join("\n", errors));
    }

    /// <summary>ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL (Windows) ou ENOSPC (Linux).</summary>
    public static bool IsDiskFull(IOException e) => (e.HResult & 0xFFFF) is 0x70 or 0x27 || e.HResult == 28;

    async Task HttpToPartAsync(string url, string part, IProgress<long>? bytes, CancellationToken ct)
    {
        // Un seul minuteur, réarmé à chaque bloc reçu : en-têtes puis corps.
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            long have = File.Exists(part) ? new FileInfo(part).Length : 0;
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (have > 0) req.Headers.Range = new RangeHeaderValue(have, null);
            idle.CancelAfter(IdleTimeout);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, idle.Token);
            if (res.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                File.Delete(part);
                throw new IOException("reprise refusée par le serveur, nouveau départ");
            }
            res.EnsureSuccessStatusCode();
            var append = have > 0 && res.StatusCode == HttpStatusCode.PartialContent;
            await using var src = await res.Content.ReadAsStreamAsync(idle.Token);
            await using var dst = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true);
            var buf = new byte[1 << 16];
            while (true)
            {
                idle.CancelAfter(IdleTimeout);
                var n = await src.ReadAsync(buf, idle.Token);
                if (n <= 0) break;
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                bytes?.Report(n);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IOException($"connexion figée (aucune donnée depuis {IdleTimeout.TotalSeconds:0} s)");
        }
    }
}
