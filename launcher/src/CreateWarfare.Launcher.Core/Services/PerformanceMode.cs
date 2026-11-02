namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Mode performance : désactive les mods visuels lourds du client en renommant leurs jars en .jar.disabled
/// (CurseForge et le planificateur de mise à jour gardent ces jars désactivés).
/// Les mods sont reconnus par le début de leur nom de fichier, qui ne change pas d'une version à l'autre.
/// Seuls les mods désactivés par le launcher sont réactivés : un mod que le joueur avait désactivé lui-même le reste.
/// Liste vérifiée le 02/11/2026 (pack 1.8.2) : aucun autre mod du pack ne dépend de ceux-ci, sauf leurs compats
/// (emf_compat_* → EMF, pfsable → Presence Footsteps), désactivées avec eux.
/// </summary>
public static class PerformanceMode
{
    public static readonly string[] Prefixes =
    {
        "entity_model_features",      // EMF (modèles de Fresh Animations)
        "entity_texture_features",    // ETF
        "emf_compat_",                // compats EMF (dépendent d'EMF)
        "visuality",
        "ParticleEffects",
        "explosiveenhancement",
        "BetterAnimationsCollection",
        "skinlayers3d",               // 3D Skin Layers
        "PresenceFootsteps",
        "pfsable",                    // Presence Footsteps x Sable (dépend de Presence Footsteps)
        "fallingleaves",
        "particular-",
        "effectual-",
        "sound-physics-remastered",
    };

    const string Disabled = ".disabled";

    public static string? PrefixOf(string fileName) =>
        Prefixes.FirstOrDefault(p => fileName.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Applique le mode. <paramref name="ours"/> = préfixes désactivés par le launcher (gardés dans les réglages) :
    /// complété quand on active, vidé quand on désactive. Renvoie le nombre de jars renommés.
    /// </summary>
    public static int Apply(string instanceDir, bool on, ISet<string> ours)
    {
        var mods = Path.Combine(instanceDir, "mods");
        if (!Directory.Exists(mods)) return 0;
        var renamed = 0;
        foreach (var f in Directory.EnumerateFiles(mods).ToList())
        {
            var name = Path.GetFileName(f);
            var prefix = PrefixOf(name);
            if (prefix == null) continue;
            if (on && name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                // Jar actif : jamais désactivé, ou reposé par une mise à jour.
                File.Move(f, f + Disabled, overwrite: true);
                ours.Add(prefix);
                renamed++;
            }
            else if (!on && name.EndsWith(".jar" + Disabled, StringComparison.OrdinalIgnoreCase) && ours.Contains(prefix))
            {
                var target = f[..^Disabled.Length];
                if (File.Exists(target)) File.Delete(f); // le jar actif est déjà là
                else File.Move(f, target);
                renamed++;
            }
        }
        if (!on) ours.Clear();
        return renamed;
    }
}
