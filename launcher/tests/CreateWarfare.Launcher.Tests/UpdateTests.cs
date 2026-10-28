using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class UpdateTests
{
    static readonly string[] ProtectedRules = { "options.txt", "config/inventoryprofilesnext/", "saves/" };

    /// <summary>Instance en 1.0 posée par le launcher, avec des fichiers du joueur à ne pas toucher.</summary>
    static (FakeInstance f, VersionInfo v2) Setup()
    {
        var f = new FakeInstance();
        f.Write("mods/a.jar", "A1");
        f.Write("mods/b.jar", "B1");
        f.Write("mods/joueur.jar", "PLAYER");
        f.Write("config/x.toml", "x=1");
        f.Write("kubejs/old.js", "old");
        f.Write("config/inventoryprofilesnext/inventoryprofiles.json", "{\"touches\":\"du joueur\"}");
        f.Write("options.txt", "key_key.jump:key.keyboard.k");
        f.Write("saves/monde/level.dat", "monde");
        f.WriteState("1.0.0", new[] { "a.jar", "b.jar" }, new[] { "config/x.toml", "kubejs/old.js", "config/inventoryprofilesnext/inventoryprofiles.json" });

        f.WriteOverride("config/x.toml", "x=2");
        f.WriteOverride("kubejs/new.js", "new");
        f.WriteOverride("config/inventoryprofilesnext/inventoryprofiles.json", "{\"pack\":true}");
        f.WriteOverride("mods/horscf.jar", "HORS-CF");
        var v2 = new VersionInfo
        {
            Version = "2.0.0",
            Mods = { f.CdnMod("a.jar", "A1"), f.CdnMod("c.jar", "C2") },
            Protected = ProtectedRules.ToList(),
        };
        return (f, v2);
    }

    static FilePlan Plan(FakeInstance f, VersionInfo v, InstanceInfo? info = null) =>
        UpdatePlanner.Plan(f.Instance, v, f.Overrides, UpdatePlanner.ReadState(f.Instance), info);

    static FileActionKind KindOf(FilePlan p, string rel) => p.Actions.Single(a => a.RelPath == rel).Kind;

    [Fact]
    public void Plan_classe_chaque_fichier()
    {
        var (f, v2) = Setup();
        using (f)
        {
            var p = Plan(f, v2);
            Assert.Equal(FileActionKind.Same, KindOf(p, "mods/a.jar"));
            Assert.Equal(FileActionKind.Add, KindOf(p, "mods/c.jar"));
            Assert.Equal(FileActionKind.Add, KindOf(p, "mods/horscf.jar"));
            Assert.Equal(FileActionKind.Remove, KindOf(p, "mods/b.jar"));
            Assert.Equal(FileActionKind.Replace, KindOf(p, "config/x.toml"));
            Assert.Equal(FileActionKind.Add, KindOf(p, "kubejs/new.js"));
            Assert.Equal(FileActionKind.Remove, KindOf(p, "kubejs/old.js"));
            Assert.Equal(FileActionKind.Protected, KindOf(p, "config/inventoryprofilesnext/inventoryprofiles.json"));
            Assert.DoesNotContain(p.Actions, a => a.RelPath == "mods/joueur.jar");
            Assert.DoesNotContain(p.Actions, a => a.RelPath.StartsWith("saves/") || a.RelPath == "options.txt");
            Assert.Equal(2, p.DownloadSize); // seul c.jar est téléchargé
        }
    }

    [Fact]
    public async Task Mise_a_jour_normale()
    {
        var (f, v2) = Setup();
        using (f)
        {
            await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v2), null, default);

            Assert.False(f.Exists("mods/b.jar"));
            Assert.Equal("C2", f.Read("mods/c.jar"));
            Assert.Equal("HORS-CF", f.Read("mods/horscf.jar"));
            Assert.Equal("x=2", f.Read("config/x.toml"));
            Assert.Equal("new", f.Read("kubejs/new.js"));
            Assert.False(f.Exists("kubejs/old.js"));
            // jamais touchés
            Assert.Equal("PLAYER", f.Read("mods/joueur.jar"));
            Assert.Equal("{\"touches\":\"du joueur\"}", f.Read("config/inventoryprofilesnext/inventoryprofiles.json"));
            Assert.Equal("key_key.jump:key.keyboard.k", f.Read("options.txt"));
            Assert.Equal("monde", f.Read("saves/monde/level.dat"));

            var state = UpdatePlanner.ReadState(f.Instance)!;
            Assert.Equal("2.0.0", state.Version);
            Assert.Equal(new[] { "a.jar", "c.jar", "horscf.jar" }, state.Mods);

            // sauvegarde des fichiers remplacés ou supprimés
            var backup = Directory.GetDirectories(Path.Combine(f.Instance, UpdateApplier.BackupDirName)).Single();
            Assert.Equal("B1", File.ReadAllText(Path.Combine(backup, "mods", "b.jar")));
            Assert.Equal("x=1", File.ReadAllText(Path.Combine(backup, "config", "x.toml")));

            // une seconde passe ne trouve plus rien à faire
            Assert.True(Plan(f, v2).IsEmpty);
        }
    }

    [Fact]
    public async Task Echec_au_milieu_remet_tout_en_etat()
    {
        var (f, v2) = Setup();
        using (f)
        {
            var before = f.Snapshot();
            var applier = new UpdateApplier(new Downloader(f.Cache));
            int n = 0;
            applier.BeforeWrite = _ => { if (++n == 4) throw new IOException("disque plein (simulé)"); };

            await Assert.ThrowsAsync<IOException>(() => applier.ApplyAsync(f.Instance, Plan(f, v2), null, default));

            Assert.Equal(before, f.Snapshot());
            Assert.Equal("1.0.0", UpdatePlanner.ReadState(f.Instance)!.Version);
        }
    }

    [Fact]
    public async Task Hash_faux_rien_n_est_ecrit()
    {
        var (f, v2) = Setup();
        using (f)
        {
            v2.Mods[1].Sha1 = new string('0', 40);
            var before = f.Snapshot();
            await Assert.ThrowsAsync<IOException>(() =>
                new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v2), null, default));
            Assert.Equal(before, f.Snapshot());
        }
    }

    [Fact]
    public void Instance_importee_par_CurseForge_sans_etat()
    {
        using var f = new FakeInstance();
        f.Write("mods/ancien.jar", "OLD");                 // jar d'une version précédente
        f.Write("mods/homonyme.jar", "AUTRE");             // même nom qu'un ancien jar, autre contenu : au joueur
        f.Write("kubejs/old.js", "old");
        f.Write("config/perso.toml", "a moi");
        var info = new InstanceInfo(f.Instance, "Create Warfare", "g", "1.3.1", DateTime.Now, new[] { "kubejs/old.js" });
        var v = new VersionInfo
        {
            Version = "1.5.1",
            RetiredMods = { new RetiredMod { File = "ancien.jar", Sha1 = FakeInstance.Sha1("OLD") },
                            new RetiredMod { File = "homonyme.jar", Sha1 = FakeInstance.Sha1("PACK") } },
        };

        var p = UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, info);

        Assert.Equal("1.3.1", p.FromVersion);
        Assert.Equal(FileActionKind.Remove, KindOf(p, "mods/ancien.jar"));
        Assert.Equal(FileActionKind.Remove, KindOf(p, "kubejs/old.js"));
        Assert.DoesNotContain(p.Actions, a => a.RelPath is "mods/homonyme.jar" or "config/perso.toml");
    }

    [Fact]
    public async Task Fichier_protege_absent_est_cree_et_jar_desactive_reste_desactive()
    {
        using var f = new FakeInstance();
        f.Write("mods/a.jar.disabled", "A1");
        f.WriteOverride("config/inventoryprofilesnext/inventoryprofiles.json", "{\"pack\":true}");
        var v = new VersionInfo { Version = "1.0.0", Mods = { f.CdnMod("a.jar", "A1") }, Protected = ProtectedRules.ToList() };

        var p = UpdatePlanner.Plan(f.Instance, v, f.Overrides, null, null);
        Assert.Equal(FileActionKind.Add, KindOf(p, "config/inventoryprofilesnext/inventoryprofiles.json"));
        Assert.Equal(FileActionKind.Same, KindOf(p, "mods/a.jar"));

        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);
        Assert.True(f.Exists("config/inventoryprofilesnext/inventoryprofiles.json"));
        Assert.False(f.Exists("mods/a.jar"));
    }

    [Fact]
    public async Task Ancienne_version_desactivee_est_retiree()
    {
        using var f = new FakeInstance();
        f.Write("mods/b.jar.disabled", "B1");
        f.WriteState("1.0.0", new[] { "b.jar" }, Array.Empty<string>());
        var v = new VersionInfo { Version = "2.0.0" };
        var p = Plan(f, v);
        Assert.Equal(FileActionKind.Remove, KindOf(p, "mods/b.jar.disabled"));
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, p, null, default);
        Assert.False(f.Exists("mods/b.jar.disabled"));
    }

    [Fact]
    public async Task Garde_deux_sauvegardes()
    {
        var (f, _) = Setup();
        using (f)
        {
            for (int i = 0; i < 4; i++)
            {
                f.WriteOverride("config/x.toml", $"x={10 + i}");
                var v = new VersionInfo { Version = $"3.{i}.0", Protected = ProtectedRules.ToList() };
                await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(f.Instance, Plan(f, v), null, default);
            }
            Assert.Equal(UpdateApplier.BackupsToKeep, Directory.GetDirectories(Path.Combine(f.Instance, UpdateApplier.BackupDirName)).Length);
        }
    }
}
