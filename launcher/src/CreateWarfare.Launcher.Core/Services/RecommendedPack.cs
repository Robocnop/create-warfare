namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Pack de textures recommandé (PureBDcraft 256x). Redistribution interdite : le launcher ne le télécharge
/// ni ne l'embarque, il détecte seulement sa présence et renvoie vers bdcraft.net.
/// </summary>
public static class RecommendedPack
{
    /// <summary>Nom exact du zip attendu dans resourcepacks/ (le même que dans config/defaultoptions/options.txt).</summary>
    public const string FileName = "PureBDcraft 256x MC121.zip";
    /// <summary>Page de téléchargement officielle (bdcraft.net).</summary>
    public const string Url = "https://bdcraft.net/downloads/purebdcraft-minecraft/";

    /// <summary>Vrai si <c>resourcepacks/PureBDcraft 256x MC121.zip</c> existe dans l'instance (le contenu n'est pas vérifié).</summary>
    public static bool IsInstalled(string? instanceDir) =>
        !string.IsNullOrEmpty(instanceDir) && File.Exists(Path.Combine(instanceDir, "resourcepacks", FileName));
}
