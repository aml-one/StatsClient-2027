using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StatsClient.UserControls;

public partial class IteroZipCelebrationOverlay : UserControl
{
    private const double ParticleCanvasSize = 320;
    private const double ParticleOrbitRadius = 128;
    private const double ParticleBaseSize = 8;

    private static readonly Color[] ParticleColors =
    [
        Color.FromRgb(56, 189, 248),
        Color.FromRgb(74, 222, 128),
        Color.FromRgb(251, 191, 36),
        Color.FromRgb(244, 114, 182),
        Color.FromRgb(129, 140, 248),
        Color.FromRgb(52, 211, 153),
        Color.FromRgb(251, 146, 60),
        Color.FromRgb(167, 139, 250),
    ];

    private readonly Ellipse[] _particles = new Ellipse[ParticleColors.Length];
    private readonly DispatcherTimer _particleTimer;
    private double _particleAngle;

    public event EventHandler? DismissRequested;
    public event EventHandler? EntranceCompleted;

    public IteroZipCelebrationOverlay()
    {
        InitializeComponent();

        for (int i = 0; i < _particles.Length; i++)
        {
            _particles[i] = new Ellipse
            {
                Width = ParticleBaseSize,
                Height = ParticleBaseSize,
                Fill = new SolidColorBrush(ParticleColors[i]),
                Opacity = 0.85,
            };
            ParticleCanvas.Children.Add(_particles[i]);
        }

        _particleTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _particleTimer.Tick += OnParticleTick;

        Loaded += (_, _) => ResetVisualState();
        Unloaded += (_, _) => StopAllAnimations();
    }

    public void PlayEntrance()
    {
        StopAllAnimations();
        ResetVisualState();
        Visibility = Visibility.Visible;

        BeginStoryboard("EntranceStoryboard", OnEntranceStoryboardCompleted);
        BeginStoryboard("RippleLoopStoryboard");
        BeginStoryboard("ZipPulseStoryboard");
        _particleTimer.Start();
    }

    private void OnEntranceStoryboardCompleted(object? sender, EventArgs e)
    {
        if (FindResource("EntranceStoryboard") is Storyboard entrance)
            entrance.Completed -= OnEntranceStoryboardCompleted;

        EntranceCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void PlayExit()
    {
        if (Visibility != Visibility.Visible)
            return;

        _particleTimer.Stop();
        StopStoryboard("EntranceStoryboard");
        StopStoryboard("RippleLoopStoryboard");
        StopStoryboard("ZipPulseStoryboard");
        BeginStoryboard("ExitStoryboard");
    }

    private void ExitStoryboard_Completed(object? sender, EventArgs e)
    {
        StopAllAnimations();
        Visibility = Visibility.Collapsed;
        ResetVisualState();
    }

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResetVisualState()
    {
        Backdrop.Opacity = 0;
        CardScale.ScaleX = 0.45;
        CardScale.ScaleY = 0.45;
        CardContent.Opacity = 0;
        ZipIconScale.ScaleX = 1;
        ZipIconScale.ScaleY = 1;
        ZipIconRotate.Angle = -18;

        ZipGlow.Opacity = 0.35;
        RippleRing1.Opacity = 0;
        RippleRing2.Opacity = 0;

        if (RippleRing1.RenderTransform is ScaleTransform ripple1)
        {
            ripple1.ScaleX = 0.55;
            ripple1.ScaleY = 0.55;
        }

        if (RippleRing2.RenderTransform is ScaleTransform ripple2)
        {
            ripple2.ScaleX = 0.55;
            ripple2.ScaleY = 0.55;
        }
    }

    private void OnParticleTick(object? sender, EventArgs e)
    {
        _particleAngle = (_particleAngle + 1.35) % 360;
        double spacing = 360.0 / _particles.Length;
        double center = ParticleCanvasSize * 0.5;

        for (int i = 0; i < _particles.Length; i++)
        {
            double radians = (_particleAngle + i * spacing) * Math.PI / 180.0;
            double depth = Math.Sin(radians);
            double depth01 = (depth + 1.0) * 0.5;
            double size = ParticleBaseSize * (0.55 + 0.65 * depth01);

            Ellipse particle = _particles[i];
            particle.Width = size;
            particle.Height = size;
            particle.Opacity = 0.25 + 0.75 * depth01;

            Canvas.SetLeft(particle, center + ParticleOrbitRadius * Math.Cos(radians) - size * 0.5);
            Canvas.SetTop(particle, center + ParticleOrbitRadius * 0.72 * Math.Sin(radians) - size * 0.5);
            Panel.SetZIndex(particle, depth >= 0 ? 2 : 0);
        }
    }

    private void BeginStoryboard(string resourceKey, EventHandler? onCompleted = null)
    {
        if (FindResource(resourceKey) is not Storyboard storyboard)
            return;

        if (onCompleted is not null)
            storyboard.Completed += onCompleted;

        storyboard.Begin(this, true);
    }

    private void StopStoryboard(string resourceKey)
    {
        if (FindResource(resourceKey) is Storyboard storyboard)
        {
            storyboard.Stop(this);
            storyboard.Remove(this);
        }
    }

    private void StopAllAnimations()
    {
        _particleTimer.Stop();
        StopStoryboard("EntranceStoryboard");
        StopStoryboard("RippleLoopStoryboard");
        StopStoryboard("ExitStoryboard");
        StopStoryboard("ZipPulseStoryboard");
    }
}
