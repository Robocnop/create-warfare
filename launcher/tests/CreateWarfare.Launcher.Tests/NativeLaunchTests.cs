using System.Net;
using System.Text;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Native;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

/// <summary>Lancement natif : manifestes, ligne de commande, Java, fichiers du jeu (sans réseau ni vrai jeu).</summary>
public class NativeLaunchTests
{
    // Extrait fidèle de versions/1.21.1/1.21.1.json et du profil NeoForge 21.1.264 (mêmes formes : règles, natives, ${variables})
    const string Vanilla = """
    {
      "id": "1.21.1", "type": "release", "mainClass": "net.minecraft.client.main.Main", "assets": "17",
      "assetIndex": { "id": "17", "sha1": "fe8483e9e95c56b261ed6ab05aa57f83d669edf1", "size": 449757, "url": "https://piston-meta.mojang.com/v1/packages/fe84/17.json" },
      "downloads": { "client": { "sha1": "30c73b1c5da787909b2f73340419fdf13b9def88", "size": 26836906, "url": "https://piston-data.mojang.com/v1/objects/30c7/client.jar" } },
      "javaVersion": { "component": "java-runtime-delta", "majorVersion": 21 },
      "libraries": [
        { "name": "ca.weblite:java-objc-bridge:1.1", "downloads": { "artifact": { "path": "ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar", "sha1": "aa", "size": 1, "url": "https://libraries.minecraft.net/a.jar" } },
          "rules": [ { "action": "allow", "os": { "name": "osx" } } ] },
        { "name": "com.google.guava:guava:32.1.2-jre", "downloads": { "artifact": { "path": "com/google/guava/guava/32.1.2-jre/guava-32.1.2-jre.jar", "sha1": "bb", "size": 2, "url": "https://libraries.minecraft.net/g.jar" } } },
        { "name": "org.lwjgl:lwjgl:3.3.3", "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar", "sha1": "cc", "size": 3, "url": "https://libraries.minecraft.net/l.jar" } } },
        { "name": "org.lwjgl:lwjgl:3.3.3:natives-windows", "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar", "sha1": "dd", "size": 4, "url": "https://libraries.minecraft.net/lw.jar" } },
          "rules": [ { "action": "allow", "os": { "name": "windows" } } ] },
        { "name": "org.lwjgl:lwjgl:3.3.3:natives-linux", "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-linux.jar", "sha1": "ee", "size": 5, "url": "https://libraries.minecraft.net/ll.jar" } },
          "rules": [ { "action": "allow", "os": { "name": "linux" } } ] }
      ],
      "arguments": {
        "game": [ "--username", "${auth_player_name}", "--version", "${version_name}", "--gameDir", "${game_directory}", "--assetsDir", "${assets_root}",
          "--assetIndex", "${assets_index_name}", "--uuid", "${auth_uuid}", "--accessToken", "${auth_access_token}", "--clientId", "${clientid}",
          "--xuid", "${auth_xuid}", "--userType", "${user_type}", "--versionType", "${version_type}",
          { "rules": [ { "action": "allow", "features": { "is_demo_user": true } } ], "value": "--demo" },
          { "rules": [ { "action": "allow", "features": { "has_custom_resolution": true } } ], "value": [ "--width", "${resolution_width}", "--height", "${resolution_height}" ] },
          { "rules": [ { "action": "allow", "features": { "has_quick_plays_support": true } } ], "value": [ "--quickPlayPath", "${quickPlayPath}" ] },
          { "rules": [ { "action": "allow", "features": { "is_quick_play_multiplayer": true } } ], "value": [ "--quickPlayMultiplayer", "${quickPlayMultiplayer}" ] } ],
        "jvm": [
          { "rules": [ { "action": "allow", "os": { "name": "osx" } } ], "value": [ "-XstartOnFirstThread" ] },
          { "rules": [ { "action": "allow", "os": { "name": "windows" } } ], "value": "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump" },
          { "rules": [ { "action": "allow", "os": { "arch": "x86" } } ], "value": "-Xss1M" },
          "-Djava.library.path=${natives_directory}", "-Dminecraft.launcher.brand=${launcher_name}", "-Dminecraft.launcher.version=${launcher_version}", "-cp", "${classpath}" ]
      }
    }
    """;

