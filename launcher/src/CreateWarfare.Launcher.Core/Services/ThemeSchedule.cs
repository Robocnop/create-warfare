namespace CreateWarfare.Launcher.Core.Services;

/// <summary>
/// Calendrier des thèmes saisonniers : LE fichier à modifier pour changer les dates.
/// <para>
/// Chaque période a un début et une fin (inclus), en date fixe (<see cref="Fixed"/>) ou relative à Pâques
/// (<see cref="Easter"/>). Une période dont la fin tombe avant le début se termine l'année suivante (Noël : 1er décembre
/// → 6 janvier). Hors de toute période : thème de base.
/// </para>
/// <para>
/// Chevauchement : la période de priorité la plus haute gagne ; à priorité égale, la première de la liste.
/// Règle retenue : une fête courte passe avant une saison longue (Halloween 40 > Noël 30 > Pâques 20 > Été 10).
/// Avec les dates par défaut, aucune période n'en chevauche une autre.
/// </para>
/// </summary>
public static class ThemeSchedule
{
    public static readonly IReadOnlyList<ThemePeriod> Periods = new ThemePeriod[]
    {
        //  thème                     début                 fin (incluse)        priorité
        new(SeasonalTheme.Halloween, Fixed(10, 15), Fixed(11, 2), 40),
        new(SeasonalTheme.Christmas, Fixed(12, 1), Fixed(1, 6), 30),
        new(SeasonalTheme.Easter, Easter(-14), Easter(+1), 20),       // 2 semaines avant Pâques → lundi de Pâques
        new(SeasonalTheme.Summer, Fixed(6, 21), Fixed(9, 22), 10),
    };

    /// <summary>Même jour chaque année (le 29 février devient le 28 les années non bissextiles).</summary>
    public static DateAnchor Fixed(int month, int day) => new FixedDate(month, day);

    /// <summary>Jour relatif au dimanche de Pâques de l'année (−14 = deux semaines avant, +1 = lundi de Pâques).</summary>
    public static DateAnchor Easter(int offsetDays) => new EasterDate(offsetDays);

    /// <summary>
    /// Dimanche de Pâques du calendrier grégorien (algorithme de Meeus/Jones/Butcher, années 1583 et après).
    /// </summary>
    public static DateTime EasterSunday(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, day);
    }

    /// <summary>Vrai si le jour tombe dans la période (en comptant les périodes commencées l'année précédente).</summary>
    public static bool Contains(ThemePeriod p, DateTime day)
    {
        day = day.Date;
        for (int y = day.Year - 1; y <= day.Year; y++)
        {
            var start = p.Start.On(y);
            var end = p.End.On(y);
            if (end < start) end = p.End.On(y + 1);
            if (day >= start && day <= end) return true;
        }
        return false;
    }

    /// <summary>Thème saisonnier du jour (thème de base hors période).</summary>
    public static string ThemeOn(DateTime day, IEnumerable<ThemePeriod>? periods = null)
    {
        ThemePeriod? best = null;
        foreach (var p in periods ?? Periods)
            if (Contains(p, day) && (best == null || p.Priority > best.Priority)) best = p;
        return best?.Theme ?? SeasonalTheme.Default;
    }

    sealed record FixedDate(int Month, int Day) : DateAnchor
    {
        public override DateTime On(int year) => new(year, Month, Math.Min(Day, DateTime.DaysInMonth(year, Month)));
    }

    sealed record EasterDate(int OffsetDays) : DateAnchor
    {
        public override DateTime On(int year) => EasterSunday(year).AddDays(OffsetDays);
    }
}

/// <summary>Une date qui se recalcule chaque année.</summary>
public abstract record DateAnchor
{
    public abstract DateTime On(int year);
}

/// <summary>Période d'un thème saisonnier (début et fin inclus).</summary>
public sealed record ThemePeriod(string Theme, DateAnchor Start, DateAnchor End, int Priority);
