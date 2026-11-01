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
