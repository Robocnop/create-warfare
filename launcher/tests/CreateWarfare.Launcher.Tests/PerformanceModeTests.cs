using CreateWarfare.Launcher.Core.Services;

namespace CreateWarfare.Launcher.Tests;

public class PerformanceModeTests
{
    static string Mods(FakeInstance f)
    {
        var d = Path.Combine(f.Instance, "mods");
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Active_puis_desactive_ne_touche_que_les_mods_lourds()
    {
        using var f = new FakeInstance();
        var mods = Mods(f);
        File.WriteAllText(Path.Combine(mods, "visuality-forge-3.0.0.jar"), "v");
        File.WriteAllText(Path.Combine(mods, "emf_compat_core_neoforge_1.21.1_2.3.2.jar"), "e");
        File.WriteAllText(Path.Combine(mods, "create-1.21.1-6.0.10.jar"), "c");
        var ours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(2, PerformanceMode.Apply(f.Instance, true, ours));
        Assert.True(File.Exists(Path.Combine(mods, "visuality-forge-3.0.0.jar.disabled")));
        Assert.True(File.Exists(Path.Combine(mods, "create-1.21.1-6.0.10.jar")));
        Assert.Contains("visuality", ours);

        Assert.Equal(2, PerformanceMode.Apply(f.Instance, false, ours));
        Assert.True(File.Exists(Path.Combine(mods, "visuality-forge-3.0.0.jar")));
        Assert.True(File.Exists(Path.Combine(mods, "emf_compat_core_neoforge_1.21.1_2.3.2.jar")));
        Assert.Empty(ours);
    }

    [Fact]
    public void Un_mod_desactive_par_le_joueur_reste_desactive()
    {
        using var f = new FakeInstance();
        var mods = Mods(f);
        File.WriteAllText(Path.Combine(mods, "fallingleaves-1.21.1-2.6.0.jar.disabled"), "f");
        var ours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        PerformanceMode.Apply(f.Instance, true, ours);
        PerformanceMode.Apply(f.Instance, false, ours);
        Assert.True(File.Exists(Path.Combine(mods, "fallingleaves-1.21.1-2.6.0.jar.disabled")));
        Assert.False(File.Exists(Path.Combine(mods, "fallingleaves-1.21.1-2.6.0.jar")));
    }

    [Fact]
    public void Un_jar_repose_par_une_mise_a_jour_est_redesactive()
    {
        using var f = new FakeInstance();
        var mods = Mods(f);
        File.WriteAllText(Path.Combine(mods, "pfsable-1.0.jar.disabled"), "ancien");
        File.WriteAllText(Path.Combine(mods, "pfsable-1.0.jar"), "nouveau"); // override du zip reposé
        var ours = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pfsable" };

        PerformanceMode.Apply(f.Instance, true, ours);
        Assert.False(File.Exists(Path.Combine(mods, "pfsable-1.0.jar")));
        Assert.Equal("nouveau", File.ReadAllText(Path.Combine(mods, "pfsable-1.0.jar.disabled")));
    }
}
