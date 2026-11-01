using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class EasterTests
{
    // Dates officielles du dimanche de Pâques (calendrier grégorien).
    [Theory]
    [InlineData(1818, 3, 22)]   // la plus précoce possible
    [InlineData(1943, 4, 25)]   // la plus tardive possible
    [InlineData(2019, 4, 21)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2029, 4, 1)]
    [InlineData(2030, 4, 21)]
    [InlineData(2038, 4, 25)]
    [InlineData(2285, 3, 22)]
    public void Dimanche_de_Paques(int y, int m, int d) =>
        Assert.Equal(new DateTime(y, m, d), ThemeSchedule.EasterSunday(y));

    [Fact]
    public void Paques_toujours_un_dimanche_entre_22_mars_et_25_avril()
    {
        for (int y = 1583; y <= 4099; y++)
        {
            var e = ThemeSchedule.EasterSunday(y);
            Assert.Equal(DayOfWeek.Sunday, e.DayOfWeek);
            Assert.InRange(e, new DateTime(y, 3, 22), new DateTime(y, 4, 25));
        }
    }
}

public class ThemeScheduleTests
{
    // Bornes demandées : veille, premier jour, dernier jour, lendemain de chaque période.
    [Theory]
    [InlineData(2026, 10, 14, "default")]
    [InlineData(2026, 10, 15, "halloween")]
    [InlineData(2026, 10, 31, "halloween")]
    [InlineData(2026, 11, 2, "halloween")]
    [InlineData(2026, 11, 3, "default")]
    [InlineData(2026, 11, 30, "default")]
    [InlineData(2026, 12, 1, "christmas")]
    [InlineData(2026, 12, 31, "christmas")]
    [InlineData(2027, 1, 1, "christmas")]
    [InlineData(2027, 1, 6, "christmas")]
    [InlineData(2027, 1, 7, "default")]
    [InlineData(2027, 6, 20, "default")]
    [InlineData(2027, 6, 21, "summer")]
    [InlineData(2027, 8, 15, "summer")]
    [InlineData(2027, 9, 22, "summer")]
    [InlineData(2027, 9, 23, "default")]
    // Pâques 2026 = 5 avril : du 22 mars au lundi 6 avril
    [InlineData(2026, 3, 21, "default")]
    [InlineData(2026, 3, 22, "easter")]
    [InlineData(2026, 4, 5, "easter")]
    [InlineData(2026, 4, 6, "easter")]
    [InlineData(2026, 4, 7, "default")]
    // Pâques 2027 = 28 mars : du 14 au lundi 29 mars
    [InlineData(2027, 3, 13, "default")]
    [InlineData(2027, 3, 14, "easter")]
    [InlineData(2027, 3, 28, "easter")]
    [InlineData(2027, 3, 29, "easter")]
    [InlineData(2027, 3, 30, "default")]
    // Pâques 2028 = 16 avril : du 2 au lundi 17 avril
    [InlineData(2028, 4, 1, "default")]
    [InlineData(2028, 4, 2, "easter")]
    [InlineData(2028, 4, 16, "easter")]
    [InlineData(2028, 4, 17, "easter")]
    [InlineData(2028, 4, 18, "default")]
    public void Theme_du_jour(int y, int m, int d, string expected) =>
        Assert.Equal(expected, SeasonalTheme.SeasonOf(new DateTime(y, m, d)));

    [Fact]
    public void Heure_du_jour_ignoree()
    {
        Assert.Equal("halloween", SeasonalTheme.SeasonOf(new DateTime(2026, 11, 2, 23, 59, 59)));
        Assert.Equal("default", SeasonalTheme.SeasonOf(new DateTime(2026, 11, 3, 0, 0, 0)));
    }

    [Fact]
    public void Chevauchement_la_priorite_la_plus_haute_gagne()
    {
        var periods = new[]
        {
            new ThemePeriod("summer", ThemeSchedule.Fixed(6, 1), ThemeSchedule.Fixed(9, 30), 10),
            new ThemePeriod("halloween", ThemeSchedule.Fixed(7, 1), ThemeSchedule.Fixed(7, 2), 40),
        };
        Assert.Equal("halloween", ThemeSchedule.ThemeOn(new DateTime(2027, 7, 1), periods));
        Assert.Equal("summer", ThemeSchedule.ThemeOn(new DateTime(2027, 7, 3), periods));
        Assert.Equal("default", ThemeSchedule.ThemeOn(new DateTime(2027, 10, 1), periods));
    }

    [Fact]
    public void Periodes_par_defaut_sans_chevauchement()
    {
        for (var d = new DateTime(2024, 1, 1); d < new DateTime(2040, 1, 1); d = d.AddDays(1))
            Assert.True(ThemeSchedule.Periods.Count(p => ThemeSchedule.Contains(p, d)) <= 1, d.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void Le_29_fevrier_passe_au_28_hors_annee_bissextile()
    {
        var p = new ThemePeriod("x", ThemeSchedule.Fixed(2, 29), ThemeSchedule.Fixed(3, 1), 1);
        Assert.True(ThemeSchedule.Contains(p, new DateTime(2027, 2, 28)));
        Assert.True(ThemeSchedule.Contains(p, new DateTime(2028, 2, 29)));
        Assert.False(ThemeSchedule.Contains(p, new DateTime(2028, 2, 28)));
    }

    [Theory]
    [InlineData("2027-03-28", 2027, 3, 28)]
    [InlineData(" 2026-12-01 ", 2026, 12, 1)]
    public void CW_DATE_lisible(string text, int y, int m, int d) =>
        Assert.Equal(new DateTime(y, m, d), SeasonalTheme.ParseDate(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("28/03/2027")]
    [InlineData("demain")]
    public void CW_DATE_illisible_ignoree(string? text) => Assert.Null(SeasonalTheme.ParseDate(text));
}

public class ThemeResolveTests
{
    [Fact]
    public void Reglage_puis_date_et_CW_THEME_prioritaire()
    {
        var halloween = new DateTime(2026, 10, 31);
        Assert.Equal("halloween", SeasonalTheme.Resolve("auto", halloween, null));
        Assert.Equal("default", SeasonalTheme.Resolve("default", halloween, null));   // le joueur peut couper la saison
        Assert.Equal("summer", SeasonalTheme.Resolve("summer", halloween, null));
        Assert.Equal("easter", SeasonalTheme.Resolve("Easter", halloween, null));
        Assert.Equal("halloween", SeasonalTheme.Resolve("inconnu", halloween, null));
        Assert.Equal("christmas", SeasonalTheme.Resolve("default", halloween, "Christmas"));
        Assert.Equal("halloween", SeasonalTheme.Resolve("auto", halloween, "n'importe quoi"));
        Assert.Equal("easter", SeasonalTheme.Resolve("auto", new DateTime(2027, 3, 28), null));
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
