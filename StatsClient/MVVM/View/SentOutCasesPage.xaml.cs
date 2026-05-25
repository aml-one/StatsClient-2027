using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace StatsClient.MVVM.View
{
    /// <summary>
    /// Interaction logic for SentOutCasesPage.xaml
    /// </summary>
    public partial class SentOutCasesPage : UserControl, INotifyPropertyChanged
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

        private static SentOutCasesPage? instance;
        public static SentOutCasesPage Instance
        {
            get => instance!;
            set
            {
                instance = value;
                RaisePropertyChangedStatic(nameof(Instance));
            }
        }
        

        public SentOutCasesPage()
        {
            Instance = this;
            InitializeComponent();
            DataContext = new SentOutCasesViewModel();
        }

    }
}
