namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Thèmes du launcher : l'identité par défaut, et des surcouches saisonnières.</summary>
public static class SeasonalTheme
{
    public const string Default = "default";
    public const string Halloween = "halloween";
    public const string Christmas = "christmas";
    public const string Summer = "summer";
    /// <summary>Réglage « automatique » : le thème suit la date.</summary>
    public const string Auto = "auto";

    /// <summary>Thèmes proposés dans les paramètres, dans l'ordre d'affichage.</summary>
    public static readonly string[] All = { Default, Halloween, Christmas, Summer };

    /// <summary>Thème Halloween du 15 octobre au 5 novembre inclus, chaque année.</summary>
    public static bool IsHalloween(DateTime day) =>
        (day.Month == 10 && day.Day >= 15) || (day.Month == 11 && day.Day <= 5);

    /// <summary>Variable CW_THEME (« halloween » ou « default », pour les essais), sinon la date.</summary>
    public static bool IsHalloween(DateTime day, string? forced) => Resolve(Auto, day, forced) == Halloween;

    public static bool IsHalloweenNow() => IsHalloween(DateTime.Now, Environment.GetEnvironmentVariable("CW_THEME"));

    /// <summary>Saison du jour : Halloween (15/10-05/11), Noël (01/12-06/01), été (juillet-août), sinon aucune.</summary>
    public static string SeasonOf(DateTime day)
    {
        if (IsHalloween(day)) return Halloween;
        if (day.Month == 12 || (day.Month == 1 && day.Day <= 6)) return Christmas;
        if (day.Month is 7 or 8) return Summer;
        return Default;
    }

    /// <summary>
    /// Thème à appliquer. Ordre : variable CW_THEME (essais), puis réglage du joueur (un thème précis), puis la date
    /// si le réglage est « auto » ou inconnu.
    /// </summary>
    public static string Resolve(string? setting, DateTime day, string? forced)
    {
        var f = forced?.Trim().ToLowerInvariant();
        if (f != null && All.Contains(f)) return f;
        var s = setting?.Trim().ToLowerInvariant();
        if (s != null && All.Contains(s)) return s;
        return SeasonOf(day);
    }

    public static string ResolveNow(string? setting) =>
        Resolve(setting, DateTime.Now, Environment.GetEnvironmentVariable("CW_THEME"));
}
