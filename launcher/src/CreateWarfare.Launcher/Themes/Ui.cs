using System.Windows;

namespace CreateWarfare.Launcher.Themes;

/// <summary>Propriétés attachées lues par les modèles de Controls.xaml (icône d'un bouton, état « en cours »).</summary>
public static class Ui
{
    /// <summary>Glyphe affiché avant le texte d'un bouton (clé Ic* de Tokens.xaml).</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached("Icon", typeof(string), typeof(Ui), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
    public static string? GetIcon(DependencyObject o) => (string?)o.GetValue(IconProperty);
    public static void SetIcon(DependencyObject o, string? v) => o.SetValue(IconProperty, v);

    /// <summary>Bouton en cours : le texte laisse la place à un indicateur qui tourne, et le clic est bloqué.</summary>
    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.RegisterAttached("IsBusy", typeof(bool), typeof(Ui), new PropertyMetadata(false));
    public static bool GetIsBusy(DependencyObject o) => (bool)o.GetValue(IsBusyProperty);
    public static void SetIsBusy(DependencyObject o, bool v) => o.SetValue(IsBusyProperty, v);

    /// <summary>Texte secondaire (sous-titre d'un élément de navigation, raccourci clavier d'un bouton).</summary>
    public static readonly DependencyProperty HintProperty =
        DependencyProperty.RegisterAttached("Hint", typeof(string), typeof(Ui), new PropertyMetadata(null));
    public static string? GetHint(DependencyObject o) => (string?)o.GetValue(HintProperty);
    public static void SetHint(DependencyObject o, string? v) => o.SetValue(HintProperty, v);
    /// <summary>Fond au survol et à l'appui (lu par les modèles de boutons communs).</summary>
    public static readonly DependencyProperty HoverBrushProperty =
        DependencyProperty.RegisterAttached("HoverBrush", typeof(System.Windows.Media.Brush), typeof(Ui), new PropertyMetadata(null));
    public static System.Windows.Media.Brush? GetHoverBrush(DependencyObject o) => (System.Windows.Media.Brush?)o.GetValue(HoverBrushProperty);
    public static void SetHoverBrush(DependencyObject o, System.Windows.Media.Brush? v) => o.SetValue(HoverBrushProperty, v);

    public static readonly DependencyProperty PressedBrushProperty =
        DependencyProperty.RegisterAttached("PressedBrush", typeof(System.Windows.Media.Brush), typeof(Ui), new PropertyMetadata(null));
    public static System.Windows.Media.Brush? GetPressedBrush(DependencyObject o) => (System.Windows.Media.Brush?)o.GetValue(PressedBrushProperty);
    public static void SetPressedBrush(DependencyObject o, System.Windows.Media.Brush? v) => o.SetValue(PressedBrushProperty, v);
}
