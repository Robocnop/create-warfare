using System.Text.Json;
using Microsoft.Win32;

namespace CreateWarfare.Launcher.Core.Services;

public static class CurseForgeLocator
{
    /// <summary>
    /// %APPDATA%\CurseForge\storage.json, clé « minecraft-settings » : une chaîne JSON à re-parser,
    /// qui contient minecraftRoot. Repli : %USERPROFILE%\curseforge\minecraft.
    /// </summary>
    public static string? FindMinecraftRoot(string? storageJson = null)
    {
        storageJson ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CurseForge", "storage.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(storageJson));
            if (doc.RootElement.TryGetProperty("minecraft-settings", out var s))
            {
                using var inner = JsonDocument.Parse(s.ValueKind == JsonValueKind.String ? s.GetString()! : s.GetRawText());
                if (inner.RootElement.TryGetProperty("minecraftRoot", out var root) && Directory.Exists(root.GetString()))
                    return root.GetString();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Info($"storage.json illisible ({e.Message}), repli sur le dossier par défaut");
        }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "curseforge", "minecraft");
        return Directory.Exists(fallback) ? fallback : null;
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
}
