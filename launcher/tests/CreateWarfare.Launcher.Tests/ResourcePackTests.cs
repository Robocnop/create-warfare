using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class ResourcePackTests
{
    const string Options = "version:3955\nresourcePacks:[\"vanilla\",\"mod_resources\",\"file/FA_old.zip\",\"file/Mon pack à moi.zip\"]\nlang:fr_fr\n";

    static ResourcePackEntry Pack(FakeInstance f, string file, string content, params string[] previous)
    {
        var m = f.CdnMod(file, content);
        return new ResourcePackEntry { File = file, Sha1 = m.Sha1, Size = m.Size, Urls = m.Urls, Previous = previous.ToList() };
    }

    static VersionInfo Version(params ResourcePackEntry[] packs)
    {
        var v = new VersionInfo { Version = "1.7.4", Protected = { "options.txt", "resourcepacks/" } };
        v.ResourcePacks.AddRange(packs);
        return v;
    }

    [Fact]
    public async Task Pose_et_active_les_packs_une_seule_fois()
    {
        using var f = new FakeInstance();
        f.Write("options.txt", Options);
        f.Write("resourcepacks/FA_old.zip", "ancien");
        var v = Version(Pack(f, "FA_new.zip", "fa", "FA_old.zip"), Pack(f, "[SBW] Sons 2.0.zip", "sons"));

        var p = UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, null);
        Assert.Equal(2, p.Count(FileActionKind.Add));
        Assert.Equal(2, p.PacksToEnable.Count);
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);

        Assert.Equal("sons", f.Read("resourcepacks/[SBW] Sons 2.0.zip"));
        Assert.True(f.Exists("resourcepacks/FA_old.zip")); // jamais supprimé
        Assert.Equal(new[] { "vanilla", "mod_resources", "file/[SBW] Sons 2.0.zip", "file/FA_new.zip", "file/Mon pack à moi.zip" },
            OptionsFile.Packs(Path.Combine(f.Instance, "options.txt")));
        var lines = f.Read("options.txt").Split('\n');
        Assert.Equal("version:3955", lines[0]);
        Assert.Equal("lang:fr_fr", lines[2]);
        Assert.Contains("Mon pack à moi", lines[1]); // accents gardés lisibles

        // le joueur désactive le pack de sons : il n'est pas réactivé
        var state = UpdatePlanner.ReadState(f.Instance)!;
        Assert.Contains("[SBW] Sons 2.0.zip", state.EnabledPacks);
        f.Write("options.txt", Options);
        var again = UpdatePlanner.Plan(f.Instance, v, f.Overrides, state, null);
        Assert.Empty(again.PacksToEnable);
        Assert.True(again.IsEmpty);
    }

    [Fact]
    public void Ne_remplace_pas_un_fichier_du_joueur_de_meme_nom()
    {
        using var f = new FakeInstance();
        f.Write("resourcepacks/x.zip", "à moi");
        var p = UpdatePlanner.Plan(f.Instance, Version(Pack(f, "x.zip", "du pack")), f.Overrides, null, null);
        Assert.Equal(FileActionKind.Protected, p.Actions.Single().Kind);
    }

    [Fact]
    public async Task Sans_options_txt_rien_n_est_cree()
    {
        using var f = new FakeInstance();
        var p = UpdatePlanner.Plan(f.Instance, Version(Pack(f, "x.zip", "x")), f.Overrides, null, null);
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);
        Assert.False(f.Exists("options.txt"));
        Assert.Contains("x.zip", UpdatePlanner.ReadState(f.Instance)!.EnabledPacks);
    }

    [Fact]
    public void Ligne_illisible_laissee_telle_quelle()
    {
        using var f = new FakeInstance();
        f.Write("options.txt", "resourcePacks:[cassé\r\nlang:fr_fr\r\n");
        var path = Path.Combine(f.Instance, "options.txt");
        Assert.Empty(OptionsFile.Enable(path, new[] { new ResourcePackEntry { File = "x.zip" } }));
        Assert.Equal("resourcePacks:[cassé\r\nlang:fr_fr\r\n", f.Read("options.txt"));
    }

    [Fact]
    public void Garde_les_fins_de_ligne_CRLF()
    {
        using var f = new FakeInstance();
        f.Write("options.txt", "a:1\r\nresourcePacks:[\"vanilla\"]\r\nb:2\r\n");
        var path = Path.Combine(f.Instance, "options.txt");
        OptionsFile.Enable(path, new[] { new ResourcePackEntry { File = "x.zip" } });
        Assert.Equal("a:1\r\nresourcePacks:[\"vanilla\",\"file/x.zip\"]\r\nb:2\r\n", f.Read("options.txt"));
    }

    [Fact]
    public async Task Echec_remet_options_txt()
    {
        using var f = new FakeInstance();
        f.Write("options.txt", Options);
        f.WriteOverride("config/x.toml", "x");
        var applier = new UpdateApplier(new Downloader(f.Cache));
        var v = Version(Pack(f, "x.zip", "x"));
        applier.BeforeWrite = a => { if (a.RelPath == "resourcepacks/x.zip") throw new IOException("simulé"); };
        await Assert.ThrowsAsync<IOException>(() => applier.ApplyAsync(f.Instance, UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, null), null, default));
        Assert.Equal(Options, f.Read("options.txt"));
        Assert.False(f.Exists("resourcepacks/x.zip"));
    }
}
