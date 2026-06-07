using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StatsClient.UserControls;

public partial class StartupParticlesOverlay : UserControl
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
        Color.FromArgb(170, 247, 148, 29),
        Color.FromArgb(140, 255, 255, 255),
        Color.FromArgb(120, 120, 168, 210),
        Color.FromArgb(130, 180, 210, 255),
    ];

    public StartupParticlesOverlay()
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

        var width = ActualWidth > 32 ? ActualWidth : 1600;
        var height = ActualHeight > 32 ? ActualHeight : 900;
        var count = (int)Math.Clamp((width * height) / 32000, 28, 72);

        for (var i = 0; i < count; i++)
        {
            var size = _random.NextDouble() * 4.5 + 2.0;
            var color = ParticleColors[_random.Next(ParticleColors.Length)];
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var element = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            var particle = new Particle
            {
                Element = element,
                X = _random.NextDouble() * Math.Max(1, width - size),
                Y = _random.NextDouble() * Math.Max(1, height - size),
                Vx = (_random.NextDouble() - 0.5) * 42,
                Vy = (_random.NextDouble() - 0.5) * 42,
                Size = size,
                OpacityBase = _random.NextDouble() * 0.35 + 0.25,
                OpacityPhase = _random.NextDouble() * Math.PI * 2,
            };

            Canvas.SetLeft(element, particle.X);
            Canvas.SetTop(element, particle.Y);
            element.Opacity = particle.OpacityBase;

            _particles.Add(particle);
            ParticleCanvas.Children.Add(element);
        }

        if (IsVisible)
        {
            StartRendering();
        }
    }

    private void StartRendering()
    {
        if (_isRenderingHooked)
        {
            return;
        }

        _lastFrameTime = DateTime.UtcNow;
        CompositionTarget.Rendering += OnRendering;
        _isRenderingHooked = true;
    }

    private void StopRendering()
    {
        if (!_isRenderingHooked)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _isRenderingHooked = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible || _particles.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var deltaSeconds = (now - _lastFrameTime).TotalSeconds;
        if (deltaSeconds <= 0 || deltaSeconds > 0.1)
        {
            deltaSeconds = 1.0 / 60.0;
        }

        _lastFrameTime = now;

        var width = ActualWidth > 1 ? ActualWidth : ParticleCanvas.ActualWidth;
        var height = ActualHeight > 1 ? ActualHeight : ParticleCanvas.ActualHeight;
        if (width < 1 || height < 1)
        {
            return;
        }

        var pulseTime = now.TimeOfDay.TotalSeconds;

        foreach (var particle in _particles)
        {
            particle.X += particle.Vx * deltaSeconds;
            particle.Y += particle.Vy * deltaSeconds;

            if (particle.X < -particle.Size)
            {
                particle.X = width;
            }
            else if (particle.X > width)
            {
                particle.X = -particle.Size;
            }

            if (particle.Y < -particle.Size)
            {
                particle.Y = height;
            }
            else if (particle.Y > height)
            {
                particle.Y = -particle.Size;
            }

            Canvas.SetLeft(particle.Element, particle.X);
            Canvas.SetTop(particle.Element, particle.Y);
            particle.Element.Opacity = particle.OpacityBase +
                (Math.Sin(pulseTime * 1.6 + particle.OpacityPhase) * 0.18);
        }
    }
}
