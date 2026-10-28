namespace CreateWarfare.Launcher.Core.Model;

/// <summary>
/// &lt;instance&gt;\.cw-launcher.json : ce que le launcher a posé. Écrit en dernier, donc une coupure
/// laisse « mise à jour à refaire », jamais « faite à moitié et marquée faite ».
/// </summary>
public sealed class LauncherState
{
    public const string FileName = ".cw-launcher.json";

    public string Version { get; set; } = "";
    /// <summary>Noms des jars du pack (mods[] + overrides/mods).</summary>
    public List<string> Mods { get; set; } = new();
    /// <summary>Fichiers d'overrides du pack, chemins relatifs avec des /.</summary>
    public List<string> Overrides { get; set; } = new();
    /// <summary>Adresses déjà proposées dans servers.dat : si le joueur les supprime, elles ne reviennent pas.</summary>
    public List<string> AddedServers { get; set; } = new();
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>%LOCALAPPDATA%\CreateWarfareLauncher\settings.json</summary>
public sealed class LauncherSettings
{
    public string? InstancePath { get; set; }
    public string? ManifestUrl { get; set; }
}

/// <summary>.cw-backup\&lt;horodatage&gt;\backup.json : de quoi revenir en arrière.</summary>
public sealed class BackupManifest
{
    public string FromVersion { get; set; } = "";
    public string ToVersion { get; set; } = "";
    /// <summary>Fichiers copiés dans la sauvegarde (remplacés ou supprimés).</summary>
    public List<string> Saved { get; set; } = new();
    /// <summary>Fichiers créés par la mise à jour (à supprimer en cas de retour arrière).</summary>
    public List<string> Created { get; set; } = new();
}
