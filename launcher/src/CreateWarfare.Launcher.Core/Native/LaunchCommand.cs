using System.Text;
using System.Text.RegularExpressions;

namespace CreateWarfare.Launcher.Core.Native;

/// <summary>Ce qu'il faut pour construire la ligne de commande du jeu.</summary>
public sealed class LaunchOptions
{
    public required string JavaExe { get; init; }
    /// <summary>Dossier de l'instance (--gameDir, dossier de travail du processus).</summary>
    public required string GameDir { get; init; }
    /// <summary>Racine contenant libraries/, assets/ et versions/.</summary>
    public required string GameRoot { get; init; }
    public required string NativesDir { get; init; }
    public required GameSession Session { get; init; }
    /// <summary>Mémoire maximale (-Xmx), en Mo.</summary>
    public required int RamMb { get; init; }
    /// <summary>--clientId (identifiant d'installation pour la télémétrie de Mojang).</summary>
    public string ClientId { get; init; } = "0";
    public int Width { get; init; } = NativeConfig.WindowWidth;
    public int Height { get; init; } = NativeConfig.WindowHeight;
    /// <summary>« hôte:port » : connexion directe au serveur au démarrage (--quickPlayMultiplayer), ou null.</summary>
    public string? QuickPlayServer { get; init; }
    public string LauncherName { get; init; } = "CreateWarfareLauncher";
    public string LauncherVersion { get; init; } = "2";
    /// <summary>Arguments Java en plus (javaArgsOverride de l'instance CurseForge, par exemple).</summary>
    public IReadOnlyList<string> ExtraJvmArgs { get; init; } = Array.Empty<string>();
    public OsInfo Os { get; init; } = OsInfo.Current;
}

/// <summary>Programme, arguments (un par élément, sans guillemets à ajouter) et dossier de travail.</summary>
public sealed record LaunchCommand(string JavaExe, IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    /// <summary>Ligne de commande pour le journal, jeton d'accès masqué.</summary>
    public string Redacted()
    {
        var sb = new StringBuilder(Quote(JavaExe));
        for (var i = 0; i < Arguments.Count; i++)
        {
            var a = Arguments[i];
            if (i > 0 && Arguments[i - 1] == "--accessToken") a = "***";
            sb.Append(' ').Append(Quote(a));
        }
        return sb.ToString();
    }

    public override string ToString() => Redacted();

    static string Quote(string s) => s.Length == 0 ? "\"\"" : s.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0 ? "\"" + s.Replace("\"", "\\\"") + "\"" : s;
}

/// <summary>
/// Ligne de commande du jeu, comme le launcher officiel (que CurseForge reproduit) : arguments JVM du profil vanilla puis
/// de NeoForge (module path, --add-opens…), mémoire, classe principale, arguments de jeu du profil vanilla puis de NeoForge.
/// Fonction pure : aucun accès disque.
/// </summary>
public static class LaunchCommandBuilder
{
    /// <summary>Options du launcher officiel pour un profil (ramassage G1) ; Java 21 les accepte sans avertissement.</summary>
    public static readonly IReadOnlyList<string> DefaultJvmArgs = new[]
    {
        "-XX:+UnlockExperimentalVMOptions", "-XX:+UseG1GC", "-XX:G1NewSizePercent=20", "-XX:G1ReservePercent=20",
        "-XX:MaxGCPauseMillis=50", "-XX:G1HeapRegionSize=32M",
    };

