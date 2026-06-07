using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private bool _deferredStartupLoadAccountInfos;
    private bool _allowPeriodicBackgroundTasks;
    private bool _deferredStartupUiScheduled;
    private bool _deferredStartupUiRunning;
    private string? _pendingStartupFilterRestore;
    private static readonly object EnsureCreatedLock = new();
    private static bool _initialStartupHandlersRegistered;
    private Timer? _startupHeartbeat;

    internal static void EnsureCreated()
    {
        lock (EnsureCreatedLock)
        {
            if (Instance is null)
            {
                _ = new MainViewModel();
            }
        }
    }

    internal void SyncMainWindowReferenceFromSplash()
    {
        if (_mainWindow is not null)
        {
            return;
        }

        if (SplashViewModel.Instance?.mainWindow is MainWindow window)
        {
            _mainWindow = window;
            RaisePropertyChanged(nameof(_MainWindow));
        }
        else if (MainWindow.Instance is MainWindow liveWindow)
        {
            _mainWindow = liveWindow;
            RaisePropertyChanged(nameof(_MainWindow));
        }
    }

    internal void TryFocusSearchField()
    {
        if (_mainWindow?.tbSearch is not { } searchBox)
        {
            return;
        }

        searchBox.Focus();
    }

    internal void TryFocusArchivesSearchField()
    {
        if (_mainWindow?.tbSearchArchives is not { } searchBox)
        {
            return;
        }

        searchBox.Focus();
    }

    /// <summary>
    /// Startup loads SQLite/SQL values on a background thread before the window exists.
    /// Re-notify bindings once the UI is shown so home-tab modules and archive stats populate.
    /// </summary>
    internal void RefreshStartupUiBindings()
    {
        RaisePropertyChanged(nameof(CbSettingModuleFolderSubscription));
        RaisePropertyChanged(nameof(CbSettingModuleAccountInfos));
        RaisePropertyChanged(nameof(CbSettingModuleLabnext));
        RaisePropertyChanged(nameof(CbSettingModuleSmartOrderNames));
        RaisePropertyChanged(nameof(CbSettingModuleDebug));
        RaisePropertyChanged(nameof(CbSettingModulePrescriptionMaker));
        RaisePropertyChanged(nameof(CbSettingModulePendingDigitals));
        RaisePropertyChanged(nameof(CbSettingModuleKnowledgeBase));
        RaisePropertyChanged(nameof(TotalOrdersInArchivesDatastore));
        RaisePropertyChanged(nameof(OrdersInArchivesDatastoreBetweenDates));
        RaisePropertyChanged(nameof(LastArchivesDatastoreRebuildDate));
    }

    private long _startupUiPingMs = -1;

    internal void LogStartupVmSnapshot(string phase)
    {
        StartupLog.WriteDetail(
            "VM",
            $"{phase}: vmHash={GetHashCode()}, InstanceHash={Instance?.GetHashCode()}, " +
            $"AppIsFullyLoaded={AppIsFullyLoaded}, IsMainWindowReady={IsMainWindowReady}, " +
            $"allowBgTasks={_allowPeriodicBackgroundTasks}, deferredScheduled={_deferredStartupUiScheduled}, " +
            $"deferredRunning={_deferredStartupUiRunning}, ModuleLabnext={CbSettingModuleLabnext}, " +
            $"ModulePM={CbSettingModulePrescriptionMaker}, ArchivesTotal='{TotalOrdersInArchivesDatastore}', " +
            $"onUiThread={Application.Current.Dispatcher.CheckAccess()}");
    }

    private void StartStartupHeartbeat()
    {
        if (_startupHeartbeat is not null)
        {
            return;
        }

        int beat = 0;
        _startupHeartbeat = new Timer(
            _ =>
            {
                beat++;
                long pingStarted = Environment.TickCount64;
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        () => _startupUiPingMs = Environment.TickCount64 - pingStarted,
                        DispatcherPriority.Send);
                }
                catch
                {
                    _startupUiPingMs = -1;
                }

                StartupLog.WriteDetail(
                    "Heartbeat",
                    $"#{beat} uiPingMs={_startupUiPingMs}, AppIsFullyLoaded={AppIsFullyLoaded}, " +
                    $"deferredRunning={_deferredStartupUiRunning}, allowBg={_allowPeriodicBackgroundTasks}, " +
                    $"mainWindow={IsMainWindowReady}");

                if (beat >= 60)
                {
                    _startupHeartbeat?.Dispose();
                    _startupHeartbeat = null;
                }
            },
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
        StartupLog.WriteDetail("Heartbeat", "Thread-pool timer started (2s interval, max 60 beats)");
    }

    /// <summary>
    /// Runs synchronously on the UI thread right after Show() so the home screen is usable
    /// without waiting for the dispatcher queue to drain layout/render work.
    /// </summary>
    private void CompleteImmediatePostStartupUi()
    {
        StartupLog.WriteStep("PostStartup: activating home screen");
        LoadingPanelVisibility = Visibility.Collapsed;

        if (_mainWindow?.mainTabControl is not null && _mainWindow.HomeTab is not null)
        {
            _mainWindow.mainTabControl.SelectedItem = _mainWindow.HomeTab;
        }

        UpdateTabChromeForSelection();
        RefreshStartupUiBindings();
        Application.Current.Dispatcher.BeginInvoke(
            RefreshStartupUiBindings,
            DispatcherPriority.ApplicationIdle);
        LogStartupVmSnapshot("PostStartup immediate");
    }

    internal void ApplyPendingStartupFilterIfNeeded()
    {
        if (string.IsNullOrEmpty(_pendingStartupFilterRestore) || !IsMainWindowReady)
        {
            return;
        }

        string filter = _pendingStartupFilterRestore;
        _pendingStartupFilterRestore = null;
        StartupLog.WriteStep($"Startup filter restore: applying '{filter}' on 3Shape tab open");
        FilterMenuItemClicked(filter);
    }

    internal void ScheduleStartupFilterRestore(string filter)
    {
        _pendingStartupFilterRestore = filter;
    }

    private static bool DirectoryExistsWithTimeout(string? path, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("Click here to", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var probe = Task.Run(() => Directory.Exists(path));
            return probe.Wait(timeoutMs) && probe.Result;
        }
        catch
        {
            return false;
        }
    }

    private static bool FileExistsWithTimeout(string? path, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var probe = Task.Run(() => File.Exists(path));
            return probe.Wait(timeoutMs) && probe.Result;
        }
        catch
        {
            return false;
        }
    }

    internal void EnsureLabnextWebViewInitialized()
    {
        if (!CbSettingModuleLabnext || _MainWindow?.webviewLabnext is null)
        {
            return;
        }

        if (_MainWindow.webviewLabnext.Source is null)
        {
            StartupLog.WriteStep("Initializing Labnext web view (on demand)");
            _MainWindow.webviewLabnext.Source = new Uri(LabnextUrl);
            LabnextLoadingHiderTimer.Start();
        }

        TryEnsureLabnextWebViewEventHandlers();
    }

    internal void TryEnsureLabnextWebViewEventHandlers()
    {
        if (!CbSettingModuleLabnext || EventHandlerAlreadyAdded || !IsMainWindowReady)
        {
            return;
        }

        var coreWebView2 = _mainWindow!.webviewLabnext?.CoreWebView2;
        if (coreWebView2 is null)
        {
            return;
        }

        coreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
        coreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        EventHandlerAlreadyAdded = true;
    }

    internal void EnsureServerLogWebViewInitialized()
    {
        if (!ServerLogCanBeRead || string.IsNullOrEmpty(ServerLogUrl) || _MainWindow?.webview is null)
        {
            return;
        }

        if (_MainWindow.webview.Source?.ToString() == ServerLogUrl)
        {
            return;
        }

        StartupLog.WriteStep("Initializing server log web view (on demand)");
        _MainWindow.webview.Source = new Uri(ServerLogUrl);
    }

    private async Task ProbeServerLogAvailabilityAsync()
    {
        string serverLogPath = @$"\\{StatsServersComputerName}\StatsSystemsLogs$\StatsSystem_log_{DateTime.Now:yyyy-MM-dd}.html";
        bool exists = await Task.Run(() => FileExistsWithTimeout(serverLogPath, 3000));

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (exists)
            {
                ServerLogCanBeRead = true;
                ServerLogUrl = serverLogPath;
            }
            else
            {
                ServerLogCanBeRead = false;
            }
        });
    }

    private async Task SetStartupStatusAsync(string message)
    {
        StartupLog.WriteStep(message);
        await Application.Current.Dispatcher.InvokeAsync(
            () => SplashViewModel.Instance.LoadingText = message,
            DispatcherPriority.Send);
        await Task.Yield();
    }

    private void InitialTasksAtApplicationStartup_DoWork(object? sender, DoWorkEventArgs e)
    {
        try
        {
            InitialTasksAtApplicationStartupCoreAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StartupLog.WriteError("Initial startup tasks failed", ex);
            throw;
        }
    }

    private async Task InitialTasksAtApplicationStartupCoreAsync()
    {
        LogStartupVmSnapshot("InitialTasks begin");
        await SetStartupStatusAsync("Reading server site name..");
        var siteName = await Task.Run(DatabaseOperations.GetServerSiteName);

        await SetStartupStatusAsync("Reading 3Shape file directory..");
        var fileDirectory = await Task.Run(DatabaseOperations.GetServerFileDirectory);

        await SetStartupStatusAsync("Initializing payment cut-off date..");
        await Task.Run(InitializePaymentListCutOffDate);

        await SetStartupStatusAsync("Loading local preferences..");
        _ = bool.TryParse(ReadLocalSetting("GlassyEffect"), out bool glassyEffect);
        _ = bool.TryParse(ReadLocalSetting("ShowAvailablePanCount"), out bool showAvailablePanCount);
        _ = bool.TryParse(ReadLocalSetting("StartAppMinimized"), out bool startAppMinimized);
        _ = bool.TryParse(ReadLocalSetting("ShowDigiCases"), out bool showDigiCases);
        _ = bool.TryParse(ReadLocalSetting("ActivePrescriptionMaker"), out bool activePrescriptionMaker);
        _ = bool.TryParse(ReadLocalSetting("OpenUpSironaScanFolder"), out bool openUpSironaScanFolder);
        _ = bool.TryParse(ReadLocalSetting("ShowEmptyPanCount"), out bool showEmptyPanCount);
        _ = bool.TryParse(ReadLocalSetting("ExtractIteroZipFiles"), out bool extractIteroZipFiles);
        _ = bool.TryParse(ReadLocalSetting("PmOpenUpPrescriptions"), out bool pmOpenUpPrescriptions);
        _ = bool.TryParse(ReadLocalSetting("ShowPendingDigiCases"), out bool showPendingDigiCases);
        _ = bool.TryParse(ReadLocalSetting("KeepUserLoggedInLabnext"), out bool keepUserLoggedInLabnext);
        _ = bool.TryParse(ReadLocalSetting("ShowDigiPrescriptionsCount"), out bool showDigiPrescriptionsCount);
        _ = bool.TryParse(ReadLocalSetting("AnnounceNewlyDesignedOrdersOnScreen"), out bool announceNewlyDesignedOrdersOnScreen);
        _ = bool.TryParse(ReadLocalSetting("ShowDigiCasesIn3ShapeTodayCount"), out bool showDigiCasesIn3ShapeTodayCount);
        _ = bool.TryParse(ReadLocalSetting("ShowOtherUsersPanNumbers"), out bool showOtherUsersPanNumbers);

        await SetStartupStatusAsync("Loading module flags..");
        _ = bool.TryParse(ReadLocalSetting("ModuleFolderSubscription"), out bool moduleFolderSubscription);
        _ = bool.TryParse(ReadLocalSetting("ModuleAccountInfos"), out bool moduleAccountInfos);
        _ = bool.TryParse(ReadLocalSetting("ModuleLabnext"), out bool moduleLabnext);
        _ = bool.TryParse(ReadLocalSetting("ModuleSmartOrderNames"), out bool moduleSmartOrderNames);
        _ = bool.TryParse(ReadLocalSetting("ModuleDebug"), out bool moduleDebug);
        _ = bool.TryParse(ReadLocalSetting("ModulePrescriptionMaker"), out bool modulePrescriptionMaker);
        _ = bool.TryParse(ReadLocalSetting("ModulePendingDigitals"), out bool modulePendingDigitals);
        _ = bool.TryParse(ReadLocalSetting("ModuleEncodeIdentifier"), out bool moduleEncodeIdentifier);
        if (!bool.TryParse(ReadLocalSetting("ModuleKnowledgeBase"), out bool moduleKnowledgeBase))
        {
            moduleKnowledgeBase = true;
        }

        _ = bool.TryParse(ReadStatsSetting("dcas_EmailWatcherActive"), out bool isDcasActive);
        if (!bool.TryParse(ReadLocalSetting("IncludePendingDigiCases"), out bool includePendingDigiCases))
        {
            includePendingDigiCases = true;
        }

        ThisSite = siteName;
        ThreeShapeDirectoryHelper = fileDirectory;

        CbSettingGlassyEffect = glassyEffect;
        CbSettingStartAppMinimized = startAppMinimized;
        CbSettingShowDigiCases = showDigiCases;
        CbSettingWatchFolderPrescriptionMaker = activePrescriptionMaker;
        CbSettingOpenUpSironaScanFolder = openUpSironaScanFolder;
        CbSettingShowEmptyPanCount = showEmptyPanCount;
        CbSettingExtractIteroZipFiles = extractIteroZipFiles;
        PmOpenUpPrescriptionsBool = pmOpenUpPrescriptions;
        CbSettingShowPendingDigiCases = showPendingDigiCases;
        CbSettingKeepUserLoggedInLabnext = keepUserLoggedInLabnext;
        CbSettingIncludePendingDigiCasesInNewlyArrived = includePendingDigiCases;
        CbSettingShowDigiPrescriptionsCount = showDigiPrescriptionsCount;
        CbSettingShowDigiCasesIn3ShapeTodayCount = showDigiCasesIn3ShapeTodayCount;
        CbSettingShowOtherUsersPanNumbers = showOtherUsersPanNumbers;

        CbSettingModuleFolderSubscription = moduleFolderSubscription;
        CbSettingModuleAccountInfos = moduleAccountInfos;
        CbSettingModuleLabnext = moduleLabnext;
        CbSettingModuleSmartOrderNames = moduleSmartOrderNames;
        CbSettingModuleDebug = moduleDebug;
        CbSettingModulePrescriptionMaker = modulePrescriptionMaker;
        CbSettingModulePendingDigitals = modulePendingDigitals;
        CbSettingModuleEncodeIdentifier = moduleEncodeIdentifier;
        CbSettingModuleKnowledgeBase = moduleKnowledgeBase;
        IsDCASIsActive = isDcasActive;

        if (moduleEncodeIdentifier)
        {
            await SetStartupStatusAsync("Loading encode identifier settings..");
            await Task.Run(LoadEncodeIdentifierSettings);
            StartupLog.WriteStep("Encode identifier settings loaded");
        }

        await SetStartupStatusAsync("Loading DCM viewer settings..");
        await Task.Run(LoadDcmViewerFuseSettings);
        StartupLog.WriteStep("DCM viewer settings loaded");

        await SetStartupStatusAsync("Reading Labnext configuration..");
        var triosInboxFolder = fileDirectory + @"3ShapeCommunicate\Inbox";
        var labnextLabId = ReadStatsSetting("LabnextLabID");
        var labnextUrl = $"https://{labnextLabId}.labnext.net/lab/";
        var searchLimit = ReadLocalSetting("SearchLimit");
        var timeOut = ReadLocalSetting("TimeoutForImportAncmnt");
        var pendingDigiCasesReplacementName = ReadLocalSetting("PendingDigiCasesReplacementName");
        if (string.IsNullOrEmpty(pendingDigiCasesReplacementName))
        {
            pendingDigiCasesReplacementName = "PendingDigi";
        }

        var fsubscrTargetFolder = ReadLocalSetting("SubscriptionCopyFolder");
        var pmWatchedPdfFolder = ReadLocalSetting("PmWatchedPdfFolder");
        if (string.IsNullOrEmpty(pmWatchedPdfFolder))
        {
            pmWatchedPdfFolder = "Click here to setup..";
        }

        var pmFinalPrescriptionsFolder = ReadLocalSetting("FinalPrescriptionsFolder");
        if (string.IsNullOrEmpty(pmFinalPrescriptionsFolder))
        {
            pmFinalPrescriptionsFolder = "Click here to setup..";
        }

        var pmSironaScansFolder = ReadLocalSetting("SironaScansFolder");
        if (string.IsNullOrEmpty(pmSironaScansFolder))
        {
            pmSironaScansFolder = "Click here to setup..";
        }

        var pmIteroExportFolder = ReadLocalSetting("IteroExportFolder");
        if (string.IsNullOrEmpty(pmIteroExportFolder))
        {
            pmIteroExportFolder = "Click here to setup..";
        }

        var pmDownloadFolder = ReadLocalSetting("PmDownloadFolder");
        if (string.IsNullOrEmpty(pmDownloadFolder))
        {
            pmDownloadFolder = "Click here to setup..";
        }

        if (pmDownloadFolder.Contains("Click here to", StringComparison.Ordinal))
        {
            pmDownloadFolder = Environment.GetEnvironmentVariable("USERPROFILE") + @"\" + @"Downloads\";
        }

        TriosInboxFolder = triosInboxFolder;
        LabnextLabID = labnextLabId;
        LabnextUrl = labnextUrl;
        LabNextWebViewStatusText = "/";

        if (!string.IsNullOrEmpty(searchLimit))
        {
            SearchLimit = searchLimit;
        }

        if (!string.IsNullOrEmpty(timeOut))
        {
            TimeOut = timeOut;
        }

        PendingDigiCasesReplacementName = pendingDigiCasesReplacementName;
        FsubscrTargetFolder = fsubscrTargetFolder;
        PmWatchedPdfFolder = pmWatchedPdfFolder;
        PmFinalPrescriptionsFolder = pmFinalPrescriptionsFolder;
        PmSironaScansFolder = pmSironaScansFolder;
        PmIteroExportFolder = pmIteroExportFolder;
        PmDownloadFolder = pmDownloadFolder;

        _deferredStartupLoadAccountInfos = moduleAccountInfos;

#if !DEBUG
        await SetStartupStatusAsync("Checking TRIOS inbox folder..");
        if (await Task.Run(() => DirectoryExistsWithTimeout(TriosInboxFolder)))
        {
            StartupLog.WriteStep("TRIOS inbox folder found — watcher will start after main window opens");
        }
#endif

        await SetStartupStatusAsync("Building pan number panel..");
        SoftwareVersion = await GetAppVersion();
        _ = double.TryParse(SoftwareVersion, out double appVersionDouble);
        AppVersionDouble = appVersionDouble;
        ResetDigiSystemColors();
        CommentRulesList = await GetCommentRulesList();

        await SetStartupStatusAsync("Preparing PDF temp folder..");
        var pdfTemp = DataBaseFolder + @"PDFTemp";
        await Task.Run(() =>
        {
            if (Directory.Exists(pdfTemp))
            {
                Directory.Delete(pdfTemp, true);
            }

            Directory.CreateDirectory(pdfTemp);
        });
        PDFTemp = pdfTemp;

        await SetStartupStatusAsync("Loading send-to list..");
        PmSendToList = await Task.Run(DatabaseOperations.GetAllSendToEnties);

        await SetStartupStatusAsync("Reporting client login..");
        await ReportClientLoginToDatabase(true);

        await SetStartupStatusAsync("Cleaning old ignored orders..");
        await Task.Run(DeleteOldOrderToIgnoredListLocalDB);

        await SetStartupStatusAsync("Cleaning old PM events..");
        await Task.Run(DeleteOldPMEventsFromLocalDB);

        await SetStartupStatusAsync("Loading search history..");
        var searchHistory = await GetBackAllSearchHistoryFromLocalDB();

        await SetStartupStatusAsync("Cleaning old search history..");
        await Task.Run(DeleteOldSearchHistoryFromLocalDB);

        await SetStartupStatusAsync("Loading prescription maker events..");
        var pmEvents = await GetBackAllEventFromLocalDB();

        await SetStartupStatusAsync("Loading inconsistency list..");
        await FillUpIngnoredOrdersInInconsistencyList();

        await SetStartupStatusAsync("Counting archive orders..");
        var totalArchives = await Task.Run(DatabaseOperations.GetTotalOrdersForArchives);

        await SetStartupStatusAsync("Loading archive date range..");
        var ordersBetweenDates = await Task.Run(DatabaseOperations.GetOrdersBetweenDatesForArchives);

        await SetStartupStatusAsync("Loading last archive rebuild date..");
        var lastRebuildDate = await Task.Run(DatabaseOperations.GetLastRebuiltDateForArchives);

        await SetStartupStatusAsync("Loading payment issue count..");
        var paymentIssueCount = await GetPaymentIssueCountFromDB();

        await SetStartupStatusAsync("Loading designer payment summaries..");
        var designerPaymentSummaryList = await GetDesignerPaymentSummaryFromDB();

        await SetStartupStatusAsync("Loading wrong-person payment orders..");
        var paidToWrongPersonOrdersList = await GetPaidToWrongPersonsOrdersListFromDB();

        SearchHistory = searchHistory;
        PrescriptionMakerEventsList = pmEvents;
        PaymentIssueCount = paymentIssueCount;
        DesignerPaymentSummaryList = designerPaymentSummaryList;
        PaidToWrongPersonOrdersList = paidToWrongPersonOrdersList;

        await SetStartupStatusAsync("Opening main window..");
        LogStartupVmSnapshot("Before main window UI dispatch");
        await Application.Current.Dispatcher.InvokeAsync(
            () =>
        {
            StartupLog.WritePhase("MainWindow", "UI dispatch entered");
            LogStartupVmSnapshot("Inside main window UI dispatch");

            TotalOrdersInArchivesDatastore = totalArchives.ToString("N0");
            OrdersInArchivesDatastoreBetweenDates = ordersBetweenDates;
            LastArchivesDatastoreRebuildDate = lastRebuildDate;

            if (SplashViewModel.Instance.mainWindow is null)
            {
                StartupLog.WriteStep("Creating main window");
                var ctorSw = System.Diagnostics.Stopwatch.StartNew();
                SplashViewModel.Instance.mainWindow = new MainWindow();
                ctorSw.Stop();
                StartupLog.WriteDetail("MainWindow", $"Constructor completed in {ctorSw.ElapsedMilliseconds}ms");
            }

            StartupLog.WritePhase("MainWindow", "FillUpEmptyPanNumberPanel begin");
            var panSw = System.Diagnostics.Stopwatch.StartNew();
            FillUpEmptyPanNumberPanel();
            panSw.Stop();
            StartupLog.WritePhase("MainWindow", $"FillUpEmptyPanNumberPanel done ({panSw.ElapsedMilliseconds}ms)");

            UpdateTabChromeForSelection();
            LoadingPanelVisibility = Visibility.Collapsed;

            if (startAppMinimized && MainWindow.Instance is not null)
            {
                MainWindow.Instance.WindowState = WindowState.Minimized;
            }

            StartupLog.WritePhase("MainWindow", "RevealMainWindowAfterStartup begin");
            RevealMainWindowAfterStartup();
            StartupLog.WritePhase("MainWindow", "RevealMainWindowAfterStartup returned");
            LogStartupVmSnapshot("After RevealMainWindowAfterStartup");
        },
            DispatcherPriority.Send).Task;
        LogStartupVmSnapshot("InitialTasks complete");
    }

    private void RevealMainWindowAfterStartup()
    {
        SyncMainWindowReferenceFromSplash();

        if (AppIsFullyLoaded)
        {
            StartupLog.WriteDetail("Reveal", "Skipped — AppIsFullyLoaded already true");
            return;
        }

        SplashViewModel.Instance.LoadingText = "Loading finished!";
        StartupLog.WriteStep("Loading finished — showing main window");
        AppIsFullyLoaded = true;

        var mainWindow = SplashViewModel.Instance.mainWindow ?? _mainWindow;
        if (mainWindow is null)
        {
            StartupLog.WriteError("Main window was null at startup reveal");
            return;
        }

        _mainWindow ??= mainWindow;
        StartupLog.WriteDetail("Reveal", $"Calling mainWindow.Show() — size={mainWindow.Width}x{mainWindow.Height}");
        var showSw = System.Diagnostics.Stopwatch.StartNew();
        mainWindow.Show();
        showSw.Stop();
        StartupLog.WriteDetail("Reveal", $"mainWindow.Show() returned in {showSw.ElapsedMilliseconds}ms");
        SplashWindow.Instance.Hide();
        CompleteImmediatePostStartupUi();
        StartupLog.WriteStep("Startup complete");
        LogStartupVmSnapshot("Startup complete");
        StartStartupHeartbeat();

        if (!_deferredStartupUiScheduled)
        {
            _deferredStartupUiScheduled = true;
            StartupLog.WriteDetail("Deferred", "Starting heavy post-startup work on thread pool");
            _ = Task.Run(RunDeferredStartupHeavyWorkAsync);
        }
        else
        {
            StartupLog.WriteDetail("Deferred", "Skipped scheduling — already scheduled");
        }
    }

    private async Task RunDeferredStartupHeavyWorkAsync()
    {
        if (_deferredStartupUiRunning)
        {
            StartupLog.WriteDetail("Deferred", "Already running — skipping duplicate entry");
            return;
        }

        _deferredStartupUiRunning = true;
        int threadId = Environment.CurrentManagedThreadId;
        StartupLog.WriteStep($"Deferred: starting (thread {threadId})");
        LogStartupVmSnapshot("Deferred begin");
        try
        {
            if (!string.IsNullOrEmpty(_pendingStartupFilterRestore))
            {
                StartupLog.WriteDetail(
                    "Deferred",
                    $"Startup filter '{_pendingStartupFilterRestore}' deferred — home screen first, applies on 3Shape tab open");
            }

            StartupLog.WriteStep("Deferred: checking prescription maker folder");
            var prescriptionFolderReady = await Task.Run(() =>
                CbSettingWatchFolderPrescriptionMaker && DirectoryExistsWithTimeout(PmWatchedPdfFolder));

            StartupLog.WriteStep("Deferred: checking iTero folders");
            var iteroFoldersReady = await Task.Run(() =>
                DirectoryExistsWithTimeout(PmDownloadFolder) && DirectoryExistsWithTimeout(PmIteroExportFolder));

            if (prescriptionFolderReady)
            {
                StartupLog.WriteStep("Deferred: enabling prescription maker watcher");
                var pmSw = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(ConfigurePrescriptionMakerFileWatcher);
                pmSw.Stop();
                StartupLog.WriteDetail("Deferred", $"Prescription maker watcher ready ({pmSw.ElapsedMilliseconds}ms)");
            }

            if (iteroFoldersReady)
            {
                StartupLog.WriteStep("Deferred: enabling iTero zip watcher");
                var iteroSw = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(ConfigureIteroZipFileWatcher);
                iteroSw.Stop();
                StartupLog.WriteDetail("Deferred", $"iTero watcher ready ({iteroSw.ElapsedMilliseconds}ms)");
            }

            if (_deferredStartupLoadAccountInfos)
            {
                StartupLog.WriteStep("Deferred: loading account infos");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await GetAccountInfosAsync();
                sw.Stop();
                StartupLog.WriteDetail("Deferred", $"Account infos loaded ({sw.ElapsedMilliseconds}ms)");
            }

            StartupLog.WriteStep("Deferred: probing server log availability");
            await ProbeServerLogAvailabilityAsync();

#if !DEBUG
            if (DirectoryExistsWithTimeout(TriosInboxFolder))
            {
                StartupLog.WriteStep("Deferred: configuring TRIOS inbox watcher");
                var triosSw = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(() =>
                {
                    fswTriosFolderWatcher.EnableRaisingEvents = false;
                    fswTriosFolderWatcher.Created -= FswTriosFolderWatcher_Created;
                    fswTriosFolderWatcher.Deleted -= FswTriosFolderWatcher_Deleted;
                    fswTriosFolderWatcher.Path = TriosInboxFolder;
                    fswTriosFolderWatcher.Filter = "*.*";
                    fswTriosFolderWatcher.NotifyFilter = NotifyFilters.DirectoryName;
                    fswTriosFolderWatcher.Created += FswTriosFolderWatcher_Created;
                    fswTriosFolderWatcher.Deleted += FswTriosFolderWatcher_Deleted;
                    fswTriosFolderWatcher.EnableRaisingEvents = true;
                });
                int triosCount = await Task.Run(() => Directory.GetDirectories(TriosInboxFolder).Length);
                await Application.Current.Dispatcher.InvokeAsync(
                    () => NewTriosCaseInInboxCount = triosCount,
                    DispatcherPriority.Background);
                triosSw.Stop();
                StartupLog.WriteDetail("Deferred", $"TRIOS watcher ready ({triosSw.ElapsedMilliseconds}ms, count={triosCount})");
            }
#endif

            StartupLog.WriteStep("Deferred startup work complete");
            LogStartupVmSnapshot("Deferred complete");
        }
        catch (Exception ex)
        {
            StartupLog.WriteError("Deferred startup work failed", ex);
        }
        finally
        {
            _deferredStartupUiRunning = false;
            _allowPeriodicBackgroundTasks = true;
            StartupLog.WriteStep("Deferred: finished — periodic background tasks enabled");
            StartupLog.Flush();
        }
    }

    public void InitialTasksAtApplicationStartup_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        if (e.Error is not null)
        {
            StartupLog.WriteError("Initial startup background worker failed", e.Error);
            AddDebugLine(e.Error);
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SyncMainWindowReferenceFromSplash();

            if (!AppIsFullyLoaded)
            {
                if (SplashViewModel.Instance.mainWindow is null)
                {
                    StartupLog.WriteStep("Revealing main window from RunWorkerCompleted fallback");
                    SplashViewModel.Instance.mainWindow = new MainWindow();
                }

                RevealMainWindowAfterStartup();
            }

#if DEBUG
            AddDebugLine(null, "App started");
#endif
            if (_allowPeriodicBackgroundTasks)
            {
                GeneralTimer_Tick(sender, e);
            }
            else
            {
                StartupLog.WriteDetail("Timer", "GeneralTimer skipped — post-startup not ready");
            }
        });
    }

    internal static void StartInitialTasks()
    {
        if (!bwInitialTasks.IsBusy)
        {
            StartupLog.WriteStep("Starting initial background tasks");
            bwInitialTasks.RunWorkerAsync();
        }
    }
}
