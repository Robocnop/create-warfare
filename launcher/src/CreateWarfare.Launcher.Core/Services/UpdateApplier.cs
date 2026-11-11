using System.Collections.Concurrent;
using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <param name="Bytes">Octets téléchargés (phase « download »), sur <paramref name="BytesTotal"/>.</param>
public sealed record ApplyProgress(string Phase, int Done, int Total, string Current, long Bytes = 0, long BytesTotal = 0);

/// <summary>Pas assez de place pour télécharger, écrire et sauvegarder : rien n'a été touché.</summary>
public sealed class InsufficientSpaceException(string message) : IOException(message);

/// <summary>La mise à jour a échoué et certains fichiers n'ont pas pu être remis : l'instance est à réparer.</summary>
public sealed class RollbackIncompleteException(string message, Exception inner, IReadOnlyList<string> notRestored, string backupDir)
    : IOException(message, inner)
{
    public IReadOnlyList<string> NotRestored { get; } = notRestored;
    public string BackupDir { get; } = backupDir;
}

public static class DiskSpace
{
    /// <summary>Octets libres sur le volume qui contient <paramref name="path"/> (ou son premier parent existant) ; null si inconnu.</summary>
    public static long? Free(string path)
    {
        try
        {
            var dir = Path.GetFullPath(path);
            while (!Directory.Exists(dir))
            {
                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || parent == dir) return null;
                dir = parent;
            }
            return new DriveInfo(dir).AvailableFreeSpace;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    public static string Format(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} Go",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} Mo",
        _ => $"{Math.Max(1, bytes / 1024)} Ko",
    };
}

/// <summary>
/// 0. vérifie qu'il y a la place de télécharger, d'écrire et de sauvegarder ;
/// 1. télécharge et vérifie tous les jars (plusieurs à la fois) avant de toucher à l'instance ;
/// 2. sauvegarde chaque fichier remplacé ou supprimé dans .cw-backup\&lt;horodatage&gt;\ ;
/// 3. applique ; en cas d'erreur, remet l'instance dans son état d'avant et dit si un fichier n'a pas pu l'être ;
/// 4. écrit .cw-launcher.json en dernier ; garde les 2 dernières sauvegardes.
/// </summary>
public sealed class UpdateApplier
{
    public const string BackupDirName = ".cw-backup";
    public const int BackupsToKeep = 2;
    const string TmpSuffix = ".cwtmp";

    readonly IJarProvider _jars;
    /// <summary>Point d'injection pour les tests : appelé avant chaque écriture.</summary>
    public Action<FileAction>? BeforeWrite { get; set; }
    /// <summary>Téléchargements simultanés.</summary>
    public int Parallelism { get; set; } = 4;
    /// <summary>Espace libre d'un dossier (remplaçable dans les tests) ; null = inconnu, pas de contrôle.</summary>
    public Func<string, long?> FreeSpace { get; set; } = DiskSpace.Free;

    public UpdateApplier(IJarProvider jars) => _jars = jars;

    public async Task ApplyAsync(string instanceDir, FilePlan plan, IProgress<ApplyProgress>? progress, CancellationToken ct)
    {
        var changes = plan.Changes.ToList();
        Log.Info($"Mise à jour {plan.FromVersion} → {plan.ToVersion} : {changes.Count} changements dans {instanceDir}");
        var downloads = changes.Where(a => a.Mod != null && a.Kind != FileActionKind.Remove).ToList();

        // 0. Place disponible
        CheckSpace(instanceDir, changes, downloads);

        // 1. Téléchargements, plusieurs à la fois
        var jarPaths = await DownloadAllAsync(downloads, progress, ct);

        // 2-3. Sauvegarde et application, sans annulation possible au milieu
        CleanLeftovers(instanceDir);
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
                // backup.json sert après un arrêt brutal : l'écrire à chaque fichier coûterait cher sur une grosse réparation
                if (i % 25 == 0) WriteBackupManifest(backupDir, backup);

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
                var target = BackupBeforeEdit(instanceDir, backupDir, backup, ServerList.FileName);
                foreach (var s in ServerList.Ensure(target, plan.ServersToAdd))
                    Log.Info($"Serveur ajouté à la liste : {s.Name} ({s.Ip})");
            }
            // Packs de ressources du pack activés une fois dans options.txt (seulement s'il existe déjà)
            if (plan.PacksToEnable.Count > 0 && File.Exists(PathRules.Resolve(instanceDir, OptionsFile.FileName)))
            {
                var target = BackupBeforeEdit(instanceDir, backupDir, backup, OptionsFile.FileName);
                foreach (var rp in OptionsFile.Enable(target, plan.PacksToEnable))
                    Log.Info($"Pack de ressources activé : {rp.File}");
            }
            WriteBackupManifest(backupDir, backup);
            progress?.Report(new ApplyProgress("apply", changes.Count, changes.Count, ""));

