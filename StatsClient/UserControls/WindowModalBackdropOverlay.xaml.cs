using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StatsClient.UserControls;

public partial class WindowModalBackdropOverlay : UserControl
{
    public WindowModalBackdropOverlay()
    {
        InitializeComponent();
        Loaded += (_, _) => ResetVisualState();
    }

    public void PlayEntrance()
    {
        StopStoryboard("ExitStoryboard");
        ResetVisualState();
        Visibility = Visibility.Visible;
        BeginStoryboard("EntranceStoryboard");
    }

    public void PlayExit()
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        StopStoryboard("EntranceStoryboard");
        BeginStoryboard("ExitStoryboard");
    }

    public void HideImmediately()
    {
        StopStoryboard("EntranceStoryboard");
        StopStoryboard("ExitStoryboard");
        Visibility = Visibility.Collapsed;
        ResetVisualState();
    }

    private void ExitStoryboard_Completed(object? sender, EventArgs e)
    {
        StopStoryboard("ExitStoryboard");
        Visibility = Visibility.Collapsed;
        ResetVisualState();
    }

    private void ResetVisualState()
    {
        Backdrop.Opacity = 0;
    }

    private void BeginStoryboard(string resourceKey)
    {
        if (FindResource(resourceKey) is not Storyboard storyboard)
        {
            return;
        }

        storyboard.Begin(this, true);
    }

    private void StopStoryboard(string resourceKey)
    {
        if (FindResource(resourceKey) is not Storyboard storyboard)
        {
            return;
        }

        storyboard.Stop(this);
        storyboard.Remove(this);
    }
}
