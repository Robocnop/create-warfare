using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CreateWarfare.Launcher.Core.Native;

/// <summary>Système vu par les règles des manifestes Mojang (os.name, os.arch, os.version).</summary>
public sealed record OsInfo(string Name, string Arch, string Version)
{
    public static OsInfo Current { get; } = new(
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "osx" : "linux",
        RuntimeInformation.OSArchitecture switch { Architecture.X86 => "x86", Architecture.Arm64 => "arm64", _ => "x86_64" },
        Environment.OSVersion.Version.ToString());

    public string ClasspathSeparator => Name == "windows" ? ";" : ":";
}

/// <summary>Règle « allow / disallow » d'une bibliothèque ou d'un argument.</summary>
public sealed record Rule(bool Allow, string? OsName, string? OsArch, string? OsVersion, IReadOnlyDictionary<string, bool> Features)
{
    bool Matches(OsInfo os, IReadOnlyDictionary<string, bool> features)
    {
        if (OsName != null && OsName != os.Name) return false;
        if (OsArch != null && OsArch != os.Arch) return false;
        if (OsVersion != null && !Regex.IsMatch(os.Version, OsVersion)) return false;
        foreach (var (k, v) in Features)
            if ((features.TryGetValue(k, out var have) && have) != v) return false;
        return true;
    }

    /// <summary>Sans règle : autorisé. Sinon, la dernière règle qui correspond décide (refus par défaut).</summary>
    public static bool Allowed(IReadOnlyList<Rule> rules, OsInfo os, IReadOnlyDictionary<string, bool>? features = null)
    {
        if (rules.Count == 0) return true;
        features ??= new Dictionary<string, bool>();
        var allowed = false;
        foreach (var r in rules)
            if (r.Matches(os, features)) allowed = r.Allow;
        return allowed;
    }
}

/// <summary>Morceau d'arguments (arguments.jvm / arguments.game) : une ou plusieurs valeurs, avec des règles éventuelles.</summary>
public sealed record ArgPart(IReadOnlyList<string> Values, IReadOnlyList<Rule> Rules);

/// <summary>Fichier téléchargeable : chemin relatif (avec des /), sha1, taille (-1 si inconnue), adresse.</summary>
public sealed record Artifact(string Path, string? Sha1, long Size, string? Url);

public sealed record AssetIndexRef(string Id, string Sha1, long Size, string Url);

public sealed record Library(string Name, Artifact? Artifact, IReadOnlyList<Rule> Rules,
    IReadOnlyDictionary<string, string>? Natives, IReadOnlyDictionary<string, Artifact>? Classifiers)
{
    /// <summary>« groupe:artefact[:classifieur] » : deux versions de la même bibliothèque ne vont jamais toutes deux sur le classpath.</summary>
    public string Key
    {
        get
        {
            var p = Maven.Split(Name);
            return p.Classifier == null ? $"{p.Group}:{p.Artifact}" : $"{p.Group}:{p.Artifact}:{p.Classifier}";
        }
    }
}

public sealed class GameVersion
{
    public string Id { get; init; } = "";
    public string? InheritsFrom { get; init; }
    public string? Type { get; init; }
    public string? MainClass { get; init; }
    public string? Assets { get; init; }
    public AssetIndexRef? AssetIndex { get; init; }
    public Artifact? Client { get; init; }
    public int? JavaMajor { get; init; }
    public IReadOnlyList<Library> Libraries { get; init; } = Array.Empty<Library>();
    public IReadOnlyList<ArgPart> Jvm { get; init; } = Array.Empty<ArgPart>();
    public IReadOnlyList<ArgPart> Game { get; init; } = Array.Empty<ArgPart>();
    /// <summary>Anciennes versions (avant 1.13) : minecraftArguments en une chaîne.</summary>
    public string? LegacyArguments { get; init; }
}

