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
        var l = v.Launcher!;
        var next = exe + ".new";
        await dl.DownloadVerifiedAsync(new[] { l.Url }, next, l.Sha256, sha1: false, bytes, ct);
        File.Move(exe, exe + ".old", overwrite: true);
        try { File.Move(next, exe); }
        catch { File.Move(exe + ".old", exe); throw; }
        Log.Info($"Launcher mis à jour vers {l.Version}, relance");
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    }
}
