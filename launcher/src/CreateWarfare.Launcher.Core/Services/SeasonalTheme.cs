using System.Globalization;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Thèmes du launcher : l'identité par défaut et des surcouches saisonnières. Les dates sont dans ThemeSchedule.
/// </summary>
public static class SeasonalTheme
{
    public const string Default = "default";
    public const string Halloween = "halloween";
    public const string Easter = "easter";
    public const string Christmas = "christmas";
    public const string Summer = "summer";
    /// <summary>Réglage « automatique » : le thème suit la date.</summary>
    public const string Auto = "auto";

    /// <summary>Thèmes connus, dans l'ordre d'affichage des paramètres.</summary>
    public static readonly string[] All = { Default, Halloween, Easter, Christmas, Summer };

    /// <summary>Thème saisonnier du jour (voir ThemeSchedule), ou le thème de base hors période.</summary>
    public static string SeasonOf(DateTime day) => ThemeSchedule.ThemeOn(day);

    /// <summary>
    /// Thème à appliquer. Ordre : <paramref name="forced"/> (variable CW_THEME, pour les essais), puis le réglage du
    /// joueur s'il désigne un thème précis, puis la date si le réglage vaut « auto » ou est inconnu.
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
        Resolve(setting, Today(), Environment.GetEnvironmentVariable("CW_THEME"));

    /// <summary>
    /// Date locale du jour, ou celle de la variable CW_DATE (AAAA-MM-JJ) pour simuler une autre date pendant les essais.
    /// </summary>
    public static DateTime Today() => ParseDate(Environment.GetEnvironmentVariable("CW_DATE")) ?? DateTime.Today;

    /// <summary>« 2027-03-28 » → date ; vide ou illisible → null.</summary>
    public static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
