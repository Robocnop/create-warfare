namespace CreateWarfare.Launcher.Core.Model;

public enum FileActionKind { Add, Replace, Remove, Same, Protected }

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

    public IEnumerable<FileAction> Changes => Actions.Where(a => a.Changes);
    public int Count(FileActionKind k) => Actions.Count(a => a.Kind == k);
    public long DownloadSize => Actions.Where(a => a.Mod != null && a.Kind is FileActionKind.Add or FileActionKind.Replace).Sum(a => a.Size);
    public bool IsEmpty => !Changes.Any() && ServersToAdd.Count == 0;
}