    const string Neo = """
    {
      "id": "neoforge-21.1.264", "inheritsFrom": "1.21.1", "type": "release", "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
      "libraries": [
        { "name": "cpw.mods:bootstraplauncher:2.0.2", "downloads": { "artifact": { "path": "cpw/mods/bootstraplauncher/2.0.2/bootstraplauncher-2.0.2.jar", "sha1": "11", "size": 6, "url": "https://maven.neoforged.net/b.jar" } } },
        { "name": "com.google.guava:guava:32.1.2-jre", "downloads": { "artifact": { "path": "com/google/guava/guava/32.1.2-jre/guava-32.1.2-jre.jar", "sha1": "bb", "size": 2, "url": "https://maven.neoforged.net/g.jar" } } }
      ],
      "arguments": {
        "game": [ "--fml.neoForgeVersion", "21.1.264", "--fml.fmlVersion", "4.0.45", "--fml.mcVersion", "1.21.1", "--fml.neoFormVersion", "20240808.144430", "--launchTarget", "forgeclient" ],
        "jvm": [ "-DignoreList=client-extra,${version_name}.jar", "-DlibraryDirectory=${library_directory}",
          "-p", "${library_directory}/cpw/mods/bootstraplauncher/2.0.2/bootstraplauncher-2.0.2.jar${classpath_separator}${library_directory}/org/ow2/asm/asm/9.10.1/asm-9.10.1.jar",
          "--add-modules", "ALL-MODULE-PATH", "--add-opens", "java.base/java.util.jar=cpw.mods.securejarhandler" ]
      }
    }
    """;

    static readonly OsInfo Win = new("windows", "x86_64", "10.0.26300");

    static GameVersion Merged() => VersionParser.Merge(VersionParser.Parse(Neo), VersionParser.Parse(Vanilla));

    static LaunchCommand Build(string? quickPlay = null) => LaunchCommandBuilder.Build(Merged(), new LaunchOptions
    {
        JavaExe = @"C:\java\bin\javaw.exe",
        GameDir = @"C:\inst\Create Warfare (1)",
        GameRoot = @"C:\root",
        NativesDir = @"C:\root\natives\neoforge-21.1.264",
        Session = new GameSession("Robo", "a4be82ed4fb847f7895e08b016d04423", "SECRET-TOKEN", "msa", "2535", false),
        RamMb = 10240,
        ClientId = "cid",
        QuickPlayServer = quickPlay,
        Os = Win,
    });

    [Fact]
    public void Fusion_heritage_bibliotheques_et_arguments()
    {
        var v = Merged();
        Assert.Equal("neoforge-21.1.264", v.Id);
        Assert.Equal("cpw.mods.bootstraplauncher.BootstrapLauncher", v.MainClass);
        Assert.Equal("17", v.AssetIndex!.Id);
        Assert.Equal(21, v.JavaMajor);
        Assert.Equal("30c73b1c5da787909b2f73340419fdf13b9def88", v.Client!.Sha1);
        // guava une seule fois (celle de NeoForge, d'abord), bootstraplauncher en tête
        Assert.Equal(1, v.Libraries.Count(l => l.Name.StartsWith("com.google.guava:guava:")));
        Assert.Equal("cpw.mods:bootstraplauncher:2.0.2", v.Libraries[0].Name);
        Assert.Contains(v.Libraries, l => l.Artifact!.Url == "https://maven.neoforged.net/g.jar");
        // arguments du parent puis de l'enfant
        Assert.Equal("--username", v.Game[0].Values[0]);
        Assert.Equal("--launchTarget", v.Game[^2].Values[0]);
    }

