using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Méthode de lancement choisie dans CurseForge (minecraft-settings.gameLaunchMethod de storage.json).</summary>
public enum CurseForgeLaunchMethod { Unknown, CurseForge, MojangLauncher }

public static class CurseForgeLocator
{
    /// <summary>Identifiant de Minecraft chez CurseForge (gameTypeID de minecraftinstance.json).</summary>
    public const int MinecraftGameId = 432;

    static string DefaultStorageJson =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CurseForge", "storage.json");

    /// <summary>
    /// %APPDATA%\CurseForge\storage.json, clé « minecraft-settings » : une chaîne JSON à re-parser,
    /// qui contient minecraftRoot. Repli : %USERPROFILE%\curseforge\minecraft.
    /// </summary>
    public static string? FindMinecraftRoot(string? storageJson = null)
    {
        var root = ReadMinecraftSetting(storageJson, "minecraftRoot", e => e.ValueKind == JsonValueKind.String ? e.GetString() : null);
        if (root != null && Directory.Exists(root)) return root;
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "curseforge", "minecraft");
        return Directory.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// Méthode de lancement de CurseForge : 1 = son propre lanceur (le seul qui accepte le lancement par lien),
    /// 0 = launcher Mojang. Seule cette clé est lue : les comptes et jetons de CurseForge ne sont jamais touchés.
    /// </summary>
    public static CurseForgeLaunchMethod ReadLaunchMethod(string? storageJson = null) =>
        ReadMinecraftSetting(storageJson, "gameLaunchMethod", e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? (int?)v : null) switch
        {
            1 => CurseForgeLaunchMethod.CurseForge,
            0 => CurseForgeLaunchMethod.MojangLauncher,
            _ => CurseForgeLaunchMethod.Unknown,
        };

    static T? ReadMinecraftSetting<T>(string? storageJson, string key, Func<JsonElement, T?> read)
    {
        storageJson ??= DefaultStorageJson;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(storageJson));
            if (doc.RootElement.TryGetProperty("minecraft-settings", out var s))
            {
                using var inner = JsonDocument.Parse(s.ValueKind == JsonValueKind.String ? s.GetString()! : s.GetRawText());
                if (inner.RootElement.ValueKind == JsonValueKind.Object && inner.RootElement.TryGetProperty(key, out var v))
                    return read(v);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Info($"storage.json illisible ({e.Message}), réglage {key} inconnu");
        }
        return default;
    }

    /// <summary>CurseForge.exe, via le protocole curseforge:// enregistré par l'installateur.</summary>
    public static string? FindExe()
    {
        try
        {
            var cmd = Registry.ClassesRoot.OpenSubKey(@"curseforge\shell\open\command")?.GetValue(null) as string;
            if (cmd != null)
            {
                var exe = cmd.StartsWith('"') ? cmd[1..cmd.IndexOf('"', 1)] : cmd.Split(' ')[0];
                if (File.Exists(exe)) return exe;
            }
        }
        catch (Exception e) { Log.Info($"Registre CurseForge illisible : {e.Message}"); }
        var guess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "CurseForge Windows", "CurseForge.exe");
        return File.Exists(guess) ? guess : null;
    }

    /// <summary>
    /// Lien de lancement d'une instance, celui que CurseForge écrit lui-même dans ses raccourcis Bureau
    /// (« Créer un raccourci » d'un modpack) : curseforge://launch-game?instanceId=&lt;guid&gt;&amp;gameId=432.
    /// CurseForge lance alors le jeu avec son compte Microsoft, sans clic sur Play. Le guid est celui de minecraftinstance.json.
    /// </summary>
    /// <exception cref="ArgumentException">guid absent ou mal formé (il finit dans une ligne de commande).</exception>
    public static string LaunchUri(string? instanceGuid)
    {
        if (!Guid.TryParse(instanceGuid, out var g))
            throw new ArgumentException($"guid d'instance invalide : « {instanceGuid} »", nameof(instanceGuid));
        return $"curseforge://launch-game?instanceId={g:D}&gameId={MinecraftGameId}";
    }

    /// <summary>
    /// CurseForge.exe avec le lien en argument : exactement ce que fait le protocole curseforge:// (« CurseForge.exe "%1" »).
    /// S'il tourne déjà, la nouvelle instance transmet le lien à la première (verrou d'instance unique d'Electron) et se ferme.
    /// </summary>
    public static ProcessStartInfo LaunchStartInfo(string exe, string? instanceGuid)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" };
        psi.ArgumentList.Add(LaunchUri(instanceGuid));
        return psi;
    }
}