    /// <summary>
    /// Assemble : arguments JVM du profil, -Xms/-Xmx, options G1, arguments Java de l'instance (sans -Xmx/-Xms), classe principale,
    /// arguments de jeu. Lève InvalidDataException si le profil n'a pas de mainClass.
    /// </summary>
    public static LaunchCommand Build(GameVersion v, LaunchOptions o)
    {
        if (v.MainClass == null) throw new InvalidDataException($"Profil {v.Id} sans mainClass");
        var sep = o.Os.ClasspathSeparator;
        var libDir = Path.Combine(o.GameRoot, "libraries");
        var classpath = LibraryResolver.Resolve(v, o.Os).Where(l => !l.ExtractNatives)
            .Select(l => Native(Path.Combine(libDir, l.File.Path)))
            .Append(VersionJar(o.GameRoot, v.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var vars = new Dictionary<string, string>
        {
            ["natives_directory"] = o.NativesDir,
            ["launcher_name"] = o.LauncherName,
            ["launcher_version"] = o.LauncherVersion,
            ["classpath"] = string.Join(sep, classpath),
            ["classpath_separator"] = sep,
            ["library_directory"] = libDir,
            ["version_name"] = v.Id,
            ["auth_player_name"] = o.Session.Name,
            ["game_directory"] = o.GameDir,
            ["assets_root"] = Path.Combine(o.GameRoot, "assets"),
            ["game_assets"] = Path.Combine(o.GameRoot, "assets"),
            ["assets_index_name"] = v.AssetIndex?.Id ?? v.Assets ?? "legacy",
            ["auth_uuid"] = o.Session.Uuid,
            ["auth_access_token"] = o.Session.AccessToken,
            ["auth_session"] = o.Session.AccessToken,
            ["clientid"] = o.ClientId,
            ["auth_xuid"] = o.Session.Xuid,
            ["user_type"] = o.Session.UserType,
            ["user_properties"] = "{}",
            ["version_type"] = v.Type ?? "release",
            ["resolution_width"] = o.Width.ToString(),
            ["resolution_height"] = o.Height.ToString(),
            ["quickPlayMultiplayer"] = o.QuickPlayServer ?? "",
        };
        var features = new Dictionary<string, bool>
        {
            ["is_demo_user"] = false,
            ["has_custom_resolution"] = true,
            ["has_quick_plays_support"] = false,
            ["is_quick_play_singleplayer"] = false,
            ["is_quick_play_multiplayer"] = o.QuickPlayServer != null,
            ["is_quick_play_realms"] = false,
        };

        var args = new List<string>();
        foreach (var a in Expand(v.Jvm, o.Os, features, vars)) args.Add(a);
        NormalizeModulePath(args, sep);
        args.Add($"-Xms{Math.Min(512, o.RamMb)}m");
        args.Add($"-Xmx{o.RamMb}m");
        args.AddRange(DefaultJvmArgs);
        args.AddRange(o.ExtraJvmArgs.Where(a => !a.StartsWith("-Xmx", StringComparison.Ordinal) && !a.StartsWith("-Xms", StringComparison.Ordinal)));
        args.Add(v.MainClass);
        if (v.Game.Count > 0) args.AddRange(Expand(v.Game, o.Os, features, vars));
        else if (v.LegacyArguments != null)
            args.AddRange(v.LegacyArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => Substitute(t, vars)));
        return new LaunchCommand(o.JavaExe, args, o.GameDir);
    }

    /// <summary>Jar de la version (copie du client vanilla) : versions/&lt;id&gt;/&lt;id&gt;.jar. NeoForge l'ignore (-DignoreList).</summary>
    public static string VersionJar(string gameRoot, string id) => Path.Combine(gameRoot, "versions", id, id + ".jar");

    static IEnumerable<string> Expand(IEnumerable<ArgPart> parts, OsInfo os, IReadOnlyDictionary<string, bool> features, IReadOnlyDictionary<string, string> vars)
    {
        foreach (var p in parts)
        {
            if (!Rule.Allowed(p.Rules, os, features)) continue;
            foreach (var val in p.Values) yield return Substitute(val, vars);
        }
    }

    static readonly Regex Var = new(@"\$\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    /// <summary>Remplace les ${variables} connues ; une variable inconnue reste telle quelle (visible dans le journal).</summary>
    public static string Substitute(string s, IReadOnlyDictionary<string, string> vars) =>
        Var.Replace(s, m => vars.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>Le module path (-p) arrive avec des / venant du profil : même écriture que le classpath, sinon un jar y figurerait deux fois.</summary>
    static void NormalizeModulePath(List<string> args, string sep)
    {
        for (var i = 0; i < args.Count - 1; i++)
            if (args[i] is "-p" or "--module-path")
                args[i + 1] = string.Join(sep, args[i + 1].Split(sep, StringSplitOptions.RemoveEmptyEntries).Select(Native));
        for (var i = 0; i < args.Count; i++)
            if (args[i].StartsWith("-DlibraryDirectory=", StringComparison.Ordinal))
                args[i] = "-DlibraryDirectory=" + Native(args[i]["-DlibraryDirectory=".Length..]);
    }

    static string Native(string p) => Path.GetFullPath(p);

    /// <summary>Découpe des arguments Java saisis à la main (guillemets doubles acceptés).</summary>
    public static List<string> SplitArgs(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var cur = new StringBuilder();
        bool quoted = false, any = false;
        foreach (var ch in text)
        {
            if (ch == '"') { quoted = !quoted; any = true; continue; }
            if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (any || cur.Length > 0) list.Add(cur.ToString());
                cur.Clear();
                any = false;
                continue;
            }
            cur.Append(ch);
        }
        if (any || cur.Length > 0) list.Add(cur.ToString());
        return list;
    }
}
