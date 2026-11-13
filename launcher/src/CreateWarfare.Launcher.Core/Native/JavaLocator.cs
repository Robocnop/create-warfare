using System.IO.Compression;
using System.Text.Json;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Core.Native;

/// <param name="Home">Dossier de Java (contient bin\ et release).</param>
/// <param name="Source">D'où il vient (journal) : CurseForge, JAVA_HOME, launcher…</param>
public sealed record JavaInstall(string Home, string Executable, string Version, int Major, string Source);

/// <summary>
/// Java 21 64 bits pour NeoForge 21.1 : d'abord celui que CurseForge utilise pour l'instance (java-runtime-delta, Temurin),
/// puis celui du launcher, du launcher officiel, JAVA_HOME et les installations courantes. Sinon, téléchargement Adoptium.
/// </summary>
public static class JavaLocator
{
    public const string AdoptiumApi =
        "https://api.adoptium.net/v3/assets/latest/21/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse";

    /// <summary>Champs utiles du fichier « release » d'un JDK/JRE (JAVA_VERSION, OS_ARCH, IMPLEMENTOR).</summary>
    public static Dictionary<string, string> ParseRelease(string text)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            d[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('"');
        }
        return d;
    }

    /// <summary>« 21.0.4 » → 21, « 1.8.0_402 » → 8, illisible → 0.</summary>
    public static int MajorOf(string? version)
    {
        if (string.IsNullOrEmpty(version)) return 0;
        var parts = version.Split('.', '_', '+', '-');
        if (!int.TryParse(parts[0], out var first)) return 0;
        return first == 1 && parts.Length > 1 && int.TryParse(parts[1], out var second) ? second : first;
    }

    /// <summary>Java utilisable dans <paramref name="home"/> (javaw.exe, version 21, 64 bits) ; null sinon.</summary>
    public static JavaInstall? Inspect(string home, string source)
    {
        try
        {
            var exe = Path.Combine(home, "bin", OperatingSystem.IsWindows() ? "javaw.exe" : "java");
            var release = Path.Combine(home, "release");
            if (!File.Exists(exe) || !File.Exists(release)) return null;
            var info = ParseRelease(File.ReadAllText(release));
            info.TryGetValue("JAVA_VERSION", out var version);
            var major = MajorOf(version);
            if (major != NativeConfig.JavaMajor) return null;
            if (info.TryGetValue("OS_ARCH", out var arch) && arch is not ("x86_64" or "amd64")) return null;
            return new JavaInstall(home, exe, version!, major, source);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Dossiers candidats, du plus fiable au moins fiable.</summary>
    public static IEnumerable<(string Home, string Source)> Candidates(string? curseForgeMinecraftRoot)
    {
        if (Environment.GetEnvironmentVariable("CW_JAVA_HOME") is { Length: > 0 } forced) yield return (forced, "CW_JAVA_HOME");
        if (curseForgeMinecraftRoot != null)
        {
            var install = Path.Combine(curseForgeMinecraftRoot, "Install");
            yield return (Path.Combine(install, "java", "java-runtime-delta"), "CurseForge");
            yield return (Path.Combine(install, "runtime", "java-runtime-delta", "windows-x64", "java-runtime-delta"), "CurseForge");
            yield return (Path.Combine(install, "java", "Jre_21"), "CurseForge");
        }
        foreach (var d in Children(NativeConfig.JavaRoot)) yield return (d, "launcher");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return (Path.Combine(local, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Local", "runtime",
            "java-runtime-delta", "windows-x64", "java-runtime-delta"), "launcher officiel");
        yield return (Path.Combine(roaming, ".minecraft", "runtime", "java-runtime-delta", "windows-x64", "java-runtime-delta"), "launcher officiel");
        if (Environment.GetEnvironmentVariable("JAVA_HOME") is { Length: > 0 } jh) yield return (jh, "JAVA_HOME");
        foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetEnvironmentVariable("ProgramW6432") }
                     .Where(p => !string.IsNullOrEmpty(p)).Distinct())
            foreach (var vendor in new[] { "Eclipse Adoptium", "Java", "Microsoft", "Zulu", "BellSoft", "Amazon Corretto" })
                foreach (var d in Children(Path.Combine(pf!, vendor)).Where(d => Path.GetFileName(d).Contains("21")))
                    yield return (d, vendor);
    }

    static IEnumerable<string> Children(string dir)
    {
        try { return Directory.Exists(dir) ? Directory.GetDirectories(dir).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase).ToList() : Enumerable.Empty<string>(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Enumerable.Empty<string>(); }
    }

    /// <summary>Premier Java 21 valide parmi <see cref="Candidates"/>, ou null (il faut alors <see cref="DownloadAsync"/>).</summary>
    public static JavaInstall? Find(string? curseForgeMinecraftRoot)
    {
        foreach (var (home, source) in Candidates(curseForgeMinecraftRoot))
            if (Inspect(home, source) is { } j)
            {
                Log.Info($"Java {j.Version} trouvé ({j.Source}) : {j.Home}");
                return j;
            }
        return null;
    }

    /// <summary>
    /// Télécharge le dernier JRE 21 Temurin (API Adoptium), vérifie son sha256 et l'extrait dans <paramref name="javaRoot"/>.
    /// </summary>
    public static async Task<JavaInstall> DownloadAsync(Downloader dl, string javaRoot, IProgress<long>? bytes, CancellationToken ct, string api = AdoptiumApi)
    {
        var json = await dl.GetStringAsync(api, ct);
        var (link, sha256, name, release) = ParseAdoptium(json);
        Directory.CreateDirectory(javaRoot);
        var zip = Path.Combine(javaRoot, name);
        await dl.DownloadVerifiedAsync(new[] { link }, zip, sha256, sha1: false, bytes, ct);

        var target = Path.Combine(javaRoot, Safe(release) + "-jre");
        var tmp = Path.Combine(javaRoot, ".extract-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using (var archive = ZipFile.OpenRead(zip))
                foreach (var e in archive.Entries)
                {
                    if (e.FullName.EndsWith('/')) continue;
                    var dest = PathRules.Resolve(tmp, e.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    e.ExtractToFile(dest, overwrite: true);
                }
            // le zip contient un seul dossier (jdk-21.0.x+y-jre)
            var top = Directory.GetDirectories(tmp);
            var home = top.Length == 1 && !File.Exists(Path.Combine(tmp, "release")) ? top[0] : tmp;
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(home, target);
        }
        finally
        {
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (IOException) { }
            try { File.Delete(zip); } catch (IOException) { }
        }
        return Inspect(target, "launcher") ?? throw new InvalidDataException("Java téléchargé inutilisable (javaw.exe ou release absent).");
    }

    /// <summary>Premier paquet de la réponse Adoptium : adresse, sha256, nom du zip, nom de la version.</summary>
    public static (string Link, string Sha256, string Name, string Release) ParseAdoptium(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var a in doc.RootElement.EnumerateArray())
        {
            if (!a.TryGetProperty("binary", out var b) || !b.TryGetProperty("package", out var p)) continue;
            var link = p.GetProperty("link").GetString();
            var sum = p.GetProperty("checksum").GetString();
            var name = p.GetProperty("name").GetString();
            var release = a.TryGetProperty("release_name", out var r) ? r.GetString() : null;
            if (link == null || sum == null || name == null || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            UrlRules.EnsureAllowed(link);
            return (link, sum, Safe(name), release ?? Path.GetFileNameWithoutExtension(name));
        }
        throw new InvalidDataException("Aucun Java 21 pour Windows x64 dans la réponse d'Adoptium.");
    }

    static string Safe(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
