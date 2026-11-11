using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Compare l'instance à la version visée, sans rien écrire. Règles :
/// - un fichier protégé n'est jamais écrasé ni supprimé, seulement créé s'il manque ;
/// - on ne supprime que ce que le pack a posé (état local, overrides de l'import CurseForge,
///   ou jar d'une ancienne version reconnu par son hash) : les jars du joueur ne sont jamais touchés ;
/// - un jar désactivé par le joueur (.jar.disabled) reste désactivé, y compris quand le mod change de version ;
/// - un fichier du pack que le joueur a modifié est gardé tant que le pack ne le change pas (sauf en réparation).
/// </summary>
public static class UpdatePlanner
{
    public static LauncherState? ReadState(string instanceDir)
    {
        var f = Path.Combine(instanceDir, LauncherState.FileName);
        try { return File.Exists(f) ? JsonSerializer.Deserialize(File.ReadAllText(f), JsonCtx.Default.LauncherState) : null; }
        catch (JsonException e) { Log.Error($"{f} illisible, ignoré", e); return null; }
    }

    /// <summary>Version installée : celle du launcher, sinon celle du zip importé dans CurseForge.</summary>
    public static string? InstalledVersion(string instanceDir, InstanceInfo? info) =>
        ReadState(instanceDir)?.Version ?? info?.ImportedVersion;

    /// <param name="repair">Réparation : tout fichier du pack différent est remis, même modifié par le joueur.</param>
    public static FilePlan Plan(string instanceDir, VersionInfo v, string overridesDir, LauncherState? state, InstanceInfo? info, bool repair = false)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        var actions = new List<FileAction>();
        var wanted = new HashSet<string>(cmp);

