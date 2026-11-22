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
        CardContentGrid.SizeChanged += (_, _) => UpdateCardContentClip();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        MouseLeftButtonUp += (_, _) => BeginDismiss();
    }

    private void UpdateCardContentClip()
    {
        if (CardContentGrid.ActualWidth <= 0 || CardContentGrid.ActualHeight <= 0)
            return;

        CardContentGrid.Clip = new RectangleGeometry(
            new Rect(0, 0, CardContentGrid.ActualWidth, CardContentGrid.ActualHeight),
            10,
            10);
    }

    public void Apply(WatchListStatusChange change)
    {
        _appearedUtc = DateTime.UtcNow;
        TitleText.Text = change.Title;
        OrderIdText.Text = change.IntOrderID;

        if (!string.IsNullOrWhiteSpace(change.StatusDetail))
        {
            MessageText.Text = change.StatusDetail;
            MessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")!);

            if (!string.IsNullOrWhiteSpace(change.PatientName))
            {
                PatientNameText.Text = change.PatientName;
                PatientNameText.Visibility = Visibility.Visible;
            }
            else
            {
                PatientNameText.Text = string.Empty;
                PatientNameText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            MessageText.Text = change.Message;
            MessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")!);
            PatientNameText.Text = string.Empty;
            PatientNameText.Visibility = Visibility.Collapsed;
        }

        UpdateAgeText();

        var accent = (Color)ColorConverter.ConvertFromString(change.AccentColor)!;
        AccentBar.Fill = new SolidColorBrush(accent);
        PulseBrush.Color = accent;

        if (string.IsNullOrWhiteSpace(change.PanNumber))
        {
            PanNumberText.Visibility = Visibility.Collapsed;
            PanNumberText.Text = string.Empty;
            MessageText.Margin = new Thickness(0, 0, 0, 0);
        }
        else
        {
            PanNumberText.Text = change.PanNumber.Trim();
            PanNumberText.Foreground = new SolidColorBrush(Color.FromArgb(0x66, accent.R, accent.G, accent.B));
            PanNumberText.Visibility = Visibility.Visible;
            MessageText.Margin = new Thickness(0, 0, 52, 0);
        }
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
        UpdateCardContentClip();
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
