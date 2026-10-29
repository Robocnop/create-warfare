using System.Text.Json.Serialization;

namespace CreateWarfare.Launcher.Core.Model;

/// <summary>Contenu de version.json, publié par tools/publish_launcher.py.</summary>
public sealed class VersionInfo
{
    public string Pack { get; set; } = "Create Warfare";
    public string Version { get; set; } = "";
    public string Minecraft { get; set; } = "";
    public string Neoforge { get; set; } = "";
    public string MinLauncherVersion { get; set; } = "1.0.0";
    public LauncherRelease? Launcher { get; set; }
    public RemoteFile Zip { get; set; } = new();
    /// <summary>Jars à télécharger (côté both/client), hors jars livrés dans overrides/mods.</summary>
    public List<ModEntry> Mods { get; set; } = new();
    /// <summary>Jars des versions précédentes du pack : seuls eux peuvent être supprimés sans état local.</summary>
    public List<RetiredMod> RetiredMods { get; set; } = new();
    /// <summary>Chemins (préfixes si terminés par /) jamais écrasés ni supprimés, seulement créés s'ils manquent.</summary>
    public List<string> Protected { get; set; } = new();
    /// <summary>Serveurs ajoutés une fois à la liste multijoueur du joueur (servers.dat), sans toucher aux autres.</summary>
    public List<ServerEntry> Servers { get; set; } = new();
    public string? Notice { get; set; }
    public string Changelog { get; set; } = "";
    /// <summary>Pré-release du pack (même format), visible seulement en mode développeur. Ignorée par les launchers 1.0.x.</summary>
    public VersionInfo? Prerelease { get; set; }
}

public sealed class RemoteFile
{
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

public sealed class LauncherRelease
{
    public string Version { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

public sealed class ModEntry
{
    public string File { get; set; } = "";
    public string Sha1 { get; set; } = "";
    public long Size { get; set; }
    public List<string> Urls { get; set; } = new();
}

public sealed class ServerEntry
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
}

public sealed class RetiredMod
{
    public string File { get; set; } = "";
    public string Sha1 { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(LauncherState))]
[JsonSerializable(typeof(LauncherSettings))]
[JsonSerializable(typeof(BackupManifest))]
public partial class JsonCtx : JsonSerializerContext;
