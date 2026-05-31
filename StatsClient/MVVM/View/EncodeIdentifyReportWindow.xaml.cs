using System.Windows;

namespace StatsClient.MVVM.View;

public partial class EncodeIdentifyReportWindow : Window
{
    public EncodeIdentifyReportWindow(string report)
    {
        InitializeComponent();
        ReportTextBox.Text = report ?? string.Empty;
        ReportTextBox.CaretIndex = 0;
        ReportTextBox.Focus();
        ReportTextBox.Select(0, 0);
    }

    private void Copy_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ReportTextBox.Text ?? string.Empty);
        }
        catch
        {
            // ignore clipboard errors (RDP, locked clipboard, etc.)
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}

