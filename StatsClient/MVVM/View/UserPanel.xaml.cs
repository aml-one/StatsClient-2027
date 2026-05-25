using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
    /// Interaction logic for UserPanel.xaml
    /// </summary>
    public partial class UserPanel : UserControl, INotifyPropertyChanged
    {
        private Dictionary<string, bool> expandStatesLeft = [];
        public string? DesignerID { get; set; }
        

        public event PropertyChangedEventHandler? PropertyChanged;
        public static event PropertyChangedEventHandler? PropertyChangedStatic;

        public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
        {
            PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
        }

        
        private static UserPanel? instance;
        public static UserPanel? Instance
        {
            get => instance;
            set
            {
                instance = value;
                RaisePropertyChangedStatic(nameof(Instance));
            }
        }

        public System.Timers.Timer _timer;

        public UserPanel(string designerID)
        {
            Instance = this;
            InitializeComponent();
            DesignerID = designerID;
            UserPanelViewModel.Instance.DesignerID = designerID;
           
            PropertyGroupDescription groupDescription = new("SentOn");
            listView.Items.GroupDescriptions.Add(groupDescription);

            _timer = new System.Timers.Timer(4000);
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    listView.Items.Refresh();
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
            }
        }

        private void ListViewLeft_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView? listView = sender as ListView;
            if (listView!.View is GridView gView)
            {
                var workingWidth = listView.ActualWidth - SystemParameters.VerticalScrollBarWidth; // take into account vertical scrollbar

                double width = workingWidth - 268;
                if (width < 0) width = 100;


                gView.Columns[0].Width = 34;
                gView.Columns[1].Width = width;
                gView.Columns[2].Width = 45;
                gView.Columns[3].Width = 44;
                gView.Columns[4].Width = 30;
                gView.Columns[5].Width = 110;
            }
        }

        private void ExpanderLeft_Loaded(object sender, RoutedEventArgs e)
        {
            var expander = (Expander)sender;
            var dc = (CollectionViewGroup)expander.DataContext;
            var groupName = dc.Name.ToString();
            if (expandStatesLeft.TryGetValue(groupName!, out var value))
                expander.IsExpanded = value;
        }

        private void ExpanderLeft_ExpandedCollapsed(object sender, RoutedEventArgs e)
        {
            var expander = (Expander)sender;
            var dc = (CollectionViewGroup)expander.DataContext;
            var groupName = dc.Name.ToString();
            expandStatesLeft[groupName!] = expander.IsExpanded;
        }
    }
}
