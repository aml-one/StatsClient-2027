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
using static StatsClient.MVVM.Core.Functions;

namespace StatsClient.MVVM.View
{
    /// <summary>
    /// Interaction logic for SetPanColorWindow.xaml
    /// </summary>
    public partial class SetPanColorWindow : Window, INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler? PropertyChangedStatic;
        public event PropertyChangedEventHandler? PropertyChanged;

        public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
        {
            PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
        }

        public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
        {
            PropertyChanged?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
        }

        private static SetPanColorWindow? staticInstance;
        public static SetPanColorWindow StaticInstance
        {
            get => staticInstance!;
            set
            {
                staticInstance = value;
                RaisePropertyChangedStatic(nameof(StaticInstance));
            }
        }
        
        private SetPanColorWindow? instance;
        public SetPanColorWindow Instance
        {
            get => instance!;
            set
            {
                instance = value;
                RaisePropertyChangedStatic(nameof(Instance));
            }
        }

        bool IsItDarkColor = false;
        public SetPanColorWindow(string? panNumber = "", string? originalColor = "")
        {
            Instance = this;
            StaticInstance = this;
            InitializeComponent();

            if (!string.IsNullOrEmpty(originalColor) && !originalColor.Equals("0-0-0"))
            {
                try
                {
                    string oColor = originalColor.Replace("#FF", "");
                    string r = oColor[..2];
                    string g = oColor.Substring(2, 2);
                    string b = oColor.Substring(4, 2);

                    int cR = Convert.ToInt32(r, 16);
                    int cG = Convert.ToInt32(g, 16);
                    int cB = Convert.ToInt32(b, 16);
                    IsItDarkColor = CheckIfItsDarkColor($"{cR}-{cG}-{cB}");
                }
                catch (Exception)
                {
                    IsItDarkColor = false;
                }
            }
            else
                IsItDarkColor = true;

            SetPanColorViewModel.StaticInstance!.IsItDarkColor = IsItDarkColor;
            SetPanColorViewModel.StaticInstance!.PanNumber = panNumber!;

            if (originalColor == "0-0-0")
            {
                SetPanColorViewModel.StaticInstance!.WindowTitle = "Pick a color:";
                SetPanColorViewModel.StaticInstance!.OriginalColor = "255-255-255";
            }
            else
            {
                SetPanColorViewModel.StaticInstance!.WindowTitle = "Pick the new color:";
                SetPanColorViewModel.StaticInstance!.OriginalColor = originalColor!;
            }
        }
    }
}
