using StatsClient.MVVM.Core;
using StatsClient.MVVM.Core.OrderCreation;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.Functions;

namespace StatsClient.MVVM.ViewModel;

public sealed class OrderCreationViewModel : ObservableObject
{
    private string panNumber = "";
    private string doctorName = "";
    private string parsedDoctorNameFromPdf = "";
    private string patientFirstName = "";
    private string patientLastName = "";
    private string selectedShade = "";
    private string teethNumbers = "";
    private string comments = "";
    private string scannerOrderId = "";
    private string digitalSystem = "";
    private string selectedCustomerName = "";
    private string finalOrderId = "";
    private bool showComments = true;
    private bool showScannerOrderId = true;
    private bool isScrewRetained;
    private bool isInitializing;
    private bool suppressCustomerPreferenceSave;
    private bool isGenerating;
    private string statusMessage = string.Empty;
    private CancellationTokenSource? customerSearchCts;

    public OrderCreationViewModel()
    {
        Shades = new ObservableCollection<PrescriptionShadeOption>(PrescriptionShadeCatalog.All);
        ICollectionView shadesView = CollectionViewSource.GetDefaultView(Shades);
        shadesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PrescriptionShadeOption.Group)));
        ShadesView = shadesView;
        DigitalSystems = new ObservableCollection<string>(PrescriptionOrderNameBuilder.DigitalSystems);
        CustomerSuggestionsList = [];
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        GenerateOrderCommand = new RelayCommand(_ => _ = GenerateOrderAsync());
        SwapPatientNamesCommand = new RelayCommand(_ => SwapPatientNames());
        CopyFinalOrderIdCommand = new RelayCommand(_ => CopyFinalOrderId(closeAfter: false));
        CopyFinalOrderIdAndCloseCommand = new RelayCommand(_ => CopyFinalOrderId(closeAfter: true));

        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            teethNumbers = "8,9";
        }
    }

    public ObservableCollection<PrescriptionShadeOption> Shades { get; }

    public ICollectionView ShadesView { get; }

    public ObservableCollection<string> DigitalSystems { get; }

    public ObservableCollection<string> CustomerSuggestionsList { get; }

    public bool HasCustomerSuggestions => CustomerSuggestionsList.Count > 0;

    public bool IsScrewRetained
    {
        get => isScrewRetained;
        set
        {
            if (SetField(ref isScrewRetained, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public event EventHandler? RequestClose;

    public ICommand CancelCommand { get; }

    public ICommand GenerateOrderCommand { get; }

    public ICommand SwapPatientNamesCommand { get; }

    public ICommand CopyFinalOrderIdCommand { get; }

    public ICommand CopyFinalOrderIdAndCloseCommand { get; }

    public string PanNumber
    {
        get => panNumber;
        private set => SetField(ref panNumber, value);
    }

    public string DoctorName
    {
        get => doctorName;
        set
        {
            if (!SetField(ref doctorName, value))
            {
                return;
            }

            if (!isInitializing)
            {
                suppressCustomerPreferenceSave = true;
                selectedCustomerName = "";
                RaisePropertyChanged(nameof(SelectedCustomerName));
                suppressCustomerPreferenceSave = false;
            }

            _ = RefreshCustomerSuggestionsAsync();
            _ = RebuildFinalOrderIdAsync();
        }
    }

    public string PatientFirstName
    {
        get => patientFirstName;
        set
        {
            if (SetField(ref patientFirstName, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public string PatientLastName
    {
        get => patientLastName;
        set
        {
            if (SetField(ref patientLastName, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public string SelectedShade
    {
        get => selectedShade;
        set
        {
            if (SetField(ref selectedShade, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public string TeethNumbers
    {
        get => teethNumbers;
        set
        {
            if (SetField(ref teethNumbers, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public string Comments
    {
        get => comments;
        set => SetField(ref comments, value);
    }

    public string ScannerOrderId
    {
        get => scannerOrderId;
        set
        {
            if (SetField(ref scannerOrderId, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public string DigitalSystem
    {
        get => digitalSystem;
        set
        {
            if (SetField(ref digitalSystem, value))
            {
                _ = RebuildFinalOrderIdAsync();
            }
        }
    }

    public string SelectedCustomerName
    {
        get => selectedCustomerName;
        set
        {
            if (!SetField(ref selectedCustomerName, value))
            {
                return;
            }

            if (!suppressCustomerPreferenceSave
                && !isInitializing
                && !string.IsNullOrWhiteSpace(value)
                && !string.IsNullOrWhiteSpace(parsedDoctorNameFromPdf))
            {
                _ = SaveCustomerPreferenceAsync(parsedDoctorNameFromPdf, value);
            }

            _ = RebuildFinalOrderIdAsync();
        }
    }

    public string FinalOrderId
    {
        get => finalOrderId;
        set => SetField(ref finalOrderId, value);
    }

    public bool ShowComments
    {
        get => showComments;
        private set => SetField(ref showComments, value);
    }

    public bool ShowScannerOrderId
    {
        get => showScannerOrderId;
        private set => SetField(ref showScannerOrderId, value);
    }

    public bool IsGenerating
    {
        get => isGenerating;
        private set => SetField(ref isGenerating, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (SetField(ref statusMessage, value))
            {
                RaisePropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public void LoadFromExtractedFields(PrescriptionPdfExtractedFields fields, string panNumberValue)
    {
        isInitializing = true;
        try
        {
            PanNumber = panNumberValue;
            parsedDoctorNameFromPdf = fields.CustomerName ?? "";
            DoctorName = parsedDoctorNameFromPdf;
            PatientFirstName = fields.PatientFirstName ?? "";
            PatientLastName = fields.PatientLastName ?? "";
            SelectedShade = PrescriptionShadeCatalog.NormalizeExtractedShade(fields.Shade) ?? "";
            TeethNumbers = fields.TeethNumbers ?? "";
            Comments = fields.Comments ?? "";
            ScannerOrderId = fields.OrderId ?? "";
            DigitalSystem = PrescriptionOrderNameBuilder.ResolveDigitalSystem(fields.PageHeaderIsHigh);
            ShowComments = PrescriptionOrderNameBuilder.ShowComments(fields.PageHeaderIsHigh);
            ShowScannerOrderId = PrescriptionOrderNameBuilder.ShowScannerOrderId(fields.PageHeaderIsHigh);
            IsScrewRetained = false;
            SelectedCustomerName = "";
        }
        finally
        {
            isInitializing = false;
        }

        _ = RefreshCustomerSuggestionsAsync();
        _ = RebuildFinalOrderIdAsync();
    }

    private async Task RefreshCustomerSuggestionsAsync()
    {
        customerSearchCts?.Cancel();
        customerSearchCts = new CancellationTokenSource();
        CancellationToken token = customerSearchCts.Token;

        try
        {
            await Task.Delay(250, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        string searchTerm = DoctorName.Trim();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            suppressCustomerPreferenceSave = true;
            try
            {
                CustomerSuggestionsList.Clear();
                SelectedCustomerName = "";
                RaisePropertyChanged(nameof(HasCustomerSuggestions));
            }
            finally
            {
                suppressCustomerPreferenceSave = false;
            }

            return;
        }

        List<string> matches = [];
        if (searchTerm.Length >= 2)
        {
            matches = await SearchThreeShapeCustomersAsync(searchTerm);
        }
        else if (string.Equals(searchTerm, parsedDoctorNameFromPdf.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            matches = await SearchThreeShapeCustomersAsync(searchTerm);
        }

        bool includeSavedPreference = !string.IsNullOrWhiteSpace(parsedDoctorNameFromPdf)
            && string.Equals(searchTerm, parsedDoctorNameFromPdf.Trim(), StringComparison.OrdinalIgnoreCase);

        string? savedPreference = null;
        if (includeSavedPreference)
        {
            List<string> saved = await CustomerHasSuggestedName(parsedDoctorNameFromPdf);
            if (saved.Count > 0)
            {
                savedPreference = saved[0];
            }
        }

        suppressCustomerPreferenceSave = true;
        try
        {
            CustomerSuggestionsList.Clear();

            if (!string.IsNullOrWhiteSpace(savedPreference))
            {
                AddCustomerSuggestionIfMissing(savedPreference);
            }

            foreach (string match in matches)
            {
                AddCustomerSuggestionIfMissing(match);
            }

            RaisePropertyChanged(nameof(HasCustomerSuggestions));

            if (!string.IsNullOrWhiteSpace(savedPreference)
                && CustomerSuggestionsList.Any(x => x.Equals(savedPreference, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedCustomerName = savedPreference;
            }
            else if (CustomerSuggestionsList.Count == 1)
            {
                SelectedCustomerName = CustomerSuggestionsList[0];
            }
            else if (!CustomerSuggestionsList.Any(x => x.Equals(SelectedCustomerName, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedCustomerName = "";
            }
        }
        finally
        {
            suppressCustomerPreferenceSave = false;
        }
    }

    private void AddCustomerSuggestionIfMissing(string name)
    {
        if (CustomerSuggestionsList.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CustomerSuggestionsList.Add(name);
    }

    private static async Task SaveCustomerPreferenceAsync(string doctorKey, string selectedName)
    {
        string cleaned = CleanUpCustomerName(selectedName);
        await SaveCustomerPreference(doctorKey.Trim(), cleaned);
    }

    private void SwapPatientNames()
    {
        string first = patientFirstName;
        string last = patientLastName;
        patientFirstName = last;
        patientLastName = first;
        RaisePropertyChanged(nameof(PatientFirstName));
        RaisePropertyChanged(nameof(PatientLastName));
        _ = RebuildFinalOrderIdAsync();
    }

    private void CopyFinalOrderId(bool closeAfter)
    {
        if (!string.IsNullOrWhiteSpace(FinalOrderId))
        {
            Clipboard.SetText(FinalOrderId);
        }

        if (closeAfter)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task RebuildFinalOrderIdAsync()
    {
        FinalOrderId = await PrescriptionOrderNameBuilder.BuildAsync(
            PanNumber,
            DoctorName,
            SelectedCustomerName,
            PatientFirstName,
            PatientLastName,
            TeethNumbers,
            SelectedShade,
            ScannerOrderId,
            DigitalSystem,
            IsScrewRetained);
    }

    private async Task GenerateOrderAsync()
    {
        if (IsGenerating)
        {
            return;
        }

        IsGenerating = true;
        StatusMessage = "Creating order files...";
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        try
        {
            if (string.IsNullOrWhiteSpace(FinalOrderId))
            {
                await RebuildFinalOrderIdAsync();
            }

            string customer = !string.IsNullOrWhiteSpace(SelectedCustomerName)
                ? SelectedCustomerName
                : DoctorName;

            OrderCreationInput input = OrderCreationGenerationService.CreateInput(
                FinalOrderId,
                customer,
                PatientFirstName,
                PatientLastName,
                TeethNumbers,
                SelectedShade,
                Comments,
                ScannerOrderId,
                IsScrewRetained);

            OrderCreationGenerationResult result = await Task.Run(() =>
                new OrderCreationGenerationService().Generate(input));

            Window owner = MainWindow.Instance;

            if (!result.Success)
            {
                StatusMessage = result.Message;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    new SMessageBox(
                        "Create order",
                        result.Message,
                        SMessageBoxButtons.Ok,
                        MainViewModel.NotificationIcon.Error,
                        20,
                        owner).ShowDialog();
                });
                return;
            }

            StatusMessage = $"Created files in:\n{result.OrderFolderPath}";

            MainViewModel.Instance.ShowNotificationMessage(
                "Order files created",
                $"Temporary order folder:\n{result.OrderFolderPath}",
                MainViewModel.NotificationIcon.Success);

            try
            {
                if (!string.IsNullOrWhiteSpace(result.OrderFolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = result.OrderFolderPath,
                        UseShellExecute = true,
                    });
                }
            }
            catch
            {
            }
        }
        finally
        {
            IsGenerating = false;
            Mouse.OverrideCursor = null;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
