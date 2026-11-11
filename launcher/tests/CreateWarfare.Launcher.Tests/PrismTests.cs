using System.Text.Json.Nodes;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

/// <summary>Version Linux : détection de Prism Launcher, création et mise à jour de l'instance (fichiers seulement, sans Prism).</summary>
public class PrismTests
{
    static string Dir(params string[] parts)
    {
        var d = Path.Combine(parts);
        Directory.CreateDirectory(d);
        return d;
    }

    // ------------------------------------------------------------ Détection

    [Fact]
    public void Trouve_le_flatpak_et_l_installation_native()
    {
        using var f = new FakeInstance();
        var home = Dir(f.Root, "home");
        Assert.Empty(PrismLocator.Find(home, null, null));

        var native = Dir(home, ".local", "share", "PrismLauncher");
        var flatpak = Dir(home, ".var", "app", "org.prismlauncher.PrismLauncher", "data", "PrismLauncher");
        var found = PrismLocator.Find(home, null, null);

        Assert.Equal(new[] { (native, "native"), (flatpak, "flatpak") }, found.Select(p => (p.DataDir, p.Kind)));
    }

    [Fact]
    public void XDG_DATA_HOME_passe_en_premier_sans_doublon()
    {
        using var f = new FakeInstance();
        var home = Dir(f.Root, "home");
        var xdg = Dir(f.Root, "données xdg");
        var custom = Dir(xdg, "PrismLauncher");
        Dir(home, ".local", "share", "PrismLauncher");

        Assert.Equal(custom, PrismLocator.Find(home, xdg, null)[0].DataDir);
        // XDG_DATA_HOME égal au dossier par défaut : une seule entrée
        Assert.Single(PrismLocator.Find(home, Path.Combine(home, ".local", "share"), null));
    }

    [Fact]
    public void Dossier_indique_par_le_joueur_est_pris_seul()
    {
        using var f = new FakeInstance();
        var home = Dir(f.Root, "home");
        Dir(home, ".local", "share", "PrismLauncher");
        var portable = Dir(f.Root, "Prism portable");

        var found = PrismLocator.Find(home, null, portable);

        Assert.Equal((portable, "custom"), (found.Single().DataDir, found.Single().Kind));
        Assert.Empty(PrismLocator.Find(home, null, Path.Combine(f.Root, "absent")));
    }

    [Fact]
    public void Entre_plusieurs_installations_celle_qui_a_deja_l_instance_gagne()
    {
        using var f = new FakeInstance();
        var home = Dir(f.Root, "home");
        var native = Dir(home, ".local", "share", "PrismLauncher");
        var flatpak = Dir(home, ".var", "app", "org.prismlauncher.PrismLauncher", "data", "PrismLauncher");
        File.WriteAllText(Path.Combine(native, "prismlauncher.cfg"), "Language=fr\n");
        File.WriteAllText(Path.Combine(flatpak, "prismlauncher.cfg"), "Language=fr\n");
        File.SetLastWriteTimeUtc(Path.Combine(native, "prismlauncher.cfg"), DateTime.UtcNow.AddDays(-30));

        // sans instance : la configuration utilisée le plus récemment
        Assert.Equal(flatpak, PrismLocator.Choose(PrismLocator.Find(home, null, null))!.DataDir);

        File.WriteAllText(Path.Combine(Dir(native, "instances", PrismInstance.FolderName), "instance.cfg"), "name=Create Warfare\n");
        Assert.Equal(native, PrismLocator.Choose(PrismLocator.Find(home, null, null))!.DataDir);
    }

    [Theory]
    [InlineData("", "instances")]
    [InlineData("InstanceDir=mes instances\n", "mes instances")]
    [InlineData("[General]\nConfigVersion=1.3\nInstanceDir=jeux\nLanguage=fr\n", "jeux")]
    public void Dossier_des_instances_lu_dans_prismlauncher_cfg(string cfg, string expected)
    {
        using var f = new FakeInstance();
        var data = Dir(f.Root, "PrismLauncher");
        if (cfg.Length > 0) File.WriteAllText(Path.Combine(data, "prismlauncher.cfg"), cfg);
        Assert.Equal(Path.Combine(data, expected), PrismLocator.InstancesDir(data));
    }

