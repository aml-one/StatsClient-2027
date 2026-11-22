using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StatsClient.UserControls;

public partial class InfinityCometLoader : UserControl
{
    private const double CanvasWidth = 280;
    private const double CanvasHeight = 140;
    private const double CenterX = CanvasWidth / 2;
    private const double CenterY = CanvasHeight / 2;
    private const double RadiusX = 108;
    private const double RadiusY = 46;
    private const double RadiansPerSecond = 5.2;
    private const double MaxStepSeconds = 0.05;
    private const int TrailCount = 10;
    private const double HeadSize = 12;
    private const double GlowSize = 28;

    private readonly Ellipse[] _trail;
    private double _phase;
    private long _lastTickUtcTicks = DateTime.UtcNow.Ticks;
    private bool _renderingSubscribed;

    public InfinityCometLoader()
    {
        InitializeComponent();
        _trail = [Trail0, Trail1, Trail2, Trail3, Trail4, Trail5, Trail6, Trail7, Trail8, Trail9];

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public void EnsureAnimationRunning()
    {
        if (!IsLoaded)
        {
            return;
        }

        _lastTickUtcTicks = DateTime.UtcNow.Ticks;
        if (!_renderingSubscribed)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderingSubscribed = true;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildInfinityGuide();
        EnsureAnimationRunning();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopAnimation();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            EnsureAnimationRunning();
        }
        else
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        if (!_renderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _renderingSubscribed = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible || !IsLoaded)
        {
            return;
        }

        var now = DateTime.UtcNow.Ticks;
        var elapsedSeconds = (now - _lastTickUtcTicks) / (double)TimeSpan.TicksPerSecond;
        _lastTickUtcTicks = now;

        if (elapsedSeconds <= 0)
        {
            return;
        }

        elapsedSeconds = Math.Min(elapsedSeconds, MaxStepSeconds);
        _phase = (_phase + elapsedSeconds * RadiansPerSecond) % (Math.PI * 2);

        for (int i = 0; i < TrailCount; i++)
        {
            double trailPhase = _phase - i * 0.22;
            PlaceCometElement(_trail[i], trailPhase, baseSize: 9 - i * 0.55, opacity: Math.Max(0, 0.82 - i * 0.08));
        }

        PlaceCometElement(CometGlow, _phase, GlowSize, 0.55);
        PlaceCometElement(CometHead, _phase, HeadSize, 1.0);
        Panel.SetZIndex(CometHead, 4);
        Panel.SetZIndex(CometGlow, 3);
    }

    private void BuildInfinityGuide()
    {
        var figure = new PathFigure { IsClosed = false };
        bool started = false;

        for (double t = 0; t <= Math.PI * 2; t += 0.04)
        {
            (double x, double y) = GetPosition(t);
            var point = new Point(x, y);
            if (!started)
            {
                figure.StartPoint = point;
                started = true;
            }
            else
            {
                figure.Segments.Add(new LineSegment(point, true));
            }
        }

        InfinityGuide.Data = new PathGeometry([figure]);
    }

    private void PlaceCometElement(Shape element, double phase, double baseSize, double opacity)
    {
        (double x, double y) = GetPosition(phase);
        element.Width = baseSize;
        element.Height = baseSize;
        element.Opacity = opacity;
        Canvas.SetLeft(element, x - baseSize / 2);
        Canvas.SetTop(element, y - baseSize / 2);
    }

    /// <summary>Horizontal lemniscate — sideways figure-8.</summary>
    private static (double X, double Y) GetPosition(double t)
    {
        double x = CenterX + RadiusX * Math.Sin(t);
        double y = CenterY + RadiusY * Math.Sin(t) * Math.Cos(t);
        return (x, y);
    }
}