            // 4. État en dernier
            var state = new LauncherState
            {
                Version = plan.ToVersion,
                Mods = plan.PackMods,
                Overrides = plan.PackOverrides,
                AddedServers = plan.AddedServers,
                EnabledPacks = plan.EnabledPacks,
                OverrideHashes = plan.OverrideHashes,
                ModIds = plan.ModIds,
                UpdatedUtc = DateTime.UtcNow,
            };
            AtomicFile.WriteAllText(Path.Combine(instanceDir, LauncherState.FileName),
                JsonSerializer.Serialize(state, JsonCtx.Default.LauncherState));
        }
        catch (Exception e)
        {
            Log.Error($"Échec de la mise à jour, retour arrière depuis {backupDir}", e);
            var notRestored = Rollback(instanceDir, backupDir, backup);
            if (notRestored.Count > 0)
                throw new RollbackIncompleteException(
                    $"{e.Message}\n{notRestored.Count} fichier(s) n'ont pas pu être remis en place (copie dans {backupDir})", e, notRestored, backupDir);
            throw;
        }

        if (backup.Saved.Count == 0) Directory.Delete(backupDir, true);
        PruneBackups(instanceDir);
        Log.Info($"Mise à jour {plan.ToVersion} appliquée");
    }

    /// <summary>
    /// Refuse de commencer s'il manque de la place : téléchargements absents du cache, fichiers à écrire, copies de sauvegarde,
    /// avec 10 % de marge. Un disque plein au milieu de l'écriture est le pire cas : le retour arrière écrit lui aussi.
    /// </summary>
    void CheckSpace(string instanceDir, List<FileAction> changes, List<FileAction> downloads)
    {
        long toDownload = downloads.Where(a => !_jars.IsCached(a.Mod!)).Sum(a => a.Size);
        long toWrite = changes.Where(a => a.Kind != FileActionKind.Remove).Sum(a => a.Size);
        long toBackup = 0;
        foreach (var a in changes)
        {
            var target = PathRules.Resolve(instanceDir, a.RelPath);
            if (File.Exists(target)) toBackup += new FileInfo(target).Length;
        }
        static long Margin(long n) => n == 0 ? 0 : n + n / 10 + (16L << 20);

        var cacheDir = (_jars as Downloader)?.CacheDir;
        var instFree = FreeSpace(instanceDir);
        var cacheFree = cacheDir == null ? null : FreeSpace(cacheDir);
        var sameVolume = cacheDir == null || (OperatingSystem.IsWindows()
            ? string.Equals(Path.GetPathRoot(Path.GetFullPath(cacheDir)), Path.GetPathRoot(Path.GetFullPath(instanceDir)), StringComparison.OrdinalIgnoreCase)
            : cacheFree == instFree);

        if (sameVolume) Require(instanceDir, instFree, Margin(toDownload + toWrite + toBackup));
        else
        {
            Require(cacheDir!, cacheFree, Margin(toDownload));
            Require(instanceDir, instFree, Margin(toWrite + toBackup));
        }

        static void Require(string dir, long? free, long need)
        {
            if (free == null || need <= free) return;
            var where = OperatingSystem.IsWindows() ? Path.GetPathRoot(Path.GetFullPath(dir)) : dir;
            throw new InsufficientSpaceException(
                $"Espace disque insuffisant sur {where} : il faut {DiskSpace.Format(need)}, il reste {DiskSpace.Format(free.Value)}. " +
                $"Libère au moins {DiskSpace.Format(need - free.Value)}, puis réessaie");
        }
    }

    sealed class Counters { public long Bytes; public int Count; public long LastReport; }
    sealed class DirectProgress(Action<long> report) : IProgress<long> { public void Report(long value) => report(value); }

    async Task<IDictionary<string, string>> DownloadAllAsync(List<FileAction> downloads, IProgress<ApplyProgress>? progress, CancellationToken ct)
    {
        var jarPaths = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long total = downloads.Sum(a => a.Size);
        var c = new Counters();

        void Report(string current, bool force)
        {
            if (progress == null) return;
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref c.LastReport);
            if (!force && (now - last < 100 || Interlocked.CompareExchange(ref c.LastReport, now, last) != last)) return;
            progress.Report(new ApplyProgress("download", Volatile.Read(ref c.Count), downloads.Count, current,
                Math.Min(Interlocked.Read(ref c.Bytes), total), total));
        }

        Report(downloads.Count > 0 ? downloads[0].Mod!.File : "", force: true);
        await Parallel.ForEachAsync(downloads,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Parallelism), CancellationToken = ct },
            async (a, token) =>
            {
                long got = 0;
                var bytes = new DirectProgress(n =>
                {
                    Interlocked.Add(ref got, n);
                    Interlocked.Add(ref c.Bytes, n);
                    Report(a.Mod!.File, force: false);
                });
                jarPaths[a.RelPath] = await _jars.GetJarAsync(a.Mod!, bytes, token);
                // jar déjà en cache, repris, ou retéléchargé après une coupure : on recale le compteur sur sa taille
                Interlocked.Add(ref c.Bytes, a.Size - Interlocked.Read(ref got));
                Interlocked.Increment(ref c.Count);
                Report(a.Mod!.File, force: true);
            });
        progress?.Report(new ApplyProgress("download", downloads.Count, downloads.Count, "", total, total));
        return jarPaths;
    }

    /// <summary>Fichiers temporaires laissés dans mods/ par un arrêt brutal pendant une écriture.</summary>
    static void CleanLeftovers(string instanceDir)
    {
        var mods = Path.Combine(instanceDir, "mods");
        if (!Directory.Exists(mods)) return;
        foreach (var f in Directory.EnumerateFiles(mods, "*" + TmpSuffix).ToList())
            TryRun(() => File.Delete(f), f);
    }

    /// <summary>Sauvegarde un fichier que le launcher va modifier sur place (une seule fois par mise à jour) ; renvoie son chemin.</summary>
    static string BackupBeforeEdit(string instanceDir, string backupDir, BackupManifest backup, string rel)
    {
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
        return target;
    }

    static void CopyAtomic(string src, string target)
    {
        var tmp = target + TmpSuffix;
        try
        {
            File.Copy(src, tmp, overwrite: true);
            File.Move(tmp, target, overwrite: true);
        }
        catch
        {
            TryRun(() => File.Delete(tmp), tmp);
            throw;
        }
    }

    static void WriteBackupManifest(string backupDir, BackupManifest b) =>
        AtomicFile.WriteAllText(Path.Combine(backupDir, "backup.json"), JsonSerializer.Serialize(b, JsonCtx.Default.BackupManifest));

    /// <summary>Remet l'instance comme avant. Renvoie les fichiers qui n'ont pas pu être remis (liste vide = tout est revenu).</summary>
    public static List<string> Rollback(string instanceDir, string backupDir, BackupManifest backup)
    {
        var failed = new List<string>();
        foreach (var rel in backup.Created)
            if (!TryRun(() => File.Delete(PathRules.Resolve(instanceDir, rel)), rel)) failed.Add(rel);
        foreach (var rel in backup.Saved)
            if (!TryRun(() =>
                {
                    var target = PathRules.Resolve(instanceDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(PathRules.Resolve(backupDir, rel), target, overwrite: true);
                }, rel)) failed.Add(rel);
        return failed;
    }

    static bool TryRun(Action a, string rel)
    {
        try { a(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Opération impossible sur {rel}", e);
            return false;
        }
    }

    static void PruneBackups(string instanceDir)
    {
        var root = Path.Combine(instanceDir, BackupDirName);
        if (!Directory.Exists(root)) return;
        foreach (var old in Directory.GetDirectories(root).OrderByDescending(d => d, StringComparer.Ordinal).Skip(BackupsToKeep))
            TryRun(() => Directory.Delete(old, true), old);
    }
}