        // 1. Overrides du zip (config/, kubejs/, mods/ hors CurseForge…)
        var overrides = Directory.Exists(overridesDir)
            ? Directory.EnumerateFiles(overridesDir, "*", SearchOption.AllDirectories)
                .Select(f => (rel: PathRules.Normalize(Path.GetRelativePath(overridesDir, f)), src: f))
                .OrderBy(x => x.rel, cmp).ToList()
            : new();
        var overrideHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (rel, src) in overrides)
        {
            wanted.Add(rel);
            var target = PathRules.Resolve(instanceDir, rel);
            var size = new FileInfo(src).Length;
            var packHash = Hashing.Sha256(src);
            overrideHashes[rel] = packHash;
            FileActionKind kind;
            if (!File.Exists(target)) kind = FileActionKind.Add;
            else if (PathRules.IsProtected(rel, v.Protected)) kind = FileActionKind.Protected;
            else if (Hashing.Sha256(target) == packHash) kind = FileActionKind.Same;
            // Le pack livre le même fichier qu'à la dernière installation, mais celui de l'instance diffère :
            // c'est le joueur (ou le mod au lancement) qui l'a changé. On le garde. Les jars livrés dans overrides/mods sont toujours remis.
            else if (!repair && !rel.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)
                     && state?.OverrideHashes.TryGetValue(rel, out var installed) == true
                     && string.Equals(installed, packHash, StringComparison.OrdinalIgnoreCase)) kind = FileActionKind.Kept;
            else kind = FileActionKind.Replace;
            actions.Add(new FileAction(rel, kind, size, OverrideSource: src));
        }

        // 2. Jars téléchargés
        var modIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in v.Mods.OrderBy(m => m.File, cmp))
        {
            var rel = "mods/" + m.File;
            if (!wanted.Add(rel)) continue; // déjà livré dans overrides/mods
            if (m.Id.Length > 0) modIds[m.Id] = m.File;
            var target = PathRules.Resolve(instanceDir, rel);
            FileActionKind kind;
            if (File.Exists(target)) kind = Hashing.Matches(target, m.Sha1, sha1: true) ? FileActionKind.Same : FileActionKind.Replace;
            else if (File.Exists(target + ".disabled")) kind = FileActionKind.Same;
            else if (WasDisabled(instanceDir, m, state))
            {
                // Le joueur avait désactivé la version précédente de ce mod : la nouvelle arrive désactivée elle aussi.
                actions.Add(new FileAction(rel + ".disabled", FileActionKind.Add, m.Size, Mod: m));
                continue;
            }
            else kind = FileActionKind.Add;
            actions.Add(new FileAction(rel, kind, m.Size, Mod: m));
        }

        // 2b. Packs de ressources : créés s'ils manquent, jamais remplacés ni supprimés (resourcepacks/ est au joueur)
        foreach (var rp in v.ResourcePacks.OrderBy(r => r.File, cmp))
        {
            var rel = "resourcepacks/" + rp.File;
            if (!wanted.Add(rel)) continue;
            var target = PathRules.Resolve(instanceDir, rel);
            var kind = !File.Exists(target) ? FileActionKind.Add
                : Hashing.Matches(target, rp.Sha1, sha1: true) ? FileActionKind.Same : FileActionKind.Protected;
            actions.Add(new FileAction(rel, kind, rp.Size, Mod: rp.AsDownload()));
        }

        // 3. Suppressions : seulement ce que le pack avait posé
        var oldOverrides = state?.Overrides ?? info?.ModpackOverrides.ToList() ?? new List<string>();
        var candidates = new HashSet<string>(cmp);
        foreach (var o in oldOverrides) candidates.Add(PathRules.Normalize(o));
        foreach (var jar in state?.Mods ?? new()) { candidates.Add("mods/" + jar); candidates.Add("mods/" + jar + ".disabled"); }

        var retired = v.RetiredMods.GroupBy(r => r.File, cmp).ToDictionary(g => g.Key, g => g.Select(r => r.Sha1).ToHashSet(cmp), cmp);
        var modsDir = Path.Combine(instanceDir, "mods");
        if (Directory.Exists(modsDir))
            foreach (var f in Directory.EnumerateFiles(modsDir))
            {
                var name = Path.GetFileName(f);
                var baseName = StripDisabled(name);
                if (wanted.Contains("mods/" + baseName)) continue;
                if (retired.TryGetValue(baseName, out var hashes) && hashes.Contains(Hashing.Sha1(f)))
                    candidates.Add("mods/" + name);
            }

        foreach (var rel in candidates.OrderBy(x => x, cmp))
        {
            if (wanted.Contains(rel) || wanted.Contains(StripDisabled(rel)) || PathRules.IsProtected(rel, v.Protected)) continue;
            var target = PathRules.Resolve(instanceDir, rel);
            if (File.Exists(target)) actions.Add(new FileAction(rel, FileActionKind.Remove, new FileInfo(target).Length));
        }

        // 4. Serveurs du pack : proposés une seule fois, comparés à la liste qui existera après l'application
        var alreadyOffered = new HashSet<string>(state?.AddedServers ?? new(), cmp);
        var serversPath = PathRules.Resolve(instanceDir, ServerList.FileName);
        var futureList = File.Exists(serversPath) ? serversPath
            : overrides.FirstOrDefault(o => o.rel.Equals(ServerList.FileName, StringComparison.OrdinalIgnoreCase)).src;
        var present = new HashSet<string>(futureList == null ? new() : ServerList.Ips(futureList), cmp);
        var serversToAdd = v.Servers.Where(s => !alreadyOffered.Contains(s.Ip) && !present.Contains(s.Ip)).ToList();

        var packMods = wanted.Where(w => w.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) && !w[5..].Contains('/'))
            .Select(w => w[5..]).OrderBy(x => x, cmp).ToList();
        return new FilePlan
        {
            FromVersion = state?.Version ?? info?.ImportedVersion ?? "",
            ToVersion = v.Version,
            Actions = actions.OrderBy(a => a.Kind is FileActionKind.Same or FileActionKind.Protected or FileActionKind.Kept)
                .ThenBy(a => a.RelPath, cmp).ToList(),
            PackMods = packMods,
            PackOverrides = overrides.Select(o => o.rel).ToList(),
            ServersToAdd = serversToAdd,
            AddedServers = alreadyOffered.Union(v.Servers.Select(s => s.Ip), cmp).ToList(),
            PacksToEnable = PendingPacks(v, state),
            EnabledPacks = (state?.EnabledPacks ?? new()).Union(v.ResourcePacks.Select(r => r.File), cmp).ToList(),
            OverrideHashes = overrideHashes,
            ModIds = modIds,
        };
    }

    /// <summary>Vrai si l'état connaît ce mod sous un autre nom de jar, et que ce jar-là est désactivé (.jar.disabled sans .jar).</summary>
    static bool WasDisabled(string instanceDir, ModEntry m, LauncherState? state)
    {
        if (m.Id.Length == 0 || state == null || !state.ModIds.TryGetValue(m.Id, out var old)) return false;
        if (string.IsNullOrEmpty(old) || old.Equals(m.File, StringComparison.OrdinalIgnoreCase)) return false;
        var oldPath = PathRules.Resolve(instanceDir, "mods/" + old);
        return File.Exists(oldPath + ".disabled") && !File.Exists(oldPath);
    }

    /// <summary>Packs de ressources du pack jamais encore activés dans cette instance.</summary>
    public static List<ResourcePackEntry> PendingPacks(VersionInfo v, LauncherState? state)
    {
        var done = new HashSet<string>(state?.EnabledPacks ?? new(), StringComparer.OrdinalIgnoreCase);
        return v.ResourcePacks.Where(r => !done.Contains(r.File)).ToList();
    }

    static string StripDisabled(string name) =>
        name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? name[..^".disabled".Length] : name;
}
