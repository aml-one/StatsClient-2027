using StatsClient.MVVM.ViewModel;
using System.Windows;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class OrderCreationWindow : Window
{
    private readonly OrderCreationViewModel _viewModel;

    public OrderCreationWindow(Window owner, OrderCreationViewModel viewModel)
    {
        Owner = owner;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;

        Left = owner.Left + 80;
        Top = owner.Top + 60;

        _viewModel.RequestClose += (_, _) => Close();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
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
        Close();
    }

}
