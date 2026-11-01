using System.Text;
using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// options.txt de Minecraft. Le launcher n'y touche qu'une ligne, resourcePacks, pour activer les packs de ressources
/// du pack ; toutes les autres lignes restent identiques à l'octet près.
/// </summary>
public static class OptionsFile
{
    public const string FileName = "options.txt";
    const string Key = "resourcePacks:";

    /// <summary>Packs actifs, comme les écrit Minecraft (« vanilla », « file/x.zip »…), du moins au plus prioritaire.</summary>
    public static List<string> Packs(string path)
    {
        if (!File.Exists(path)) return new();
        var line = File.ReadAllLines(path, Encoding.UTF8).FirstOrDefault(l => l.StartsWith(Key, StringComparison.Ordinal));
        return line == null ? new() : Parse(line[Key.Length..].TrimEnd('\r')) ?? new();
    }

    /// <summary>
    /// Active les packs absents. Un pack dont une version précédente est active prend sa place ; sinon il est mis
    /// juste au-dessus des packs des mods, sous ceux du joueur. Renvoie les packs activés. Sans options.txt (Default
    /// Options l'écrira au premier lancement avec les packs du pack) ou avec une ligne illisible, ne fait rien.
    /// </summary>
    public static List<ResourcePackEntry> Enable(string path, IEnumerable<ResourcePackEntry> wanted)
    {
        var enabled = new List<ResourcePackEntry>();
        if (!File.Exists(path)) return enabled;
        var lines = File.ReadAllText(path, Encoding.UTF8).Split('\n').ToList();
        var idx = lines.FindIndex(l => l.StartsWith(Key, StringComparison.Ordinal));
        var cr = idx >= 0 && lines[idx].EndsWith('\r');
        var packs = idx >= 0 ? Parse(lines[idx][Key.Length..].TrimEnd('\r')) : new List<string> { "vanilla" };
        if (packs == null)
        {
            Log.Info($"{path} : ligne resourcePacks illisible, packs non activés");
            return enabled;
        }

        foreach (var rp in wanted)
        {
            var name = "file/" + rp.File;
            if (packs.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            bool IsPrevious(string p) => rp.Previous.Any(prev => p.Equals("file/" + prev, StringComparison.OrdinalIgnoreCase));
            var old = packs.FindIndex(IsPrevious);
            if (old >= 0) packs[old] = name;
            else
            {
                var anchor = Math.Max(packs.IndexOf("mod_resources"), packs.IndexOf("vanilla"));
                packs.Insert(anchor + 1, name);
            }
            packs.RemoveAll(IsPrevious); // une autre ancienne version encore active ferait doublon
            enabled.Add(rp);
        }
        if (enabled.Count == 0) return enabled;

        var newLine = Key + Serialize(packs) + (cr ? "\r" : "");
        if (idx >= 0) lines[idx] = newLine;
        else lines.Insert(lines.Count > 0 && lines[^1].Length == 0 ? lines.Count - 1 : lines.Count, newLine);
        AtomicFile.WriteAllText(path, string.Join('\n', lines));
        return enabled;
    }

    static List<string>? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String) return null;
                list.Add(e.GetString()!);
            }
            return list;
        }
        catch (JsonException) { return null; }
    }

    static string Serialize(List<string> packs)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartArray();
            foreach (var p in packs) w.WriteStringValue(p);
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
