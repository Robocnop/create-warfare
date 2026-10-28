using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;
using Xunit.Abstractions;

namespace CreateWarfare.Launcher.Tests;

/// <summary>
/// Plan sur une vraie instance, sans rien écrire. Lancé seulement si CW_REAL_INSTANCE (dossier de l'instance)
/// et CW_LAUNCHER_MANIFEST (version.json généré par publish_launcher.py --local) sont définis.
/// </summary>
public class RealInstanceDryRun(ITestOutputHelper output)
{
    [Fact]
    public async Task Plan_sur_l_instance_reelle()
    {
        var dir = Environment.GetEnvironmentVariable("CW_REAL_INSTANCE");
        var manifest = Environment.GetEnvironmentVariable("CW_LAUNCHER_MANIFEST");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(manifest)) return;

        var dl = new Downloader(Path.Combine(Path.GetTempPath(), "cw-launcher-dryrun"));
        var v = await dl.FetchManifestAsync(manifest, default);
        var overrides = await dl.GetOverridesAsync(v, null, default);
        var info = InstanceFinder.Read(dir);
        var plan = UpdatePlanner.Plan(dir, v, overrides, UpdatePlanner.ReadState(dir), info);

        output.WriteLine($"{plan.FromVersion} -> {plan.ToVersion} : replace {plan.Count(FileActionKind.Replace)}, add {plan.Count(FileActionKind.Add)}, " +
                         $"remove {plan.Count(FileActionKind.Remove)}, same {plan.Count(FileActionKind.Same)}, protected {plan.Count(FileActionKind.Protected)}, " +
                         $"download {plan.DownloadSize / 1048576.0:0.0} MB");
        foreach (var a in plan.Changes.Concat(plan.Actions.Where(a => a.Kind == FileActionKind.Protected)))
            output.WriteLine($"  {a.Kind,-9} {a.RelPath}");
        foreach (var s in plan.ServersToAdd)
            output.WriteLine($"  Serveur   {s.Name} ({s.Ip})");

        // le servers.dat écrit par Minecraft doit ressortir identique à l'octet près (copie, l'original n'est pas touché)
        var servers = Path.Combine(dir, ServerList.FileName);
        if (File.Exists(servers))
        {
            var copy = Path.Combine(Path.GetTempPath(), "cw-servers-roundtrip.dat");
            Nbt.WriteFile(copy, Nbt.ReadFile(servers));
            Assert.Equal(File.ReadAllBytes(servers), File.ReadAllBytes(copy));
            output.WriteLine($"servers.dat : {ServerList.Ips(servers).Count} serveurs, relu et réécrit à l'identique");
        }
    }
}
