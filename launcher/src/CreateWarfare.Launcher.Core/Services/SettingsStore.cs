using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;

namespace CreateWarfare.Launcher.Core.Services;

public static class SettingsStore
{
    public const string DefaultManifestUrl = "https://raw.githubusercontent.com/Robocnop/create-warfare/master/version.json";

    static string FilePath => Path.Combine(Log.AppDataDir, "settings.json");

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), JsonCtx.Default.LauncherSettings) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException) { Log.Error("settings.json illisible", e); }
        return new();
    }

    public static void Save(LauncherSettings s)
    {
        Directory.CreateDirectory(Log.AppDataDir);
        AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(s, JsonCtx.Default.LauncherSettings));
    }

    /// <summary>Variable CW_LAUNCHER_MANIFEST (tests locaux), puis réglage, puis dépôt public.</summary>
    public static string ManifestUrl(LauncherSettings s) =>
        Environment.GetEnvironmentVariable("CW_LAUNCHER_MANIFEST") is { Length: > 0 } env ? env
        : string.IsNullOrWhiteSpace(s.ManifestUrl) ? DefaultManifestUrl : s.ManifestUrl!;
}

public static class AtomicFile
{
    /// <summary>Écrit dans un .tmp puis remplace : jamais de fichier à moitié écrit.</summary>
    public static void WriteAllText(string path, string text)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, overwrite: true);
    }
}