    [Fact]
    public void Regles_par_systeme()
    {
        var libs = LibraryResolver.Resolve(Merged(), Win).Select(l => l.File.Path).ToList();
        Assert.Contains("org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar", libs);
        Assert.DoesNotContain(libs, p => p.Contains("natives-linux") || p.Contains("objc"));
        var linux = LibraryResolver.Resolve(Merged(), new OsInfo("linux", "x86_64", "6")).Select(l => l.File.Path).ToList();
        Assert.Contains(linux, p => p.Contains("natives-linux"));

        var allowWin = new[] { new Rule(true, "windows", null, null, new Dictionary<string, bool>()) };
        Assert.True(Rule.Allowed(allowWin, Win));
        Assert.False(Rule.Allowed(allowWin, new OsInfo("osx", "arm64", "14")));
        var disallowOsx = new[] { new Rule(true, null, null, null, new Dictionary<string, bool>()), new Rule(false, "osx", null, null, new Dictionary<string, bool>()) };
        Assert.True(Rule.Allowed(disallowOsx, Win));
        Assert.False(Rule.Allowed(disallowOsx, new OsInfo("osx", "arm64", "14")));
        var demo = new[] { new Rule(true, null, null, null, new Dictionary<string, bool> { ["is_demo_user"] = true }) };
        Assert.False(Rule.Allowed(demo, Win, new Dictionary<string, bool> { ["is_demo_user"] = false }));
        Assert.True(Rule.Allowed(demo, Win, new Dictionary<string, bool> { ["is_demo_user"] = true }));
        Assert.False(Rule.Allowed(new[] { new Rule(true, null, "x86", null, new Dictionary<string, bool>()) }, Win));
    }

    [Fact]
    public void Ligne_de_commande_comme_CurseForge()
    {
        var a = Build().Arguments.ToList();
        var main = a.IndexOf("cpw.mods.bootstraplauncher.BootstrapLauncher");
        Assert.True(main > 0);
        var jvm = a.Take(main).ToList();
        var game = a.Skip(main + 1).ToList();

        Assert.Contains("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump", jvm);
        Assert.DoesNotContain("-XstartOnFirstThread", jvm);
        Assert.DoesNotContain("-Xss1M", jvm);
        Assert.Contains("-Xmx10240m", jvm);
        Assert.Contains("-Xms512m", jvm);
        Assert.Contains(@"-Djava.library.path=C:\root\natives\neoforge-21.1.264", jvm);
        Assert.Contains("-DignoreList=client-extra,neoforge-21.1.264.jar", jvm);
        Assert.Contains(@"-DlibraryDirectory=C:\root\libraries", jvm);
        Assert.Contains("ALL-MODULE-PATH", jvm);
        Assert.Contains("java.base/java.util.jar=cpw.mods.securejarhandler", jvm);
        // module path : chemins Windows, même écriture que le classpath
        var mp = jvm[jvm.IndexOf("-p") + 1];
        Assert.Equal(@"C:\root\libraries\cpw\mods\bootstraplauncher\2.0.2\bootstraplauncher-2.0.2.jar;C:\root\libraries\org\ow2\asm\asm\9.10.1\asm-9.10.1.jar", mp);
        var cp = jvm[jvm.IndexOf("-cp") + 1].Split(';');
        Assert.Equal(@"C:\root\libraries\cpw\mods\bootstraplauncher\2.0.2\bootstraplauncher-2.0.2.jar", cp[0]);
        Assert.Equal(@"C:\root\versions\neoforge-21.1.264\neoforge-21.1.264.jar", cp[^1]);
        Assert.Contains(@"C:\root\libraries\org\lwjgl\lwjgl\3.3.3\lwjgl-3.3.3-natives-windows.jar", cp);
        Assert.Equal(1, cp.Count(p => p.Contains("guava")));

        string After(string flag) => game[game.IndexOf(flag) + 1];
        Assert.Equal("Robo", After("--username"));
        Assert.Equal("neoforge-21.1.264", After("--version"));
        Assert.Equal(@"C:\inst\Create Warfare (1)", After("--gameDir"));
        Assert.Equal(@"C:\root\assets", After("--assetsDir"));
        Assert.Equal("17", After("--assetIndex"));
        Assert.Equal("a4be82ed4fb847f7895e08b016d04423", After("--uuid"));
        Assert.Equal("SECRET-TOKEN", After("--accessToken"));
        Assert.Equal("msa", After("--userType"));
        Assert.Equal("cid", After("--clientId"));
        Assert.Equal("2535", After("--xuid"));
        Assert.Equal("1024", After("--width"));
        Assert.Equal("forgeclient", After("--launchTarget"));
        Assert.DoesNotContain("--demo", game);
        Assert.DoesNotContain("--quickPlayPath", game);
        Assert.DoesNotContain("--quickPlayMultiplayer", game);
        Assert.DoesNotContain(a, x => x.Contains("${"));
    }

