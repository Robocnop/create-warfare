using System.Diagnostics;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Désinstallation : sauvegarde facultative des créations du joueur, suppression de l'instance CurseForge
/// (CurseForge retire le profil quand le dossier disparaît), puis suppression du launcher et de ses données
/// par un script lancé après la fermeture, car un exe ne peut pas s'effacer pendant qu'il tourne.
/// </summary>
public static class Uninstaller
{
    /// <summary>Dossiers de l'instance qui appartiennent au joueur.</summary>
    public static readonly string[] PlayerFolders = ["saves", "screenshots", "schematics"];

    public static string DefaultBackupRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Create Warfare (sauvegarde)");

    /// <summary>Copie les dossiers du joueur dans &lt;root&gt;\&lt;horodatage&gt;. Renvoie ce dossier, ou null s'il n'y avait rien.</summary>
    public static string? BackupPlayerData(string instanceDir, string backupRoot, DateTime now)
    {
        var dest = Path.Combine(backupRoot, now.ToString("yyyy-MM-dd_HH-mm-ss"));
        var copied = false;
        foreach (var name in PlayerFolders)
        {
            var src = Path.Combine(instanceDir, name);
            if (!Directory.Exists(src) || !Directory.EnumerateFileSystemEntries(src).Any()) continue;
            CopyDir(src, Path.Combine(dest, name));
            copied = true;
        }
        return copied ? dest : null;
    }

    /// <summary>Supprime le dossier de l'instance, fichiers en lecture seule compris.</summary>
    public static void DeleteInstance(string instanceDir)
    {
        if (!Directory.Exists(instanceDir)) return;
        foreach (var f in Directory.EnumerateFiles(instanceDir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(instanceDir, recursive: true);
        Log.Info($"Instance supprimée : {instanceDir}");
    }

    /// <summary>Script cmd : attend la fin du launcher, efface l'exe, les données du launcher, puis lui-même.</summary>
    public static string SelfRemovalScript(int pid, string exePath, string appDataDir) =>
        "@echo off\r\n" +
        ":wait\r\n" +
        $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
        $"del /f /q \"{exePath}\" \"{exePath}.old\" 2>nul\r\n" +
        $"rmdir /s /q \"{appDataDir}\" 2>nul\r\n" +
        "del /f /q \"%~f0\"\r\n";

    /// <summary>Lance le script en arrière-plan ; l'appelant doit fermer l'application juste après.</summary>
    public static void ScheduleSelfRemoval()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Chemin du launcher inconnu");
        var script = Path.Combine(Path.GetTempPath(), $"cw-uninstall-{Environment.ProcessId}.cmd");
        File.WriteAllText(script, SelfRemovalScript(Environment.ProcessId, exe, Log.AppDataDir));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
        Log.Info("Suppression du launcher programmée");
    }

    static void CopyDir(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(src, dir)));
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(dest, Path.GetRelativePath(src, file)), overwrite: true);
    }
}
