using System.Text.Json;

namespace CreateWarfare.Launcher.Core.Services;

/// <param name="ImportedVersion">Version du zip importé dans CurseForge (manifest.version).</param>
/// <param name="ModpackOverrides">Fichiers posés par l'import CurseForge, chemins avec des /.</param>
public sealed record InstanceInfo(
    string Path,
    string Name,
    string? Guid,
    string? ImportedVersion,
    DateTime LastPlayed,
    IReadOnlyList<string> ModpackOverrides);

public static class InstanceFinder
{
    public const string PackName = "Create Warfare";

    /// <summary>Lit un dossier d'instance ; null s'il n'a pas de minecraftinstance.json valide.</summary>
    public static InstanceInfo? Read(string dir)
    {
        var file = Path.Combine(dir, "minecraftinstance.json");
        if (!File.Exists(file)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var r = doc.RootElement;
            static string? Str(JsonElement e, string k) =>
                e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var overrides = new List<string>();
            if (r.TryGetProperty("modpackOverrides", out var o) && o.ValueKind == JsonValueKind.Array)
                overrides.AddRange(o.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => PathRules.Normalize(x.GetString()!)));

            var lastPlayed = r.TryGetProperty("lastPlayed", out var lp) && lp.ValueKind == JsonValueKind.String && lp.TryGetDateTime(out var d) ? d : DateTime.MinValue;
            var manifest = r.TryGetProperty("manifest", out var m) ? m : default;
            return new InstanceInfo(dir, Str(r, "name") ?? "", Str(r, "guid"), Str(manifest, "version"), lastPlayed, overrides);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Log.Info($"{file} illisible : {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Le dossier peut s'appeler « Create Warfare (1) » : on se fie au champ name. Priorité au guid mémorisé,
    /// sinon à l'instance jouée le plus récemment.
    /// </summary>
    public static InstanceInfo? Find(string? minecraftRoot, string? rememberedGuid)
    {
        var dir = minecraftRoot == null ? null : Path.Combine(minecraftRoot, "Instances");
        if (dir == null || !Directory.Exists(dir)) return null;
        var all = Directory.EnumerateDirectories(dir).Select(Read).OfType<InstanceInfo>().ToList();
        return all.FirstOrDefault(i => rememberedGuid != null && i.Guid == rememberedGuid)
            ?? all.Where(i => i.Name.Equals(PackName, StringComparison.OrdinalIgnoreCase))
                  .OrderByDescending(i => i.LastPlayed).FirstOrDefault();
    }
}
