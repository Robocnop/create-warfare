using System.Text.Json;
using System.Text.Json.Nodes;

namespace CreateWarfare.Launcher.Core.Services;

// Prism Launcher (Linux) : où il range ses instances, et comment en créer une qu'il sait ouvrir.
// Format vérifié le 11/11/2026 dans le code de Prism (branche develop) :
//   - InstanceList.cpp : une instance = un dossier qui contient instance.cfg, avec InstanceType vide ou « OneSix » ;
//   - MinecraftInstance::gameRoot : le jeu est dans <instance>/minecraft (ou .minecraft si lui seul existe) ;
//   - PackProfile.cpp : mmc-pack.json = { "formatVersion": 1, "components": [ { "uid", "version", "important" } ] } ;
//   - BaseInstance.cpp : OverrideCommands + PreLaunchCommand, lancée après le chargement des composants et avant
//     l'analyse du dossier mods ; variables $INST_DIR, $INST_MC_DIR, $INST_JAVA (MinecraftInstance::createEnvironment) ;
//   - INIFile.cpp : un instance.cfg sans ConfigVersion est lu ligne par ligne (clé=valeur), puis converti par Prism.

/// <param name="Kind">« flatpak », « native » ou « custom » (dossier donné par le joueur, installation portable).</param>
public sealed record PrismInstall(string DataDir, string Kind)
{
    public string InstancesDir => PrismLocator.InstancesDir(DataDir);
    public string ConfigFile => Path.Combine(DataDir, "prismlauncher.cfg");
}

public static class PrismLocator
{
    public const string FlatpakId = "org.prismlauncher.PrismLauncher";
    const string DirName = "PrismLauncher";

    /// <summary>
    /// Installations de Prism trouvées, sans doublon. Un dossier donné par le joueur passe seul et sans condition ;
    /// sinon $XDG_DATA_HOME/PrismLauncher, ~/.local/share/PrismLauncher, puis le dossier du Flatpak.
    /// </summary>
    public static List<PrismInstall> Find(string home, string? xdgDataHome, string? explicitDir)
    {
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            var dir = Path.GetFullPath(explicitDir);
            // installation portable : le dossier choisi contient l'exécutable, les données sont à côté
            return Directory.Exists(dir) ? new() { new PrismInstall(dir, "custom") } : new();
        }

        var found = new List<PrismInstall>();
        void Add(string dir, string kind)
        {
            dir = Path.GetFullPath(dir);
            if (Directory.Exists(dir) && !found.Any(f => f.DataDir == dir)) found.Add(new PrismInstall(dir, kind));
        }
        if (!string.IsNullOrWhiteSpace(xdgDataHome)) Add(Path.Combine(xdgDataHome, DirName), "native");
        Add(Path.Combine(home, ".local", "share", DirName), "native");
        Add(Path.Combine(home, ".var", "app", FlatpakId, "data", DirName), "flatpak");
        return found;
    }

    /// <summary>
    /// S'il y en a plusieurs : celle qui a déjà l'instance, sinon celle dont la configuration a servi le plus récemment.
    /// </summary>
    public static PrismInstall? Choose(IReadOnlyList<PrismInstall> found)
    {
        if (found.Count == 0) return null;
        return found.FirstOrDefault(f => File.Exists(Path.Combine(f.InstancesDir, PrismInstance.FolderName, "instance.cfg")))
               ?? found.OrderByDescending(f => File.Exists(f.ConfigFile) ? File.GetLastWriteTimeUtc(f.ConfigFile) : DateTime.MinValue).First();
    }

    /// <summary>Dossier des instances : réglage InstanceDir de prismlauncher.cfg (relatif aux données, ou absolu), « instances » par défaut.</summary>
    public static string InstancesDir(string dataDir)
    {
        var value = IniLines.Get(Path.Combine(dataDir, "prismlauncher.cfg"), "InstanceDir");
        if (string.IsNullOrWhiteSpace(value)) value = "instances";
        return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(dataDir, value));
    }
}

