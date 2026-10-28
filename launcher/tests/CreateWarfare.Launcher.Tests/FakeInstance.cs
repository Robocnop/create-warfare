using System.Security.Cryptography;
using System.Text;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

/// <summary>Dossier temporaire : une fausse instance, un faux zip extrait (overrides/) et un faux CDN.</summary>
public sealed class FakeInstance : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "cw-test-" + Guid.NewGuid().ToString("N")[..8]);
    public string Instance => Path.Combine(Root, "instance");
    public string Overrides => Path.Combine(Root, "zip", "overrides");
    public string Cdn => Path.Combine(Root, "cdn");
    public string Cache => Path.Combine(Root, "cache");

    public FakeInstance()
    {
        Directory.CreateDirectory(Instance);
        Directory.CreateDirectory(Overrides);
        Directory.CreateDirectory(Cdn);
    }

    public void Write(string rel, string content) => WriteAt(Instance, rel, content);
    public void WriteOverride(string rel, string content) => WriteAt(Overrides, rel, content);
    public string Read(string rel) => File.ReadAllText(Path.Combine(Instance, rel));
    public bool Exists(string rel) => File.Exists(Path.Combine(Instance, rel));

    static void WriteAt(string root, string rel, string content)
    {
        var p = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
    }

    public static string Sha1(string content) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    /// <summary>Publie un jar sur le faux CDN et renvoie son entrée de version.json.</summary>
    public ModEntry CdnMod(string file, string content)
    {
        var p = Path.Combine(Cdn, Sha1(content) + "-" + file);
        File.WriteAllText(p, content);
        return new ModEntry { File = file, Sha1 = Sha1(content), Size = content.Length, Urls = { p } };
    }

    public void WriteState(string version, IEnumerable<string> mods, IEnumerable<string> overrides) =>
        File.WriteAllText(Path.Combine(Instance, LauncherState.FileName),
            System.Text.Json.JsonSerializer.Serialize(new LauncherState { Version = version, Mods = mods.ToList(), Overrides = overrides.ToList() },
                JsonCtx.Default.LauncherState));

    /// <summary>Empreinte de toute l'instance (hors sauvegardes) pour vérifier un retour arrière.</summary>
    public Dictionary<string, string> Snapshot() =>
        Directory.EnumerateFiles(Instance, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(UpdateApplier.BackupDirName))
            .ToDictionary(f => PathRules.Normalize(Path.GetRelativePath(Instance, f)), Hashing.Sha256);

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }
}
