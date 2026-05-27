using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class ArchiveImportPanNumberWindow : Window
{
    private static readonly Regex DigitsOnlyRegex = new(@"^\d+$", RegexOptions.CultureInvariant);

    public int? PanNumber { get; private set; }

    public ArchiveImportPanNumberWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PanNumberTextBox.Focus();
            PanNumberTextBox.SelectAll();
        };
    }

    public static int? ShowDialog(Window owner)
    {
        var window = new ArchiveImportPanNumberWindow { Owner = owner };
        return window.ShowDialog() == true ? window.PanNumber : null;
    }

    private void PanNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !char.IsDigit(e.Text, 0);
    }

    private void PanNumberTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TrySubmit();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => TrySubmit();

    private void TrySubmit()
    {
        string text = PanNumberTextBox.Text.Trim();
        if (!DigitsOnlyRegex.IsMatch(text) || !int.TryParse(text, out int pan) || pan <= 0)
        {
            MessageBox.Show(this, "Please enter a valid pan number (digits only).", "Pan number", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PanNumber = pan;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        PanNumber = null;
        DialogResult = false;
        Close();
    }
}
