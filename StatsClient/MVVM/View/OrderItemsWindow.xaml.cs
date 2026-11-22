using StatsClient.MVVM.ViewModel;
using System.Windows;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class OrderItemsWindow : Window
{
    public OrderItemsWindow()
    {
        InitializeComponent();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseDialog();
            }
        };
    }

    public static void ShowDialog(Window owner)
    {
        var window = new OrderItemsWindow
        {
            Owner = owner,
            DataContext = OrderInfoViewModel.Instance,
        };
        window.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseDialog();

    private void CloseDialog()
    {
        DialogResult = true;
        Close();
    }

    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { Name: "CloseButton" })
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
        }
    }

    private void CloseButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CloseDialog();
    }
}
