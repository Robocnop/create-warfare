using System.Text.Json;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

/// <summary>Lancement par CurseForge : lien curseforge://launch-game (celui de ses raccourcis Bureau) et lecture de sa méthode de lancement.</summary>
public class CurseForgeLaunchTests
{
    const string Guid1 = "ae86dee3-5891-4a34-9135-9ba84fea7739";

    [Fact]
    public void Le_lien_est_celui_des_raccourcis_de_CurseForge()
    {
        Assert.Equal($"curseforge://launch-game?instanceId={Guid1}&gameId=432", CurseForgeLocator.LaunchUri(Guid1));
        // guid normalisé (accolades, majuscules) : CurseForge compare la chaîne telle quelle à minecraftinstance.json
        Assert.Equal(CurseForgeLocator.LaunchUri(Guid1), CurseForgeLocator.LaunchUri("{" + Guid1.ToUpperInvariant() + "}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ae86dee3-5891-4a34-9135-9ba84fea7739&gameId=1")]
    [InlineData("\" --inspect=9229 \"")]
    public void Un_guid_invalide_est_refuse(string? guid)
    {
        Assert.Throws<ArgumentException>(() => CurseForgeLocator.LaunchUri(guid));
    }

    [Fact]
    public void CurseForge_recoit_le_lien_en_un_seul_argument()
    {
        var exe = Path.Combine(Path.GetTempPath(), "CurseForge Windows", "CurseForge.exe");
        var psi = CurseForgeLocator.LaunchStartInfo(exe, Guid1);
        Assert.Equal(exe, psi.FileName);
        Assert.Equal(new[] { CurseForgeLocator.LaunchUri(Guid1) }, psi.ArgumentList);
        Assert.False(psi.UseShellExecute);
        Assert.Equal(Path.GetDirectoryName(exe), psi.WorkingDirectory);
    }

    static string Storage(string dir, object? minecraftSettings, bool asString = true)
    {
        var path = Path.Combine(dir, "storage.json");
        var root = new Dictionary<string, object?>();
        if (minecraftSettings != null)
            root["minecraft-settings"] = asString ? JsonSerializer.Serialize(minecraftSettings) : minecraftSettings;
        // un jeton factice à côté : la lecture ne doit s'appuyer que sur minecraft-settings
        root["game-user-info"] = "{\"accessToken\":\"ne-pas-lire\"}";
        File.WriteAllText(path, JsonSerializer.Serialize(root));
        return path;
    }

    [Theory]
    [InlineData(1, CurseForgeLaunchMethod.CurseForge)]
    [InlineData(0, CurseForgeLaunchMethod.MojangLauncher)]
    [InlineData(7, CurseForgeLaunchMethod.Unknown)]
    public void Methode_de_lancement_lue_dans_storage_json(int value, CurseForgeLaunchMethod expected)
    {
        using var f = new FakeInstance();
        Assert.Equal(expected, CurseForgeLocator.ReadLaunchMethod(Storage(f.Root, new { gameLaunchMethod = value })));
        Assert.Equal(expected, CurseForgeLocator.ReadLaunchMethod(Storage(f.Root, new { gameLaunchMethod = value }, asString: false)));
    }

    [Fact]
    public void Methode_inconnue_si_storage_json_manque_ou_est_illisible()
    {
        using var f = new FakeInstance();
        Assert.Equal(CurseForgeLaunchMethod.Unknown, CurseForgeLocator.ReadLaunchMethod(Path.Combine(f.Root, "absent.json")));
        Assert.Equal(CurseForgeLaunchMethod.Unknown, CurseForgeLocator.ReadLaunchMethod(Storage(f.Root, null)));
        Assert.Equal(CurseForgeLaunchMethod.Unknown, CurseForgeLocator.ReadLaunchMethod(Storage(f.Root, new { gameLaunchMethod = "1" })));
        var broken = Path.Combine(f.Root, "broken.json");
        File.WriteAllText(broken, "{\"minecraft-settings\":\"{pas du json\"}");
        Assert.Equal(CurseForgeLaunchMethod.Unknown, CurseForgeLocator.ReadLaunchMethod(broken));
        File.WriteAllText(broken, "{\"minecraft-settings\":\"[1,2]\"}");
        Assert.Equal(CurseForgeLaunchMethod.Unknown, CurseForgeLocator.ReadLaunchMethod(broken));
    }

    [Fact]
    public void Le_dossier_Minecraft_se_lit_toujours_avec_la_methode_de_lancement()
    {
        using var f = new FakeInstance();
        var storage = Storage(f.Root, new { gameLaunchMethod = 1, minecraftRoot = f.Instance });
        Assert.Equal(f.Instance, CurseForgeLocator.FindMinecraftRoot(storage));
        Assert.Equal(CurseForgeLaunchMethod.CurseForge, CurseForgeLocator.ReadLaunchMethod(storage));
    }
}
