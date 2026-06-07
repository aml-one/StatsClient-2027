using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StatsClient.UserControls;

public partial class HolographicParticlesOverlay : UserControl
{
    private sealed class Particle
    {
        public required Ellipse Element { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; init; }
        public double Vy { get; init; }
        public double Size { get; init; }
        public double OpacityBase { get; init; }
        public double OpacityPhase { get; init; }
    }

    private readonly List<Particle> _particles = [];
    private readonly Random _random = new();
    private bool _isRenderingHooked;
    private DateTime _lastFrameTime = DateTime.UtcNow;

    private static readonly Color[] ParticleColors =
    [
        Color.FromArgb(150, 56, 189, 248),
        Color.FromArgb(120, 103, 232, 249),
        Color.FromArgb(110, 94, 234, 212),
        Color.FromArgb(90, 248, 250, 252),
    ];

    public HolographicParticlesOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        SizeChanged += (_, _) => ResetParticles();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ResetParticles();

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopRendering();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ResetParticles();
            StartRendering();
        }
        else
        {
            StopRendering();
        }
    }

    private void ResetParticles()
    {
        StopRendering();
        ParticleCanvas.Children.Clear();
        _particles.Clear();

        var width = ActualWidth > 32 ? ActualWidth : 360;
        var height = ActualHeight > 32 ? ActualHeight : 100;
        var count = (int)Math.Clamp((width * height) / 18000, 16, 40);

        for (var i = 0; i < count; i++)
        {
            var size = _random.NextDouble() * 3.2 + 1.4;
            var color = ParticleColors[_random.Next(ParticleColors.Length)];
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var element = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                IsHitTestVisible = false,
            };

            var particle = new Particle
            {
                Element = element,
                X = _random.NextDouble() * Math.Max(1, width - size),
                Y = _random.NextDouble() * Math.Max(1, height - size),
                Vx = (_random.NextDouble() - 0.5) * 28,
                Vy = (_random.NextDouble() - 0.5) * 18,
                Size = size,
                OpacityBase = _random.NextDouble() * 0.35 + 0.2,
                OpacityPhase = _random.NextDouble() * Math.PI * 2,
            };

            Canvas.SetLeft(element, particle.X);
            Canvas.SetTop(element, particle.Y);
            ParticleCanvas.Children.Add(element);
            _particles.Add(particle);
        }

        if (IsVisible)
            StartRendering();
    }

    private void StartRendering()
    {
        if (_isRenderingHooked)
            return;

        _isRenderingHooked = true;
        _lastFrameTime = DateTime.UtcNow;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_isRenderingHooked)
            return;

        _isRenderingHooked = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible || _particles.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var dt = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;
        if (dt <= 0 || dt > 0.1)
            dt = 0.016;

        var width = ActualWidth > 1 ? ActualWidth : 360;
        var height = ActualHeight > 1 ? ActualHeight : 100;
        var t = now.TimeOfDay.TotalSeconds;

        foreach (var p in _particles)
        {
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;

            if (p.X < 0) p.X = width;
            if (p.X > width) p.X = 0;
            if (p.Y < 0) p.Y = height;
            if (p.Y > height) p.Y = 0;

            p.Element.Opacity = p.OpacityBase + Math.Sin(t * 2.2 + p.OpacityPhase) * 0.18;
            Canvas.SetLeft(p.Element, p.X);
            Canvas.SetTop(p.Element, p.Y);
        }
    }
}
