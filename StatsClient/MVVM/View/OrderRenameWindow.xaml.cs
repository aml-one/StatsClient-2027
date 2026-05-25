using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using KeyEventHandler = System.Windows.Input.KeyEventHandler;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace StatsClient.MVVM.View;

public partial class OrderRenameWindow : Window, INotifyPropertyChanged
{
    private OrderRenameWindow? instance;
    public OrderRenameWindow Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChanged(nameof(Instance));
        }
    }
    
    private static OrderRenameWindow? staticInstance;
    public static OrderRenameWindow StaticInstance
    {
        get => staticInstance!;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    public OrderRenameWindow(ThreeShapeOrdersModel ThreeShapeObject)
    {
        Instance = this;
        StaticInstance = this;
        InitializeComponent();
        OrderRenameViewModel.Instance._RenameWindow = this;
        OrderRenameViewModel.Instance.ThreeShapeObject = ThreeShapeObject;

        if (!string.IsNullOrEmpty(ThreeShapeObject!.Patient_FirstName))
            OrderRenameViewModel.Instance.PatientName = ThreeShapeObject.Patient_FirstName + " " + ThreeShapeObject.Patient_LastName;
        else
            OrderRenameViewModel.Instance.PatientName = ThreeShapeObject.Patient_LastName!;

        OrderRenameViewModel.Instance.OrderID = ThreeShapeObject.IntOrderID!;
        OrderRenameViewModel.Instance.OrderIDBeforeChange = ThreeShapeObject.IntOrderID!;
        OrderRenameViewModel.Instance.ShowResetButton = Visibility.Collapsed;
        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

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


    public void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            try
            {
                this.DragMove();
            }
            catch { }
    }

}
