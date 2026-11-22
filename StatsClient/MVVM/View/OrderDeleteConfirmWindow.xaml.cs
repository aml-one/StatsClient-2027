using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class OrderDeleteConfirmWindow : Window
{
    private readonly string _expectedOrderId;

    public OrderDeleteConfirmWindow(string expectedOrderId)
    {
        _expectedOrderId = expectedOrderId.Trim();
        InitializeComponent();
        ExpectedOrderIdRun.Text = _expectedOrderId;
        ConfirmTextBox.Focus();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CancelAndClose();
            }
        };
    }

    public static bool Confirm(Window owner, string expectedOrderId)
    {
        var window = new OrderDeleteConfirmWindow(expectedOrderId)
        {
            Owner = owner,
        };

        return window.ShowDialog() == true;
    }

    private void ConfirmTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        DeleteButton.IsEnabled = string.Equals(
            ConfirmTextBox.Text.Trim(),
            _expectedOrderId,
            StringComparison.Ordinal);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!DeleteButton.IsEnabled)
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    private void CancelAndClose()
    {
        DialogResult = false;
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
        CancelAndClose();
    }
}
