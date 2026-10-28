using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

public sealed record ApplyProgress(string Phase, int Done, int Total, string Current);

/// <summary>
/// 1. télécharge et vérifie tous les jars avant de toucher à l'instance ;
/// 2. sauvegarde chaque fichier remplacé ou supprimé dans .cw-backup\&lt;horodatage&gt;\ ;
/// 3. applique ; en cas d'erreur, remet l'instance dans son état d'avant ;
/// 4. écrit .cw-launcher.json en dernier ; garde les 2 dernières sauvegardes.
/// </summary>
public sealed class UpdateApplier
{
    public const string BackupDirName = ".cw-backup";
    public const int BackupsToKeep = 2;

    readonly IJarProvider _jars;
    /// <summary>Point d'injection pour les tests : appelé avant chaque écriture.</summary>
    public Action<FileAction>? BeforeWrite { get; set; }

    public UpdateApplier(IJarProvider jars) => _jars = jars;

    public async Task ApplyAsync(string instanceDir, FilePlan plan, IProgress<ApplyProgress>? progress, CancellationToken ct)
    {
        var changes = plan.Changes.ToList();
        Log.Info($"Mise à jour {plan.FromVersion} → {plan.ToVersion} : {changes.Count} changements dans {instanceDir}");

        // 1. Téléchargements
        var downloads = changes.Where(a => a.Mod != null && a.Kind != FileActionKind.Remove).ToList();
        var jarPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < downloads.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new ApplyProgress("download", i, downloads.Count, downloads[i].Mod!.File));
            jarPaths[downloads[i].RelPath] = await _jars.GetJarAsync(downloads[i].Mod!, null, ct);
        }
        progress?.Report(new ApplyProgress("download", downloads.Count, downloads.Count, ""));

        // 2-3. Sauvegarde et application, sans annulation possible au milieu
        var backupDir = Path.Combine(instanceDir, BackupDirName, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        var backup = new BackupManifest { FromVersion = plan.FromVersion, ToVersion = plan.ToVersion };
        Directory.CreateDirectory(backupDir);
        try
        {
            for (int i = 0; i < changes.Count; i++)
            {
                var a = changes[i];
                progress?.Report(new ApplyProgress("apply", i, changes.Count, a.RelPath));
                var target = PathRules.Resolve(instanceDir, a.RelPath);
                BeforeWrite?.Invoke(a);

                if (File.Exists(target))
                {
                    var saved = PathRules.Resolve(backupDir, a.RelPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Copy(target, saved, overwrite: true);
                    backup.Saved.Add(a.RelPath);
                }
                else backup.Created.Add(a.RelPath);
                WriteBackupManifest(backupDir, backup);

                if (a.Kind == FileActionKind.Remove) File.Delete(target);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    CopyAtomic(a.Mod != null ? jarPaths[a.RelPath] : a.OverrideSource!, target);
                }
            }
            // Serveurs du pack ajoutés à la liste multijoueur, sauvegardée avant comme les autres fichiers
            if (plan.ServersToAdd.Count > 0)
            {
                var rel = ServerList.FileName;
                var target = PathRules.Resolve(instanceDir, rel);
                if (!backup.Saved.Contains(rel) && !backup.Created.Contains(rel))
                {
                    if (File.Exists(target))
                    {
                        File.Copy(target, PathRules.Resolve(backupDir, rel), overwrite: true);
                        backup.Saved.Add(rel);
                    }
                    else backup.Created.Add(rel);
                    WriteBackupManifest(backupDir, backup);
                }
                foreach (var s in ServerList.Ensure(target, plan.ServersToAdd))
                    Log.Info($"Serveur ajouté à la liste : {s.Name} ({s.Ip})");
            }
            progress?.Report(new ApplyProgress("apply", changes.Count, changes.Count, ""));

            // 4. État en dernier
            var state = new LauncherState
            {
                Version = plan.ToVersion,
                Mods = plan.PackMods,
                Overrides = plan.PackOverrides,
                AddedServers = plan.AddedServers,
                UpdatedUtc = DateTime.UtcNow,
            };
            AtomicFile.WriteAllText(Path.Combine(instanceDir, LauncherState.FileName),
                JsonSerializer.Serialize(state, JsonCtx.Default.LauncherState));
        }
        catch (Exception e)
        {
            Log.Error($"Échec de la mise à jour, retour arrière depuis {backupDir}", e);
            Rollback(instanceDir, backupDir, backup);
            throw;
        }

        if (backup.Saved.Count == 0) Directory.Delete(backupDir, true);
        PruneBackups(instanceDir);
        Log.Info($"Mise à jour {plan.ToVersion} appliquée");
    }

    static void CopyAtomic(string src, string target)
    {
        var tmp = target + ".cwtmp";
        File.Copy(src, tmp, overwrite: true);
        File.Move(tmp, target, overwrite: true);
    }

    static void WriteBackupManifest(string backupDir, BackupManifest b) =>
        AtomicFile.WriteAllText(Path.Combine(backupDir, "backup.json"), JsonSerializer.Serialize(b, JsonCtx.Default.BackupManifest));

    public static void Rollback(string instanceDir, string backupDir, BackupManifest backup)
    {
        foreach (var rel in backup.Created)
            TryRun(() => File.Delete(PathRules.Resolve(instanceDir, rel)), rel);
        foreach (var rel in backup.Saved)
            TryRun(() =>
            {
                var target = PathRules.Resolve(instanceDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(PathRules.Resolve(backupDir, rel), target, overwrite: true);
            }, rel);
    }

    static void TryRun(Action a, string rel)
    {
        try { a(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Error($"Retour arrière impossible pour {rel}", e); }
    }

    static void PruneBackups(string instanceDir)
    {
        var root = Path.Combine(instanceDir, BackupDirName);
        if (!Directory.Exists(root)) return;
        foreach (var old in Directory.GetDirectories(root).OrderByDescending(d => d, StringComparer.Ordinal).Skip(BackupsToKeep))
            TryRun(() => Directory.Delete(old, true), old);
    }
}
