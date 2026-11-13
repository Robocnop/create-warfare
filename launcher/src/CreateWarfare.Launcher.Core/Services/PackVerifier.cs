using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Résultat d'une vérification : fichiers remis en place (ajoutés ou remplacés) et supprimés.</summary>
public sealed record VerifyResult(int Added, int Replaced, int Removed)
{
    public int Total => Added + Replaced + Removed;
}

/// <summary>
/// Vérification complète avant de jouer : compare l'instance à l'index du pack (sha1 des jars, sha256 des overrides)
/// avec <see cref="UpdatePlanner"/> et répare avec <see cref="UpdateApplier"/> (sauvegarde, retour arrière).
/// Les règles de l'import sont celles d'une mise à jour normale : options, saves et fichiers protégés ne sont jamais touchés,
/// un fichier de configuration modifié par le joueur est gardé tant que le pack ne l'a pas changé, les jars du joueur restent.
/// </summary>
public static class PackVerifier
{
    /// <summary>Plan de vérification, sans rien écrire.</summary>
    public static FilePlan Check(string instanceDir, VersionInfo v, string overridesDir, InstanceInfo? info) =>
        UpdatePlanner.Plan(instanceDir, v, overridesDir, UpdatePlanner.ReadState(instanceDir), info, repair: false);

    /// <summary>Compare et répare. Ne fait rien (sans réécrire l'état) si tout est conforme.</summary>
    public static async Task<VerifyResult> VerifyAndRepairAsync(string instanceDir, VersionInfo v, string overridesDir, InstanceInfo? info,
        IJarProvider jars, IProgress<ApplyProgress>? progress, CancellationToken ct)
    {
        var plan = await Task.Run(() => Check(instanceDir, v, overridesDir, info), ct);
        var changes = plan.Changes.ToList();
        if (changes.Count == 0)
        {
            Log.Info("Vérification : tous les fichiers sont conformes");
            return new VerifyResult(0, 0, 0);
        }
        Log.Info($"Vérification : {changes.Count} fichier(s) à réparer");
        await new UpdateApplier(jars).ApplyAsync(instanceDir, plan, progress, ct);
        return new VerifyResult(
            changes.Count(a => a.Kind == FileActionKind.Add),
            changes.Count(a => a.Kind == FileActionKind.Replace),
            changes.Count(a => a.Kind == FileActionKind.Remove));
    }
}
