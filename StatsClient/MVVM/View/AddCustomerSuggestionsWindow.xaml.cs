using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

/// <summary>
/// Interaction logic for AddCustomerSuggestionsWindow.xaml
/// </summary>
public partial class AddCustomerSuggestionsWindow : Window, INotifyPropertyChanged
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

    private static AddCustomerSuggestionsWindow? staticInstance;
    public static AddCustomerSuggestionsWindow StaticInstance
    {
        get => staticInstance!;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    public AddCustomerSuggestionsWindow(string? CustomerName = "")
    {
        StaticInstance = this;
        InitializeComponent();
        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
        AddCustomerSuggestionsViewModel.StaticInstance!.CustomerName = CustomerName;
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

}
