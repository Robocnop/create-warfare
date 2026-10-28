using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class ServiceTests
{
    [Fact]
    public async Task Downloader_essaie_l_url_suivante_si_le_hash_est_faux()
    {
        using var f = new FakeInstance();
        var good = f.CdnMod("m.jar", "BON");
        var bad = Path.Combine(f.Cdn, "faux.jar");
        File.WriteAllText(bad, "CORROMPU");
        good.Urls.Insert(0, bad);

        var path = await new Downloader(f.Cache).GetJarAsync(good, null, default);
        Assert.Equal("BON", File.ReadAllText(path));
    }

    [Fact]
    public void Chemin_hors_instance_refuse()
    {
        using var f = new FakeInstance();
        Assert.Throws<InvalidDataException>(() => PathRules.Resolve(f.Instance, "../evade.txt"));
        Assert.Throws<InvalidDataException>(() => PathRules.Resolve(f.Instance, "config/../../evade.txt"));
    }

    [Theory]
    [InlineData("options.txt", true)]
    [InlineData("Options.TXT", true)]
    [InlineData("config/inventoryprofilesnext/x.json", true)]
    [InlineData("config\\inventoryprofilesnext\\x.json", true)]
    [InlineData("config/options.txt", false)]
    [InlineData("config/inventoryprofilesnextplus.json", false)]
    public void Regles_de_protection(string rel, bool expected) =>
        Assert.Equal(expected, PathRules.IsProtected(rel, new[] { "options.txt", "config/inventoryprofilesnext/" }));

    [Fact]
    public void Storage_json_de_CurseForge()
    {
        using var f = new FakeInstance();
        var storage = Path.Combine(f.Root, "storage.json");
        var inner = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["minecraftRoot"] = f.Instance });
        File.WriteAllText(storage, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["minecraft-settings"] = inner }));
        Assert.Equal(f.Instance, CurseForgeLocator.FindMinecraftRoot(storage));
    }

    [Fact]
    public void Instance_trouvee_par_son_nom_meme_si_le_dossier_a_un_suffixe()
    {
        using var f = new FakeInstance();
        var dir = Path.Combine(f.Root, "Instances", "Create Warfare (1)");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(f.Root, "Instances", "Create Warfare")); // dossier vide, sans json
        File.WriteAllText(Path.Combine(dir, "minecraftinstance.json"),
            "{\"name\":\"Create Warfare\",\"guid\":\"abc\",\"lastPlayed\":\"2026-10-28T07:24:11\",\"manifest\":{\"version\":\"1.3.1\"},\"modpackOverrides\":[\"config\\\\a.toml\"]}");

        var i = InstanceFinder.Find(f.Root, null)!;
        Assert.Equal(dir, i.Path);
        Assert.Equal("1.3.1", i.ImportedVersion);
        Assert.Equal(new[] { "config/a.toml" }, i.ModpackOverrides);
    }

    [Theory]
    [InlineData("1.5.1", "1.5.0", true)]
    [InlineData("v1.10.0", "1.9.9", true)]
    [InlineData("1.0.0-beta", "1.0.0", false)]
    public void Comparaison_de_versions(string a, string b, bool greater) =>
        Assert.Equal(greater, SelfUpdater.ParseVersion(a) > SelfUpdater.ParseVersion(b));
}
