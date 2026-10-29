using System.Text.Json;
using CreateWarfare.Launcher.Core.Model;
using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class VersionAndUninstallTests
{
    static VersionInfo Manifest() =>
        JsonSerializer.Deserialize("""
            { "version": "1.5.4", "zip": { "url": "a" },
              "prerelease": { "version": "1.5.5", "zip": { "url": "b" }, "mods": [ { "file": "x.jar" } ] } }
            """, JsonCtx.Default.VersionInfo)!;

    [Fact]
    public void Prerelease_lue_depuis_version_json()
    {
        var m = Manifest();
        Assert.Equal("1.5.4", m.Version);
        Assert.Equal("1.5.5", m.Prerelease!.Version);
        Assert.Equal("x.jar", m.Prerelease.Mods[0].File);
    }

    [Fact]
    public void Sans_mode_dev_seule_la_stable_est_proposee()
    {
        var m = Manifest();
        Assert.Single(VersionPicker.Choices(m, devMode: false));
        Assert.Equal("1.5.4", VersionPicker.Pick(m, devMode: false, Channels.Prerelease).Info.Version);
    }

    [Fact]
    public void Mode_dev_propose_et_garde_la_prerelease()
    {
        var m = Manifest();
        Assert.Equal(2, VersionPicker.Choices(m, devMode: true).Count);
        var pick = VersionPicker.Pick(m, devMode: true, Channels.Prerelease);
        Assert.True(pick.IsPrerelease);
        Assert.Equal("1.5.5", pick.Info.Version);
    }

    [Fact]
    public void Sans_prerelease_publiee_on_retombe_sur_la_stable()
    {
        var m = Manifest();
        m.Prerelease = null;
        Assert.Equal("1.5.4", VersionPicker.Pick(m, devMode: true, Channels.Prerelease).Info.Version);
    }

    [Fact]
    public void Desinstallation_sauvegarde_les_mondes_puis_supprime_l_instance()
    {
        using var f = new FakeInstance();
        Directory.CreateDirectory(Path.Combine(f.Instance, "saves", "Monde", "region"));
        File.WriteAllText(Path.Combine(f.Instance, "saves", "Monde", "region", "r.0.0.mca"), "MONDE");
        Directory.CreateDirectory(Path.Combine(f.Instance, "screenshots"));
        var ro = Path.Combine(f.Instance, "mods", "lecture-seule.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(ro)!);
        File.WriteAllText(ro, "x");
        File.SetAttributes(ro, FileAttributes.ReadOnly);

        var root = Path.Combine(f.Cache, "sauvegarde");
        var saved = Uninstaller.BackupPlayerData(f.Instance, root, new DateTime(2026, 10, 29, 12, 0, 0));
        Uninstaller.DeleteInstance(f.Instance);

        Assert.Equal(Path.Combine(root, "2026-10-29_12-00-00"), saved);
        Assert.Equal("MONDE", File.ReadAllText(Path.Combine(saved!, "saves", "Monde", "region", "r.0.0.mca")));
        Assert.False(Directory.Exists(Path.Combine(saved!, "screenshots")));
        Assert.False(Directory.Exists(f.Instance));
    }

    [Fact]
    public void Rien_a_sauvegarder_renvoie_null()
    {
        using var f = new FakeInstance();
        Assert.Null(Uninstaller.BackupPlayerData(f.Instance, Path.Combine(f.Cache, "s"), DateTime.Now));
    }

    [Fact]
    public void Script_de_suppression_attend_le_launcher_puis_efface_tout()
    {
        var script = Uninstaller.SelfRemovalScript(1234, @"C:\Jeux\CreateWarfareLauncher.exe", @"C:\Users\x\AppData\Local\CreateWarfareLauncher");
        Assert.Contains("PID eq 1234", script);
        Assert.Contains(@"del /f /q ""C:\Jeux\CreateWarfareLauncher.exe""", script);
        Assert.Contains(@"rmdir /s /q ""C:\Users\x\AppData\Local\CreateWarfareLauncher""", script);
        Assert.EndsWith("del /f /q \"%~f0\"\r\n", script);
    }
}
