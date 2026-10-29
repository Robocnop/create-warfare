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
