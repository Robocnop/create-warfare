using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using CreateWarfare.Launcher.Themes;
using CreateWarfare.Launcher.ViewModels;

namespace CreateWarfare.Launcher;

public partial class MainWindow : Window
{
    MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => OnStateChanged();
        Loaded += async (_, _) =>
        {
            await Vm.RefreshAsync();
            // Essais et captures : CW_START_PAGE=news|gallery|settings[:game|appearance|launcher|instance|advanced]|logs|about
            if (Environment.GetEnvironmentVariable("CW_START_PAGE") is { Length: > 0 } page)
            {
                var parts = page.Split(':', 2);
                if (parts[0] == "repair") Vm.RepairCommand.Execute(null); // analyse seule : rien n'est écrit sans confirmation
                else Vm.Navigate(parts[0]);
                if (parts.Length == 2) Vm.SettingsTab = parts[1];
            }
        };
        PreviewKeyDown += OnKey;
        ThemeManager.Changing += BeginThemeFade;
        Closed += (_, _) => ThemeManager.Changing -= BeginThemeFade;
    }

    /// <summary>
    /// Fondu entre deux thèmes : capture de la fenêtre avec l'ancien thème, posée par-dessus, puis effacée pendant
    /// que la nouvelle palette s'applique dessous. Pas de clignotement ni de rechargement. Coupé si Windows réduit les animations.
    /// </summary>
    void BeginThemeFade()
    {
        if (!IsLoaded || !Motion.Enabled || Content is not FrameworkElement root || root.ActualWidth < 1 || root.ActualHeight < 1) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var shot = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        ThemeFade.Visibility = Visibility.Collapsed;
        shot.Render(root);
        shot.Freeze();
        // fond de l'ancien thème sous la capture (zones transparentes de la grille)
        ThemeFade.Background = (TryFindResource("Bg") as Brush)?.CloneCurrentValue();
        ThemeFadeImage.Source = shot;
        ThemeFade.Visibility = Visibility.Visible;
        var fade = new DoubleAnimation(1, 0, Motion.Of(380)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        fade.Completed += (_, _) =>
        {
            ThemeFade.Visibility = Visibility.Collapsed;
            ThemeFadeImage.Source = null;
            ThemeFade.BeginAnimation(OpacityProperty, null);
        };
        ThemeFade.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Raccourcis : Entrée = bouton principal (accueil), Échap = retour, Ctrl+, = paramètres, flèches = images.</summary>
    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Vm.GoBack(); e.Handled = true; return; }
        if (Vm.IsDialogOpen)
        {
            if (e.Key == Key.Enter) { Vm.DialogYesCommand.Execute(null); e.Handled = true; }
            return;
        }
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control) { Vm.Navigate("settings"); e.Handled = true; return; }
        if (Vm.IsLightboxOpen)
        {
            if (e.Key == Key.Left) { Vm.LightboxPrevCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Right) { Vm.LightboxNextCommand.Execute(null); e.Handled = true; }
            return;
        }
        if (Vm.IsHome && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.ButtonBase)
        {
            if (e.Key == Key.Enter && !Vm.IsOnboarding && Vm.PrimaryCommand.CanExecute(null)) { Vm.PrimaryCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Left) { Vm.PrevSlideCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Right) { Vm.NextSlideCommand.Execute(null); e.Handled = true; }
        }
        if (Vm.IsOnboarding && e.Key == Key.Enter) { Vm.FinishOnboardingCommand.Execute(null); e.Handled = true; }
    }

    void OnCarouselHover(object sender, MouseEventArgs e) => Vm.CarouselPaused = true;
    void OnCarouselLeave(object sender, MouseEventArgs e) => Vm.CarouselPaused = false;

    void OnStateChanged()
    {
        // Agrandie, la fenêtre occupe exactement la zone de travail (WM_GETMINMAXINFO plus bas) : pas de bordure.
        BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
        MaxButton.Content = FindResource(WindowState == WindowState.Maximized ? "IcRestore" : "IcMaximize");
        MaxButton.ToolTip = WindowState == WindowState.Maximized ? "Restaurer" : "Agrandir";
    }

    /// <summary>
    /// Sans cadre Windows (WindowStyle=None), une fenêtre agrandie prend tout l'écran : la barre des tâches passe
    /// par-dessus le bas de la fenêtre. On donne à Windows la zone de travail de l'écran où se trouve la fenêtre.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)?.AddHook(WndProc);
    }

    static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;
        var monitor = Native.MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var mi = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (monitor == IntPtr.Zero || !Native.GetMonitorInfo(monitor, ref mi)) return IntPtr.Zero;
        var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.MINMAXINFO>(lParam);
        mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
        mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
        mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
        mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
        System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
        return IntPtr.Zero; // laisser Windows compléter (taille minimale, etc.)
    }

    static class Native
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
    }

    void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void OnClose(object sender, RoutedEventArgs e) => Close();
}