public static class PrismInstance
{
    /// <summary>Nom du dossier = identifiant de l'instance pour « prismlauncher --launch ». Sans espace : pas de guillemets à gérer.</summary>
    public const string FolderName = "CreateWarfare";
    public const string DisplayName = "Create Warfare";
    /// <summary>Nom du binaire copié dans l'instance, lancé par Prism avant chaque partie.</summary>
    public const string UpdaterName = "create-warfare";
    /// <summary>
    /// Apostrophes : Prism les comprend (Commandline::splitArgs) et elles n'ont rien de spécial dans un fichier INI,
    /// alors que des guillemets devraient être échappés une fois le fichier réécrit par Prism.
    /// </summary>
    public const string PreLaunchCommand = "'$INST_DIR/" + UpdaterName + "' prelaunch";

    public const string MinecraftUid = "net.minecraft";
    public const string NeoForgeUid = "net.neoforged";

    /// <summary>Dossier du jeu, selon la même règle que Prism : .minecraft seulement s'il existe seul.</summary>
    public static string GameDir(string instanceDir)
    {
        var plain = Path.Combine(instanceDir, "minecraft");
        var dotted = Path.Combine(instanceDir, ".minecraft");
        return Directory.Exists(dotted) && !Directory.Exists(plain) ? dotted : plain;
    }

    /// <summary>
    /// Crée l'instance si elle manque, sinon ne corrige que ce qui est à nous : les versions de Minecraft et de NeoForge,
    /// et la commande de pré-lancement. La mémoire et les autres réglages du joueur ne sont écrits qu'à la création
    /// (ou si <paramref name="memoryMb"/> est donné explicitement). Renvoie vrai si l'instance vient d'être créée.
    /// </summary>
    /// <param name="resetCommand">Réparation : remet la commande de pré-lancement même si le joueur l'a changée.</param>
    public static bool Ensure(string instanceDir, string minecraft, string neoforge, int? memoryMb, int defaultMemoryMb, bool resetCommand = false)
    {
        if (string.IsNullOrWhiteSpace(minecraft) || string.IsNullOrWhiteSpace(neoforge))
            throw new InvalidDataException("version.json ne donne pas les versions de Minecraft et de NeoForge");
        var cfg = Path.Combine(instanceDir, "instance.cfg");
        var created = !File.Exists(cfg);
        Directory.CreateDirectory(GameDir(instanceDir));
        SyncComponents(instanceDir, minecraft, neoforge);

        if (created)
        {
            AtomicFile.WriteAllText(cfg, string.Join("\n",
                "InstanceType=OneSix",
                "name=" + DisplayName,
                "iconKey=default",
                "OverrideMemory=true",
                "MinMemAlloc=512",
                "MaxMemAlloc=" + (memoryMb ?? defaultMemoryMb),
                "OverrideCommands=true",
                "PreLaunchCommand=" + PreLaunchCommand,
                ""));
            return true;
        }

        var lines = File.ReadAllLines(cfg).ToList();
        var before = string.Join("\n", lines);
        var current = IniLines.Get(lines, "PreLaunchCommand");
        if (resetCommand || string.IsNullOrWhiteSpace(current) || current.Contains(UpdaterName, StringComparison.Ordinal))
        {
            IniLines.Set(lines, "OverrideCommands", "true");
            IniLines.Set(lines, "PreLaunchCommand", PreLaunchCommand);
        }
        if (memoryMb is { } mb)
        {
            IniLines.Set(lines, "OverrideMemory", "true");
            IniLines.Set(lines, "MaxMemAlloc", mb.ToString());
        }
        if (string.Join("\n", lines) != before) AtomicFile.WriteAllText(cfg, string.Join("\n", lines) + "\n");
        return false;
    }

