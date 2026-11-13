using System.Diagnostics;
using CreateWarfare.Launcher.Core.Native;
using CreateWarfare.Launcher.Core.Services;
using Xunit.Abstractions;

namespace CreateWarfare.Launcher.Tests;

/// <summary>
/// Essai réel du lancement natif, sans compte Microsoft : lancé seulement si CW_REAL_LAUNCH (dossier d'une COPIE de
/// l'instance, jamais l'instance du joueur) et CW_OFFLINE_TEST (pseudo) sont définis. CW_GAME_ROOT choisit le dossier
/// des bibliothèques et assets (sinon celui du launcher). Installe, lance le jeu, attend que les mods soient chargés et
/// l'écran titre prêt (logs/latest.log), puis ferme le jeu.
/// </summary>
public class RealNativeLaunchTrial(ITestOutputHelper output)
{
    [Fact]
    public async Task Lance_le_jeu_jusqu_a_l_ecran_titre()
    {
        var instance = Environment.GetEnvironmentVariable("CW_REAL_LAUNCH");
        var session = OfflineTest.FromEnvironment();
        if (string.IsNullOrEmpty(instance) || session == null) return;

        var cfRoot = CurseForgeLocator.FindMinecraftRoot();
        var launcher = new NativeLauncher(new Downloader(Path.Combine(Path.GetTempPath(), "cw-launcher-trial")), cfRoot);
        var sw = Stopwatch.StartNew();
        var java = await launcher.EnsureJavaAsync(null, default);
        output.WriteLine($"Java {java.Version} ({java.Source}) : {java.Executable}  [{sw.Elapsed.TotalSeconds:0.0} s]");
        var last = "";
        var game = await launcher.EnsureGameAsync(java, new Progress<InstallProgress>(p =>
        {
            var line = $"{p.Phase} {p.Done}/{p.Total}";
            if (line != last && (p.Done == p.Total || p.Phase != "check")) { last = line; }
        }), default);
        output.WriteLine($"Jeu prêt dans {game.GameRoot}, {game.Version.Libraries.Count} bibliothèques  [{sw.Elapsed.TotalSeconds:0.0} s]");

        var cmd = NativeLauncher.BuildCommand(game, java, instance, session, 8192, "cw-trial", server: null);
        var cmdLength = string.Join(' ', cmd.Arguments).Length;
        output.WriteLine($"Ligne de commande : {cmd.Arguments.Count} arguments, {cmdLength} caractères");
        output.WriteLine(cmd.Redacted());
        Assert.DoesNotContain("${", string.Join(' ', cmd.Arguments));

        var log = Path.Combine(instance, "logs", "latest.log");
        if (File.Exists(log)) File.Delete(log);
        using var p = NativeLauncher.Start(cmd);
        try
        {
            var deadline = DateTime.UtcNow.AddMinutes(8);
            string text = "";
            while (DateTime.UtcNow < deadline && !p.HasExited)
            {
                await Task.Delay(3000);
                text = ReadShared(log);
                if (text.Contains("Sound engine started") && text.Contains("textures/atlas/gui.png-atlas")) break;
            }
            Assert.False(p.HasExited, $"Le jeu s'est arrêté (code {(p.HasExited ? p.ExitCode : 0)}).\n{Tail(text, 40)}");
            await Task.Delay(8000); // laisse l'écran titre s'afficher
            text = ReadShared(log);
            var modFiles = text.Split('\n').Count(l => l.Contains("Found mod file") && l.Contains("mods folder locator"));
            output.WriteLine($"Mods trouvés dans mods/ : {modFiles}  [{sw.Elapsed.TotalSeconds:0.0} s]");
            foreach (var l in text.Split('\n').Where(l => l.Contains("ModLauncher running") || l.Contains("JVM identified") || l.Contains("Found mod file \"client-")
                         || l.Contains("Found mod file \"neoforge-") || l.Contains("Sound engine started") || l.Contains("Loading ") && l.Contains("mods")))
                output.WriteLine("  " + l.Trim());
            Assert.Contains("Sound engine started", text);
            Assert.True(modFiles > 200, "trop peu de mods chargés : " + modFiles);
            using (var q = new System.Management.ManagementObjectSearcher($"SELECT Name, CommandLine FROM Win32_Process WHERE ProcessId = {p.Id}"))
                foreach (var o in q.Get())
                    output.WriteLine($"WMI : {o["Name"]}, ligne de commande {(o["CommandLine"] as string)?.Length ?? -1} caractères, " +
                                     $"contient l'instance : {(o["CommandLine"] as string)?.Contains(instance, StringComparison.OrdinalIgnoreCase)} : " +
                                     ((o["CommandLine"] as string)?.Length < 300 ? o["CommandLine"] : ""));
            Assert.True(ProcessGuard.GameRunning(instance), "ProcessGuard ne voit pas le jeu");
        }
        finally
        {
            if (!p.HasExited) p.Kill(entireProcessTree: true);
            await p.WaitForExitAsync();
        }
    }

    /// <summary>
    /// Installation à partir de rien, sans miroir CurseForge (CW_REAL_INSTALL_FRESH = dossier vide) : tout est téléchargé
    /// chez Mojang et NeoForge, et l'installateur officiel produit le client patché, vérifié par son empreinte figée.
    /// </summary>
    [Fact]
    public async Task Installation_complete_sans_CurseForge()
    {
        var root = Environment.GetEnvironmentVariable("CW_REAL_INSTALL_FRESH");
        if (string.IsNullOrEmpty(root)) return;
        var java = JavaLocator.Find(CurseForgeLocator.FindMinecraftRoot()) ?? throw new InvalidOperationException("Java 21 introuvable");
        var dl = new Downloader(Path.Combine(root, "dl-cache"));
        var installer = new GameInstaller(root, Array.Empty<string>(), dl);
        var sw = Stopwatch.StartNew();
        InstallProgress? lastDownload = null;
        var phases = new HashSet<string>();
        var game = await installer.EnsureAsync(java.Executable, new SyncProgress(p => { lock (phases) { phases.Add(p.Phase); if (p.Phase == "download") lastDownload = p; } }), default);
        output.WriteLine($"Installé en {sw.Elapsed.TotalSeconds:0} s, phases : {string.Join(", ", phases)}, dernier téléchargement : {lastDownload}");
        Assert.Contains("neoforge", phases);
        foreach (var (path, sha1) in NativeConfig.InstallerOutputs)
            Assert.True(Hashing.Matches(Path.Combine(root, "libraries", path), sha1, sha1: true), path);
        // second passage : rien à télécharger, pas d'installateur
        phases.Clear();
        sw.Restart();
        await installer.EnsureAsync(java.Executable, new SyncProgress(p => { lock (phases) phases.Add(p.Phase); }), default);
        output.WriteLine($"Second passage : {sw.Elapsed.TotalSeconds:0.0} s, phases : {string.Join(", ", phases)}");
        Assert.DoesNotContain("download", phases);
        Assert.DoesNotContain("neoforge", phases);
    }

    sealed class SyncProgress(Action<InstallProgress> a) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) => a(value);
    }

    static string ReadShared(string path)
    {
        if (!File.Exists(path)) return "";
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    static string Tail(string s, int lines) => string.Join('\n', s.Split('\n').TakeLast(lines));
}
