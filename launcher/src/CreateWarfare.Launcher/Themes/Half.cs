using System.Globalization;
using System.Windows.Data;

namespace CreateWarfare.Launcher.Themes;

/// <summary>Divise une taille par deux (quart d'arc de l'indicateur qui tourne).</summary>
public sealed class Half : IValueConverter
{
    public static readonly Half Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => value is double d ? d / 2 : 0d;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
