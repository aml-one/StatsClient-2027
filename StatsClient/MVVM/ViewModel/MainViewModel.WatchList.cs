using StatsClient.MVVM.Core;

using StatsClient.MVVM.Model;

using StatsClient.MVVM.Services;

using System.Diagnostics;

using System.Windows;

using System.Windows.Threading;

using static StatsClient.MVVM.Core.DatabaseOperations;

using static StatsClient.MVVM.Core.Functions;



namespace StatsClient.MVVM.ViewModel;



public partial class MainViewModel

{

    private readonly DispatcherTimer _watchListPollTimer = new();

    private readonly DispatcherTimer _watchListRushScanTimer = new();

    private bool _watchListPollInFlight;

    private bool _watchListRushScanInFlight;



    private List<ThreeShapeOrdersModel> watchListDisplayCases = [];

    public List<ThreeShapeOrdersModel> WatchListDisplayCases

    {

        get => watchListDisplayCases;

        set

        {

            watchListDisplayCases = value;

            RaisePropertyChanged(nameof(WatchListDisplayCases));

            RaisePropertyChanged(nameof(WatchListCount));

        }

    }



    public int WatchListCount => WatchListDisplayCases.Count;



    public RelayCommand SwitchToWatchListTabCommand { get; private set; } = null!;

    public RelayCommand RefreshWatchListTabCommand { get; private set; } = null!;

    public RelayCommand TestWatchListNotificationCommand { get; private set; } = null!;



    private void InitializeWatchList()

    {

        WatchListStore.Load();

        WatchListStore.Changed += OnWatchListStoreChanged;

        WatchListStore.PurgeOlderThanDays(7);



        SwitchToWatchListTabCommand = new RelayCommand(_ => SwitchToWatchListTab());

        RefreshWatchListTabCommand = new RelayCommand(_ => _ = RefreshWatchListTabAsync());

        TestWatchListNotificationCommand = new RelayCommand(_ => ShowTestWatchListNotification());



        _watchListPollTimer.Tick += WatchListPollTimer_Tick;

        _watchListPollTimer.Interval = TimeSpan.FromMinutes(1);

        _watchListPollTimer.Start();



        _watchListRushScanTimer.Tick += WatchListRushScanTimer_Tick;

        _watchListRushScanTimer.Interval = TimeSpan.FromMinutes(15);

        _watchListRushScanTimer.Start();



        SyncAllWatchListEntryStatuses(persist: true);

        _ = RefreshWatchListTabAsync();

        ApplyWatchFlagsToCurrent3ShapeList();



        _ = RunWatchListRushScanAsync();

    }



    private void ShowTestWatchListNotification()

    {

        Debug.WriteLine("[WatchList] Test notification requested.");

        WatchListNotificationManager.Show(new WatchListStatusChange

        {

            IntOrderID = "TEST-ORDER",

            Title = "Test notification",

            Message = "Watch list notifications are working.\nClick to dismiss.",

            AccentColor = WatchListLabels.AccentColorForStatus("psScanned"),

            PanNumber = "0000"

        });

    }



    private void OnWatchListStoreChanged()

    {

        Application.Current?.Dispatcher.BeginInvoke(() =>

        {

            ApplyWatchFlagsToCurrent3ShapeList();

            _ = RefreshWatchListTabAsync();

        });

    }



    public bool IsOrderOnWatchList(string? orderId) => WatchListStore.Contains(orderId);



    public void ToggleWatchListForOrder(ThreeShapeOrdersModel order, bool onWatchList)

    {

        if (string.IsNullOrWhiteSpace(order.IntOrderID))

            return;



        if (onWatchList)

        {

            var entry = CreateWatchEntryFromOrder(order);

            SyncWatchListEntryStatuses([entry], persist: true);

            WatchListStore.AddOrUpdate(entry);

            order.IsOnWatchList = true;

        }

        else

        {

            WatchListStore.Remove(order.IntOrderID);

            order.IsOnWatchList = false;

        }



        ApplyWatchFlagsToCurrent3ShapeList();

        _ = RefreshWatchListTabAsync();

    }



    private async void WatchListRushScanTimer_Tick(object? sender, EventArgs e)