    [Fact]
    public void Connexion_directe_et_jeton_masque()
    {
        var cmd = Build("jeu.robocnop.be:27018");
        var a = cmd.Arguments.ToList();
        Assert.Equal("jeu.robocnop.be:27018", a[a.IndexOf("--quickPlayMultiplayer") + 1]);
        var shown = cmd.Redacted();
        Assert.DoesNotContain("SECRET-TOKEN", shown);
        Assert.DoesNotContain("SECRET-TOKEN", cmd.ToString());
        Assert.Contains("--accessToken ***", shown);
        Assert.Contains("\"C:\\inst\\Create Warfare (1)\"", shown);
    }

    [Fact]
    public void Mode_hors_ligne_jamais_de_connexion_au_serveur()
    {
        var off = OfflineTest.From("CwTrial")!;
        var game = new InstalledGame(@"C:\root", Merged(), @"C:\root\natives\x");
        var java = new JavaInstall(@"C:\java", @"C:\java\bin\javaw.exe", "21.0.10", 21, "test");
        var cmd = NativeLauncher.BuildCommand(game, java, Path.GetTempPath(), off, 8192, "cid", NativeConfig.ServerAddress);
        Assert.DoesNotContain("--quickPlayMultiplayer", cmd.Arguments);
        var a = cmd.Arguments.ToList();
        Assert.Equal("0", a[a.IndexOf("--accessToken") + 1]);
        Assert.Equal("legacy", a[a.IndexOf("--userType") + 1]);
    }

    [Fact]
    public void Hors_ligne_uuid_et_pseudo()
    {
        Assert.Equal("b50ad385829d3141a2167e7d7539ba7f", OfflineTest.OfflineUuid("Notch")); // valeur du jeu (UUID.nameUUIDFromBytes)
        var s = OfflineTest.From(" Robo_42 ")!;
        Assert.Equal("Robo_42", s.Name);
        Assert.True(s.Offline);
        Assert.Equal("0", s.AccessToken);
        Assert.Null(OfflineTest.From(null));
        Assert.Null(OfflineTest.From(""));
        Assert.Null(OfflineTest.From("ab"));
        Assert.Null(OfflineTest.From("nom avec espace"));
        Assert.Null(OfflineTest.From("x".PadRight(17, 'x')));
        Assert.DoesNotContain(s.AccessToken + ",", s.ToString());
    }

    [Fact]
    public void Coordonnees_maven()
    {
        Assert.Equal("net/neoforged/neoform/1.21.1-20240808.144430/neoform-1.21.1-20240808.144430.zip", Maven.Path("net.neoforged:neoform:1.21.1-20240808.144430@zip"));
        Assert.Equal("net/neoforged/neoforge/21.1.264/neoforge-21.1.264-universal.jar", Maven.Path("net.neoforged:neoforge:21.1.264:universal"));
        Assert.Equal("org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar", Maven.Path("org.lwjgl:lwjgl:3.3.3"));
        Assert.Throws<InvalidDataException>(() => Maven.Path("pas-maven"));
    }

    [Fact]
    public void Arguments_java_saisis_a_la_main()
    {
        Assert.Equal(new[] { "-Xss2m", "-Dname=a b", "-XX:+UseZGC" }, LaunchCommandBuilder.SplitArgs("  -Xss2m \"-Dname=a b\"   -XX:+UseZGC "));
        Assert.Empty(LaunchCommandBuilder.SplitArgs(null));
        var dir = Path.Combine(Path.GetTempPath(), "cw-jvmargs-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "minecraftinstance.json"), "{\"javaArgsOverride\": \"-XX:+UseZGC -Xmx2G\"}");
            Assert.Equal(new[] { "-XX:+UseZGC", "-Xmx2G" }, NativeLauncher.InstanceJvmArgs(dir));
            File.WriteAllText(Path.Combine(dir, "minecraftinstance.json"), "{\"javaArgsOverride\": null}");
            Assert.Empty(NativeLauncher.InstanceJvmArgs(dir));
        }
        finally { Directory.Delete(dir, true); }
        // la RAM des paramètres gagne sur un -Xmx de l'instance
        var cmd = LaunchCommandBuilder.Build(Merged(), new LaunchOptions
        {
            JavaExe = "java", GameDir = @"C:\i", GameRoot = @"C:\r", NativesDir = @"C:\n", RamMb = 6144, Os = Win,
            Session = OfflineTest.From("Robo")!, ExtraJvmArgs = new[] { "-Xmx2G", "-XX:+UseZGC" },
        });
        Assert.Contains("-Xmx6144m", cmd.Arguments);
        Assert.DoesNotContain("-Xmx2G", cmd.Arguments);
        Assert.Contains("-XX:+UseZGC", cmd.Arguments);
    }

