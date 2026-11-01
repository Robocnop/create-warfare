using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class SeasonalThemeTests
{
    [Theory]
    [InlineData(2026, 10, 14, false)]
    [InlineData(2026, 10, 15, true)]
    [InlineData(2026, 10, 31, true)]
    [InlineData(2026, 11, 5, true)]
    [InlineData(2026, 11, 6, false)]
    [InlineData(2027, 6, 1, false)]
    public void Halloween_du_15_octobre_au_5_novembre(int y, int m, int d, bool expected) =>
        Assert.Equal(expected, SeasonalTheme.IsHalloween(new DateTime(y, m, d)));

    [Fact]
    public void CW_THEME_force_le_theme()
    {
        Assert.True(SeasonalTheme.IsHalloween(new DateTime(2027, 6, 1), "Halloween"));
        Assert.False(SeasonalTheme.IsHalloween(new DateTime(2026, 10, 31), "default"));
        Assert.True(SeasonalTheme.IsHalloween(new DateTime(2026, 10, 31), "n'importe quoi"));
    }
}

public class ThemeResolveTests
{
    [Theory]
    [InlineData(2026, 10, 31, "halloween")]
    [InlineData(2026, 12, 24, "christmas")]
    [InlineData(2027, 1, 6, "christmas")]
    [InlineData(2027, 1, 7, "default")]
    [InlineData(2027, 7, 14, "summer")]
    [InlineData(2027, 9, 1, "default")]
    public void Saison_selon_la_date(int y, int m, int d, string expected) =>
        Assert.Equal(expected, SeasonalTheme.SeasonOf(new DateTime(y, m, d)));

    [Fact]
    public void Reglage_puis_date_et_CW_THEME_prioritaire()
    {
        var halloween = new DateTime(2026, 10, 31);
        Assert.Equal("halloween", SeasonalTheme.Resolve("auto", halloween, null));
        Assert.Equal("default", SeasonalTheme.Resolve("default", halloween, null));   // le joueur peut couper la saison
        Assert.Equal("summer", SeasonalTheme.Resolve("summer", halloween, null));
        Assert.Equal("halloween", SeasonalTheme.Resolve("inconnu", halloween, null));
        Assert.Equal("christmas", SeasonalTheme.Resolve("default", halloween, "Christmas"));
    }
}

public class LauncherInfoTests
{
    [Fact]
    public async Task Ping_lit_joueurs_version_et_motd_d_un_faux_serveur()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync();
            var s = c.GetStream();
            var buf = new byte[512];
            await s.ReadAsync(buf);
            var json = System.Text.Encoding.UTF8.GetBytes("{\"version\":{\"name\":\"NeoForge 1.21.1\"},\"players\":{\"max\":20,\"online\":3},\"description\":{\"text\":\"Create \",\"extra\":[{\"text\":\"Warfare\"}]}}");
            var body = new List<byte> { 0x00 };
            ServerPing.WriteVarInt(body, json.Length);
            body.AddRange(json);
            var packet = new List<byte>();
            ServerPing.WriteVarInt(packet, body.Count);
            packet.AddRange(body);
            await s.WriteAsync(packet.ToArray());
            await Task.Delay(200);
        });
        var st = await ServerPing.QueryAsync("127.0.0.1", port, TimeSpan.FromSeconds(5));
        await server;
        listener.Stop();
        Assert.True(st.Online, st.Error);
        Assert.Equal(3, st.Players);
        Assert.Equal(20, st.MaxPlayers);
        Assert.Equal("NeoForge 1.21.1", st.Version);
        Assert.Equal("Create Warfare", st.Motd);
    }

    [Fact]
    public async Task Ping_d_un_serveur_absent_donne_hors_ligne_sans_exception()
    {
        var st = await ServerPing.QueryAsync("127.0.0.1", 1, TimeSpan.FromSeconds(2));
        Assert.False(st.Online);
        Assert.NotNull(st.Error);
    }

    [Fact]
    public void Changelog_decoupe_en_versions_et_categories()
    {
        var text = "1.8.0 — 2026-11-01\n\n- First-person Model retiré : il gênait les armes.\n- Traductions : 1 310 textes ajoutés\n  pour les mods.\n\n1.7.8 — 2026-11-01\n\n- 10 erreurs KubeJS corrigées : retiré.";
        var e = ChangelogParser.Parse(text);
        Assert.Equal(2, e.Count);
        Assert.Equal("1.8.0", e[0].Version);
        Assert.Equal("2026-11-01", e[0].Date);
        Assert.Equal(2, e[0].Items.Count);
        Assert.Equal(ChangelogParser.Removed, e[0].Items[0].Category);
        Assert.Equal("Traductions", e[0].Items[1].Title);
        Assert.EndsWith("pour les mods.", e[0].Items[1].Text);
        Assert.Equal(ChangelogParser.Fixed, e[1].Items[0].Category);
        Assert.Empty(ChangelogParser.Parse(null));
    }

    [Theory]
    [InlineData(32768, 10240)]
    [InlineData(16384, 10240)]
    [InlineData(12288, 8192)]
    [InlineData(8192, 6144)]
    [InlineData(0, 10240)]
    public void Ram_conseillee_selon_le_pc(long total, int expected) =>
        Assert.Equal(expected, MemoryAdvisor.RecommendedMb(total));

    [Fact]
    public void Ecrit_allocatedMemory_sans_toucher_au_reste()
    {
        using var f = new FakeInstance();
        f.Write("minecraftinstance.json", "{\"name\":\"Create Warfare\",\"allocatedMemory\":16384,\"guid\":\"abc\",\"installedAddons\":[1,2]}");
        Assert.Equal(16384, MemoryAdvisor.ReadAllocated(f.Instance));
        MemoryAdvisor.WriteAllocated(f.Instance, 10240);
        Assert.Equal(10240, MemoryAdvisor.ReadAllocated(f.Instance));
        var json = f.Read("minecraftinstance.json");
        Assert.Contains("\"guid\": \"abc\"", json);
        Assert.Contains("\"name\": \"Create Warfare\"", json);
        Assert.Null(MemoryAdvisor.ReadAllocated(Path.Combine(f.Root, "absent")));
    }

    [Fact]
    public async Task Contenu_distant_avec_repli_sur_la_copie_locale()
    {
        using var f = new FakeInstance();
        var src = Path.Combine(f.Root, "content.json");
        var cache = Path.Combine(f.Root, "cache", "content.json");
        File.WriteAllText(src, "{ // commentaire\n \"news\": [{\"title\":\"Saison 2\",\"tag\":\"Event\"}], \"gallery\": [{\"image\":\"base.png\",\"caption\":\"La base\"}], \"links\": {\"discord\": \"\"}, }");
        using var http = new HttpClient();
        var c = await LauncherContent.LoadAsync(src, cache, http, default);
        Assert.Equal("Saison 2", c.News[0].Title);
        Assert.Equal("La base", c.Gallery[0].Caption);
        Assert.Equal("https://github.com/Robocnop/create-warfare", c.Links.Github);
        File.Delete(src);
        var again = await LauncherContent.LoadAsync(Path.Combine(f.Root, "absent.json"), cache, http, default);
        Assert.Equal("Saison 2", again.News[0].Title);
    }
}
