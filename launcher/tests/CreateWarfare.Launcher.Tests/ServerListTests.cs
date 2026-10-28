using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class ServerListTests
{
    static readonly ServerEntry Pack = new() { Name = "Create Warfare", Ip = "jeu.robocnop.be:27018" };

    /// <summary>servers.dat comme l'écrit Minecraft : icône, acceptTextures, nom accentué et emoji.</summary>
    static void WritePlayerList(string path)
    {
        var s = new NbtCompound()
            .Set("ip", new NbtTag(Nbt.String, "play.exemple.fr"))
            .Set("name", new NbtTag(Nbt.String, "Serveur de Léa 🎮"))
            .Set("icon", new NbtTag(Nbt.String, "iVBORw0KGgo="))
            .Set("acceptTextures", new NbtTag(Nbt.Byte, (sbyte)1));
        var list = new NbtList { ElementType = Nbt.Compound };
        list.Items.Add(s);
        Nbt.WriteFile(path, new NbtCompound().Set("servers", new NbtTag(Nbt.List, list)));
    }

    [Fact]
    public void Ajoute_a_la_fin_sans_toucher_aux_serveurs_du_joueur()
    {
        using var f = new FakeInstance();
        var path = Path.Combine(f.Instance, "servers.dat");
        WritePlayerList(path);

        Assert.Single(ServerList.Ensure(path, new[] { Pack }));
        Assert.Empty(ServerList.Ensure(path, new[] { Pack })); // déjà présent

        var root = Nbt.ReadFile(path);
        var items = ((NbtList)root.Get("servers")!.Value).Items.Cast<NbtCompound>().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Serveur de Léa 🎮", items[0].GetString("name"));
        Assert.Equal("iVBORw0KGgo=", items[0].GetString("icon"));
        Assert.Equal((sbyte)1, items[0].Get("acceptTextures")!.Value);
        Assert.Equal("jeu.robocnop.be:27018", items[1].GetString("ip"));
    }

    [Fact]
    public void Cree_la_liste_si_elle_n_existe_pas()
    {
        using var f = new FakeInstance();
        var path = Path.Combine(f.Instance, "servers.dat");
        ServerList.Ensure(path, new[] { Pack });
        Assert.Equal(new[] { Pack.Ip }, ServerList.Ips(path));
    }

    [Fact]
    public async Task Mise_a_jour_ajoute_le_serveur_une_seule_fois()
    {
        using var f = new FakeInstance();
        WritePlayerList(Path.Combine(f.Instance, "servers.dat"));
        var v = new VersionInfo { Version = "1.5.2", Servers = { Pack }, Protected = { "servers.dat" } };

        var p = UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, null);
        Assert.Single(p.ServersToAdd);
        Assert.False(p.IsEmpty);
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);
        Assert.Equal(new[] { "play.exemple.fr", Pack.Ip }, ServerList.Ips(Path.Combine(f.Instance, "servers.dat")));
        Assert.Contains(Pack.Ip, UpdatePlanner.ReadState(f.Instance)!.AddedServers);

        // le joueur supprime le serveur : il ne revient pas à la mise à jour suivante
        WritePlayerList(Path.Combine(f.Instance, "servers.dat"));
        var again = UpdatePlanner.Plan(f.Instance, v, f.Overrides, UpdatePlanner.ReadState(f.Instance), null);
        Assert.Empty(again.ServersToAdd);
    }

    [Fact]
    public async Task Liste_absente_creee_depuis_le_zip_sans_doublon()
    {
        using var f = new FakeInstance();
        var packList = Path.Combine(f.Overrides, "servers.dat");
        ServerList.Ensure(packList, new[] { Pack });
        var v = new VersionInfo { Version = "1.5.2", Servers = { Pack }, Protected = { "servers.dat" } };

        var p = UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, null);
        Assert.Empty(p.ServersToAdd);
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);
        Assert.Equal(new[] { Pack.Ip }, ServerList.Ips(Path.Combine(f.Instance, "servers.dat")));
    }

    [Fact]
    public async Task Echec_remet_la_liste_du_joueur()
    {
        using var f = new FakeInstance();
        var path = Path.Combine(f.Instance, "servers.dat");
        WritePlayerList(path);
        var before = File.ReadAllBytes(path);
        f.WriteOverride("config/x.toml", "x");
        f.WriteOverride("config/y.toml", "y");
        var v = new VersionInfo { Version = "1.5.2", Servers = { Pack }, Protected = { "servers.dat" } };
        var applier = new UpdateApplier(new Downloader(f.Cache));
        int n = 0;
        applier.BeforeWrite = _ => { if (++n == 2) throw new IOException("simulé"); };

        await Assert.ThrowsAsync<IOException>(() => applier.ApplyAsync(f.Instance, UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, null), null, default));
        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
