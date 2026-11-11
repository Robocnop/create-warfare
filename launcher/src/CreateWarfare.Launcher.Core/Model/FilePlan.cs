namespace CreateWarfare.Launcher.Core.Model;

/// <summary>Kept : fichier du pack modifié par le joueur alors que le pack ne l'a pas changé depuis la dernière installation.</summary>
public enum FileActionKind { Add, Replace, Remove, Same, Protected, Kept }

/// <param name="OverrideSource">Fichier d'override extrait du zip, sinon null.</param>
/// <param name="Mod">Jar à télécharger, sinon null.</param>
public sealed record FileAction(
    string RelPath,
    FileActionKind Kind,
    long Size,
    string? OverrideSource = null,
    ModEntry? Mod = null)
{
    public bool Changes => Kind is FileActionKind.Add or FileActionKind.Replace or FileActionKind.Remove;
}

public sealed class FilePlan
{
    public required string FromVersion { get; init; }
    public required string ToVersion { get; init; }
    public required List<FileAction> Actions { get; init; }
    /// <summary>Jars et overrides du pack après application (vont dans l'état).</summary>
    public required List<string> PackMods { get; init; }
    public required List<string> PackOverrides { get; init; }
    /// <summary>Serveurs à ajouter à servers.dat.</summary>
    public List<ServerEntry> ServersToAdd { get; init; } = new();
    /// <summary>Adresses à mémoriser dans l'état après application.</summary>
    public List<string> AddedServers { get; init; } = new();
    /// <summary>Packs de ressources à activer dans options.txt (jamais proposés avant).</summary>
    public List<ResourcePackEntry> PacksToEnable { get; init; } = new();
    /// <summary>Packs à mémoriser dans l'état après application.</summary>
    public List<string> EnabledPacks { get; init; } = new();
    /// <summary>sha256 de chaque override livré par le pack (va dans l'état).</summary>
    public Dictionary<string, string> OverrideHashes { get; init; } = new();
    /// <summary>Identifiant de mod → nom du jar de cette version (va dans l'état).</summary>
    public Dictionary<string, string> ModIds { get; init; } = new();

    public IEnumerable<FileAction> Changes => Actions.Where(a => a.Changes);
    public int Count(FileActionKind k) => Actions.Count(a => a.Kind == k);
    public long DownloadSize => Actions.Where(a => a.Mod != null && a.Kind is FileActionKind.Add or FileActionKind.Replace).Sum(a => a.Size);
    public bool IsEmpty => !Changes.Any() && ServersToAdd.Count == 0 && PacksToEnable.Count == 0;
}
