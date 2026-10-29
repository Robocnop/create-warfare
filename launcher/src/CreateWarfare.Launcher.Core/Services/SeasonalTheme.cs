namespace CreateWarfare.Launcher.Core.Services;

public static class SeasonalTheme
{
    /// <summary>Thème Halloween du 15 octobre au 5 novembre inclus, chaque année.</summary>
    public static bool IsHalloween(DateTime day) =>
        (day.Month == 10 && day.Day >= 15) || (day.Month == 11 && day.Day <= 5);

    /// <summary>Variable CW_THEME (« halloween » ou « default », pour les essais), sinon la date.</summary>
    public static bool IsHalloween(DateTime day, string? forced) => forced?.Trim().ToLowerInvariant() switch
    {
        "halloween" => true,
        "default" => false,
        _ => IsHalloween(day),
    };

    public static bool IsHalloweenNow() => IsHalloween(DateTime.Now, Environment.GetEnvironmentVariable("CW_THEME"));
}