        => await RunWatchListRushScanAsync();



    private async Task RunWatchListRushScanAsync()

    {

        if (_watchListRushScanInFlight || ThreeShapeServerIsDown)

            return;



        if (string.IsNullOrWhiteSpace(DtYesterday) || string.IsNullOrWhiteSpace(DtToday))

            return;



        _watchListRushScanInFlight = true;

        try

        {

            string from = DtYesterday + RestDayStart;

            string to = DtToday + RestDayEnd;



            var candidates = await Task.Run(() =>

                WatchListRushScanQuery.QueryTodayAndYesterdayScanned(from, to));



            bool anyAdded = false;

            foreach (var candidate in candidates)

            {

                if (WatchListStore.Contains(candidate.IntOrderID))

                    continue;



                var entry = CreateWatchEntryFromRushCandidate(candidate);

                SyncWatchListEntryStatuses([entry], persist: false);

                WatchListStore.AddOrUpdate(entry);

                anyAdded = true;



                string patient = $"{entry.Patient_FirstName} {entry.Patient_LastName}".Trim();

                string message = "Automatically added — scanned today or yesterday with rush in order comments.";

                if (!string.IsNullOrWhiteSpace(patient))

                    message += $"\n{patient}";



                WatchListNotificationManager.Show(new WatchListStatusChange

                {

                    IntOrderID = entry.IntOrderID,

                    Title = "Rush case on watch list",

                    Message = message,

                    AccentColor = WatchListLabels.AccentColorForStatus("psScanned"),

                    PatientName = patient,

                    PanNumber = entry.PanNumber

                });

            }



            if (anyAdded)

            {

                Application.Current?.Dispatcher.BeginInvoke(() =>

                {

                    ApplyWatchFlagsToCurrent3ShapeList();

                    _ = RefreshWatchListTabAsync();

                });

            }

        }

        finally

        {

            _watchListRushScanInFlight = false;

        }

    }



    private WatchListEntry CreateWatchEntryFromRushCandidate(WatchListRushScanCandidate candidate)

    {

        var inList = Current3ShapeOrderList.FirstOrDefault(x =>

            string.Equals(x.IntOrderID, candidate.IntOrderID, StringComparison.OrdinalIgnoreCase));

        if (inList is not null)

            return CreateWatchEntryFromOrder(inList);



        string status = candidate.MaxProcessStatusID ?? "psScanned";

        string processLock = string.IsNullOrWhiteSpace(candidate.ProcessLockID) ? "plReady" : candidate.ProcessLockID;

        string image = @"\Images\ListViewIcons\" + IconSelect(status, "", processLock) + ".png";



        return new WatchListEntry

        {

            IntOrderID = candidate.IntOrderID,

            AddedUtc = DateTime.UtcNow,

            LastProcessStatusID = status,

            LastProcessLockID = processLock,

            Patient_FirstName = candidate.Patient_FirstName,

            Patient_LastName = candidate.Patient_LastName,

            Customer = candidate.Customer,

            Items = candidate.Items,

            ImageSource = image

        };

    }



    private static WatchListEntry CreateWatchEntryFromOrder(ThreeShapeOrdersModel order) => new()

    {

        IntOrderID = order.IntOrderID ?? "",

        AddedUtc = DateTime.UtcNow,

        LastProcessStatusID = order.MaxProcessStatusID ?? order.ProcessStatusID,

        LastProcessLockID = order.ProcessLockID,

        Patient_FirstName = order.Patient_FirstName,

        Patient_LastName = order.Patient_LastName,

        Customer = order.Customer,

        Items = order.Items,

        PanNumber = order.PanNumber,

        ImageSource = order.ImageSource

    };



    public void ApplyWatchFlagsToCurrent3ShapeList()

    {

        foreach (var item in Current3ShapeOrderList)

            item.IsOnWatchList = WatchListStore.Contains(item.IntOrderID);

    }



    private async Task RefreshWatchListTabAsync()

