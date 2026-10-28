using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Liste des serveurs multijoueur (servers.dat). Le launcher n'y fait qu'ajouter les serveurs du pack qui manquent,
/// sans toucher aux autres entrées ni à leur ordre.
/// </summary>
public static class ServerList
{
    public const string FileName = "servers.dat";

    public static List<string> Ips(string path) =>
        File.Exists(path) ? Servers(Nbt.ReadFile(path)).Select(s => s.GetString("ip") ?? "").ToList() : new();

    static IEnumerable<NbtCompound> Servers(NbtCompound root) =>
        root.Get("servers")?.Value is NbtList { ElementType: Nbt.Compound } l ? l.Items.Cast<NbtCompound>() : Enumerable.Empty<NbtCompound>();

    /// <summary>Ajoute à la fin les serveurs absents (comparés par adresse, sans tenir compte de la casse). Renvoie ceux ajoutés.</summary>
    public static List<ServerEntry> Ensure(string path, IEnumerable<ServerEntry> wanted)
    {
        var root = File.Exists(path) ? Nbt.ReadFile(path) : new NbtCompound();
        if (root.Get("servers")?.Value is not NbtList { ElementType: Nbt.Compound or Nbt.End } list)
        {
            list = new NbtList();
            root.Set("servers", new NbtTag(Nbt.List, list));
        }
        list.ElementType = Nbt.Compound;
        var have = list.Items.Cast<NbtCompound>().Select(s => s.GetString("ip") ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<ServerEntry>();
        foreach (var s in wanted)
        {
            if (!have.Add(s.Ip)) continue;
            list.Items.Add(new NbtCompound()
                .Set("name", new NbtTag(Nbt.String, s.Name))
                .Set("ip", new NbtTag(Nbt.String, s.Ip)));
            added.Add(s);
        }
        if (added.Count > 0) Nbt.WriteFile(path, root);
        return added;
    }
}