    [Fact]
    public void Dossier_des_instances_absolu()
    {
        using var f = new FakeInstance();
        var data = Dir(f.Root, "PrismLauncher");
        var elsewhere = Dir(f.Root, "autre disque", "instances");
        File.WriteAllText(Path.Combine(data, "prismlauncher.cfg"), "InstanceDir=" + elsewhere + "\n");
        Assert.Equal(elsewhere, PrismLocator.InstancesDir(data));
    }

    // ------------------------------------------------------------ Création de l'instance

    [Fact]
    public void Instance_creee_avec_les_versions_de_version_json()
    {
        using var f = new FakeInstance();
        var inst = Path.Combine(f.Root, "instances", PrismInstance.FolderName);

        Assert.True(PrismInstance.Ensure(inst, "1.21.1", "21.1.264", memoryMb: null, defaultMemoryMb: 8192));

        var cfg = Path.Combine(inst, "instance.cfg");
        Assert.Equal("OneSix", IniLines.Get(cfg, "InstanceType"));
        Assert.Equal("Create Warfare", IniLines.Get(cfg, "name"));
        Assert.Equal("true", IniLines.Get(cfg, "OverrideMemory"));
        Assert.Equal("8192", IniLines.Get(cfg, "MaxMemAlloc"));
        Assert.Equal("true", IniLines.Get(cfg, "OverrideCommands"));
        Assert.Equal("'$INST_DIR/create-warfare' prelaunch", IniLines.Get(cfg, "PreLaunchCommand"));
        Assert.True(PrismInstance.HasOurCommand(inst));
        Assert.True(Directory.Exists(Path.Combine(inst, "minecraft")));
        Assert.Equal(Path.Combine(inst, "minecraft"), PrismInstance.GameDir(inst));

        var pack = JsonNode.Parse(File.ReadAllText(Path.Combine(inst, "mmc-pack.json")))!;
        Assert.Equal(1, pack["formatVersion"]!.GetValue<int>());
        var comps = pack["components"]!.AsArray();
        Assert.Equal(2, comps.Count);
        Assert.Equal(("net.minecraft", "1.21.1", true), (comps[0]!["uid"]!.GetValue<string>(), comps[0]!["version"]!.GetValue<string>(), comps[0]!["important"]!.GetValue<bool>()));
        Assert.Equal(("net.neoforged", "21.1.264"), (comps[1]!["uid"]!.GetValue<string>(), comps[1]!["version"]!.GetValue<string>()));
        Assert.Equal(("1.21.1", "21.1.264"), PrismInstance.ReadComponents(inst));
    }

    [Fact]
    public void Versions_absentes_de_version_json_refusees()
    {
        using var f = new FakeInstance();
        Assert.Throws<InvalidDataException>(() => PrismInstance.Ensure(Path.Combine(f.Root, "i"), "1.21.1", "", null, 8192));
        Assert.False(Directory.Exists(Path.Combine(f.Root, "i")));
    }

