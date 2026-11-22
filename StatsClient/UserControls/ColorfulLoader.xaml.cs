using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StatsClient.UserControls;

public partial class ColorfulLoader : UserControl
{
    private const double CanvasWidth = 272;
    private const double CanvasHeight = 210;
    private const double OrbitCenterX = CanvasWidth / 2;
    private const double OrbitCenterY = CanvasHeight / 2;
    private const double OrbitRadiusX = 98;
    private const double OrbitRadiusY = 36;
    private const double MoonBaseSize = 10;
    private const int LogoZIndex = 2;
    private const int MoonBehindZIndex = 1;
    private const int MoonFrontZIndex = 3;

    private readonly Ellipse[] _moons;
    private readonly DispatcherTimer _orbitTimer;
    private double _orbitAngle;

    public ColorfulLoader()
    {
        InitializeComponent();
        _moons = [Moon0, Moon1, Moon2, Moon3, Moon4, Moon5, Moon6, Moon7];

        _orbitTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _orbitTimer.Tick += OnOrbitTick;

        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CenterLogo();
        if (IsVisible)
            StartAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            StartAnimation();
        else
            StopAnimation();
    }

    private void CenterLogo()
    {
        LogoHost.UpdateLayout();
        double logoWidth = LogoHost.ActualWidth > 0 ? LogoHost.ActualWidth : 200;
        Canvas.SetLeft(LogoHost, OrbitCenterX - logoWidth / 2);
        Canvas.SetTop(LogoHost, OrbitCenterY - LogoHost.ActualHeight / 2);
        Panel.SetZIndex(LogoHost, LogoZIndex);
    }

    private void StartAnimation()
    {
        if (!_orbitTimer.IsEnabled)
            _orbitTimer.Start();
    }

    private void StopAnimation()
    {
        _orbitTimer.Stop();
    }

    private void OnOrbitTick(object? sender, EventArgs e)
    {
        _orbitAngle = (_orbitAngle + 1.0) % 360;
        double moonSpacing = 360.0 / _moons.Length;

        for (int i = 0; i < _moons.Length; i++)
        {
            double radians = (_orbitAngle + i * moonSpacing) * Math.PI / 180.0;
            double depth = Math.Sin(radians);
            double depth01 = (depth + 1.0) * 0.5;
            double size = MoonBaseSize * (0.55 + 0.55 * depth01);

            double x = OrbitCenterX + OrbitRadiusX * Math.Cos(radians) - size / 2;
            double y = OrbitCenterY + OrbitRadiusY * Math.Sin(radians) - size / 2;

            Ellipse moon = _moons[i];
            moon.Width = size;
            moon.Height = size;
            moon.Opacity = 0.3 + 0.7 * depth01;
            Canvas.SetLeft(moon, x);
            Canvas.SetTop(moon, y);
            Panel.SetZIndex(moon, depth >= 0 ? MoonFrontZIndex : MoonBehindZIndex);
        }

        Panel.SetZIndex(LogoHost, LogoZIndex);
    }
}
