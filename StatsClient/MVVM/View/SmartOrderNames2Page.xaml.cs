using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StatsClient.MVVM.View
{
    /// <summary>
    /// Interaction logic for SmartOrderNames2Page.xaml
    /// </summary>
    public partial class SmartOrderNames2Page : Window, INotifyPropertyChanged
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

        private static SmartOrderNames2Page? staticInstance;
        public static SmartOrderNames2Page? StaticInstance
        {
            get => staticInstance;
            set
            {
                staticInstance = value;
                RaisePropertyChangedStatic(nameof(StaticInstance));
            }
        }


        public SmartOrderNames2Page()
        {
            StaticInstance = this;
            InitializeComponent();
            this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
            smartOrderNamesTabControl.SelectedIndex = 0;
        }

        private void HandleEsc(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Hide();
        }

        private void RenameButton_GotFocus(object sender, RoutedEventArgs e)
        {
            renameButton.Background = new SolidColorBrush(Color.FromArgb(255, 0, 128, 72));
            renameButton.Foreground = Brushes.Beige;
        }

        private void RenameButton_LostFocus(object sender, RoutedEventArgs e)
        {
            renameButton.Background = new SolidColorBrush(Color.FromArgb(255, 82, 105, 94));
            renameButton.Foreground = Brushes.Silver;
        }

        
        public void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                try
                {
                    this.DragMove();
                }
                catch { }
        }

        internal void JumpToTab(string tabName)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                var index = tabName switch
                {
                    "Start" => 0,
                    "Customer" => 1,
                    "System" => 2,
                    "ExtraInfo" => 3,
                    "Characteristics" => 4,
                    "Shade" => 5,
                    "Review" => 6,
                    _ => 0,
                };
                smartOrderNamesTabControl.SelectedIndex = index;

                
                if (index == 3)
                    dexisIdTextBox.Focus();

                if (index == 1)
                {
                    if (SmartOrderNames2ViewModel.StaticInstance.CustomerSuggestionsList.Count > 0)
                        listBoxCustomerVariations.Focus();
                    else
                        btnNext1.Focus();
                }

                if (index == 2)
                {
                    listBoxDigiSystem.Focus();
                    listBoxDigiSystem.SelectedIndex = 8;

                    var listBoxItem = (ListBoxItem)listBoxDigiSystem.ItemContainerGenerator.ContainerFromItem(listBoxDigiSystem.SelectedItem);
                    listBoxItem.Focus();
                }

                if (index == 4)
                {
                    btnNSCR.Focus();
                }
                
                if (index == 5)
                {
                    noShadeButtonCentered.Focus();
                }

                if (index == 6)
                {
                    renameButton.Focus();
                }
            }));
        }

        public void SetCursorToEndOfTextBox()
        {
            dexisIdTextBox.SelectionStart = dexisIdTextBox.Text.Length;
            dexisIdTextBox.SelectionLength = 0;
        }
    }
}