    {

        var entries = WatchListStore.Entries.OrderByDescending(e => e.AddedUtc).ToList();

        if (entries.Count == 0)

        {

            WatchListDisplayCases = [];

            return;

        }



        if (!ThreeShapeServerIsDown)

            await Task.Run(() => SyncWatchListEntryStatuses(entries, persist: true));



        var ids = entries.Select(e => e.IntOrderID).ToList();

        Dictionary<string, WatchListOrderDetailsRow> details = [];

        Dictionary<string, string> checkedOutDesigners = [];



        if (!ThreeShapeServerIsDown)

        {

            details = await Task.Run(() => WatchListOrderDetailsQuery.QueryByOrderIds(ids));

            checkedOutDesigners = await Task.Run(GetCheckedOutDesignerFriendlyNamesByOrderId);

        }



        var display = entries

            .Select(entry => BuildWatchListDisplayModel(entry, details))

            .ToList();



        ApplyCheckedOutDesignerNames(display, checkedOutDesigners);



        Application.Current?.Dispatcher.Invoke(() =>

        {

            WatchListDisplayCases = display;

            if (_MainWindow?.listViewWatchList is not null)

            {

                _MainWindow.listViewWatchList.ItemsSource = WatchListDisplayCases;

                _MainWindow.listViewWatchList.Items.Refresh();

            }

        });

    }



    private ThreeShapeOrdersModel BuildWatchListDisplayModel(

        WatchListEntry entry,

        IReadOnlyDictionary<string, WatchListOrderDetailsRow> details)

    {

        var inList = Current3ShapeOrderList.FirstOrDefault(x =>

            string.Equals(x.IntOrderID, entry.IntOrderID, StringComparison.OrdinalIgnoreCase));

        if (inList is not null)

            return CloneOrderForWatchListTab(inList);



        details.TryGetValue(entry.IntOrderID, out WatchListOrderDetailsRow? row);



        string status = row?.MaxProcessStatusID ?? entry.LastProcessStatusID ?? "psCreated";

        string processLock = string.IsNullOrWhiteSpace(row?.ProcessLockID)

            ? (string.IsNullOrWhiteSpace(entry.LastProcessLockID) ? "plReady" : entry.LastProcessLockID)

            : row.ProcessLockID;

        string scanSource = row?.ScanSource ?? "";

        string image = entry.ImageSource ?? @"\Images\ListViewIcons\" + IconSelect(status, scanSource, processLock) + ".png";



        string panNumber = entry.PanNumber ?? "";

        if (string.IsNullOrWhiteSpace(panNumber))

            panNumber = TryParsePanFromOrderId(entry.IntOrderID);



        string panColor = GetBackPanColorHEX(panNumber);

        if (panNumber == "" || panColor == ColorSchemeResourceCatalog.GetHex("WhiteBackground"))

            panColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Transparent");



        string extOrderId = row?.ExtOrderID ?? "";

        if (int.TryParse(extOrderId, out _))

            extOrderId = "";



        string modificationDate = FormatModificationDateFriendly(row?.ModificationDate);

        string lastTouched = string.IsNullOrWhiteSpace(row?.UserID)

            ? ""

            : ReadComputerName(row.UserID);



        return new ThreeShapeOrdersModel

        {

            IntOrderID = entry.IntOrderID,

            Patient_FirstName = row?.Patient_FirstName ?? entry.Patient_FirstName,

            Patient_LastName = row?.Patient_LastName ?? entry.Patient_LastName,

            Customer = row?.Customer ?? entry.Customer,

            Items = row?.Items ?? entry.Items,

            PanNumber = panNumber,

            PanColor = panColor,

            ImageSource = image,

            ExtOrderID = extOrderId,

            ProcessStatusID = status,

            MaxProcessStatusID = status,

            ProcessLockID = processLock,

            ScanSource = scanSource,

            ScanSourceFriendlyName = GetScanner(scanSource),

            CaseStatus = CaseStatusSelect(status, scanSource, processLock),

            ModificationDate = modificationDate,

            LastModifiedComputerName = lastTouched,

            MaxCreateDateFriendly = row?.MaxCreateDate ?? "",

            Shade = DetermininingShade(entry.IntOrderID),

            IsOnWatchList = true,

            IsWatchListRow = true,

            IsCheckedOut = string.Equals(processLock, "plCheckedOut", StringComparison.OrdinalIgnoreCase),

        };

    }