/// <summary>Lecture des JSON de version (vanilla et chargeur de mods) et fusion d'un profil avec son parent (inheritsFrom).</summary>
public static class VersionParser
{
    public static GameVersion Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var args = r.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
        return new GameVersion
        {
            Id = Str(r, "id") ?? throw new InvalidDataException("JSON de version sans id"),
            InheritsFrom = Str(r, "inheritsFrom"),
            Type = Str(r, "type"),
            MainClass = Str(r, "mainClass"),
            Assets = Str(r, "assets"),
            AssetIndex = Obj(r, "assetIndex") is { } ai && Str(ai, "id") is { } aid
                ? new AssetIndexRef(aid, Str(ai, "sha1") ?? "", Long(ai, "size") ?? -1, Str(ai, "url") ?? "") : null,
            Client = Obj(r, "downloads") is { } d && Obj(d, "client") is { } c ? ArtifactOf(c, "") : null,
            JavaMajor = Obj(r, "javaVersion") is { } jv ? (int?)Long(jv, "majorVersion") : null,
            Libraries = Arr(r, "libraries").Select(ParseLibrary).ToList(),
            Jvm = args.ValueKind == JsonValueKind.Object ? Arr(args, "jvm").Select(ParseArg).ToList() : new List<ArgPart>(),
            Game = args.ValueKind == JsonValueKind.Object ? Arr(args, "game").Select(ParseArg).ToList() : new List<ArgPart>(),
            LegacyArguments = Str(r, "minecraftArguments"),
        };
    }

    /// <summary>
    /// Profil enfant + parent, comme le launcher officiel : arguments du parent puis de l'enfant, bibliothèques de l'enfant
    /// d'abord ; une bibliothèque déjà fournie par l'enfant (même groupe:artefact:classifieur) n'est pas reprise du parent.
    /// </summary>
    public static GameVersion Merge(GameVersion child, GameVersion parent)
    {
        var libs = new List<Library>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var l in child.Libraries.Concat(parent.Libraries))
            if (keys.Add(l.Key + "|" + RuleSignature(l))) libs.Add(l);
        return new GameVersion
        {
            Id = child.Id,
            InheritsFrom = null,
            Type = child.Type ?? parent.Type,
            MainClass = child.MainClass ?? parent.MainClass,
            Assets = child.Assets ?? parent.Assets,
            AssetIndex = child.AssetIndex ?? parent.AssetIndex,
            Client = child.Client ?? parent.Client,
            JavaMajor = child.JavaMajor ?? parent.JavaMajor,
            Libraries = libs,
            Jvm = parent.Jvm.Concat(child.Jvm).ToList(),
            Game = parent.Game.Concat(child.Game).ToList(),
            LegacyArguments = child.LegacyArguments ?? parent.LegacyArguments,
        };
    }

    // Les natives LWJGL de 1.21.1 ont le même groupe:artefact:classifieur pour plusieurs OS seulement si leurs règles diffèrent :
    // la signature des règles garde ces variantes distinctes.
    static string RuleSignature(Library l) => string.Join(";", l.Rules.Select(r => $"{r.Allow}{r.OsName}{r.OsArch}"));

    static Library ParseLibrary(JsonElement e)
    {
        var name = Str(e, "name") ?? throw new InvalidDataException("bibliothèque sans nom");
        Artifact? artifact = null;
        Dictionary<string, Artifact>? classifiers = null;
        if (Obj(e, "downloads") is { } d)
        {
            if (Obj(d, "artifact") is { } art) artifact = ArtifactOf(art, Maven.Path(name));
            if (Obj(d, "classifiers") is { } cl)
            {
                classifiers = new();
                foreach (var p in cl.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Object) classifiers[p.Name] = ArtifactOf(p.Value, Maven.Path(name + ":" + p.Name));
            }
        }
        else
        {
            // Ancien format : seulement « name » et l'adresse d'un dépôt maven (url) ; pas d'empreinte
            var baseUrl = Str(e, "url") ?? "https://libraries.minecraft.net/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";
            var path = Maven.Path(name);
            artifact = new Artifact(path, null, -1, baseUrl + path);
        }
        Dictionary<string, string>? natives = null;
        if (Obj(e, "natives") is { } n)
        {
            natives = new();
            foreach (var p in n.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String) natives[p.Name] = p.Value.GetString()!;
        }
        return new Library(name, artifact, ParseRules(e), natives, classifiers);
    }

    static Artifact ArtifactOf(JsonElement e, string fallbackPath) => new(
        Str(e, "path") ?? fallbackPath, Str(e, "sha1"), Long(e, "size") ?? -1, Str(e, "url") is { Length: > 0 } u ? u : null);

    static ArgPart ParseArg(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String) return new ArgPart(new[] { e.GetString()! }, Array.Empty<Rule>());
        var values = e.TryGetProperty("value", out var v)
            ? v.ValueKind == JsonValueKind.String ? new List<string> { v.GetString()! }
              : v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
              : new List<string>()
            : new List<string>();
        return new ArgPart(values, ParseRules(e));
    }

    static IReadOnlyList<Rule> ParseRules(JsonElement e)
    {
        var list = new List<Rule>();
        foreach (var r in Arr(e, "rules"))
        {
            var os = Obj(r, "os");
            var features = new Dictionary<string, bool>();
            if (Obj(r, "features") is { } f)
                foreach (var p in f.EnumerateObject())
                    if (p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) features[p.Name] = p.Value.GetBoolean();
            list.Add(new Rule(Str(r, "action") != "disallow", os is { } o ? Str(o, "name") : null, os is { } o2 ? Str(o2, "arch") : null,
                os is { } o3 ? Str(o3, "version") : null, features));
        }
        return list;
    }

    static string? Str(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static long? Long(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;

    static JsonElement? Obj(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    static IEnumerable<JsonElement> Arr(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();
}

/// <summary>Coordonnées maven « groupe:artefact:version[:classifieur][@extension] ».</summary>
public static class Maven
{
    public sealed record Coords(string Group, string Artifact, string Version, string? Classifier, string Extension);

    public static Coords Split(string name)
    {
        var ext = "jar";
        var at = name.IndexOf('@');
        if (at >= 0) { ext = name[(at + 1)..]; name = name[..at]; }
        var p = name.Split(':');
        if (p.Length < 3) throw new InvalidDataException($"Coordonnées maven invalides : {name}");
        return new Coords(p[0], p[1], p[2], p.Length > 3 ? p[3] : null, ext);
    }

    /// <summary>Chemin relatif dans libraries/ (avec des /).</summary>
    public static string Path(string name)
    {
        var c = Split(name);
        var file = $"{c.Artifact}-{c.Version}{(c.Classifier == null ? "" : "-" + c.Classifier)}.{c.Extension}";
        return $"{c.Group.Replace('.', '/')}/{c.Artifact}/{c.Version}/{file}";
    }
}

/// <summary>Bibliothèque retenue pour ce système : chemin dans libraries/, et si c'est une native à extraire (ancien format).</summary>
public sealed record ResolvedLibrary(Library Source, Artifact File, bool ExtractNatives);

public static class LibraryResolver
{
    /// <summary>Bibliothèques autorisées par leurs règles pour <paramref name="os"/>, dans l'ordre du profil.</summary>
    public static List<ResolvedLibrary> Resolve(GameVersion v, OsInfo os)
    {
        var list = new List<ResolvedLibrary>();
        foreach (var l in v.Libraries)
        {
            if (!Rule.Allowed(l.Rules, os)) continue;
            if (l.Natives != null && l.Natives.TryGetValue(os.Name, out var classifier))
            {
                classifier = classifier.Replace("${arch}", os.Arch == "x86" ? "32" : "64");
                if (l.Classifiers != null && l.Classifiers.TryGetValue(classifier, out var nat)) list.Add(new ResolvedLibrary(l, nat, true));
                continue; // l'artefact principal d'une native ancienne n'existe pas toujours
            }
            if (l.Artifact != null) list.Add(new ResolvedLibrary(l, l.Artifact, false));
        }
        return list;
    }
}
