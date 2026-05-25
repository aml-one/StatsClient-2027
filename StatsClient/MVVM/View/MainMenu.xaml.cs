using StatsClient.MVVM.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace StatsClient.MVVM.View
{
    public partial class MainMenu : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public static event PropertyChangedEventHandler? PropertyChangedStatic;

        public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
        {
            PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
        }

        private static MainMenu? staticInstance;
        public static MainMenu StaticInstance
        {
            get => staticInstance!;
            set
            {
                staticInstance = value;
                RaisePropertyChangedStatic(nameof(StaticInstance));
            }
        }

        public MainMenu()
        {
            StaticInstance = this;
            InitializeComponent();
        }

        public void FocusOnMainMenu()
        {
            focusedTextBox.Focus();
        }
    }
}
