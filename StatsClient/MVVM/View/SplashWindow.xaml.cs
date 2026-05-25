using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StatsClient.MVVM.View
{
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
            Instance = this;
            InitializeComponent();
        }

        public void CloseApp()
        {
            Application.Current.Shutdown();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Start with Hollywood intro sequence
            Storyboard? introAml = this.FindResource("IntroAmlAnimation")! as Storyboard;
            introAml!.Completed += IntroAml_Completed;
            introAml!.Begin();
        }

        private void IntroAml_Completed(object? sender, EventArgs e)
        {
            // Hold AmL for a moment, then fade out
            Task.Delay(800).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    Storyboard? fadeOut = this.FindResource("IntroAmlFadeOutAnimation")! as Storyboard;
                    fadeOut!.Completed += IntroAmlFadeOut_Completed;
                    fadeOut!.Begin();
                });
            });
        }

        private void IntroAmlFadeOut_Completed(object? sender, EventArgs e)
        {
            // Show "presents" text
            Storyboard? presents = this.FindResource("IntroPresentsAnimation")! as Storyboard;
            presents!.Completed += IntroPresents_Completed;
            presents!.Begin();
        }

        private void IntroPresents_Completed(object? sender, EventArgs e)
        {
            // Hold "presents" briefly, then fade out and start main animation
            Task.Delay(600).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    Storyboard? fadeOut = this.FindResource("IntroPresensFadeOutAnimation")! as Storyboard;
                    fadeOut!.Completed += IntroPresensFadeOut_Completed;
                    fadeOut!.Begin();
                });
            });
        }

        private void IntroPresensFadeOut_Completed(object? sender, EventArgs e)
        {
            // Start main logo animation
            Storyboard? sb = this.FindResource("LogoOpacityAnimation")! as Storyboard;
            sb!.Completed += LogoOpacityStoryboard_Completed;
            sb!.Begin();
        }

        private void LogoOpacityStoryboard_Completed(object? sender, EventArgs e)
        {
            Storyboard? sbBG4 = this.FindResource("BackgroundEaseInAnimation")! as Storyboard;
            sbBG4!.Begin();

            Storyboard? sb2 = this.FindResource("LogoMoveAnimation")! as Storyboard;
            sb2!.Begin();
            Storyboard? sb3 = this.FindResource("LogoSizeAnimation")! as Storyboard;
            sb3!.Completed += LogoSizeStoryboard_Completed;
            sb3!.Begin();
        }


        private void LogoSizeStoryboard_Completed(object? sender, EventArgs e)
        {
            Storyboard? sb = this.FindResource("PanelOpacityAnimation")! as Storyboard;
            sb!.Completed += BackgroundOpacityStoryboard_Completed;
            sb!.Begin();

            Storyboard? sb2 = this.FindResource("Panel2OpacityAnimation")! as Storyboard;
            sb2!.Begin();

            Storyboard? sbVers = this.FindResource("VersionNumberEaseInAnimation")! as Storyboard;
            sbVers!.Begin();

            Storyboard? sbStats = this.FindResource("StatsTextAnimation")! as Storyboard;
            sbStats!.Begin();
        }


        private void BackgroundOpacityStoryboard_Completed(object? sender, EventArgs e)
        {
            SplashViewModel.Instance.StartLoading();
        }
    }
}
