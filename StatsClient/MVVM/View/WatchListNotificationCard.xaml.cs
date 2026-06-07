using StatsClient.MVVM.Core;
using StatsClient.MVVM.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StatsClient.MVVM.View;

public partial class WatchListNotificationCard : UserControl
{
    public event EventHandler? Dismissed;

    private Storyboard? _slideIn;
    private Storyboard? _dismiss;
    private DispatcherTimer? _ageTimer;
    private DateTime _appearedUtc;

    public WatchListNotificationCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        MouseLeftButtonUp += (_, _) => BeginDismiss();
    }

    public void Apply(WatchListStatusChange change)
    {
        _appearedUtc = DateTime.UtcNow;
        TitleText.Text = change.Title;
        OrderIdText.Text = string.IsNullOrWhiteSpace(change.PanNumber)
            ? change.IntOrderID
            : $"{change.IntOrderID}  ·  Pan {change.PanNumber}";
        MessageText.Text = change.Message;
        UpdateAgeText();

        var accent = (Color)ColorConverter.ConvertFromString(change.AccentColor)!;
        AccentBar.Fill = new SolidColorBrush(accent);
        PulseBrush.Color = accent;
    }

    public void PlaySlideIn()
    {
        _slideIn ??= (Storyboard)Resources["SlideInStoryboard"];
        _slideIn.Begin(this);
    }

    public void BeginDismiss()
    {
        StopAgeTimer();

        if (_dismiss is null)
        {
            _dismiss = (Storyboard)Resources["DismissStoryboard"];
            _dismiss.Completed += (_, _) => Dismissed?.Invoke(this, EventArgs.Empty);
        }

        IsHitTestVisible = false;
        _dismiss.Begin(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartAgeTimer();
        PlaySlideIn();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAgeTimer();
    }

    private void StartAgeTimer()
    {
        if (_ageTimer is not null)
            return;

        _ageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ageTimer.Tick += (_, _) => UpdateAgeText();
        _ageTimer.Start();
        UpdateAgeText();
    }

    private void StopAgeTimer()
    {
        if (_ageTimer is null)
            return;

        _ageTimer.Stop();
        _ageTimer = null;
    }

    private void UpdateAgeText()
    {
        if (AgeText is null)
            return;

        AgeText.Text = WatchListNotificationAgeFormatter.Format(_appearedUtc);
    }
}