    /// <summary>La commande de pré-lancement de l'instance est-elle la nôtre ?</summary>
    public static bool HasOurCommand(string instanceDir)
    {
        var cfg = Path.Combine(instanceDir, "instance.cfg");
        return IniLines.Get(cfg, "PreLaunchCommand")?.Contains(UpdaterName, StringComparison.Ordinal) == true
               && string.Equals(IniLines.Get(cfg, "OverrideCommands"), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Écrit ou met à jour mmc-pack.json. Seules les versions de Minecraft et de NeoForge sont touchées ; les autres
    /// composants (LWJGL, ajouts du joueur) restent. Renvoie vrai si une version a changé ou si le fichier a été créé.
    /// </summary>
    public static bool SyncComponents(string instanceDir, string minecraft, string neoforge)
    {
        var file = Path.Combine(instanceDir, "mmc-pack.json");
        JsonObject root;
        JsonArray components;
        try
        {
            root = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject() : new JsonObject();
        }
        catch (JsonException) { root = new JsonObject(); }
        if (root["components"] is JsonArray existing) components = existing;
        else
        {
            components = new JsonArray();
            root["components"] = components;
        }
        var changed = !File.Exists(file);
        changed |= SetComponent(components, MinecraftUid, minecraft, important: true, first: true);
        changed |= SetComponent(components, NeoForgeUid, neoforge, important: false, first: false);
        if (root["formatVersion"]?.GetValueKind() != JsonValueKind.Number)
        {
            root["formatVersion"] = 1;
            changed = true;
        }
        if (changed)
        {
            Directory.CreateDirectory(instanceDir);
            AtomicFile.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        return changed;
    }

    static bool SetComponent(JsonArray components, string uid, string version, bool important, bool first)
    {
        var c = components.OfType<JsonObject>().FirstOrDefault(o => o["uid"]?.GetValueKind() == JsonValueKind.String && o["uid"]!.GetValue<string>() == uid);
        if (c == null)
        {
            c = new JsonObject { ["uid"] = uid, ["version"] = version };
            if (important) c["important"] = true;
            if (first) components.Insert(0, c); else components.Add(c);
            return true;
        }
        var current = c["version"]?.GetValueKind() == JsonValueKind.String ? c["version"]!.GetValue<string>() : null;
        if (current == version) return false;
        c["version"] = version;
        // ce que Prism avait mis en cache pour l'ancienne version (nom, dépendances) ne vaut plus
        foreach (var key in new[] { "cachedVersion", "cachedName", "cachedRequires", "cachedConflicts", "cachedVolatile" }) c.Remove(key);
        return true;
    }

    /// <summary>Versions inscrites dans mmc-pack.json (null si absentes).</summary>
    public static (string? Minecraft, string? NeoForge) ReadComponents(string instanceDir)
    {
        try
        {
            var comps = (JsonNode.Parse(File.ReadAllText(Path.Combine(instanceDir, "mmc-pack.json"))) as JsonObject)?["components"] as JsonArray;
            string? Version(string uid) => comps?.OfType<JsonObject>()
                .FirstOrDefault(o => o["uid"]?.GetValueKind() == JsonValueKind.String && o["uid"]!.GetValue<string>() == uid)?["version"]?.GetValue<string>();
            return (Version(MinecraftUid), Version(NeoForgeUid));
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { return (null, null); }
    }
}

/// <summary>
/// Fichiers « clé=valeur » de Prism (instance.cfg, prismlauncher.cfg), dans leurs deux formes : lignes simples, ou
/// format QSettings avec une section [General]. On ne touche qu'aux lignes demandées, le reste est gardé tel quel.
/// </summary>
public static class IniLines
{
    public static string? Get(string file, string key)
    {
        try { return File.Exists(file) ? Get(File.ReadAllLines(file), key) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public static string? Get(IEnumerable<string> lines, string key)
    {
        foreach (var line in lines)
        {
            var eq = line.IndexOf('=');
            if (eq <= 0 || !line[..eq].Trim().Equals(key, StringComparison.Ordinal)) continue;
            var value = line[(eq + 1)..].Trim();
            // QSettings entoure de guillemets les valeurs qui contiennent ; , = ou des guillemets
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
            return value;
        }
        return null;
    }

    /// <summary>Remplace la ligne de la clé, ou l'ajoute (juste sous [General] s'il y a des sections).</summary>
    public static void Set(List<string> lines, string key, string value)
    {
        var text = key + "=" + value;
        for (int i = 0; i < lines.Count; i++)
        {
            var eq = lines[i].IndexOf('=');
            if (eq > 0 && lines[i][..eq].Trim().Equals(key, StringComparison.Ordinal))
            {
                lines[i] = text;
                return;
            }
        }
        var general = lines.FindIndex(l => l.Trim().Equals("[General]", StringComparison.OrdinalIgnoreCase));
        if (general >= 0) lines.Insert(general + 1, text);
        else
        {
            while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            lines.Add(text);
        }
    }
}
