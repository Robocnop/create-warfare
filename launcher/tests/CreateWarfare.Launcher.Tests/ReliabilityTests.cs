using System.Diagnostics;
using System.Security.Cryptography;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

/// <summary>Cas limites de l'audit du 11/11 : réseau capricieux, disque plein, retour arrière incomplet, chemins accentués.</summary>
public class ReliabilityTests
{
    static byte[] Payload(int size, int seed = 1)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }

    static string Sha1(byte[] b) => Convert.ToHexString(SHA1.HashData(b)).ToLowerInvariant();

    static Downloader FastDownloader(string cache) => new(cache) { RetryDelay = _ => TimeSpan.Zero, IdleTimeout = TimeSpan.FromSeconds(5) };

    sealed class SyncProgress<T>(Action<T> a) : IProgress<T> { public void Report(T value) => a(value); }

    // ------------------------------------------------------------ Réseau

    [Fact]
    public async Task Coupure_au_milieu_reprend_ou_le_telechargement_s_est_arrete()
    {
        using var f = new FakeInstance();
        using var http = new FakeHttp();
        var body = Payload(300_000);
        http.Handler = r => new FakeHttp.Response(200, body, CutAfter: r.Number == 1 ? 100_000 : -1);
        var mod = new ModEntry { File = "gros.jar", Sha1 = Sha1(body), Size = body.Length, Urls = { http.Url + "gros.jar" } };

        var path = await FastDownloader(f.Cache).GetJarAsync(mod, null, default);

        Assert.Equal(body, File.ReadAllBytes(path));
        Assert.Equal(2, http.Requests.Count);
        Assert.True(http.Requests[1].RangeStart > 0, "le second essai doit reprendre le .part, pas repartir de zéro");
    }

    [Fact]
    public async Task Connexion_figee_abandonnee_apres_le_delai_d_inactivite()
    {
        using var f = new FakeInstance();
        using var http = new FakeHttp();
        http.Handler = _ => new FakeHttp.Response(200, Payload(1000), Stall: true);
        var mod = new ModEntry { File = "muet.jar", Sha1 = new string('a', 40), Size = 1000, Urls = { http.Url + "muet.jar" } };
        var dl = FastDownloader(f.Cache);
        dl.IdleTimeout = TimeSpan.FromMilliseconds(300);
        dl.Attempts = 2;

        var sw = Stopwatch.StartNew();
        var e = await Assert.ThrowsAsync<IOException>(() => dl.GetJarAsync(mod, null, default));

        Assert.Contains("figée", e.Message);
        Assert.Equal(2, http.Requests.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"abandon trop lent : {sw.Elapsed}");
    }

    [Fact]
    public async Task Erreur_500_passagere_est_reessayee()
    {
        using var f = new FakeInstance();
        using var http = new FakeHttp();
        var body = Payload(5000);
        http.Handler = r => r.Number == 1 ? new FakeHttp.Response(500, Array.Empty<byte>()) : new FakeHttp.Response(200, body);
        var mod = new ModEntry { File = "m.jar", Sha1 = Sha1(body), Size = body.Length, Urls = { http.Url + "m.jar" } };

        var path = await FastDownloader(f.Cache).GetJarAsync(mod, null, default);

        Assert.Equal(body, File.ReadAllBytes(path));
        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task Erreur_404_passe_a_l_adresse_suivante_sans_reessayer()
    {
        using var f = new FakeInstance();
        using var http = new FakeHttp();
        var body = Payload(5000);
        http.Handler = r => r.Path == "/absent.jar" ? new FakeHttp.Response(404, Array.Empty<byte>()) : new FakeHttp.Response(200, body);
        var mod = new ModEntry { File = "m.jar", Sha1 = Sha1(body), Size = body.Length, Urls = { http.Url + "absent.jar", http.Url + "present.jar" } };

        await FastDownloader(f.Cache).GetJarAsync(mod, null, default);

        Assert.Equal(new[] { "/absent.jar", "/present.jar" }, http.Requests.Select(r => r.Path));
    }

    [Fact]
    public async Task Part_corrompu_d_une_autre_session_repart_de_zero()
    {
        using var f = new FakeInstance();
        using var http = new FakeHttp();
        var body = Payload(20_000);
        http.Handler = _ => new FakeHttp.Response(200, body);
        var mod = new ModEntry { File = "m.jar", Sha1 = Sha1(body), Size = body.Length, Urls = { http.Url + "m.jar" } };
        var jars = Path.Combine(f.Cache, "jars");
        Directory.CreateDirectory(jars);
        File.WriteAllBytes(Path.Combine(jars, mod.Sha1 + ".jar.part"), Payload(500, seed: 99)); // début faux

        var path = await FastDownloader(f.Cache).GetJarAsync(mod, null, default);

        Assert.Equal(body, File.ReadAllBytes(path));
        Assert.Equal(0, http.Requests[^1].RangeStart);
    }

    [Theory]
    [InlineData("http://exemple.invalid/mod.jar", false)]
    [InlineData("https://exemple.invalid/mod.jar", true)]
    [InlineData("http://127.0.0.1:8080/mod.jar", true)]
    [InlineData("http://localhost/mod.jar", true)]
    public void Seul_https_est_accepte_hors_machine_locale(string url, bool accepted)
    {
        if (accepted) UrlRules.EnsureAllowed(url);
        else Assert.Throws<InvalidDataException>(() => UrlRules.EnsureAllowed(url));
    }

    [Fact]
    public async Task Adresse_http_refusee_sans_aucune_requete()
    {
        using var f = new FakeInstance();
        var mod = new ModEntry { File = "m.jar", Sha1 = new string('a', 40), Size = 1, Urls = { "http://exemple.invalid/m.jar" } };
        var e = await Assert.ThrowsAsync<IOException>(() => FastDownloader(f.Cache).GetJarAsync(mod, null, default));
        Assert.Contains("https obligatoire", e.Message);
        await Assert.ThrowsAsync<InvalidDataException>(() => FastDownloader(f.Cache).FetchManifestAsync("http://exemple.invalid/version.json", default));
    }

    [Fact]
    public async Task Version_json_illisible_n_est_pas_une_erreur_de_reseau()
    {
        using var f = new FakeInstance();
        var file = Path.Combine(f.Root, "version.json");
        File.WriteAllText(file, "{ pas du json");
        await Assert.ThrowsAsync<InvalidDataException>(() => new Downloader(f.Cache).FetchManifestAsync(file, default));
    }

    [Fact]
    public async Task Cache_nettoye_apres_une_mise_a_jour()
    {
        using var f = new FakeInstance();
        var dl = new Downloader(f.Cache);
        var garde = f.CdnMod("garde.jar", "GARDE");
        var vieux = f.CdnMod("vieux.jar", "VIEUX");
        var pGarde = await dl.GetJarAsync(garde, null, default);
        var pVieux = await dl.GetJarAsync(vieux, null, default);
        Directory.CreateDirectory(Path.Combine(f.Cache, "overrides", "ancien"));

        dl.Prune(new VersionInfo { Version = "2.0.0", Mods = { garde }, Zip = { Sha256 = "courant" } });

        Assert.True(File.Exists(pGarde));
        Assert.False(File.Exists(pVieux));
        Assert.False(Directory.Exists(Path.Combine(f.Cache, "overrides", "ancien")));
    }

    // ------------------------------------------------------------ Application

    static (FakeInstance f, VersionInfo v) Small()
    {
        var f = new FakeInstance();
        f.Write("mods/a.jar", "A1");
        f.Write("config/x.toml", "x=1");
        f.Write("kubejs/old.js", "old");
        f.WriteState("1.0.0", new[] { "a.jar" }, new[] { "config/x.toml", "kubejs/old.js" });
        f.WriteOverride("config/x.toml", "x=2");
        f.WriteOverride("kubejs/new.js", "new");
        return (f, new VersionInfo { Version = "2.0.0", Mods = { f.CdnMod("a.jar", "A1"), f.CdnMod("c.jar", "C2") } });
    }

    static FilePlan Plan(FakeInstance f, VersionInfo v, bool repair = false) =>
        UpdatePlanner.Plan(f.Instance, v, f.Overrides, UpdatePlanner.ReadState(f.Instance), null, repair);

    static FileActionKind KindOf(FilePlan p, string rel) => p.Actions.Single(a => a.RelPath == rel).Kind;

    [Fact]
    public async Task Disque_trop_plein_refuse_avant_de_toucher_a_quoi_que_ce_soit()
    {
        var (f, v) = Small();
        using (f)
        {
            var before = f.Snapshot();
            v.Mods[1].Size = 500L << 20; // 500 Mo annoncés
            var applier = new UpdateApplier(new Downloader(f.Cache)) { FreeSpace = _ => 100L << 20 };

            var e = await Assert.ThrowsAsync<InsufficientSpaceException>(() => applier.ApplyAsync(f.Instance, Plan(f, v), null, default));

            Assert.Contains("Espace disque insuffisant", e.Message);
            Assert.Equal(before, f.Snapshot());
            Assert.False(Directory.Exists(Path.Combine(f.Instance, UpdateApplier.BackupDirName)));
            Assert.False(Directory.Exists(Path.Combine(f.Cache, "jars")), "rien ne doit être téléchargé");
        }
    }

    [Fact]
    public async Task Espace_inconnu_ne_bloque_pas()
    {
        var (f, v) = Small();
        using (f)
        {
            await new UpdateApplier(new Downloader(f.Cache)) { FreeSpace = _ => null }.ApplyAsync(f.Instance, Plan(f, v), null, default);
            Assert.Equal("C2", f.Read("mods/c.jar"));
        }
    }

    [Fact]
    public async Task Retour_arriere_incomplet_est_signale_avec_les_fichiers_concernes()
    {
        var (f, v) = Small();
        using (f)
        {
            var applier = new UpdateApplier(new Downloader(f.Cache));
            FileStream? verrou = null;
            int n = 0;
            applier.BeforeWrite = _ =>
            {
                if (++n != 3) return;
                // config/x.toml vient d'être remplacé : on le verrouille, sa remise en place échouera
                verrou = new FileStream(Path.Combine(f.Instance, "config", "x.toml"), FileMode.Open, FileAccess.Read, FileShare.None);
                throw new IOException("panne simulée");
            };
            try
            {
                var e = await Assert.ThrowsAsync<RollbackIncompleteException>(() => applier.ApplyAsync(f.Instance, Plan(f, v), null, default));
                Assert.Contains("config/x.toml", e.NotRestored);
                Assert.Contains("panne simulée", e.Message);
                Assert.Equal("x=1", File.ReadAllText(Path.Combine(e.BackupDir, "config", "x.toml")));
            }
            finally { verrou?.Dispose(); }
            Assert.Equal("1.0.0", UpdatePlanner.ReadState(f.Instance)!.Version);
        }
    }

    [Fact]
    public async Task Progression_en_octets_pendant_les_telechargements_en_parallele()
    {
        using var f = new FakeInstance();
        var v = new VersionInfo { Version = "1.0.0" };
        for (int i = 0; i < 12; i++) v.Mods.Add(f.CdnMod($"m{i:00}.jar", new string((char)('A' + i), 1000 + i)));
        var seen = new List<ApplyProgress>();
        var progress = new SyncProgress<ApplyProgress>(p => { lock (seen) seen.Add(p); });

        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v), progress, default);

        var last = seen.Last(p => p.Phase == "download");
        Assert.Equal(12, last.Done);
        Assert.Equal(v.Mods.Sum(m => m.Size), last.BytesTotal);
        Assert.Equal(last.BytesTotal, last.Bytes);
        Assert.All(seen.Where(p => p.Phase == "download"), p => Assert.InRange(p.Bytes, 0, p.BytesTotal));
        for (int i = 0; i < 12; i++) Assert.True(f.Exists($"mods/m{i:00}.jar"));
    }

    [Fact]
    public async Task Fichier_temporaire_d_un_arret_brutal_est_nettoye()
    {
        var (f, v) = Small();
        using (f)
        {
            f.Write("mods/b.jar.cwtmp", "moitié");
            await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v), null, default);
            Assert.False(f.Exists("mods/b.jar.cwtmp"));
        }
    }

    [Fact]
    public async Task Chemin_avec_espaces_et_accents()
    {
        using var f = new FakeInstance();
        var instance = Path.Combine(f.Root, "Jeux de Jérôme", "Create Warfare (1) été");
        Directory.CreateDirectory(Path.Combine(instance, "config"));
        File.WriteAllText(Path.Combine(instance, "config", "réglage perso.toml"), "à moi");
        f.WriteOverride("config/déjà là.toml", "pack");
        var v = new VersionInfo { Version = "1.0.0", Mods = { f.CdnMod("mod été.jar", "ETE") } };
        var cache = Path.Combine(f.Root, "cache é è");

        var plan = UpdatePlanner.Plan(instance, v, f.Overrides, null, null);
        await new UpdateApplier(new Downloader(cache)).ApplyAsync(instance, plan, null, default);

        Assert.Equal("ETE", File.ReadAllText(Path.Combine(instance, "mods", "mod été.jar")));
        Assert.Equal("pack", File.ReadAllText(Path.Combine(instance, "config", "déjà là.toml")));
        Assert.Equal("à moi", File.ReadAllText(Path.Combine(instance, "config", "réglage perso.toml")));
        Assert.True(UpdatePlanner.Plan(instance, v, f.Overrides, UpdatePlanner.ReadState(instance), null).IsEmpty);
    }

    // ------------------------------------------------------------ Configs du joueur, mods désactivés

    [Fact]
    public async Task Config_modifiee_par_le_joueur_gardee_tant_que_le_pack_ne_la_change_pas()
    {
        var (f, v) = Small();
        using (f)
        {
            await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v), null, default); // pose x=2
            f.Write("config/x.toml", "x=joueur");

            // version suivante, le pack livre toujours x=2 : le réglage du joueur reste
            var v3 = new VersionInfo { Version = "3.0.0", Mods = v.Mods };
            var p3 = Plan(f, v3);
            Assert.Equal(FileActionKind.Kept, KindOf(p3, "config/x.toml"));
            await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p3, null, default);
            Assert.Equal("x=joueur", f.Read("config/x.toml"));
            Assert.Equal("3.0.0", UpdatePlanner.ReadState(f.Instance)!.Version);

            // « Réparer » remet le fichier du pack
            Assert.Equal(FileActionKind.Replace, KindOf(Plan(f, v3, repair: true), "config/x.toml"));

            // le pack change le fichier : il est remplacé, la version du joueur est sauvegardée
            f.WriteOverride("config/x.toml", "x=4");
            var p4 = Plan(f, new VersionInfo { Version = "4.0.0", Mods = v.Mods });
            Assert.Equal(FileActionKind.Replace, KindOf(p4, "config/x.toml"));
            await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p4, null, default);
            Assert.Equal("x=4", f.Read("config/x.toml"));
            var backup = Directory.GetDirectories(Path.Combine(f.Instance, UpdateApplier.BackupDirName)).OrderBy(d => d).Last();
            Assert.Equal("x=joueur", File.ReadAllText(Path.Combine(backup, "config", "x.toml")));
        }
    }

    [Fact]
    public void Etat_d_un_ancien_launcher_sans_hash_remplace_comme_avant()
    {
        var (f, v) = Small();
        using (f)
        {
            // .cw-launcher.json écrit par un launcher 1.4.1 : pas de overrideHashes
            Assert.Empty(UpdatePlanner.ReadState(f.Instance)!.OverrideHashes);
            Assert.Equal(FileActionKind.Replace, KindOf(Plan(f, v), "config/x.toml"));
        }
    }

    [Fact]
    public async Task Jar_livre_dans_le_zip_est_toujours_remis()
    {
        using var f = new FakeInstance();
        f.WriteOverride("mods/horscf.jar", "PACK");
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, new VersionInfo { Version = "1.0.0" }), null, default);
        f.Write("mods/horscf.jar", "ABIME");
        Assert.Equal(FileActionKind.Replace, KindOf(Plan(f, new VersionInfo { Version = "1.1.0" }), "mods/horscf.jar"));
    }

    [Fact]
    public async Task Mod_desactive_par_le_joueur_le_reste_quand_il_change_de_version()
    {
        using var f = new FakeInstance();
        var v1 = new VersionInfo { Version = "1.0.0", Mods = { f.CdnMod("shaders-1.0.jar", "S1"), f.CdnMod("autre-1.0.jar", "O1") } };
        v1.Mods[0].Id = "shaders";
        v1.Mods[1].Id = "autre";
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v1), null, default);
        File.Move(Path.Combine(f.Instance, "mods", "shaders-1.0.jar"), Path.Combine(f.Instance, "mods", "shaders-1.0.jar.disabled"));

        var v2 = new VersionInfo { Version = "2.0.0", Mods = { f.CdnMod("shaders-2.0.jar", "S2"), f.CdnMod("autre-2.0.jar", "O2") } };
        v2.Mods[0].Id = "shaders";
        v2.Mods[1].Id = "autre";
        var p = Plan(f, v2);
        Assert.Equal(FileActionKind.Add, KindOf(p, "mods/shaders-2.0.jar.disabled"));
        Assert.Equal(FileActionKind.Remove, KindOf(p, "mods/shaders-1.0.jar.disabled"));
        Assert.Equal(FileActionKind.Add, KindOf(p, "mods/autre-2.0.jar"));
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);

        Assert.Equal("S2", f.Read("mods/shaders-2.0.jar.disabled"));
        Assert.False(f.Exists("mods/shaders-2.0.jar"));
        Assert.False(f.Exists("mods/shaders-1.0.jar.disabled"));
        Assert.Equal("O2", f.Read("mods/autre-2.0.jar"));
        Assert.True(Plan(f, v2).IsEmpty);
        Assert.Equal("shaders-2.0.jar", UpdatePlanner.ReadState(f.Instance)!.ModIds["shaders"]);
    }

    // ------------------------------------------------------------ Launcher : mise à jour et désinstallation

    static LauncherRelease Release(FakeInstance f, string content)
    {
        var src = Path.Combine(f.Cdn, "nouveau.exe");
        File.WriteAllText(src, content);
        return new LauncherRelease { Version = "9.9.9", Url = src, Sha256 = Hashing.Sha256(src), Size = content.Length };
    }

    [Fact]
    public async Task Mise_a_jour_du_launcher_echange_les_exe_et_lance_le_nouveau()
    {
        using var f = new FakeInstance();
        var dir = Path.Combine(f.Root, "Mes jeux é");
        Directory.CreateDirectory(dir);
        var exe = Path.Combine(dir, "CreateWarfareLauncher.exe");
        File.WriteAllText(exe, "ANCIEN");
        string? started = null;

        await SelfUpdater.ReplaceAndRestartAsync(exe, Release(f, "NOUVEAU"), new Downloader(f.Cache), null, default, p => started = p);

        Assert.Equal(exe, started);
        Assert.Equal("NOUVEAU", File.ReadAllText(exe));
        Assert.Equal("ANCIEN", File.ReadAllText(exe + ".old"));
    }

    [Fact]
    public async Task Nouveau_launcher_qui_ne_demarre_pas_l_ancien_est_remis()
    {
        using var f = new FakeInstance();
        var exe = Path.Combine(f.Root, "CreateWarfareLauncher.exe");
        File.WriteAllText(exe, "ANCIEN");

        await Assert.ThrowsAsync<InvalidOperationException>(() => SelfUpdater.ReplaceAndRestartAsync(
            exe, Release(f, "NOUVEAU"), new Downloader(f.Cache), null, default, _ => throw new InvalidOperationException("refusé")));

        Assert.Equal("ANCIEN", File.ReadAllText(exe));
        Assert.False(File.Exists(exe + ".old"));
    }

    [Fact]
    public async Task Hash_du_nouveau_launcher_faux_l_exe_n_est_pas_touche()
    {
        using var f = new FakeInstance();
        var exe = Path.Combine(f.Root, "CreateWarfareLauncher.exe");
        File.WriteAllText(exe, "ANCIEN");
        var release = Release(f, "NOUVEAU");
        release.Sha256 = new string('0', 64);

        await Assert.ThrowsAsync<IOException>(() => SelfUpdater.ReplaceAndRestartAsync(exe, release, new Downloader(f.Cache), null, default, _ => { }));

        Assert.Equal("ANCIEN", File.ReadAllText(exe));
        Assert.False(File.Exists(exe + ".new"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Script_de_desinstallation_et_dossier_avec_accent(bool withCodePage)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new FakeInstance();
        var dir = Path.Combine(f.Root, "Jérôme été");
        var data = Path.Combine(dir, "données");
        Directory.CreateDirectory(data);
        var exe = Path.Combine(dir, "CreateWarfareLauncher.exe");
        File.WriteAllText(exe, "exe");
        File.WriteAllText(Path.Combine(data, "settings.json"), "{}");
        var script = Path.Combine(f.Root, "uninstall.cmd");
        var text = Uninstaller.SelfRemovalScript(9_999_999, exe, data); // PID inexistant : pas d'attente
        // sans la ligne chcp (script de la 1.4.1), cmd lit le chemin dans la page de codes OEM et ne trouve rien
        if (!withCodePage) text = text.Replace("chcp 65001 >nul\r\n", "");
        File.WriteAllText(script, text);

        using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false })!;
        Assert.True(p.WaitForExit(20_000));

        Assert.Equal(withCodePage, !File.Exists(exe));
        Assert.Equal(withCodePage, !Directory.Exists(data));
    }
}
