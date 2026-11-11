using System.Diagnostics;
using System.Reflection;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Nouvelle version du launcher annoncée dans version.json : télécharge l'exe à côté (.new), vérifie son SHA-256,
/// renomme l'exe en cours en .old, pose le nouveau, le relance. Le .old est supprimé au lancement suivant.
/// </summary>
public static class SelfUpdater
{
    public static Version CurrentVersion { get; } = ParseVersion(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0");

    public static Version ParseVersion(string s)
    {
        var core = s.Split('-', '+')[0].Trim().TrimStart('v');
        return Version.TryParse(core.Contains('.') ? core : core + ".0", out var v) ? v : new Version(0, 0);
    }

    public static string Display(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";

    static string? ExePath => Environment.ProcessPath;

    public static void CleanupOld()
    {
        try { if (ExePath != null && File.Exists(ExePath + ".old")) File.Delete(ExePath + ".old"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Info($".old encore verrouillé : {e.Message}"); }
    }

    public static bool UpdateAvailable(VersionInfo v) =>
        v.Launcher is { Version.Length: > 0, Url.Length: > 0 } l && ParseVersion(l.Version) > CurrentVersion;

    public static bool PackRequiresUpdate(VersionInfo v) => ParseVersion(v.MinLauncherVersion) > CurrentVersion;

    /// <summary>Renvoie après avoir lancé le nouvel exe : l'appelant doit fermer l'application.</summary>
    public static async Task UpdateAndRestartAsync(VersionInfo v, Downloader dl, IProgress<long>? bytes, CancellationToken ct)
    {
        var exe = ExePath ?? throw new InvalidOperationException("Chemin de l'exe inconnu");
        await ReplaceAndRestartAsync(exe, v.Launcher!, dl, bytes, ct, path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }));
    }

    /// <summary>
    /// Vérifie d'abord qu'on peut écrire à côté de l'exe (sinon 70 Mo seraient téléchargés pour rien), télécharge et
    /// vérifie le nouvel exe, échange les fichiers, lance le nouveau. Si le lancement échoue, l'ancien exe est remis.
    /// </summary>
    public static async Task ReplaceAndRestartAsync(string exe, LauncherRelease l, Downloader dl, IProgress<long>? bytes,
        CancellationToken ct, Action<string> start)
    {
        var dir = Path.GetDirectoryName(exe)!;
        var probe = Path.Combine(dir, ".cw-write-test-" + Environment.ProcessId);
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            throw new IOException($"Impossible d'écrire dans {dir} : déplace le launcher dans un dossier à toi (Bureau, Documents), puis réessaie", e);
        }

        var next = exe + ".new";
        await dl.DownloadVerifiedAsync(new[] { l.Url }, next, l.Sha256, sha1: false, bytes, ct);
        File.Move(exe, exe + ".old", overwrite: true);
        try
        {
            File.Move(next, exe);
            Log.Info($"Launcher mis à jour vers {l.Version}, relance");
            start(exe);
        }
        catch (Exception e)
        {
            Log.Error("Nouveau launcher non lancé, retour à l'ancien", e);
            if (File.Exists(exe)) File.Move(exe, next, overwrite: true);
            File.Move(exe + ".old", exe);
            throw;
        }
    }
}
