using StatsClient.MVVM.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace StatsClient.UserControls
{
    public partial class Pan : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }


        private string panNumber = "";
        public string PanNumber 
        { 
            get => panNumber;
            set
            {
                panNumber = value;
                RaisePropertyChanged(nameof(PanNumber));
            }
        }
        
        private string panColor = "";
        public string PanColor
        { 
            get => panColor;
            set
            {
                panColor = value;
                RaisePropertyChanged(nameof(PanColor));
            }
        }
        
        public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register("ClickCommand", typeof(RelayCommand),
                                                  typeof(Pan), new UIPropertyMetadata(null));

        public static readonly DependencyProperty PanNumberProperty = DependencyProperty.Register("PanNumber", typeof(object),
                                                  typeof(Pan), new PropertyMetadata(null));
        
        public static readonly DependencyProperty PanColorProperty = DependencyProperty.Register("PanColor", typeof(object),
                                                  typeof(Pan), new PropertyMetadata(null));
        
        
        public RelayCommand ClickCommand
        {
            get { return (RelayCommand)GetValue(ClickCommandProperty); }
            set { SetValue(ClickCommandProperty, value); }
        }

        public Pan()
        {
            this.DataContext = this;
            InitializeComponent();
        }
    }
}
