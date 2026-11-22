using StatsClient.MVVM.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class PrescriptionPreviousNumbersWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<PrescriptionPreviousNumberRow> Rows { get; } = [];

    public event EventHandler<string>? PanNumberSelected;

    public PrescriptionPreviousNumbersWindow(Window owner)
    {
        Owner = owner;
        InitializeComponent();
        DataContext = this;

        Left = owner.Left + 60;
        Top = owner.Top + 120;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    public void SetRows(IEnumerable<PrescriptionPreviousNumberRow> rows)
    {
        Rows.Clear();
        foreach (PrescriptionPreviousNumberRow row in rows)
        {
            Rows.Add(row);
        }
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

    private void NumbersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NumbersList.SelectedItem is not PrescriptionPreviousNumberRow row)
        {
            return;
        }

        PanNumberSelected?.Invoke(this, row.PanNumber);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
