using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CreateWarfare.Launcher.Themes;

namespace CreateWarfare.Launcher.Controls;

/// <summary>
/// Carrousel plein cadre de l'accueil : fondu enchaîné entre deux calques et zoom lent (effet Ken Burns).
/// Les images (ImageSource déjà décodées) viennent du ViewModel ; sans image, l'illustration dessinée (HeroArt) reste.
/// Pause au survol (IsPaused), navigation par Next/Previous/Index, animations coupées si Windows le demande.
/// </summary>
public sealed class Carousel : Grid
{
    readonly HeroArt _fallback = new() { ShowTitle = false };
    readonly Rectangle[] _layers = { NewLayer(), NewLayer() };
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(8) };
    int _front;

    public Carousel()
    {
        ClipToBounds = true;
        Children.Add(_fallback);
        foreach (var l in _layers) Children.Add(l);
        _timer.Tick += (_, _) => { if (!IsPaused && Count > 1) Index = (Index + 1) % Count; };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => { if (IsVisible) _timer.Start(); else _timer.Stop(); };
    }

    // Un Rectangle peint par un ImageBrush, pas un Image : en UniformToFill, un Image plus large que son cadre
    // est calé à gauche (le sujet d'une image carrée sur fond large se retrouvait décalé à droite) ; le pinceau, lui, centre.
    static Rectangle NewLayer() => new()
    {
        Fill = new ImageBrush { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center },
        Opacity = 0,
        RenderTransformOrigin = new Point(0.5, 0.5),
        RenderTransform = new ScaleTransform(1, 1),
    };

    int Count => Images?.Cast<object>().Count() ?? 0;

    public static readonly DependencyProperty ImagesProperty = DependencyProperty.Register(
        nameof(Images), typeof(IEnumerable), typeof(Carousel), new PropertyMetadata(null, (d, _) => ((Carousel)d).Reset()));
    /// <summary>Liste d'ImageSource.</summary>
    public IEnumerable? Images { get => (IEnumerable?)GetValue(ImagesProperty); set => SetValue(ImagesProperty, value); }

    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
        nameof(Index), typeof(int), typeof(Carousel),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Carousel)d).Show()));
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused), typeof(bool), typeof(Carousel), new PropertyMetadata(false));
    public bool IsPaused { get => (bool)GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    void Reset()
    {
        foreach (var l in _layers) { l.BeginAnimation(OpacityProperty, null); l.Opacity = 0; ((ImageBrush)l.Fill).ImageSource = null; }
        _fallback.Visibility = Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (Index != 0) Index = 0; else Show();
        if (Images is System.Collections.Specialized.INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged -= OnCollectionChanged;
            ncc.CollectionChanged += OnCollectionChanged;
        }
    }

    void OnCollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _fallback.Visibility = Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (((ImageBrush)_layers[_front].Fill).ImageSource == null) Show();
    }

    void Show()
    {
        var list = Images?.Cast<object>().ToList();
        if (list == null || list.Count == 0) return;
        var src = list[Math.Clamp(Index, 0, list.Count - 1)] as ImageSource;
        if (src == null) return;

        var back = _layers[1 - _front];
        var front = _layers[_front];
        ((ImageBrush)back.Fill).ImageSource = src;
        Panel.SetZIndex(back, 2);
        Panel.SetZIndex(front, 1);

        var fade = new DoubleAnimation(0, 1, Motion.Of(900)) { EasingFunction = new QuadraticEase() };
        back.BeginAnimation(OpacityProperty, fade);
        front.BeginAnimation(OpacityProperty, new DoubleAnimation(0, Motion.Of(900)) { BeginTime = Motion.Enabled ? TimeSpan.FromMilliseconds(900) : TimeSpan.Zero });

        // Zoom lent, différent à chaque image (avant / arrière)
        var scale = (ScaleTransform)back.RenderTransform;
        var zoomIn = Index % 2 == 0;
        var zoom = new DoubleAnimation(zoomIn ? 1.0 : 1.08, zoomIn ? 1.08 : 1.0, Motion.Of(9000));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, Motion.Enabled ? zoom : null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, Motion.Enabled ? zoom : null);
        _front = 1 - _front;
        _timer.Stop();
        if (IsVisible) _timer.Start();
    }
}
