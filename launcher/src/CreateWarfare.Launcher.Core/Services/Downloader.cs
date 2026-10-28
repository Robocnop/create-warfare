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
}

/// <summary>
/// Téléchargements HTTP (ou copie si l'URL est un chemin local : tests et essais hors ligne),
/// reprise des .part, vérification du hash avant tout usage. Cache : %TEMP%\cw-launcher.
/// </summary>
public sealed class Downloader : IJarProvider
{
    readonly HttpClient _http;
    public string CacheDir { get; }

    public Downloader(string? cacheDir = null, HttpClient? http = null)
    {
        CacheDir = cacheDir ?? Path.Combine(Path.GetTempPath(), "cw-launcher");
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"CreateWarfareLauncher/{SelfUpdater.CurrentVersion}");
    }

    static bool IsLocal(string url, out string path)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.IsFile) { path = u.LocalPath; return true; }
        path = url;
        return !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<VersionInfo> FetchManifestAsync(string url, CancellationToken ct)
    {
        string json;
        if (IsLocal(url, out var p)) json = await File.ReadAllTextAsync(p, ct);
        else
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            json = await res.Content.ReadAsStringAsync(ct);
        }
        return JsonSerializer.Deserialize(json, JsonCtx.Default.VersionInfo)
            ?? throw new InvalidDataException("version.json vide");
    }

    public async Task<string> GetJarAsync(ModEntry mod, IProgress<long>? bytes, CancellationToken ct)
    {
        var dest = Path.Combine(CacheDir, "jars", mod.Sha1.ToLowerInvariant() + ".jar");
        await DownloadVerifiedAsync(mod.Urls, dest, mod.Sha1, sha1: true, bytes, ct);
        return dest;
    }

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

    /// <summary>Essaie chaque URL dans l'ordre ; le fichier n'apparaît à <paramref name="dest"/> que si le hash correspond.</summary>
    public async Task DownloadVerifiedAsync(IEnumerable<string> urls, string dest, string hash, bool sha1,
        IProgress<long>? bytes, CancellationToken ct)
    {
        if (Hashing.Matches(dest, hash, sha1)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var part = dest + ".part";
        var errors = new List<string>();
        foreach (var url in urls)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (IsLocal(url, out var local)) File.Copy(local, part, overwrite: true);
                else await HttpToPartAsync(url, part, bytes, ct);
                if (Hashing.Matches(part, hash, sha1))
                {
                    File.Move(part, dest, overwrite: true);
                    return;
                }
                errors.Add($"{url} : hash différent");
                File.Delete(part);
            }
            catch (Exception e) when (e is HttpRequestException or IOException || (e is TaskCanceledException && !ct.IsCancellationRequested))
            {
                errors.Add($"{url} : {e.Message}");
            }
        }
        throw new IOException($"Téléchargement impossible de {Path.GetFileName(dest)}\n" + string.Join("\n", errors));
    }

    async Task HttpToPartAsync(string url, string part, IProgress<long>? bytes, CancellationToken ct)
    {
        long have = File.Exists(part) ? new FileInfo(part).Length : 0;
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (have > 0) req.Headers.Range = new RangeHeaderValue(have, null);
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(part);
            await HttpToPartAsync(url, part, bytes, ct);
            return;
        }
        res.EnsureSuccessStatusCode();
        var append = have > 0 && res.StatusCode == HttpStatusCode.PartialContent;
        await using var src = await res.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true);
        var buf = new byte[1 << 16];
        int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            bytes?.Report(n);
        }
    }
}