    private static ThreeShapeOrdersModel CloneOrderForWatchListTab(ThreeShapeOrdersModel source) => new()

    {

        IntOrderID = source.IntOrderID,

        Patient_FirstName = source.Patient_FirstName,

        Patient_LastName = source.Patient_LastName,

        Patient_RefNo = source.Patient_RefNo,

        ExtOrderID = source.ExtOrderID,

        OrderComments = source.OrderComments,

        Items = source.Items,

        OperatorName = source.OperatorName,

        Customer = source.Customer,

        ManufName = source.ManufName,

        CacheMaterialName = source.CacheMaterialName,

        ScanSource = source.ScanSource,

        CacheMaxScanDate = source.CacheMaxScanDate,

        TraySystemType = source.TraySystemType,

        MaxCreateDate = source.MaxCreateDate,

        MaxProcessStatusID = source.MaxProcessStatusID,

        ProcessStatusID = source.ProcessStatusID,

        AltProcessStatusID = source.AltProcessStatusID,

        ProcessLockID = source.ProcessLockID,

        WasSent = source.WasSent,

        ModificationDate = source.ModificationDate,

        ImageSource = source.ImageSource,

        PanColor = source.PanColor,

        PanColorName = source.PanColorName,

        CaseStatus = source.CaseStatus,

        PanNumber = source.PanNumber,

        Shade = source.Shade,

        LastModificationForSorting = source.LastModificationForSorting,

        LastModifiedComputerName = source.LastModifiedComputerName,

        CreateDateForSorting = source.CreateDateForSorting,

        ScanSourceFriendlyName = source.ScanSourceFriendlyName,

        CacheMaxScanDateFriendly = source.CacheMaxScanDateFriendly,

        MaxCreateDateFriendly = source.MaxCreateDateFriendly,

        DesignerName = source.DesignerName,

        IsOnWatchList = true,

        IsWatchListRow = true,

        IsCheckedOut = source.IsCheckedOut,

        ShowCheckedOutDesignerFromStats = source.ShowCheckedOutDesignerFromStats,

        HasAnyImage = source.HasAnyImage,

    };



    private static string TryParsePanFromOrderId(string orderId)

    {

        if (string.IsNullOrWhiteSpace(orderId))

            return "";



        string first = orderId.Split('-')[0];

        return int.TryParse(first, out int pan) ? pan.ToString() : "";

    }



    private string FormatModificationDateFriendly(string? modificationDate)

    {

        if (string.IsNullOrWhiteSpace(modificationDate))

            return "";



        if (!DateTime.TryParse(modificationDate, out DateTime dt))

            return modificationDate;



        if (IsItToday(modificationDate))

            return dt.ToString("h:mm tt");



        _ = DateTime.TryParse(DtThisMonday, out DateTime dtLastWeekSunday);

        dtLastWeekSunday = dtLastWeekSunday.AddDays(-1);



        if (dt > dtLastWeekSunday)

            return dt.ToString("dddd - h:mm tt");



        if (IsItThisYear(modificationDate))

            return dt.ToString("MM/dd - h:mm tt");



        return modificationDate;

    }



    private void SyncAllWatchListEntryStatuses(bool persist)

    {

        if (ThreeShapeServerIsDown)

            return;



        SyncWatchListEntryStatuses(WatchListStore.Entries.ToList(), persist);

    }



    private static void SyncWatchListEntryStatuses(IEnumerable<WatchListEntry> entries, bool persist)

