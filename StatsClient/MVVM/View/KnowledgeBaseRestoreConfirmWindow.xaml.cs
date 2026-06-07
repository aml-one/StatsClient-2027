using System.Windows;

namespace StatsClient.MVVM.View;

public partial class KnowledgeBaseRestoreConfirmWindow : Window
{
    public KnowledgeBaseRestoreConfirmWindow()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(ConfirmTextBox.Text.Trim(), "RESTORE", StringComparison.OrdinalIgnoreCase))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show(this, "Type RESTORE to confirm.", "Confirmation required", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