    [Fact]
    public void Instance_existante_reecrite_par_Prism_seules_nos_lignes_changent()
    {
        using var f = new FakeInstance();
        var inst = Dir(f.Root, "instances", PrismInstance.FolderName);
        // ce que Prism écrit après avoir ouvert l'instance : format QSettings, réglages du joueur, composants résolus
        File.WriteAllText(Path.Combine(inst, "instance.cfg"), string.Join("\n",
            "[General]", "ConfigVersion=1.3", "InstanceType=OneSix", "name=Create Warfare", "MaxMemAlloc=12288", "OverrideMemory=true",
            "OverrideCommands=true", "PreLaunchCommand='$INST_DIR/create-warfare' prelaunch", "JavaPath=/opt/java21/bin/java", "totalTimePlayed=5400", ""));
        File.WriteAllText(Path.Combine(inst, "mmc-pack.json"), """
            {"components":[
              {"cachedName":"LWJGL 3","cachedVersion":"3.3.3","dependencyOnly":true,"uid":"org.lwjgl3","version":"3.3.3"},
              {"cachedName":"Minecraft","cachedRequires":[{"suggests":"3.3.3","uid":"org.lwjgl3"}],"cachedVersion":"1.21.1","important":true,"uid":"net.minecraft","version":"1.21.1"},
              {"cachedName":"NeoForge","cachedRequires":[{"equals":"1.21.1","uid":"net.minecraft"}],"cachedVersion":"21.1.200","uid":"net.neoforged","version":"21.1.200"}
            ],"formatVersion":1}
            """);

        Assert.False(PrismInstance.Ensure(inst, "1.21.1", "21.1.264", memoryMb: null, defaultMemoryMb: 8192));

        var cfg = Path.Combine(inst, "instance.cfg");
        Assert.Equal("12288", IniLines.Get(cfg, "MaxMemAlloc"));              // mémoire du joueur gardée
        Assert.Equal("/opt/java21/bin/java", IniLines.Get(cfg, "JavaPath"));
        Assert.Equal("5400", IniLines.Get(cfg, "totalTimePlayed"));
        Assert.StartsWith("[General]", File.ReadAllText(cfg));

        var comps = JsonNode.Parse(File.ReadAllText(Path.Combine(inst, "mmc-pack.json")))!["components"]!.AsArray();
        Assert.Equal(3, comps.Count);
        Assert.Equal("org.lwjgl3", comps[0]!["uid"]!.GetValue<string>());       // composant de Prism gardé, à sa place
        Assert.Equal("1.21.1", comps[1]!["cachedVersion"]!.GetValue<string>()); // Minecraft inchangé : cache gardé
        Assert.Equal("21.1.264", comps[2]!["version"]!.GetValue<string>());
        Assert.Null(comps[2]!["cachedVersion"]);                                 // NeoForge a changé : cache de l'ancienne version retiré
        Assert.Null(comps[2]!["cachedRequires"]);

        // rien à changer : les fichiers ne sont pas réécrits
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(inst, "mmc-pack.json"));
        Assert.False(PrismInstance.SyncComponents(inst, "1.21.1", "21.1.264"));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path.Combine(inst, "mmc-pack.json")));
    }

    [Fact]
    public void Memoire_changee_seulement_si_le_joueur_le_demande()
    {
        using var f = new FakeInstance();
        var inst = Path.Combine(f.Root, PrismInstance.FolderName);
        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", null, 8192);
        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", null, 6144);            // autre PC, autre conseil : pas touché
        Assert.Equal("8192", IniLines.Get(Path.Combine(inst, "instance.cfg"), "MaxMemAlloc"));
        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", 10240, 6144);           // --memory 10240
        Assert.Equal("10240", IniLines.Get(Path.Combine(inst, "instance.cfg"), "MaxMemAlloc"));
    }

    [Fact]
    public void Commande_du_joueur_gardee_sauf_en_reparation()
    {
        using var f = new FakeInstance();
        var inst = Dir(f.Root, PrismInstance.FolderName);
        var cfg = Path.Combine(inst, "instance.cfg");
        File.WriteAllText(cfg, "[General]\nConfigVersion=1.3\nname=Create Warfare\nOverrideCommands=true\nPreLaunchCommand=gamemoderun\n");

        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", null, 8192);
        Assert.Equal("gamemoderun", IniLines.Get(cfg, "PreLaunchCommand"));
        Assert.False(PrismInstance.HasOurCommand(inst));

        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", null, 8192, resetCommand: true);
        Assert.True(PrismInstance.HasOurCommand(inst));
    }

    [Fact]
    public void Commande_ajoutee_sous_General_si_elle_manque()
    {
        using var f = new FakeInstance();
        var inst = Dir(f.Root, PrismInstance.FolderName);
        var cfg = Path.Combine(inst, "instance.cfg");
        File.WriteAllText(cfg, "[General]\nConfigVersion=1.3\nname=Create Warfare\n\n[UI]\nx=1\n");

        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", null, 8192);

        var lines = File.ReadAllLines(cfg);
        var general = Array.IndexOf(lines, "[General]");
        var ui = Array.IndexOf(lines, "[UI]");
        var cmd = Array.FindIndex(lines, l => l.StartsWith("PreLaunchCommand="));
        Assert.True(general < cmd && cmd < ui, "la commande doit être dans la section [General]");
        Assert.True(PrismInstance.HasOurCommand(inst));
    }

    [Fact]
    public void Dossier_point_minecraft_d_une_ancienne_instance_respecte()
    {
        using var f = new FakeInstance();
        var inst = Dir(f.Root, "ancienne");
        Dir(inst, ".minecraft");
        Assert.Equal(Path.Combine(inst, ".minecraft"), PrismInstance.GameDir(inst));
        Dir(inst, "minecraft");
        Assert.Equal(Path.Combine(inst, "minecraft"), PrismInstance.GameDir(inst));
    }

    [Theory]
    [InlineData("PreLaunchCommand=\"\\\"$INST_DIR/x\\\" prelaunch\"", "\"$INST_DIR/x\" prelaunch")] // valeur échappée par QSettings
    [InlineData("PreLaunchCommand='$INST_DIR/x' prelaunch", "'$INST_DIR/x' prelaunch")]
    [InlineData("  PreLaunchCommand = a b ", "a b")]
    public void Valeur_d_une_ligne_ini(string line, string expected) =>
        Assert.Equal(expected, IniLines.Get(new[] { "[General]", "Autre=1", line }, "PreLaunchCommand"));

    // ------------------------------------------------------------ Le pack dans l'instance Prism

    [Fact]
    public async Task Installation_puis_mise_a_jour_gardent_les_reglages_du_joueur()
    {
        using var f = new FakeInstance();
        var inst = Path.Combine(f.Root, "PrismLauncher", "instances", PrismInstance.FolderName);
        PrismInstance.Ensure(inst, "1.21.1", "21.1.264", null, 8192);
        var game = PrismInstance.GameDir(inst);
        string[] rules = { "options.txt", "servers.dat", "saves/", "screenshots/", "config/inventoryprofilesnext/" };

        f.WriteOverride("config/x.toml", "x=1");
        ServerList.Ensure(Path.Combine(f.Overrides, "servers.dat"), new[] { new ServerEntry { Name = "Create Warfare", Ip = "jeu.robocnop.be:27018" } });
        var v1 = new VersionInfo { Version = "1.0.0", Minecraft = "1.21.1", Neoforge = "21.1.264", Mods = { f.CdnMod("a-1.jar", "A1") }, Protected = rules.ToList() };
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(game, UpdatePlanner.Plan(game, v1, f.Overrides, null, null), null, default);
        Assert.Equal("A1", File.ReadAllText(Path.Combine(game, "mods", "a-1.jar")));
        Assert.Equal(new[] { "jeu.robocnop.be:27018" }, ServerList.Ips(Path.Combine(game, "servers.dat")));   // posée au premier lancement

        // le joueur joue : touches, serveurs, monde, capture
        File.WriteAllText(Path.Combine(game, "options.txt"), "key_key.jump:key.keyboard.k\n");
        ServerList.Ensure(Path.Combine(game, "servers.dat"), new[] { new ServerEntry { Name = "Mon serveur", Ip = "play.exemple.fr" } });
        File.WriteAllText(Path.Combine(Dir(game, "saves", "monde"), "level.dat"), "monde");
        File.WriteAllText(Path.Combine(Dir(game, "screenshots"), "1.png"), "image");

        f.WriteOverride("config/x.toml", "x=2");
        f.WriteOverride("options.txt", "valeurs du pack");
        var v2 = new VersionInfo { Version = "2.0.0", Minecraft = "1.21.1", Neoforge = "21.1.264", Mods = { f.CdnMod("a-2.jar", "A2") }, Protected = rules.ToList() };
        var plan = UpdatePlanner.Plan(game, v2, f.Overrides, UpdatePlanner.ReadState(game), null);
        await new UpdateApplier(new Downloader(f.Cache)).ApplyAsync(game, plan, null, default);

        Assert.Equal("A2", File.ReadAllText(Path.Combine(game, "mods", "a-2.jar")));
        Assert.False(File.Exists(Path.Combine(game, "mods", "a-1.jar")));
        Assert.Equal("x=2", File.ReadAllText(Path.Combine(game, "config", "x.toml")));
        Assert.Equal("key_key.jump:key.keyboard.k\n", File.ReadAllText(Path.Combine(game, "options.txt")));
        Assert.Equal(new[] { "jeu.robocnop.be:27018", "play.exemple.fr" }, ServerList.Ips(Path.Combine(game, "servers.dat")));
        Assert.Equal("monde", File.ReadAllText(Path.Combine(game, "saves", "monde", "level.dat")));
        Assert.Equal("image", File.ReadAllText(Path.Combine(game, "screenshots", "1.png")));
        Assert.Equal("2.0.0", UpdatePlanner.ReadState(game)!.Version);
        // les fichiers de l'instance Prism ne sont pas dans le dossier du jeu : jamais vus par la mise à jour
        Assert.True(File.Exists(Path.Combine(inst, "instance.cfg")));
        Assert.True(File.Exists(Path.Combine(inst, "mmc-pack.json")));
    }
}
