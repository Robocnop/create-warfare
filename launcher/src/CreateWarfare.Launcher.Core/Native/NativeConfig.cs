using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Core.Native;

/// <summary>
/// Lancement natif (launcher 2.0, étape 2) : versions et empreintes figées du jeu, dossiers et réglages.
/// Les empreintes ont été relevées le 12/11/2026 sur les manifestes Mojang, le dépôt maven de NeoForge et l'installation
/// CurseForge de Robo ; les sorties de l'installateur NeoForge (patch du client) sont reproductibles octet pour octet.
/// </summary>
public static class NativeConfig
{
    public const string MinecraftVersion = "1.21.1";
    public const string NeoForgeVersion = "21.1.264";
    public const string NeoFormVersion = "1.21.1-20240808.144430";
    public static string VersionId => "neoforge-" + NeoForgeVersion;
    public const int JavaMajor = 21;

    /// <summary>versions/1.21.1/1.21.1.json (manifeste Mojang version_manifest_v2).</summary>
    public const string VanillaJsonSha1 = "9aa86ea608c3ec777dcdc09e6c5a06c85b53599e";
    public static string VanillaJsonUrl => $"https://piston-meta.mojang.com/v1/packages/{VanillaJsonSha1}/{MinecraftVersion}.json";

    /// <summary>neoforge-21.1.264-installer.jar (maven.neoforged.net, fichier .sha256 publié à côté).</summary>
    public const string InstallerSha256 = "d7c035dd5436a7c524dfbad6df191b0ef34179fd3edfee9dc99262af3b80239b";
    public static string InstallerUrl =>
        $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{NeoForgeVersion}/neoforge-{NeoForgeVersion}-installer.jar";

    /// <summary>
    /// Fichiers produits par l'installateur (aucune empreinte dans ses JSON) : client patché et ressources du client.
    /// Ce sont ceux que cherche ProductionClientProvider de FML 4 (« client-…-srg.jar » + patch neoforge:client dans latest.log),
    /// avec le jar universal, qui a son sha1 dans install_profile.json.
    /// </summary>
    public static readonly IReadOnlyList<(string Path, string Sha1)> InstallerOutputs = new[]
    {
        ($"net/neoforged/neoforge/{NeoForgeVersion}/neoforge-{NeoForgeVersion}-client.jar", "cb01ae604c907526cacc54f8967981ae46af35c5"),
        ($"net/minecraft/client/{NeoFormVersion}/client-{NeoFormVersion}-srg.jar", "a4827225b3c07662ca68b03ea20d11433a0c0488"),
        ($"net/minecraft/client/{NeoFormVersion}/client-{NeoFormVersion}-extra.jar", "db5c59932751d66c2f57c1c2de41b48712620975"),
    };

    public const string AssetsBaseUrl = "https://resources.download.minecraft.net/";

    /// <summary>Adresse du serveur pour la connexion directe (--quickPlayMultiplayer).</summary>
    public const string ServerAddress = "jeu.robocnop.be:27018";

    /// <summary>Taille de fenêtre au premier lancement, comme CurseForge (le jeu retient ensuite le plein écran).</summary>
    public const int WindowWidth = 1024, WindowHeight = 768;

    /// <summary>
    /// Bibliothèques, assets et versions du lancement natif : %LOCALAPPDATA%\CreateWarfareLauncher\minecraft,
    /// ou CW_GAME_ROOT (essais). Les fichiers déjà présents chez CurseForge y sont liés (lien physique) plutôt que copiés.
    /// </summary>
    public static string GameRoot =>
        Environment.GetEnvironmentVariable("CW_GAME_ROOT") is { Length: > 0 } env ? env : Path.Combine(Log.AppDataDir, "minecraft");

    /// <summary>Java 21 téléchargé par le launcher (Adoptium) : %LOCALAPPDATA%\CreateWarfareLauncher\java.</summary>
    public static string JavaRoot => Path.Combine(Log.AppDataDir, "java");

    /// <summary>
    /// Dossiers où des fichiers du jeu existent peut-être déjà : installation CurseForge (Install), puis launcher officiel.
    /// Chaque fichier y est vérifié par son empreinte avant d'être repris.
    /// </summary>
    public static IReadOnlyList<string> MirrorRoots(string? curseForgeMinecraftRoot)
    {
        var list = new List<string>();
        if (curseForgeMinecraftRoot != null) list.Add(Path.Combine(curseForgeMinecraftRoot, "Install"));
        list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"));
        return list.Where(Directory.Exists).ToList();
    }
}
