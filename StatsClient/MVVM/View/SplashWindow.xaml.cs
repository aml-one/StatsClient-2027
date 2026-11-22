using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace StatsClient.MVVM.View;

/// <summary>
/// Interaction logic for SplashWindow.xaml
/// </summary>
public partial class SplashWindow : Window, INotifyPropertyChanged
{
    public static event PropertyChangedEventHandler? PropertyChangedStatic;
#pragma warning disable CS0067 // The event 'SplashWindow.PropertyChanged' is never used
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067 // The event 'SplashWindow.PropertyChanged' is never used

    public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
    {
        PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }

    private static SplashWindow? instance;
    public static SplashWindow Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }

    public SplashWindow()
    {
        SyncfusionLicenseRegistrar.Register();
        Instance = this;
        InitializeComponent();
    }

    public void CloseApp()
    {
        Application.Current.Shutdown();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseApp();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storyboard? title = FindResource("TitleRevealAnimation") as Storyboard;
        title!.Completed += TitleReveal_Completed;
        title.Begin();
    }

    private void TitleReveal_Completed(object? sender, EventArgs e)
    {
        Storyboard? logo = FindResource("LogoAssembleAnimation") as Storyboard;
        logo!.Completed += LogoAssemble_Completed;
        logo!.Begin();

        Storyboard? version = FindResource("VersionRevealAnimation") as Storyboard;
        version!.Begin();
    }

    private void LogoAssemble_Completed(object? sender, EventArgs e)
    {
        if (FindResource("LogoIdleAnimation") is Storyboard idle)
            idle.Begin();

        Storyboard? loading = FindResource("LoadingAreaRevealAnimation") as Storyboard;
        loading!.Completed += LoadingArea_Completed;
        loading!.Begin();

        if (FindResource("LoadingTextAnimation") is Storyboard loadingPulse)
            loadingPulse.Begin();
    }

    private void LoadingArea_Completed(object? sender, EventArgs e)
    {
        SplashViewModel.Instance.StartLoading();
    }
}
