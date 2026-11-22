using StatsClient.MVVM.Core;

using StatsClient.MVVM.Model;

using StatsClient.MVVM.Services;

using System.Diagnostics;

using System.IO;

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

    private bool _watchListRefreshPending;
    private bool _watchListRefreshPendingAgain;



    private List<ThreeShapeOrdersModel> watchListDisplayCases = [];

    public List<ThreeShapeOrdersModel> WatchListDisplayCases

    {

        get => watchListDisplayCases;

        set

        {

            watchListDisplayCases = value;

            RaisePropertyChanged(nameof(WatchListDisplayCases));

        }

    }



    public int WatchListCount => WatchListStore.Entries.Count;

    private void RaiseWatchListCountChanged() => RaisePropertyChanged(nameof(WatchListCount));



    public RelayCommand SwitchToWatchListTabCommand { get; private set; } = null!;

    public RelayCommand RefreshWatchListTabCommand { get; private set; } = null!;

    public RelayCommand TestWatchListNotificationCommand { get; private set; } = null!;



    private void InitializeWatchList()

    {
        PerfDiagLog.Write("WatchList", "InitializeWatchList");

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

        RaiseWatchListCountChanged();

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
        if (Application.Current?.Dispatcher is not { } dispatcher)
            return;

        dispatcher.BeginInvoke(() =>
        {
            if (!ReferenceEquals(Instance, this))
                return;

            try
            {
                RaiseWatchListCountChanged();
                ApplyWatchFlagsToCurrent3ShapeList();
                ApplyWatchFlagsToGlobalSearch3ShapeList();
                ScheduleWatchListRefresh();
            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
            }
        });
    }

    private void ScheduleWatchListRefresh()
    {
        if (_watchListRefreshPending)
        {
            _watchListRefreshPendingAgain = true;
            return;
        }

        _watchListRefreshPending = true;
        Application.Current?.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                do
                {
                    _watchListRefreshPendingAgain = false;
                    await RefreshWatchListTabAsync();
                } while (_watchListRefreshPendingAgain);
            }
            finally
            {
                _watchListRefreshPending = false;
            }
        }, DispatcherPriority.Background);
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
        ApplyWatchFlagsToGlobalSearch3ShapeList();

        _ = RefreshWatchListTabAsync();

    }



    public bool ToggleWatchListFromListItem(object? dataContext)
    {
        ThreeShapeOrdersModel? order = ResolveOrderForWatchListToggle(dataContext);
        if (order is null || string.IsNullOrWhiteSpace(order.IntOrderID))
            return false;

        bool newState = !IsOrderOnWatchList(order.IntOrderID);
        ToggleWatchListForOrder(order, newState);

        if (dataContext is GlobalSearchModel globalSearch)
            globalSearch.IsOnWatchList = newState;

        string panLabel = string.IsNullOrWhiteSpace(order.PanNumber) ? order.IntOrderID : $"Pan {order.PanNumber}";
        ShowNotificationMessage(
            "Watch List",
            newState
                ? $"{panLabel} added to your watch list."
                : $"{panLabel} removed from your watch list.",
            newState ? NotificationIcon.Success : NotificationIcon.Info);

        return newState;
    }



    private ThreeShapeOrdersModel? ResolveOrderForWatchListToggle(object? dataContext)
    {
        if (dataContext is ThreeShapeOrdersModel order)
        {
            var inList = Current3ShapeOrderList.FirstOrDefault(o =>
                string.Equals(o.IntOrderID, order.IntOrderID, StringComparison.OrdinalIgnoreCase));
            return inList ?? order;
        }

        if (dataContext is GlobalSearchModel globalSearch)
            return CreateOrderInfoModelFromGlobalSearch(globalSearch);

        return null;
    }



    public void ApplyWatchFlagsToGlobalSearch3ShapeList()
    {
        if (GlobalSearchResult3Shape.Count == 0)
            return;

        foreach (var item in GlobalSearchResult3Shape)
        {
            if (item is null)
                continue;

            item.IsOnWatchList = WatchListStore.Contains(item.IntOrderId);
        }
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

                string message = "Automatically added.";

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
        var list = Current3ShapeOrderList;
        if (list.Count == 0)
            return;

        foreach (var item in list.ToList())
        {
            if (item is null)
                continue;

            item.IsOnWatchList = WatchListStore.Contains(item.IntOrderID);
        }
    }



    private async Task RefreshWatchListTabAsync()

    {
        await PerfDiagLog.TrackAsync("WatchList", "RefreshWatchListTab", async () =>
        {
        var entries = WatchListStore.Entries.OrderByDescending(e => e.AddedUtc).ToList();

        if (entries.Count == 0)

        {

            WatchListDisplayCases = [];

            return;

        }



        await Task.Run(() => SyncWatchListEntryStatuses(entries, persist: true));



        var ids = entries.Select(e => e.IntOrderID).ToList();

        Dictionary<string, WatchListOrderDetailsRow> details = [];

        Dictionary<string, string> checkedOutDesigners = [];



        details = await Task.Run(() => WatchListOrderDetailsQuery.QueryByOrderIds(ids));

        checkedOutDesigners = await Task.Run(GetCheckedOutDesignerFriendlyNamesByOrderId);



        var display = entries

            .Select(entry => BuildWatchListDisplayModel(entry, details))

            .ToList();



        ApplyCheckedOutDesignerNames(display, checkedOutDesigners);

        var uiDispatcher = Application.Current?.Dispatcher;
        if (uiDispatcher is not null && uiDispatcher.CheckAccess())
            WatchListDisplayCases = display;
        else
            uiDispatcher?.Invoke(() => WatchListDisplayCases = display);

        }, $"entries={WatchListStore.Entries.Count}");
    }



    private ThreeShapeOrdersModel BuildWatchListDisplayModel(
        WatchListEntry entry,
        IReadOnlyDictionary<string, WatchListOrderDetailsRow> details)
    {
        details.TryGetValue(entry.IntOrderID, out WatchListOrderDetailsRow? row);

        string status = PickWatchListDisplayStatus(row?.MaxProcessStatusID, entry.LastProcessStatusID);
        string processLock = WatchListOrderDetailsQuery.NormalizeProcessLock(
            string.IsNullOrWhiteSpace(row?.ProcessLockID)
                ? entry.LastProcessLockID
                : row.ProcessLockID);
        string scanSource = row?.ScanSource ?? "";

        string image = @"\Images\ListViewIcons\" + IconSelect(status, scanSource, processLock) + ".png";

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

        var model = new ThreeShapeOrdersModel
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
            IsCaseWereDesigned = string.Equals(status, "psModelled", StringComparison.OrdinalIgnoreCase),
            IsCheckedOut = string.Equals(processLock, "plCheckedOut", StringComparison.OrdinalIgnoreCase),
        };

        ApplyWatchListDesignerDisplay(model);
        return model;
    }

    private void ApplyWatchListDesignerDisplay(ThreeShapeOrdersModel model)
    {
        if (string.IsNullOrWhiteSpace(model.IntOrderID))
            return;

        List<DesignerHistoryModel> designerHistory = LoadDesignerHistory(model.IntOrderID);
        model.HasDesignerHistory = designerHistory.Count > 0;
        model.DesignerHistory = designerHistory;

        if (string.IsNullOrWhiteSpace(model.ExtOrderID) && designerHistory.Count > 0)
        {
            model.ExtOrderID = designerHistory[0].DesignerName;
            model.DesignerName = designerHistory[0].DesignerName;
        }

        model.DesignedDateTime = ResolveDesignedDateTimeForList(designerHistory, model.Patient_RefNo, DtThisMonday);
        model.ShowDesignedDateTimeInColumn = ShouldShowDesignedDateTimeInDesignerColumn(
            model.ExtOrderID,
            model.DesignedDateTime,
            model.ShowCheckedOutDesignerFromStats);
    }

    private List<DesignerHistoryModel> LoadDesignerHistory(string orderId)
    {
        var designerHistory = new List<DesignerHistoryModel>();
        if (string.IsNullOrWhiteSpace(ThreeShapeDirectoryHelper))
            return designerHistory;

        string designedByFile = $@"{ThreeShapeDirectoryHelper}{orderId}\History\DesignedBy";
        if (!File.Exists(designedByFile))
            designedByFile = designedByFile.Replace(@"\History\", @"\");

        if (!File.Exists(designedByFile))
            return designerHistory;

        try
        {
            foreach (string line in File.ReadAllLines(designedByFile))
            {
                string[] parts = line.Split(']');
                if (parts.Length < 2)
                    continue;

                if (!DateTime.TryParse(parts[0].Replace("[", ""), out DateTime designedAt))
                    continue;

                designerHistory.Add(new DesignerHistoryModel
                {
                    Year = $"[{designedAt:yyyy}]",
                    Day = designedAt.ToString("ddd"),
                    Date = designedAt.ToString("M/d"),
                    Time = designedAt.ToString("h:mm tt"),
                    DesignerName = parts[1].Trim(),
                    DesignedAt = designedAt,
                });
            }

            if (designerHistory.Count > 0)
                designerHistory.Reverse();
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }

        return designerHistory;
    }



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

        if (dtLastWeekSunday != default)
            dtLastWeekSunday = dtLastWeekSunday.AddDays(-1);



        if (dt > dtLastWeekSunday && dtLastWeekSunday != default)
            return dt.ToString("dddd - h:mm tt");



        if (IsItThisYear(modificationDate))

            return dt.ToString("MM/dd - h:mm tt");



        return modificationDate;

    }



    private void SyncAllWatchListEntryStatuses(bool persist)

        => SyncWatchListEntryStatuses(WatchListStore.Entries.ToList(), persist);



    private void SyncWatchListEntryStatuses(IEnumerable<WatchListEntry> entries, bool persist)

    {

        var list = entries.Where(e => !string.IsNullOrWhiteSpace(e.IntOrderID)).ToList();

        if (list.Count == 0)

            return;



        var (statuses, succeeded) = WatchListStatusQuery.QueryStatusesWithResult(list.Select(e => e.IntOrderID).ToList());

        if (succeeded)
            ThreeShapeServerIsDown = false;



        bool anyChanged = false;

        foreach (var entry in list)

        {

            var live = statuses.FirstOrDefault(s =>

                string.Equals(s.IntOrderID, entry.IntOrderID, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(live.IntOrderID))

                continue;



            bool statusChanged = !string.Equals(entry.LastProcessStatusID, live.ProcessStatusID, StringComparison.OrdinalIgnoreCase);

            bool lockChanged = !string.Equals(entry.LastProcessLockID, live.ProcessLockID, StringComparison.OrdinalIgnoreCase);

            if (!statusChanged && !lockChanged)

                continue;



            entry.LastProcessStatusID = live.ProcessStatusID;

            entry.LastProcessLockID = live.ProcessLockID;



            if (persist)
            {
                WatchListStore.AddOrUpdate(entry, notifyChanged: false);
                anyChanged = true;
            }

        }

        if (persist && anyChanged)
            WatchListStore.NotifyChanged();

    }



    private void SwitchToWatchListTab()

    {

        Application.Current.Dispatcher.Invoke(() =>

        {

            LabnextCanReload = false;

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
        await PerfDiagLog.TrackAsync("WatchList", "PollTimer_Tick", async () =>
        {
        if (_watchListPollInFlight)
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



            var (statuses, succeeded) = await Task.Run(() =>
                WatchListStatusQuery.QueryStatusesWithResult(entries.Select(x => x.IntOrderID).ToList()));

            if (succeeded)
                ThreeShapeServerIsDown = false;

            var foundIds = statuses
                .Select(s => s.IntOrderID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (succeeded)
            {
                foreach (var entry in entries.ToList())
                {
                    if (foundIds.Contains(entry.IntOrderID))
                        continue;

                    NotifyWatchListRenamedOrRemoved(entry);
                    WatchListStore.Remove(entry.IntOrderID);
                }
            }

            foreach (var entry in WatchListStore.Entries.ToList())

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

                        WatchListLabels.TitleForNewStatus(live.ProcessStatusID),

                        $"{WatchListLabels.DescribeProcessStatus(entry.LastProcessStatusID)} → {WatchListLabels.DescribeProcessStatus(live.ProcessStatusID)}",

                        WatchListLabels.AccentColorForStatusChange(live.ProcessStatusID, live.OrderComments),

                        statusDetail: true);

                }

                else if (lockChanged)

                {

                    NotifyWatchListChange(

                        entry,

                        WatchListLabels.TitleForNewLock(live.ProcessLockID),

                        $"{WatchListLabels.DescribeProcessLock(entry.LastProcessLockID)} → {WatchListLabels.DescribeProcessLock(live.ProcessLockID)}",

                        WatchListLabels.AccentColorForLock(live.ProcessLockID),

                        statusDetail: true);

                }



                entry.LastProcessStatusID = live.ProcessStatusID;

                entry.LastProcessLockID = live.ProcessLockID;

                WatchListStore.AddOrUpdate(entry, notifyChanged: false);

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
        });
    }



    private static string PickWatchListDisplayStatus(string? rowStatus, string? entryStatus)
    {
        static int Rank(string? status) => status switch
        {
            "psSent" => 4,
            "psModelled" => 3,
            "psScanned" => 2,
            "psCreated" => 1,
            _ => 0,
        };

        string row = string.IsNullOrWhiteSpace(rowStatus) ? "psCreated" : rowStatus;
        string entry = string.IsNullOrWhiteSpace(entryStatus) ? "psCreated" : entryStatus;
        return Rank(row) >= Rank(entry) ? row : entry;
    }



    private static bool IsTrackedProcessStatus(string? statusId) =>

        statusId is "psCreated" or "psScanned" or "psModelled" or "psSent";



    private static bool IsTrackedProcessLock(string? lockId) =>

        lockId is "plReady" or "plCheckedOut";



    private static void NotifyWatchListRenamedOrRemoved(WatchListEntry entry)
    {
        string patient = $"{entry.Patient_FirstName} {entry.Patient_LastName}".Trim();
        string message = $"{entry.IntOrderID} is no longer in 3Shape under this name.\nIt may have been renamed.";

        NotifyWatchListChange(
            entry,
            "Watched order renamed",
            message,
            WatchListLabels.AccentColorForRenamed());
    }

    private static void NotifyWatchListChange(

        WatchListEntry entry,

        string title,

        string message,

        string accentColor,

        bool statusDetail = false)

    {

        string patient = $"{entry.Patient_FirstName} {entry.Patient_LastName}".Trim();



        WatchListNotificationManager.Show(new WatchListStatusChange

        {

            IntOrderID = entry.IntOrderID,

            Title = title,

            Message = statusDetail ? message : AppendPatientLine(message, patient),

            StatusDetail = statusDetail ? message : null,

            AccentColor = accentColor,

            PatientName = statusDetail ? patient : null,

            PanNumber = entry.PanNumber

        });

    }



    private static string AppendPatientLine(string message, string patient)

    {

        if (string.IsNullOrWhiteSpace(patient))

            return message;

        return $"{message}\n{patient}";

    }

}

