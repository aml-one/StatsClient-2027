using StatsClient.MVVM.Core;
using System.Windows;

namespace StatsClient.MVVM.View;

public partial class ArchiveImportChoiceWindow : Window
{
    public ArchiveImportChoice Choice { get; private set; } = ArchiveImportChoice.Cancel;

    public ArchiveImportChoiceWindow()
    {
        InitializeComponent();
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
        Choice = ArchiveImportChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
