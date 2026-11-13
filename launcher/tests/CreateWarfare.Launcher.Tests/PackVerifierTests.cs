using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class PackVerifierTests
{
    static readonly string[] ProtectedRules = { "options.txt", "saves/" };

    static (FakeInstance f, VersionInfo v) Setup()
    {
        var f = new FakeInstance();
        f.Write("mods/a.jar", "A1");
        f.Write("mods/b.jar", "B1");
        f.Write("mods/joueur.jar", "PLAYER");
        f.Write("config/x.toml", "x=1");
        f.Write("options.txt", "touches du joueur");
        f.Write("saves/monde/level.dat", "monde");
        f.WriteOverride("config/x.toml", "x=1");
        f.WriteOverride("options.txt", "par defaut du pack");
        var v = new VersionInfo
        {
            Version = "1.0.0",
            Mods = { f.CdnMod("a.jar", "A1"), f.CdnMod("b.jar", "B1") },
            Protected = ProtectedRules.ToList(),
        };
        f.WriteState("1.0.0", new[] { "a.jar", "b.jar" }, new[] { "config/x.toml" });
        return (f, v);
    }

    static Task<VerifyResult> Verify(FakeInstance f, VersionInfo v) =>
        PackVerifier.VerifyAndRepairAsync(f.Instance, v, f.Overrides, null, new Downloader(f.Cache), null, default);

    [Fact]
    public async Task Instance_conforme_rien_a_faire()
    {
        var (f, v) = Setup();
        using (f)
        {
            var before = f.Snapshot();
            var r = await Verify(f, v);
            Assert.Equal(0, r.Total);
            Assert.Equal(before, f.Snapshot());
            Assert.False(Directory.Exists(Path.Combine(f.Instance, UpdateApplier.BackupDirName)));
        }
    }

    [Fact]
    public async Task Jar_manquant_ou_corrompu_est_repare()
    {
        var (f, v) = Setup();
        using (f)
        {
            File.Delete(Path.Combine(f.Instance, "mods", "a.jar"));
            f.Write("mods/b.jar", "CORROMPU");
            var r = await Verify(f, v);
            Assert.Equal(1, r.Added);
            Assert.Equal(1, r.Replaced);
            Assert.Equal("A1", f.Read("mods/a.jar"));
            Assert.Equal("B1", f.Read("mods/b.jar"));
            Assert.Equal(0, (await Verify(f, v)).Total); // deuxième passage : propre
        }
    }

    [Fact]
    public async Task Options_saves_et_jars_du_joueur_restent_intacts()
    {
        var (f, v) = Setup();
        using (f)
        {
            File.Delete(Path.Combine(f.Instance, "mods", "a.jar"));
            await Verify(f, v);
            Assert.Equal("touches du joueur", f.Read("options.txt"));
            Assert.Equal("monde", f.Read("saves/monde/level.dat"));
            Assert.Equal("PLAYER", f.Read("mods/joueur.jar"));
        }
    }

    [Fact]
    public async Task Config_modifiee_par_le_joueur_est_gardee_si_le_pack_n_a_pas_change()
    {
        var (f, v) = Setup();
        using (f)
        {
            // l'état connaît le hash installé de x.toml (celui du pack) ; le joueur l'a ensuite modifié
            var st = UpdatePlanner.ReadState(f.Instance)!;
            st.OverrideHashes["config/x.toml"] = Hashing.Sha256(Path.Combine(f.Overrides, "config", "x.toml"));
            File.WriteAllText(Path.Combine(f.Instance, LauncherState.FileName), System.Text.Json.JsonSerializer.Serialize(st, JsonCtx.Default.LauncherState));
            f.Write("config/x.toml", "x=perso");
            var r = await Verify(f, v);
            Assert.Equal(0, r.Total);
            Assert.Equal("x=perso", f.Read("config/x.toml"));
        }
    }

    [Fact]
    public async Task Reseau_en_panne_leve_une_erreur_et_ne_touche_a_rien()
    {
        var (f, v) = Setup();
        using (f)
        {
            File.Delete(Path.Combine(f.Instance, "mods", "a.jar"));
            v.Mods[0].Urls.Clear();
            v.Mods[0].Urls.Add(Path.Combine(f.Cdn, "introuvable.jar"));
            var before = f.Snapshot();
            await Assert.ThrowsAnyAsync<Exception>(() => Verify(f, v));
            Assert.Equal(before, f.Snapshot());
        }
    }

    [Fact]
    public void BDcraft_detecte_seulement_le_zip_attendu()
    {
        using var f = new FakeInstance();
        Assert.False(RecommendedPack.IsInstalled(f.Instance));
        f.Write("resourcepacks/autre.zip", "x");
        Assert.False(RecommendedPack.IsInstalled(f.Instance));
        f.Write("resourcepacks/" + RecommendedPack.FileName, "x");
        Assert.True(RecommendedPack.IsInstalled(f.Instance));
        Assert.False(RecommendedPack.IsInstalled(null));
        Assert.StartsWith("https://bdcraft.net", RecommendedPack.Url);
    }
}