    {

        var list = entries.Where(e => !string.IsNullOrWhiteSpace(e.IntOrderID)).ToList();

        if (list.Count == 0)

            return;



        var statuses = WatchListStatusQuery.QueryStatuses(list.Select(e => e.IntOrderID).ToList());



        foreach (var entry in list)

        {

            var live = statuses.FirstOrDefault(s =>

                string.Equals(s.IntOrderID, entry.IntOrderID, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(live.IntOrderID))

                continue;



            entry.LastProcessStatusID = live.ProcessStatusID;

            entry.LastProcessLockID = live.ProcessLockID;



            if (persist)

                WatchListStore.AddOrUpdate(entry);

        }

    }



    private void SwitchToWatchListTab()

    {

        Application.Current.Dispatcher.Invoke(() =>

        {

            LabnextCanReload = true;

            ClearAllSearchCriteria();

            _MainWindow.mainTabControl.SelectedItem = _MainWindow.watchListTab;

            UpdateTabChromeForSelection();

        });



        _ = RefreshWatchListTabAsync();

        if (!_watchListPollInFlight)

            WatchListPollTimer_Tick(null, EventArgs.Empty);

    }



    private async void WatchListPollTimer_Tick(object? sender, EventArgs e)

    {

        if (_watchListPollInFlight || ThreeShapeServerIsDown)

            return;



        _watchListPollInFlight = true;

        try

        {

            WatchListStore.PurgeOlderThanDays(7);

            var entries = WatchListStore.Entries.ToList();

            if (entries.Count == 0)

            {

                Application.Current?.Dispatcher.BeginInvoke(() => _ = RefreshWatchListTabAsync());

                return;

            }



            var statuses = await Task.Run(() => WatchListStatusQuery.QueryStatuses(entries.Select(x => x.IntOrderID).ToList()));



            foreach (var entry in entries.ToList())

            {

                var live = statuses.FirstOrDefault(s =>

                    string.Equals(s.IntOrderID, entry.IntOrderID, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(live.IntOrderID))

                    continue;



                bool statusChanged = !string.Equals(entry.LastProcessStatusID, live.ProcessStatusID, StringComparison.OrdinalIgnoreCase)

                    && (IsTrackedProcessStatus(entry.LastProcessStatusID) || IsTrackedProcessStatus(live.ProcessStatusID));

                bool lockChanged = !string.Equals(entry.LastProcessLockID, live.ProcessLockID, StringComparison.OrdinalIgnoreCase)

                    && (IsTrackedProcessLock(entry.LastProcessLockID) || IsTrackedProcessLock(live.ProcessLockID));



                if (!statusChanged && !lockChanged)

                    continue;



                if (statusChanged)

                {

                    NotifyWatchListChange(

                        entry,

                        "Process status changed",

                        $"{WatchListLabels.DescribeProcessStatus(entry.LastProcessStatusID)} → {WatchListLabels.DescribeProcessStatus(live.ProcessStatusID)}",

                        WatchListLabels.AccentColorForStatus(live.ProcessStatusID));

                }

                else if (lockChanged)

                {

                    NotifyWatchListChange(

                        entry,

                        "Lock status changed",

                        $"{WatchListLabels.DescribeProcessLock(entry.LastProcessLockID)} → {WatchListLabels.DescribeProcessLock(live.ProcessLockID)}",

                        WatchListLabels.AccentColorForLock(live.ProcessLockID));

                }



                entry.LastProcessStatusID = live.ProcessStatusID;

                entry.LastProcessLockID = live.ProcessLockID;

                WatchListStore.AddOrUpdate(entry);

            }



            Application.Current?.Dispatcher.BeginInvoke(() =>

            {

                ApplyWatchFlagsToCurrent3ShapeList();

                _ = RefreshWatchListTabAsync();

            });

        }

        finally

        {

            _watchListPollInFlight = false;

        }

    }



    private static bool IsTrackedProcessStatus(string? statusId) =>

        statusId is "psCreated" or "psScanned" or "psModelled" or "psSent";



    private static bool IsTrackedProcessLock(string? lockId) =>

        lockId is "plReady" or "plCheckedOut";



    private static void NotifyWatchListChange(

        WatchListEntry entry,

        string title,

        string message,

        string accentColor)

    {

        string patient = $"{entry.Patient_FirstName} {entry.Patient_LastName}".Trim();

        if (!string.IsNullOrWhiteSpace(patient))

            message += $"\n{patient}";



        WatchListNotificationManager.Show(new WatchListStatusChange

        {

            IntOrderID = entry.IntOrderID,

            Title = title,

            Message = message,

            AccentColor = accentColor,

            PatientName = patient,

            PanNumber = entry.PanNumber

        });

    }

}

