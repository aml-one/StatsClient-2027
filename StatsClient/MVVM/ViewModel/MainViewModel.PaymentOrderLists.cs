using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Web;
using System.Windows;
using System.Windows.Data;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.InvoicePeriodHelper;
using static StatsClient.MVVM.Core.MessageBoxes;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    #region PAYMENT ORDER LISTS PROPERTIES

    private static readonly BackgroundWorker bwLoadHistoricalCases = new();
    private static readonly BackgroundWorker bwLoadUnpaidCases = new();

    // Payment List Cut-Off Date - data before this date won't be retrieved
    private static DateTime paymentListCutOffDate = DateTime.Parse("2025-10-01");
    public static DateTime PaymentListCutOffDate
    {
        get => paymentListCutOffDate;
        set => paymentListCutOffDate = value;
    }

    private bool isLabnextLoggedIn = false;
    public bool IsLabnextLoggedIn
    {
        get => isLabnextLoggedIn;
        set
        {
            isLabnextLoggedIn = value;
            RaisePropertyChanged(nameof(IsLabnextLoggedIn));
        }
    }

    private ObservableCollection<DesignerModel> paymentOrderListDesigners = [];
    public ObservableCollection<DesignerModel> PaymentOrderListDesigners
    {
        get => paymentOrderListDesigners;
        set
        {
            paymentOrderListDesigners = value;
            RaisePropertyChanged(nameof(PaymentOrderListDesigners));
        }
    }

    private DesignerModel? selectedPaymentDesigner;
    public DesignerModel? SelectedPaymentDesigner
    {
        get => selectedPaymentDesigner;
        set
        {
            selectedPaymentDesigner = value;
            RaisePropertyChanged(nameof(SelectedPaymentDesigner));
            if (value != null)
            {
                Debug.WriteLine($"[PaymentOrderLists] Designer selected: {value.FriendlyName} (ID: {value.DesignerID})");

                // Reset pagination when designer changes
                HistoricalCurrentPage = 1;
                UnpaidCurrentPage = 1;

                LoadHistoricalCasesWithPagination();
                LoadUnpaidCasesWithPagination();
                LoadPayPeriodStatistics(null);
            }
        }
    }

    private ObservableCollection<PaymentHistoryModel> paymentHistoricalCases = [];
    public ObservableCollection<PaymentHistoryModel> PaymentHistoricalCases
    {
        get => paymentHistoricalCases;
        set
        {
            paymentHistoricalCases = value;
            RaisePropertyChanged(nameof(PaymentHistoricalCases));
            RefreshHistoricalCasesGrouping();
        }
    }

    private CollectionViewSource paymentHistoricalCasesGrouped = new();
    public CollectionViewSource PaymentHistoricalCasesGrouped
    {
        get => paymentHistoricalCasesGrouped;
        set
        {
            paymentHistoricalCasesGrouped = value;
            RaisePropertyChanged(nameof(PaymentHistoricalCasesGrouped));
        }
    }

    private ObservableCollection<PaymentHistoryModel> paymentUnpaidCases = [];
    public ObservableCollection<PaymentHistoryModel> PaymentUnpaidCases
    {
        get => paymentUnpaidCases;
        set
        {
            paymentUnpaidCases = value;
            RaisePropertyChanged(nameof(PaymentUnpaidCases));
            RefreshUnpaidCasesGrouping();
        }
    }

    private CollectionViewSource paymentUnpaidCasesGrouped = new();
    public CollectionViewSource PaymentUnpaidCasesGrouped
    {
        get => paymentUnpaidCasesGrouped;
        set
        {
            paymentUnpaidCasesGrouped = value;
            RaisePropertyChanged(nameof(PaymentUnpaidCasesGrouped));
        }
    }

    private int selectedHistoricalDays = 30;
    public int SelectedHistoricalDays
    {
        get => selectedHistoricalDays;
        set
        {
            selectedHistoricalDays = value;
            RaisePropertyChanged(nameof(SelectedHistoricalDays));
            RaisePropertyChanged(nameof(Is30DaysSelected));
            RaisePropertyChanged(nameof(Is60DaysSelected));
            RaisePropertyChanged(nameof(Is90DaysSelected));
            if (SelectedPaymentDesigner != null)
            {
                HistoricalCurrentPage = 1;
                LoadHistoricalCasesWithPagination();
            }
        }
    }

    public bool Is30DaysSelected => SelectedHistoricalDays == 30;
    public bool Is60DaysSelected => SelectedHistoricalDays == 60;
    public bool Is90DaysSelected => SelectedHistoricalDays == 90;

    private bool filterUnpaidByInvoicePeriod = false;
    public bool FilterUnpaidByInvoicePeriod
    {
        get => filterUnpaidByInvoicePeriod;
        set
        {
            filterUnpaidByInvoicePeriod = value;
            RaisePropertyChanged(nameof(FilterUnpaidByInvoicePeriod));
            if (SelectedPaymentDesigner != null)
            {
                UnpaidCurrentPage = 1;
                LoadUnpaidCasesWithPagination();
            }
        }
    }

    // Pagination for Historical Cases
    private int historicalCurrentPage = 1;
    public int HistoricalCurrentPage
    {
        get => historicalCurrentPage;
        set
        {
            historicalCurrentPage = value;
            RaisePropertyChanged(nameof(HistoricalCurrentPage));
            RaisePropertyChanged(nameof(HistoricalPageInfo));
        }
    }

    private int historicalTotalPages = 1;
    public int HistoricalTotalPages
    {
        get => historicalTotalPages;
        set
        {
            historicalTotalPages = value;
            RaisePropertyChanged(nameof(HistoricalTotalPages));
            RaisePropertyChanged(nameof(HistoricalPageInfo));
        }
    }

    private int historicalTotalItems = 0;
    public int HistoricalTotalItems
    {
        get => historicalTotalItems;
        set
        {
            historicalTotalItems = value;
            RaisePropertyChanged(nameof(HistoricalTotalItems));
            RaisePropertyChanged(nameof(HistoricalPageInfo));
        }
    }

    public string HistoricalPageInfo => $"Page {HistoricalCurrentPage} of {HistoricalTotalPages} ({HistoricalTotalItems} total cases)";

    // Pagination for Unpaid Cases
    private int unpaidCurrentPage = 1;
    public int UnpaidCurrentPage
    {
        get => unpaidCurrentPage;
        set
        {
            unpaidCurrentPage = value;
            RaisePropertyChanged(nameof(UnpaidCurrentPage));
            RaisePropertyChanged(nameof(UnpaidPageInfo));
        }
    }

    private int unpaidTotalPages = 1;
    public int UnpaidTotalPages
    {
        get => unpaidTotalPages;
        set
        {
            unpaidTotalPages = value;
            RaisePropertyChanged(nameof(UnpaidTotalPages));
            RaisePropertyChanged(nameof(UnpaidPageInfo));
        }
    }

    private int unpaidTotalItems = 0;
    public int UnpaidTotalItems
    {
        get => unpaidTotalItems;
        set
        {
            unpaidTotalItems = value;
            RaisePropertyChanged(nameof(UnpaidTotalItems));
            RaisePropertyChanged(nameof(UnpaidPageInfo));
        }
    }

    public string UnpaidPageInfo => $"Page {UnpaidCurrentPage} of {UnpaidTotalPages} ({UnpaidTotalItems} total cases)";

    private const int PageSize = 50;

    private bool isLoadingHistoricalCases = false;
    public bool IsLoadingHistoricalCases
    {
        get => isLoadingHistoricalCases;
        set
        {
            isLoadingHistoricalCases = value;
            RaisePropertyChanged(nameof(IsLoadingHistoricalCases));
        }
    }

    private bool isLoadingUnpaidCases = false;
    public bool IsLoadingUnpaidCases
    {
        get => isLoadingUnpaidCases;
        set
        {
            isLoadingUnpaidCases = value;
            RaisePropertyChanged(nameof(IsLoadingUnpaidCases));
        }
    }

    private List<PayPeriodStatisticsModel> payPeriodStatistics = [];
    public List<PayPeriodStatisticsModel> PayPeriodStatistics
    {
        get => payPeriodStatistics;
        set
        {
            payPeriodStatistics = value;
            RaisePropertyChanged(nameof(PayPeriodStatistics));
        }
    }

    private bool isLoadingStatistics = false;
    public bool IsLoadingStatistics
    {
        get => isLoadingStatistics;
        set
        {
            isLoadingStatistics = value;
            RaisePropertyChanged(nameof(IsLoadingStatistics));
        }
    }

    #endregion

    #region PAYMENT ORDER LISTS COMMANDS

    public RelayCommand SwitchTo30DaysCommand { get; set; }
    public RelayCommand SwitchTo60DaysCommand { get; set; }
    public RelayCommand SwitchTo90DaysCommand { get; set; }
    public RelayCommand OpenLabnextUrlCommand { get; set; }
    public RelayCommand LookupInLabnextByPtNameExternalCommand { get; set; }

    public RelayCommand HistoricalFirstPageCommand { get; set; }
    public RelayCommand HistoricalPreviousPageCommand { get; set; }
    public RelayCommand HistoricalNextPageCommand { get; set; }
    public RelayCommand HistoricalLastPageCommand { get; set; }

    public RelayCommand UnpaidFirstPageCommand { get; set; }
    public RelayCommand UnpaidPreviousPageCommand { get; set; }
    public RelayCommand UnpaidNextPageCommand { get; set; }
    public RelayCommand UnpaidLastPageCommand { get; set; }

    public RelayCommand LoadStatisticsCommand { get; set; }

    #endregion

    #region PAYMENT ORDER LISTS METHODS

    /// <summary>
    /// Initializes the PaymentListCutOffDate from database settings.
    /// If not set or null, defaults to 2025-10-01 and saves to database.
    /// Call this during application startup.
    /// </summary>
    public static void InitializePaymentListCutOffDate()
    {
        try
        {
            string cutOffDateString = DatabaseConnection.ReadStatsSetting("PaymentListCutOffDate");

            if (string.IsNullOrEmpty(cutOffDateString))
            {
                // Default date if not set
                PaymentListCutOffDate = DateTime.Parse("2025-10-01");
                // Save the default to database
                DatabaseConnection.WriteStatsSetting("PaymentListCutOffDate", "2025-10-01");
                Debug.WriteLine("[PaymentOrderLists] PaymentListCutOffDate not found in settings, set to default: 2025-10-01");
            }
            else
            {
                // Try to parse the date from settings
                if (DateTime.TryParse(cutOffDateString, out DateTime parsedDate))
                {
                    PaymentListCutOffDate = parsedDate;
                    Debug.WriteLine($"[PaymentOrderLists] PaymentListCutOffDate loaded from settings: {parsedDate:yyyy-MM-dd}");
                }
                else
                {
                    // If parsing fails, use default and save it
                    PaymentListCutOffDate = DateTime.Parse("2025-10-01");
                    DatabaseConnection.WriteStatsSetting("PaymentListCutOffDate", "2025-10-01");
                    Debug.WriteLine("[PaymentOrderLists] PaymentListCutOffDate invalid in settings, reset to default: 2025-10-01");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentOrderLists] Error initializing PaymentListCutOffDate: {ex.Message}");
            PaymentListCutOffDate = DateTime.Parse("2025-10-01");
        }
    }

    private void RefreshHistoricalCasesGrouping()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            PaymentHistoricalCasesGrouped = new CollectionViewSource { Source = PaymentHistoricalCases };
            PaymentHistoricalCasesGrouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PaymentHistoryModel.PayPeriod)));
            PaymentHistoricalCasesGrouped.SortDescriptions.Add(new SortDescription(nameof(PaymentHistoryModel.PayPeriod), ListSortDirection.Descending));
            RaisePropertyChanged(nameof(PaymentHistoricalCasesGrouped));
        });
    }

    private void RefreshUnpaidCasesGrouping()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            PaymentUnpaidCasesGrouped = new CollectionViewSource { Source = PaymentUnpaidCases };
            PaymentUnpaidCasesGrouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PaymentHistoryModel.PayPeriod)));
            PaymentUnpaidCasesGrouped.SortDescriptions.Add(new SortDescription(nameof(PaymentHistoryModel.PayPeriod), ListSortDirection.Descending));
            RaisePropertyChanged(nameof(PaymentUnpaidCasesGrouped));
        });
    }

    public async Task LoadPaymentDesignersAsync()
    {
        Debug.WriteLine("[PaymentOrderLists] Loading designers...");
        var designers = await GetDesignersModel();
        Debug.WriteLine($"[PaymentOrderLists] Loaded {designers.Count} designers");
        PaymentOrderListDesigners = new ObservableCollection<DesignerModel>(designers);

        // Automatically select the first designer if available
        if (PaymentOrderListDesigners.Count > 0 && SelectedPaymentDesigner == null)
        {
            Debug.WriteLine($"[PaymentOrderLists] Auto-selecting first designer: {PaymentOrderListDesigners[0].FriendlyName}");
            SelectedPaymentDesigner = PaymentOrderListDesigners[0];
        }
    }

    private void LoadHistoricalCasesWithPagination()
    {
        if (SelectedPaymentDesigner == null || bwLoadHistoricalCases.IsBusy)
            return;

        IsLoadingHistoricalCases = true;
        bwLoadHistoricalCases.RunWorkerAsync();
    }

    private void LoadUnpaidCasesWithPagination()
    {
        if (SelectedPaymentDesigner == null || bwLoadUnpaidCases.IsBusy)
            return;

        IsLoadingUnpaidCases = true;
        bwLoadUnpaidCases.RunWorkerAsync();
    }

    private void BwLoadHistoricalCases_DoWork(object? sender, DoWorkEventArgs e)
    {
        if (SelectedPaymentDesigner == null)
            return;

        Debug.WriteLine($"[PaymentOrderLists] BW Loading historical cases - Page {HistoricalCurrentPage}");

        // Use GetAwaiter().GetResult() to synchronously wait for async operation
        var result = GetPaymentHistoryByDesignerPaged(
            SelectedPaymentDesigner.DesignerID!,
            SelectedHistoricalDays,
            HistoricalCurrentPage,
            PageSize).GetAwaiter().GetResult();

        e.Result = result;
        Debug.WriteLine($"[PaymentOrderLists] BW DoWork - Set result with {result.items.Count} items");
    }

    private void BwLoadHistoricalCases_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        Debug.WriteLine($"[PaymentOrderLists] BW Historical - RunWorkerCompleted triggered");

        if (e.Error != null)
        {
            Debug.WriteLine($"[PaymentOrderLists] BW Historical - Error: {e.Error.Message}");
            IsLoadingHistoricalCases = false;
            return;
        }

        if (e.Result == null)
        {
            Debug.WriteLine($"[PaymentOrderLists] BW Historical - Result is null");
            IsLoadingHistoricalCases = false;
            return;
        }

        Debug.WriteLine($"[PaymentOrderLists] BW Historical - Result type: {e.Result.GetType().Name}");

        try
        {
            if (e.Result is ValueTuple<ObservableCollection<PaymentHistoryModel>, int> result)
            {
                var items = result.Item1;
                var totalCount = result.Item2;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    PaymentHistoricalCases = items;
                    HistoricalTotalItems = totalCount;
                    HistoricalTotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
                    IsLoadingHistoricalCases = false;

                    Debug.WriteLine($"[PaymentOrderLists] Historical cases loaded - {items.Count} items on page {HistoricalCurrentPage} of {HistoricalTotalPages}");
                });
            }
            else
            {
                Debug.WriteLine($"[PaymentOrderLists] BW Historical - Pattern match failed");
                IsLoadingHistoricalCases = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentOrderLists] BW Historical - Exception in RunWorkerCompleted: {ex.Message}");
            IsLoadingHistoricalCases = false;
        }
    }

    private void BwLoadUnpaidCases_DoWork(object? sender, DoWorkEventArgs e)
    {
        if (SelectedPaymentDesigner == null)
            return;

        Debug.WriteLine($"[PaymentOrderLists] BW Loading unpaid cases - Page {UnpaidCurrentPage}");

        DateTime? sinceDate = null;
        if (FilterUnpaidByInvoicePeriod)
        {
            sinceDate = GetLastInvoiceClosingDate();
        }

        // Use GetAwaiter().GetResult() to synchronously wait for async operation
        var result = GetUnpaidCasesByDesignerPaged(
            SelectedPaymentDesigner.DesignerID!,
            sinceDate,
            UnpaidCurrentPage,
            PageSize).GetAwaiter().GetResult();

        e.Result = result;
        Debug.WriteLine($"[PaymentOrderLists] BW DoWork - Set result with {result.items.Count} items");
    }

    private void BwLoadUnpaidCases_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        Debug.WriteLine($"[PaymentOrderLists] BW Unpaid - RunWorkerCompleted triggered");

        if (e.Error != null)
        {
            Debug.WriteLine($"[PaymentOrderLists] BW Unpaid - Error: {e.Error.Message}");
            IsLoadingUnpaidCases = false;
            return;
        }

        if (e.Result == null)
        {
            Debug.WriteLine($"[PaymentOrderLists] BW Unpaid - Result is null");
            IsLoadingUnpaidCases = false;
            return;
        }

        Debug.WriteLine($"[PaymentOrderLists] BW Unpaid - Result type: {e.Result.GetType().Name}");

        try
        {
            if (e.Result is ValueTuple<ObservableCollection<PaymentHistoryModel>, int> result)
            {
                var items = result.Item1;
                var totalCount = result.Item2;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    PaymentUnpaidCases = items;
                    UnpaidTotalItems = totalCount;
                    UnpaidTotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
                    IsLoadingUnpaidCases = false;

                    Debug.WriteLine($"[PaymentOrderLists] Unpaid cases loaded - {items.Count} items on page {UnpaidCurrentPage} of {UnpaidTotalPages}");
                });
            }
            else
            {
                Debug.WriteLine($"[PaymentOrderLists] BW Unpaid - Pattern match failed");
                IsLoadingUnpaidCases = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentOrderLists] BW Unpaid - Exception in RunWorkerCompleted: {ex.Message}");
            IsLoadingUnpaidCases = false;
        }
    }

    // Historical pagination methods
    private void HistoricalFirstPage(object o)
    {
        if (HistoricalCurrentPage != 1)
        {
            HistoricalCurrentPage = 1;
            LoadHistoricalCasesWithPagination();
        }
    }

    private void HistoricalPreviousPage(object o)
    {
        if (HistoricalCurrentPage > 1)
        {
            HistoricalCurrentPage--;
            LoadHistoricalCasesWithPagination();
        }
    }

    private void HistoricalNextPage(object o)
    {
        if (HistoricalCurrentPage < HistoricalTotalPages)
        {
            HistoricalCurrentPage++;
            LoadHistoricalCasesWithPagination();
        }
    }

    private void HistoricalLastPage(object o)
    {
        if (HistoricalCurrentPage != HistoricalTotalPages)
        {
            HistoricalCurrentPage = HistoricalTotalPages;
            LoadHistoricalCasesWithPagination();
        }
    }

    // Unpaid pagination methods
    private void UnpaidFirstPage(object o)
    {
        if (UnpaidCurrentPage != 1)
        {
            UnpaidCurrentPage = 1;
            LoadUnpaidCasesWithPagination();
        }
    }

    private void UnpaidPreviousPage(object o)
    {
        if (UnpaidCurrentPage > 1)
        {
            UnpaidCurrentPage--;
            LoadUnpaidCasesWithPagination();
        }
    }

    private void UnpaidNextPage(object o)
    {
        if (UnpaidCurrentPage < UnpaidTotalPages)
        {
            UnpaidCurrentPage++;
            LoadUnpaidCasesWithPagination();
        }
    }

    private void UnpaidLastPage(object o)
    {
        if (UnpaidCurrentPage != UnpaidTotalPages)
        {
            UnpaidCurrentPage = UnpaidTotalPages;
            LoadUnpaidCasesWithPagination();
        }
    }

    private void SwitchTo30Days(object o)
    {
        SelectedHistoricalDays = 30;
    }

    private void SwitchTo60Days(object o)
    {
        SelectedHistoricalDays = 60;
    }

    private void SwitchTo90Days(object o)
    {
        SelectedHistoricalDays = 90;
    }

    private void OpenLabnextUrl(object o)
    {
        if (o is string labnextId && !string.IsNullOrEmpty(labnextId))
        {
            string url = $"https://labnext.com/case/{labnextId}";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Handle error if needed
            }
        }
    }

    /// <summary>
    /// Checks if Labnext is logged in based on the status text
    /// </summary>
    private void CheckLabnextLoginStatus()
    {
        // If status text contains "login" (case insensitive), user is NOT logged in
        IsLabnextLoggedIn = !string.IsNullOrEmpty(LabNextWebViewStatusText) && 
                           !LabNextWebViewStatusText.Contains("login", StringComparison.OrdinalIgnoreCase) &&
                           CbSettingModuleLabnext;
    }

    /// <summary>
    /// Looks up cases in Labnext by Patient Name and opens search results in the external (default) browser
    /// </summary>
    private void LookupInLabnextByPtNameExternal(object o)
    {
        if (o is not PaymentHistoryModel model)
        {
            Debug.WriteLine("[PaymentOrderLists] Invalid model passed to LookupInLabnextByPtNameExternal");
            return;
        }

        if (!CbSettingModuleLabnext)
        {
            Debug.WriteLine("[PaymentOrderLists] Labnext module is disabled");
            ShowMessageBox("Error", 
                "Labnext module is not enabled. Please enable it in Settings.",
                SMessageBoxButtons.Close, 
                NotificationIcon.Warning, 
                5, 
                _MainWindow);
            return;
        }

        if (!IsLabnextLoggedIn)
        {
            Debug.WriteLine("[PaymentOrderLists] Not logged into Labnext");
            ShowMessageBox("Not Logged In", 
                "Please log in to Labnext first.",
                SMessageBoxButtons.Close, 
                NotificationIcon.Warning, 
                5, 
                _MainWindow);
            return;
        }

        string firstName = model.LxPatient_FirstName ?? "";
        string lastName = model.LxPatient_LastName ?? "";

        // Clean first name - remove numbers using regex
        if (!string.IsNullOrEmpty(firstName))
        {
            firstName = firstName.Trim();
            // Remove all digits using regex
            firstName = System.Text.RegularExpressions.Regex.Replace(firstName, @"\d+", "").Trim();
            firstName = firstName.ToUpper()
                                 .Replace("_", "")
                                 .Replace(",", "")
                                 .Replace("%25", "")
                                 .Replace(" STX", "")
                                 .Replace(" STT", "")
                                 .Replace("STX ", "")
                                 .Replace("STT ", "")
                                 .Replace("(STX)", "")
                                 .Replace("(STT)", "")
                                 .Replace("(", "")
                                 .Replace(")", "")
                                 .Replace("%2B", "")
                                 .Trim();
            // Remove standalone dash
            if (firstName == "-")
                firstName = "";
        }

        // Clean last name - remove numbers using regex
        if (!string.IsNullOrEmpty(lastName))
        {
            lastName = lastName.Trim();
            // Remove all digits using regex
            lastName = System.Text.RegularExpressions.Regex.Replace(lastName, @"\d+", "").Trim();
            lastName = lastName.ToUpper()
                               .Replace("_", "")
                               .Replace(",", "")
                               .Replace("%25", "")
                               .Replace(" STX", "")
                               .Replace(" STT", "")
                               .Replace("STX ", "")
                               .Replace("STT ", "")
                               .Replace("(STX)", "")
                               .Replace("(STT)", "")
                               .Replace("(", "")
                               .Replace(")", "")
                               .Replace("%2B", "")
                               .Trim();
            // Remove standalone dash
            if (lastName == "-")
                lastName = "";
        }

        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
        {
            ShowMessageBox("Error", "Cannot lookup this case by patient name.", SMessageBoxButtons.Close, NotificationIcon.Error, 5, _MainWindow);
            return;
        }

        string searchString = Uri.EscapeDataString($"{firstName} {lastName}").Trim();

        if (string.IsNullOrEmpty(searchString.Trim()))
            return;

        try
        {
            string url = $"{LabnextUrl}default/search/?q={searchString}&search_type=all";
            Debug.WriteLine($"[PaymentOrderLists] Opening search URL in external browser: {url}");

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentOrderLists] Error opening patient name search: {ex.Message}");
            ShowMessageBox("Error", 
                $"Error opening search: {ex.Message}",
                SMessageBoxButtons.Close, 
                NotificationIcon.Error, 
                5, 
                _MainWindow);
        }
    }

    private async void LoadPayPeriodStatistics(object? obj)
    {
        if (SelectedPaymentDesigner == null)
        {
            Debug.WriteLine("[PaymentOrderLists] No designer selected for statistics");
            return;
        }

        IsLoadingStatistics = true;
        Debug.WriteLine($"[PaymentOrderLists] Loading pay period statistics for designer: {SelectedPaymentDesigner.DesignerID}");

        try
        {
            var stats = await GetPayPeriodStatisticsByDesigner(SelectedPaymentDesigner.DesignerID, 12);
            PayPeriodStatistics = stats;
            Debug.WriteLine($"[PaymentOrderLists] Loaded {stats.Count} pay period statistics");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentOrderLists] Error loading statistics: {ex.Message}");
        }
        finally
        {
            IsLoadingStatistics = false;
        }
    }

    #endregion
}
