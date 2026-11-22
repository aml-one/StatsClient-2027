using StatsClient.MVVM.Core;
using System.Windows;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class ArchiveImportChoiceWindow : Window
{
    public ArchiveImportChoice Choice { get; private set; } = ArchiveImportChoice.Cancel;

    public ArchiveImportChoiceWindow()
    {
        InitializeComponent();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CancelAndClose();
            }
        };
    }

    public static ArchiveImportChoice ShowDialog(Window owner)
    {
        var window = new ArchiveImportChoiceWindow { Owner = owner };
        window.ShowDialog();
        return window.Choice;
    }

    private void ImportStandard_Click(object sender, RoutedEventArgs e)
    {
        Choice = ArchiveImportChoice.ImportStandard;
        DialogResult = true;
        Close();
    }

    private void ImportWithNewPan_Click(object sender, RoutedEventArgs e)
    {
        Choice = ArchiveImportChoice.ImportWithNewPan;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    private void CancelAndClose()
    {
        Choice = ArchiveImportChoice.Cancel;
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