    // ---------------------------------------------------------------- Java

    [Fact]
    public void Java_release_et_version()
    {
        var r = JavaLocator.ParseRelease("IMPLEMENTOR=\"Eclipse Adoptium\"\r\nJAVA_VERSION=\"21.0.10\"\r\nOS_ARCH=\"x86_64\"\r\n");
        Assert.Equal("21.0.10", r["JAVA_VERSION"]);
        Assert.Equal("x86_64", r["OS_ARCH"]);
        Assert.Equal(21, JavaLocator.MajorOf("21.0.10"));
        Assert.Equal(8, JavaLocator.MajorOf("1.8.0_402"));
        Assert.Equal(17, JavaLocator.MajorOf("17"));
        Assert.Equal(0, JavaLocator.MajorOf(null));
    }

    [Fact]
    public void Java_inspection_d_un_dossier()
    {
        var root = Path.Combine(Path.GetTempPath(), "cw-java-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            JavaInstall? Make(string name, string version, string arch = "x86_64", bool exe = true)
            {
                var home = Path.Combine(root, name);
                Directory.CreateDirectory(Path.Combine(home, "bin"));
                if (exe) File.WriteAllText(Path.Combine(home, "bin", "javaw.exe"), "");
                File.WriteAllText(Path.Combine(home, "release"), $"JAVA_VERSION=\"{version}\"\nOS_ARCH=\"{arch}\"\n");
                return JavaLocator.Inspect(home, "test");
            }
            Assert.NotNull(Make("ok", "21.0.4"));
            Assert.Null(Make("old", "17.0.2"));
            Assert.Null(Make("new", "25.0.1"));
            Assert.Null(Make("x86", "21.0.4", "x86"));
            Assert.Null(Make("noexe", "21.0.4", exe: false));
            Assert.Null(JavaLocator.Inspect(Path.Combine(root, "absent"), "test"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Reponse_Adoptium()
    {
        const string json = """
        [ { "binary": { "image_type": "jre", "package": { "checksum": "2332c0b4", "link": "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.13%2B9/OpenJDK21U-jre_x64_windows_hotspot_21.0.13_9.zip",
            "name": "OpenJDK21U-jre_x64_windows_hotspot_21.0.13_9.zip", "size": 49106397 } }, "release_name": "jdk-21.0.13+9" } ]
        """;
        var (link, sha, name, release) = JavaLocator.ParseAdoptium(json);
        Assert.StartsWith("https://github.com/adoptium/", link);
        Assert.Equal("2332c0b4", sha);
        Assert.Equal("OpenJDK21U-jre_x64_windows_hotspot_21.0.13_9.zip", name);
        Assert.Equal("jdk-21.0.13+9", release);
        Assert.Throws<InvalidDataException>(() => JavaLocator.ParseAdoptium("[]"));
        Assert.Throws<InvalidDataException>(() => JavaLocator.ParseAdoptium(json.Replace("https://github.com", "http://evil.example")));
    }

    // ---------------------------------------------------------------- Fichiers du jeu

    [Fact]
    public void Index_des_assets_et_profil_d_installation()
    {
        var files = GameInstaller.ParseAssetIndex("""
        { "objects": { "a.ogg": { "hash": "ABCDEF0123456789abcdef0123456789abcdef01", "size": 10 },
                       "b.ogg": { "hash": "abcdef0123456789abcdef0123456789abcdef01", "size": 10 },
                       "bad":   { "hash": "../../evil", "size": 1 } } }
        """);
        var f = Assert.Single(files);
        Assert.Equal("assets/objects/ab/abcdef0123456789abcdef0123456789abcdef01", f.RelPath);
        Assert.Equal("https://resources.download.minecraft.net/ab/abcdef0123456789abcdef0123456789abcdef01", f.Urls[0]);
        Assert.True(f.SizeOnly);

        var libs = GameInstaller.InstallProfileRuntimeLibraries("""
        { "libraries": [
          { "name": "net.neoforged.installertools:installertools:2.1.2", "downloads": { "artifact": { "path": "x.jar", "sha1": "1", "size": 1, "url": "https://x" } } },
          { "name": "net.neoforged:neoforge:21.1.264:universal", "downloads": { "artifact": { "path": "net/neoforged/neoforge/21.1.264/neoforge-21.1.264-universal.jar",
            "sha1": "a6d3c9d0c363750007f5cabc8df29490b9f26a5d", "size": 3558897, "url": "https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.264/neoforge-21.1.264-universal.jar" } } } ] }
        """);
        var u = Assert.Single(libs);
        Assert.Equal("a6d3c9d0c363750007f5cabc8df29490b9f26a5d", u.Sha1);
    }

    [Fact]
    public async Task Fichiers_repris_du_miroir_ou_telecharges()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cw-install-" + Guid.NewGuid().ToString("N")[..6]);
        var root = Path.Combine(tmp, "root");
        var mirror = Path.Combine(tmp, "curseforge");
        using var http = new FakeHttp();
        try
        {
            byte[] good = Encoding.UTF8.GetBytes("bibliothèque correcte"), other = Encoding.UTF8.GetBytes("autre bibliothèque");
            var goodSha = Sha1(good);
            var otherSha = Sha1(other);
            void Put(string dir, string rel, byte[] data) { var p = Path.Combine(dir, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllBytes(p, data); }
            Put(mirror, @"libraries\a\a.jar", good);                       // présent et correct chez CurseForge
            Put(mirror, @"libraries\b\b.jar", Encoding.UTF8.GetBytes("abîmé")); // présent mais faux : à télécharger
            http.Handler = r => r.Path == "/b.jar" ? new FakeHttp.Response(200, other) : new FakeHttp.Response(404, Array.Empty<byte>());

            var installer = new GameInstaller(root, new[] { mirror }, new Downloader(Path.Combine(tmp, "cache")) { RetryDelay = _ => TimeSpan.Zero });
            var missing = await installer.EnsureFilesAsync(new[]
            {
                new GameFile("libraries/a/a.jar", goodSha, good.Length, new[] { http.Url + "a.jar" }),
                new GameFile("libraries/b/b.jar", otherSha, other.Length, new[] { http.Url + "b.jar" }),
                new GameFile("libraries/c/c.jar", goodSha, good.Length, Array.Empty<string>()),
            }, null, default);

            Assert.Equal(good, File.ReadAllBytes(Path.Combine(root, @"libraries\a\a.jar")));
            Assert.Equal(other, File.ReadAllBytes(Path.Combine(root, @"libraries\b\b.jar")));
            Assert.Equal("libraries/c/c.jar", Assert.Single(missing).RelPath);
            Assert.DoesNotContain(http.Requests, r => r.Path == "/a.jar"); // repris sans réseau
            Assert.Equal(good, File.ReadAllBytes(Path.Combine(mirror, @"libraries\a\a.jar"))); // miroir intact

            // second passage : tout est déjà là, aucune requête
            var before = http.Requests.Count;
            await installer.EnsureFilesAsync(new[] { new GameFile("libraries/b/b.jar", otherSha, other.Length, new[] { http.Url + "b.jar" }) }, null, default);
            Assert.Equal(before, http.Requests.Count);

            // fichier local abîmé : remplacé
            File.WriteAllText(Path.Combine(root, @"libraries\b\b.jar"), "corrompu!!!!!!!!!!");
            await installer.EnsureFilesAsync(new[] { new GameFile("libraries/b/b.jar", otherSha, other.Length, new[] { http.Url + "b.jar" }) }, null, default);
            Assert.Equal(other, File.ReadAllBytes(Path.Combine(root, @"libraries\b\b.jar")));

            // un chemin qui sortirait de la racine est refusé
            await Assert.ThrowsAsync<InvalidDataException>(() => installer.EnsureFilesAsync(
                new[] { new GameFile("../evil.jar", goodSha, good.Length, new[] { http.Url + "a.jar" }) }, null, default));
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
    }

    [Fact]
    public void Lien_physique_ou_copie()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cw-link-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(tmp);
        try
        {
            var src = Path.Combine(tmp, "src.bin");
            File.WriteAllText(src, "contenu");
            var dest = Path.Combine(tmp, "sub", "dest.bin");
            File.WriteAllText(Path.Combine(tmp, "placeholder"), "");
            GameInstaller.LinkOrCopy(src, dest);
            Assert.Equal("contenu", File.ReadAllText(dest));
            GameInstaller.LinkOrCopy(src, dest); // remplace un fichier existant
            File.Delete(dest);
            Assert.True(File.Exists(src)); // supprimer le lien ne touche pas l'original
        }
        finally { Directory.Delete(tmp, true); }
    }

    static string Sha1(byte[] b) => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(b)).ToLowerInvariant();
}

/// <summary>Jetons : chiffrement DPAPI et session mémorisée.</summary>
public class TokenVaultTests
{
    [Fact]
    public void Chiffrement_aller_retour()
    {
        const string secret = "M.C540_BAY.0.U.-refresh-token-très-secret";
        var p = TokenVault.Protect(secret)!;
        Assert.NotEqual(secret, p);
        Assert.DoesNotContain("refresh", Encoding.UTF8.GetString(Convert.FromBase64String(p)));
        Assert.Equal(secret, TokenVault.Unprotect(p));
        Assert.NotEqual(p, TokenVault.Protect(secret)); // sel aléatoire de DPAPI
    }

    [Fact]
    public void Dechiffrement_impossible_renvoie_null()
    {
        Assert.Null(TokenVault.Protect(null));
        Assert.Null(TokenVault.Protect(""));
        Assert.Null(TokenVault.Unprotect(null));
        Assert.Null(TokenVault.Unprotect("pas du base64 !"));
        Assert.Null(TokenVault.Unprotect(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        var p = TokenVault.Protect("x", Encoding.UTF8.GetBytes("autre"));
        Assert.Null(TokenVault.Unprotect(p)); // autre entropie : refusé
    }

    [Fact]
    public void Session_memorisee_jusqu_a_expiration()
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var acc = AccountStore.ToAccount(new MsTokens("ms-access", "ms-refresh", now.AddHours(1)),
            new MinecraftLogin("mc-token", now.AddHours(24), "Robo", "a4be82ed4fb847f7895e08b016d04423", "2535"), now);
        Assert.DoesNotContain("ms-refresh", acc.RefreshToken);
        Assert.DoesNotContain("mc-token", acc.McToken);
        Assert.Equal("ms-refresh", TokenVault.Unprotect(acc.RefreshToken));

        var s = AccountStore.Cached(acc, now.AddHours(23))!;
        Assert.Equal("mc-token", s.AccessToken);
        Assert.Equal("Robo", s.Name);
        Assert.Equal("msa", s.UserType);
        Assert.False(s.Offline);
        Assert.Null(AccountStore.Cached(acc, now.AddHours(24).AddMinutes(-4))); // moins de 5 min : renouveler
        Assert.Null(AccountStore.Cached(null, now));
        acc.McToken = "illisible";
        Assert.Null(AccountStore.Cached(acc, now));

        // les réglages enregistrent le compte sans jeton en clair
        var json = System.Text.Json.JsonSerializer.Serialize(new LauncherSettings { Account = acc }, JsonCtx.Default.LauncherSettings);
        Assert.DoesNotContain("ms-refresh", json);
        Assert.DoesNotContain("mc-token", json);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, JsonCtx.Default.LauncherSettings)!;
        Assert.Equal("Robo", back.Account!.Name);
        Assert.Equal("ms-refresh", TokenVault.Unprotect(back.Account.RefreshToken));
    }
}
