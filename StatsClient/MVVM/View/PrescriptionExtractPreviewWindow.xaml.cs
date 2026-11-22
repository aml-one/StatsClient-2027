using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;

using System.Collections.ObjectModel;

using System.ComponentModel;

using System.Runtime.CompilerServices;

using System.Windows;

using System.Windows.Input;



namespace StatsClient.MVVM.View;



public partial class PrescriptionExtractPreviewWindow : Window, INotifyPropertyChanged

{

    public ObservableCollection<PrescriptionExtractPreviewRow> Rows { get; } = [];



    public PrescriptionExtractPreviewWindow(Window owner)

    {

        Owner = owner;

        InitializeComponent();

        DataContext = this;



        Left = owner.Left + 40;

        Top = owner.Top + 80;



        PreviewKeyDown += (_, e) =>

        {

            if (e.Key == Key.Escape)

                Close();

        };

    }



    public void ApplyExtractedFields(PrescriptionPdfExtractedFields? fields)

    {

        Rows.Clear();



        if (fields is null)

        {

            Rows.Add(new PrescriptionExtractPreviewRow("Status", "No extraction yet"));

            return;

        }



        string code = PrescriptionSystemCatalog.NormalizeCode(fields.PageHeaderIsHigh);

        bool showOrderId = code is "0" or "1" or "6" or "7" or "8";

        bool showComments = code is not ("0" or "1" or "4");



        AddRow("Customer (Doctor name)", fields.CustomerName);

        AddRow("Patient First name", fields.PatientFirstName);

        AddRow("Patient Last name", fields.PatientLastName);

        AddRow("Shade", fields.Shade);

        AddRow("Teeth numbers", fields.TeethNumbers);

        if (showComments)

        {

            AddRow("Comments", fields.Comments);

        }

        if (showOrderId)

        {

            AddRow("Order ID", fields.OrderId);

        }

    }



    private void AddRow(string field, string? value) =>

        Rows.Add(new PrescriptionExtractPreviewRow(field, string.IsNullOrWhiteSpace(value) ? "—" : value.Trim()));



    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)

    {

        if (e.OriginalSource is FrameworkElement { Name: "CloseButton" })

        {

            return;

        }



        if (e.ChangedButton != MouseButton.Left)

            return;



        try

        {

            DragMove();

        }

        catch

        {

            // DragMove throws if mouse button released before move starts.

        }

    }



    private void CloseButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)

    {

        e.Handled = true;

        Close();

    }



    public event PropertyChangedEventHandler? PropertyChanged;



    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}

