using DdxDentalClient;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using Syncfusion.PdfToImageConverter;
using Syncfusion.Windows.PdfViewer;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Media;
using System.Net.Http;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using TesseractOCR;
using TesseractOCR.Enums;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.Core.MessageBoxes;

using Bitmap = System.Drawing.Bitmap;
using Clipboard = System.Windows.Clipboard;





namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel : ObservableObject
{
    public static readonly string LocalConfigFolderHelper = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Stats_Client\";

    #region Properties
    private static MainViewModel? instance;
    public static MainViewModel Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }

    private MainWindow? _mainWindow;
    public MainWindow _MainWindow
    {
        get => _mainWindow!;
        set
        {
            _mainWindow = value;
            RaisePropertyChanged(nameof(_MainWindow));
        }
    }

    private SentOutCasesViewModel? sentOutCasesViewModel;
    public SentOutCasesViewModel SentOutCasesViewModel
    {
        get => sentOutCasesViewModel!;
        set
        {
            sentOutCasesViewModel = value;
            RaisePropertyChanged(nameof(SentOutCasesViewModel));
        }
    }



    //private SmartOrderNamesViewModel? smartOrderNamesViewModel;
    //public SmartOrderNamesViewModel SmartOrderNamesViewModel
    //{
    //    get => smartOrderNamesViewModel!;
    //    set
    //    {
    //        smartOrderNamesViewModel = value;
    //        RaisePropertyChanged(nameof(SmartOrderNamesViewModel));
    //    }
    //}

    private List<ImportHistoryModel> importedCasesList = [];
    public List<ImportHistoryModel> ImportedCasesList
    {
        get => importedCasesList;
        set
        {
            importedCasesList = value;
            RaisePropertyChanged(nameof(ImportedCasesList));
        }
    }

    private List<ImportHistoryModel> testImportHistoryList = [];
    public List<ImportHistoryModel> TestImportHistoryList
    {
        get => testImportHistoryList;
        set
        {
            testImportHistoryList = value;
            RaisePropertyChanged(nameof(TestImportHistoryList));
        }
    }

    private List<ExportHistoryModel> exportedCasesList = [];
    public List<ExportHistoryModel> ExportedCasesList
    {
        get => exportedCasesList;
        set
        {
            exportedCasesList = value;
            RaisePropertyChanged(nameof(ExportedCasesList));
        }
    }

    private Visibility homeButtonShows = Visibility.Collapsed;
    public Visibility HomeButtonShows
    {
        get => homeButtonShows;
        set
        {
            homeButtonShows = value;
            RaisePropertyChanged(nameof(HomeButtonShows));
        }
    }

    private Visibility refreshButtonShows = Visibility.Collapsed;
    public Visibility RefreshButtonShows
    {
        get => refreshButtonShows;
        set
        {
            refreshButtonShows = value;
            RaisePropertyChanged(nameof(RefreshButtonShows));
        }
    }

    private bool startAutoUpdateCuzAppJustStarted = true;
    public bool StartAutoUpdateCuzAppJustStarted
    {
        get => startAutoUpdateCuzAppJustStarted;
        set
        {
            startAutoUpdateCuzAppJustStarted = value;
            RaisePropertyChanged(nameof(StartAutoUpdateCuzAppJustStarted));
        }
    }

    private bool doAForceUpdateNow = false;
    public bool DoAForceUpdateNow
    {
        get => doAForceUpdateNow;
        set
        {
            doAForceUpdateNow = value;
            RaisePropertyChanged(nameof(DoAForceUpdateNow));
        }
    }

    private bool serverLogCanBeRead = false;
    public bool ServerLogCanBeRead
    {
        get => serverLogCanBeRead;
        set
        {
            serverLogCanBeRead = value;
            RaisePropertyChanged(nameof(ServerLogCanBeRead));
        }
    }

    private bool eventHandlerAlreadyAdded = false;
    public bool EventHandlerAlreadyAdded
    {
        get => eventHandlerAlreadyAdded;
        set
        {
            eventHandlerAlreadyAdded = value;
            RaisePropertyChanged(nameof(EventHandlerAlreadyAdded));
        }
    }

    private bool scrollServerLogToBottom = true;
    public bool ScrollServerLogToBottom
    {
        get => scrollServerLogToBottom;
        set
        {
            scrollServerLogToBottom = value;
            RaisePropertyChanged(nameof(ScrollServerLogToBottom));
        }
    }

    private bool serverLogWebViewIsInitialized = false;
    public bool ServerLogWebViewIsInitialized
    {
        get => serverLogWebViewIsInitialized;
        set
        {
            serverLogWebViewIsInitialized = value;
            RaisePropertyChanged(nameof(ServerLogWebViewIsInitialized));
        }
    }

    private string serverLogUrl = "";
    public string ServerLogUrl
    {
        get => serverLogUrl;
        set
        {
            serverLogUrl = value;
            RaisePropertyChanged(nameof(ServerLogUrl));
        }
    }

    private string statsServersComputerName = "";
    public string StatsServersComputerName
    {
        get => statsServersComputerName;
        set
        {
            statsServersComputerName = value;
            RaisePropertyChanged(nameof(StatsServersComputerName));
        }
    }

    private bool lookingForUpdateNow = false;
    public bool LookingForUpdateNow
    {
        get => lookingForUpdateNow;
        set
        {
            lookingForUpdateNow = value;
            RaisePropertyChanged(nameof(LookingForUpdateNow));
        }
    }



    private double appVersionDouble = 0;
    public double AppVersionDouble
    {
        get => appVersionDouble;
        set
        {
            appVersionDouble = value;
            RaisePropertyChanged(nameof(AppVersionDouble));
        }
    }

    private string updateAvailableText = "Update available!";
    public string UpdateAvailableText
    {
        get => updateAvailableText;
        set
        {
            updateAvailableText = value;
            RaisePropertyChanged(nameof(UpdateAvailableText));
        }
    }

    private string softwareVersion = "0";
    public string SoftwareVersion
    {
        get => softwareVersion;
        set
        {
            softwareVersion = value;
            RaisePropertyChanged(nameof(SoftwareVersion));
        }
    }

    private string latestAppVersion = "0";
    public string LatestAppVersion
    {
        get => latestAppVersion;
        set
        {
            latestAppVersion = value;
            RaisePropertyChanged(nameof(LatestAppVersion));
        }
    }

    private bool threeShapeServerIsDown = false;
    public bool ThreeShapeServerIsDown
    {
        get => threeShapeServerIsDown;
        set
        {
            threeShapeServerIsDown = value;
            RaisePropertyChanged(nameof(ThreeShapeServerIsDown));
        }
    }

    private bool infoTabActive = false;
    public bool InfoTabActive
    {
        get => infoTabActive;
        set
        {
            infoTabActive = value;
            RaisePropertyChanged(nameof(InfoTabActive));
        }
    }

    private bool serverIsWritingDatabase = false;
    public bool ServerIsWritingDatabase
    {
        get => serverIsWritingDatabase;
        set
        {
            serverIsWritingDatabase = value;
            RaisePropertyChanged(nameof(ServerIsWritingDatabase));
        }
    }

    private ObservableCollection<HealthReportModel> healthReports = [];
    public ObservableCollection<HealthReportModel> HealthReports
    {
        get => healthReports;
        set
        {
            healthReports = value;
            RaisePropertyChanged(nameof(HealthReports));
        }
    }

    private bool updateAvailable = false;
    public bool UpdateAvailable
    {
        get => updateAvailable;
        set
        {
            updateAvailable = value;
            RaisePropertyChanged(nameof(UpdateAvailable));
        }
    }

    private bool appIsFullyLoaded = false;
    public bool AppIsFullyLoaded
    {
        get => appIsFullyLoaded;
        set
        {
            appIsFullyLoaded = value;
            RaisePropertyChanged(nameof(AppIsFullyLoaded));
        }
    }

    private bool firstRun = true;
    public bool FirstRun
    {
        get => firstRun;
        set
        {
            firstRun = value;
            RaisePropertyChanged(nameof(FirstRun));
        }
    }

    private bool listUpdateable = false;
    public bool ListUpdateable
    {
        get => listUpdateable;
        set
        {
            listUpdateable = value;
            RaisePropertyChanged(nameof(ListUpdateable));
        }
    }



    private bool designerOpen = false;
    public bool DesignerOpen
    {
        get => designerOpen;
        set
        {
            designerOpen = value;
            RaisePropertyChanged(nameof(DesignerOpen));
        }
    }

    private string designerOpenToolTip = "";
    public string DesignerOpenToolTip
    {
        get => designerOpenToolTip;
        set
        {
            designerOpenToolTip = value;
            RaisePropertyChanged(nameof(DesignerOpenToolTip));
        }
    }

    private string triosInboxFolder = "";
    public string TriosInboxFolder
    {
        get => triosInboxFolder;
        set
        {
            triosInboxFolder = value;
            RaisePropertyChanged(nameof(TriosInboxFolder));
        }
    }


    //private bool showBottomInfoBar = false;
    //public bool ShowBottomInfoBar
    //{
    //    get => showBottomInfoBar;
    //    set
    //    {
    //        showBottomInfoBar = value;
    //        RaisePropertyChanged(nameof(ShowBottomInfoBar));
    //    }
    //}

    //private string serverStatus = "Idle";
    //public string ServerStatus
    //{
    //    get => serverStatus;
    //    set
    //    {
    //        serverStatus = value;
    //        RaisePropertyChanged(nameof(ServerStatus));
    //    }
    //}

    //private int bottomBarSize = 35;
    //public int BottomBarSize
    //{
    //    get => bottomBarSize;
    //    set
    //    {
    //        bottomBarSize = value;
    //        RaisePropertyChanged(nameof(BottomBarSize));
    //    }
    //}


    private int digiPrescriptionsTodayCount = 0;
    public int DigiPrescriptionsTodayCount
    {
        get => digiPrescriptionsTodayCount;
        set
        {
            digiPrescriptionsTodayCount = value;
            RaisePropertyChanged(nameof(DigiPrescriptionsTodayCount));
        }
    }

    private int digiCasesIn3ShapeTodayCount = 0;
    public int DigiCasesIn3ShapeTodayCount
    {
        get => digiCasesIn3ShapeTodayCount;
        set
        {
            digiCasesIn3ShapeTodayCount = value;
            RaisePropertyChanged(nameof(DigiCasesIn3ShapeTodayCount));
        }
    }

    private int sentOutIssuesCount = 0;
    public int SentOutIssuesCount
    {
        get => sentOutIssuesCount;
        set
        {
            if (value != SentOutIssuesCount)
            {
                sentOutIssuesCount = value;
                RaisePropertyChanged(nameof(SentOutIssuesCount));
                _ = GetAllSentOutIssues();
            }
        }
    }

    private int panNrDuplicatesCount = 0;
    public int PanNrDuplicatesCount
    {
        get => panNrDuplicatesCount;
        set
        {
            if (value != PanNrDuplicatesCount)
            {
                panNrDuplicatesCount = value;
                RaisePropertyChanged(nameof(PanNrDuplicatesCount));
            }
        }
    }

    private List<IssuesWithCasesModel> issuesWithCasesList = [];
    public List<IssuesWithCasesModel> IssuesWithCasesList
    {
        get => issuesWithCasesList;
        set
        {
            issuesWithCasesList = value;
            RaisePropertyChanged(nameof(IssuesWithCasesList));
        }
    }

    private ObservableCollection<DebugMessagesModel> debugMessages = [];
    public ObservableCollection<DebugMessagesModel> DebugMessages
    {
        get => debugMessages;
        set
        {
            debugMessages = value;
            RaisePropertyChanged(nameof(DebugMessages));
        }
    }


    private string activeSearchString = "";
    public string ActiveSearchString
    {
        get => activeSearchString;
        set
        {
            activeSearchString = value;
            RaisePropertyChanged(nameof(ActiveSearchString));
        }
    }

    private string activeFilterInUse = "";
    public string ActiveFilterInUse
    {
        get => activeFilterInUse;
        set
        {
            activeFilterInUse = value;
            RaisePropertyChanged(nameof(ActiveFilterInUse));
        }
    }

    private string filterString = "";
    public string FilterString
    {
        get => filterString;
        set
        {
            filterString = value;
            RaisePropertyChanged(nameof(FilterString));
        }
    }

    private string threeShapeDirectoryHelper = "";
    public string ThreeShapeDirectoryHelper
    {
        get => threeShapeDirectoryHelper;
        set
        {
            threeShapeDirectoryHelper = value;
            RaisePropertyChanged(nameof(ThreeShapeDirectoryHelper));
        }
    }

    private string serverFriendlyNameHelper = "";
    public string ServerFriendlyNameHelper
    {
        get => serverFriendlyNameHelper;
        set
        {
            serverFriendlyNameHelper = value;
            RaisePropertyChanged(nameof(ServerFriendlyNameHelper));
        }
    }

    private bool easeUpSearch = false;
    public bool EaseUpSearch
    {
        get => easeUpSearch;
        set
        {
            easeUpSearch = value;
            RaisePropertyChanged(nameof(EaseUpSearch));
        }
    }

    private bool isContextMenuOpen = false;
    public bool IsContextMenuOpen
    {
        get => isContextMenuOpen;
        set
        {
            isContextMenuOpen = value;
            RaisePropertyChanged(nameof(IsContextMenuOpen));
        }
    }

    private string searchLimit = "100";
    public string SearchLimit
    {
        get => searchLimit;
        set
        {
            searchLimit = value;
            RaisePropertyChanged(nameof(SearchLimit));
        }
    }

    private List<string> searchLimits = ["100", "150", "200", "300", "400", "500", "800", "1000", "1500", "2000", "3000"];
    public List<string> SearchLimits
    {
        get => searchLimits;
        set
        {
            searchLimits = value;
            RaisePropertyChanged(nameof(SearchLimits));
        }
    }

    private string timeOut = "20";
    public string TimeOut
    {
        get => timeOut;
        set
        {
            timeOut = value;
            RaisePropertyChanged(nameof(TimeOut));
        }
    }

    private List<string> timeOuts = ["10", "20", "30", "40", "50", "60"];
    public List<string> TimeOuts
    {
        get => timeOuts;
        set
        {
            timeOuts = value;
            RaisePropertyChanged(nameof(TimeOuts));
        }
    }

    private string thisSite = "";
    public string ThisSite
    {
        get => thisSite;
        set
        {
            thisSite = value;
            RaisePropertyChanged(nameof(ThisSite));
        }
    }

    private int todayCasesCount = 0;
    public int TodayCasesCount
    {
        get => todayCasesCount;
        set
        {
            todayCasesCount = value;
            RaisePropertyChanged(nameof(TodayCasesCount));
        }
    }

    //private int countedResultsInt = 1;
    //public int CountedResultsInt
    //{
    //    get => countedResultsInt;
    //    set
    //    {
    //        countedResultsInt = value;
    //        RaisePropertyChanged(nameof(CountedResultsInt));
    //    }
    //}

    private bool tempSearchLimitIgnore = false;
    public bool TempSearchLimitIgnore
    {
        get => tempSearchLimitIgnore;
        set
        {
            tempSearchLimitIgnore = value;
            RaisePropertyChanged(nameof(TempSearchLimitIgnore));
        }
    }

    private bool myRecent30 = false;
    public bool MyRecent30
    {
        get => myRecent30;
        set
        {
            myRecent30 = value;
            RaisePropertyChanged(nameof(MyRecent30));
        }
    }



    private Visibility loadingPanelVisibility = Visibility.Hidden;
    public Visibility LoadingPanelVisibility
    {
        get => loadingPanelVisibility;
        set
        {
            loadingPanelVisibility = value;
            RaisePropertyChanged(nameof(LoadingPanelVisibility));
        }
    }

    #region NOTIFICATION MESSAGE PROPERTIES

    private Thickness notificationMessagePosition = new(15, 0, 0, 20);
    public Thickness NotificationMessagePosition
    {
        get => notificationMessagePosition;
        set
        {
            notificationMessagePosition = value;
            RaisePropertyChanged(nameof(NotificationMessagePosition));
        }
    }

    private Visibility notificationMessageVisibility = Visibility.Collapsed;
    public Visibility NotificationMessageVisibility
    {
        get => notificationMessageVisibility;
        set
        {
            notificationMessageVisibility = value;
            RaisePropertyChanged(nameof(NotificationMessageVisibility));
        }
    }

    private string notificationMessageTitle = "";
    public string NotificationMessageTitle
    {
        get => notificationMessageTitle;
        set
        {
            notificationMessageTitle = value;
            RaisePropertyChanged(nameof(NotificationMessageTitle));
        }
    }

    private string notificationMessageBody = "";
    public string NotificationMessageBody
    {
        get => notificationMessageBody;
        set
        {
            notificationMessageBody = value;
            RaisePropertyChanged(nameof(NotificationMessageBody));
        }
    }

    private string notificationMessageGridPosition = "1";
    public string NotificationMessageGridPosition
    {
        get => notificationMessageGridPosition;
        set
        {
            notificationMessageGridPosition = value;
            RaisePropertyChanged(nameof(NotificationMessageGridPosition));
        }
    }

    private VerticalAlignment notificationMessageVertAlignment = VerticalAlignment.Bottom;
    public VerticalAlignment NotificationMessageVertAlignment
    {
        get => notificationMessageVertAlignment;
        set
        {
            notificationMessageVertAlignment = value;
            RaisePropertyChanged(nameof(NotificationMessageVertAlignment));
        }
    }

    private string notificationMessageIcon = @"\Images\MessageIcons\Info.png";
    public string NotificationMessageIcon
    {
        get => notificationMessageIcon;
        set
        {
            notificationMessageIcon = value;
            RaisePropertyChanged(nameof(NotificationMessageIcon));
        }
    }
    #endregion NOTIFICATION MESSAGE PROPERTIES


    #region SMessageBox PROPERTIES
    private SMessageBoxResult sMessageBoxxResult;
    public SMessageBoxResult SMessageBoxxResult
    {
        get => sMessageBoxxResult;
        set
        {
            sMessageBoxxResult = value;
            RaisePropertyChanged(nameof(SMessageBoxxResult));
        }
    }

    private bool messageBoxPresent = false;
    public bool MessageBoxPresent
    {
        get => messageBoxPresent;
        set
        {
            messageBoxPresent = value;
            RaisePropertyChanged(nameof(MessageBoxPresent));
        }
    }
    #endregion SMessageBox PROPERTIES


    private List<ThreeShapeOrdersModel> current3ShapeOrderList = [];
    public List<ThreeShapeOrdersModel> Current3ShapeOrderList
    {
        get => current3ShapeOrderList;
        set
        {
            current3ShapeOrderList = value;
            RaisePropertyChanged(nameof(Current3ShapeOrderList));
        }
    }


    private List<ArchivesOrdersModel> currentArchivesList = [];
    public List<ArchivesOrdersModel> CurrentArchivesList
    {
        get => currentArchivesList;
        set
        {
            currentArchivesList = value;
            RaisePropertyChanged(nameof(CurrentArchivesList));
        }
    }

    #region FOLDER SUBSCRIPTION & PENDING DIGI CASES PROPERTIES

    private List<string> pendingDigiNumbersWaitingToCollect = [];
    public List<string> PendingDigiNumbersWaitingToCollect
    {
        get => pendingDigiNumbersWaitingToCollect;
        set
        {
            pendingDigiNumbersWaitingToCollect = value;
            RaisePropertyChanged(nameof(PendingDigiNumbersWaitingToCollect));
        }
    }

    private List<ProcessedPanNumberModel> pendingDigiNumbersWaitingToProcess = [];
    public List<ProcessedPanNumberModel> PendingDigiNumbersWaitingToProcess
    {
        get => pendingDigiNumbersWaitingToProcess;
        set
        {
            pendingDigiNumbersWaitingToProcess = value;
            RaisePropertyChanged(nameof(PendingDigiNumbersWaitingToProcess));
        }
    }

    private int pendingDigiNumbersWaitingToCollectInt = 0;
    public int PendingDigiNumbersWaitingToCollectInt
    {
        get => pendingDigiNumbersWaitingToCollectInt;
        set
        {
            pendingDigiNumbersWaitingToCollectInt = value;
            RaisePropertyChanged(nameof(PendingDigiNumbersWaitingToCollectInt));
        }
    }

    private int pendingDigiNumbersWaitingToProcessInt = 0;
    public int PendingDigiNumbersWaitingToProcessInt
    {
        get => pendingDigiNumbersWaitingToProcessInt;
        set
        {
            pendingDigiNumbersWaitingToProcessInt = value;
            RaisePropertyChanged(nameof(PendingDigiNumbersWaitingToProcessInt));
        }
    }

    private string selectedPendingDigiNumber = "";
    public string SelectedPendingDigiNumber
    {
        get => selectedPendingDigiNumber;
        set
        {
            selectedPendingDigiNumber = value;
            RaisePropertyChanged(nameof(SelectedPendingDigiNumber));
            if (_MainWindow.FolderSubscriptionTabPanel.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
                SearchForPanNumberInLabnextForFolderSubscription();
        }
    }

    private string pendingDigiCasesReplacementName = "";
    public string PendingDigiCasesReplacementName
    {
        get => pendingDigiCasesReplacementName;
        set
        {
            pendingDigiCasesReplacementName = value;
            RaisePropertyChanged(nameof(PendingDigiCasesReplacementName));
            if (!string.IsNullOrEmpty(value))
                WriteLocalSetting("PendingDigiCasesReplacementName", value);
        }
    }

    private string fsubscrTargetFolder = "";
    public string FsubscrTargetFolder
    {
        get => fsubscrTargetFolder;
        set
        {
            fsubscrTargetFolder = value;
            RaisePropertyChanged(nameof(FsubscrTargetFolder));
        }
    }

    private List<string> newlyArrivedDigitalCasesList = [];
    public List<string> NewlyArrivedDigitalCasesList
    {
        get => newlyArrivedDigitalCasesList;
        set
        {
            newlyArrivedDigitalCasesList = value;
            RaisePropertyChanged(nameof(NewlyArrivedDigitalCasesList));
        }
    }

    private string fsCreationDateOfLabnextCase = "";
    public string FsCreationDateOfLabnextCase
    {
        get => fsCreationDateOfLabnextCase;
        set
        {
            fsCreationDateOfLabnextCase = value;
            RaisePropertyChanged(nameof(FsCreationDateOfLabnextCase));
        }
    }

    private string fsStatusOfLabnextCase = "";
    public string FsStatusOfLabnextCase
    {
        get => fsStatusOfLabnextCase;
        set
        {
            fsStatusOfLabnextCase = value;
            RaisePropertyChanged(nameof(FsStatusOfLabnextCase));
        }
    }

    private string fsSearchString = "";
    public string FsSearchString
    {
        get => fsSearchString;
        set
        {
            fsSearchString = value;
            RaisePropertyChanged(nameof(FsSearchString));
        }
    }

    private string fsLastDatabaseUpdate = "";
    public string FsLastDatabaseUpdate
    {
        get => fsLastDatabaseUpdate;
        set
        {
            fsLastDatabaseUpdate = value;
            RaisePropertyChanged(nameof(FsLastDatabaseUpdate));
        }
    }

    private string fsCountedEntries = "-";
    public string FsCountedEntries
    {
        get => fsCountedEntries;
        set
        {
            fsCountedEntries = value;
            RaisePropertyChanged(nameof(FsCountedEntries));
        }
    }

    private List<FolderSubscriptionModel> folderSubscriptionList = [];
    public List<FolderSubscriptionModel> FolderSubscriptionList
    {
        get => folderSubscriptionList;
        set
        {
            folderSubscriptionList = value;
            RaisePropertyChanged(nameof(FolderSubscriptionList));
        }
    }

    private FolderSubscriptionModel? fsSelectedFolderObject;
    public FolderSubscriptionModel FsSelectedFolderObject
    {
        get => fsSelectedFolderObject!;
        set
        {
            fsSelectedFolderObject = value;
            RaisePropertyChanged(nameof(FsSelectedFolderObject));
        }
    }

    private bool fsCopyPanelShows = false;
    public bool FsCopyPanelShows
    {
        get => fsCopyPanelShows;
        set
        {
            fsCopyPanelShows = value;
            RaisePropertyChanged(nameof(FsCopyPanelShows));
            if (value == true)
                fsNotificationTimer.Start();
        }
    }


    private string fsCustomNumber = "";
    public string FsCustomNumber
    {
        get => fsCustomNumber;
        set
        {
            fsCustomNumber = value;
            RaisePropertyChanged(nameof(FsCustomNumber));
        }
    }

    #endregion FOLDER SUBSCRIPTION & PENDING DIGI CASES PROPERTIES


    #region COMMENT RULES IN SETTINGS PROPERTIES
    private List<CommentRulesModel> commentRulesList = [];
    public List<CommentRulesModel> CommentRulesList
    {
        get => commentRulesList;
        set
        {
            commentRulesList = value;
            RaisePropertyChanged(nameof(CommentRulesList));
        }
    }


    private CommentRulesModel? selectedCommentRule = new();
    public CommentRulesModel? SelectedCommentRule
    {
        get => selectedCommentRule;
        set
        {
            selectedCommentRule = value;
            RaisePropertyChanged(nameof(SelectedCommentRule));
        }
    }

    private string cRNewRuleName = "";
    public string CRNewRuleName
    {
        get => cRNewRuleName;
        set
        {
            cRNewRuleName = value;
            RaisePropertyChanged(nameof(CRNewRuleName));
        }
    }

    private string cRNewCustomer = "";
    public string CRNewCustomer
    {
        get => cRNewCustomer;
        set
        {
            cRNewCustomer = value;
            RaisePropertyChanged(nameof(CRNewCustomer));
        }
    }

    private string cRNewCommentToBeInserted = "";
    public string CRNewCommentToBeInserted
    {
        get => cRNewCommentToBeInserted;
        set
        {
            cRNewCommentToBeInserted = value;
            RaisePropertyChanged(nameof(CRNewCommentToBeInserted));
        }
    }

    private string cRSelectedItemToBeContained = "";
    public string CRSelectedItemToBeContained
    {
        get => cRSelectedItemToBeContained;
        set
        {
            cRSelectedItemToBeContained = value;
            RaisePropertyChanged(nameof(CRSelectedItemToBeContained));
        }
    }

    #endregion COMMENT RULES IN SETTINGS PROPERTIES


    private bool lookingForCustomerForPaymentIssue = false;
    public bool LookingForPanNumberForPaymentIssue
    {
        get => lookingForCustomerForPaymentIssue;
        set
        {
            lookingForCustomerForPaymentIssue = value;
            RaisePropertyChanged(nameof(LookingForPanNumberForPaymentIssue));
        }
    }



    private List<string> customerSuggestionsCusNamesList = [];
    public List<string> CustomerSuggestionsCusNamesList
    {
        get => customerSuggestionsCusNamesList;
        set
        {
            customerSuggestionsCusNamesList = value;
            RaisePropertyChanged(nameof(CustomerSuggestionsCusNamesList));
        }
    }

    private List<string> customerSuggestionsReplacementsList = [];
    public List<string> CustomerSuggestionsReplacementsList
    {
        get => customerSuggestionsReplacementsList;
        set
        {
            customerSuggestionsReplacementsList = value;
            RaisePropertyChanged(nameof(CustomerSuggestionsReplacementsList));
        }
    }

    private string selectedCustomerName = "";
    public string SelectedCustomerName
    {
        get => selectedCustomerName;
        set
        {
            selectedCustomerName = value;
            RaisePropertyChanged(nameof(SelectedCustomerName));
            BuildCustomerSuggestionReplacementList();
            CSNewCustomer = value;
        }
    }

    private string selectedCustomerSuggestion = "";
    public string SelectedCustomerSuggestion
    {
        get => selectedCustomerSuggestion;
        set
        {
            selectedCustomerSuggestion = value;
            RaisePropertyChanged(nameof(SelectedCustomerSuggestion));
        }
    }

    private string cSNewCustomer = "";
    public string CSNewCustomer
    {
        get => cSNewCustomer;
        set
        {
            cSNewCustomer = value;
            RaisePropertyChanged(nameof(CSNewCustomer));
        }
    }

    private string cSNewReplacement = "";
    public string CSNewReplacement
    {
        get => cSNewReplacement;
        set
        {
            cSNewReplacement = value;
            RaisePropertyChanged(nameof(CSNewReplacement));
        }
    }



    private double currentMemoryUsage = 0;
    public double CurrentMemoryUsage
    {
        get => currentMemoryUsage;
        set
        {
            currentMemoryUsage = value;
            RaisePropertyChanged(nameof(CurrentMemoryUsage));
        }
    }

    private double totalMemory = 0;
    public double TotalMemory
    {
        get => totalMemory;
        set
        {
            totalMemory = value;
            RaisePropertyChanged(nameof(TotalMemory));
        }
    }

    private double totalMemoryInGiB = 0;
    public double TotalMemoryInGiB
    {
        get => totalMemoryInGiB;
        set
        {
            totalMemoryInGiB = value;
            RaisePropertyChanged(nameof(TotalMemoryInGiB));
        }
    }




    #region BuildingUpDates properties
    private string restDayStart = " 0:01:00.000";
    public string RestDayStart
    {
        get => restDayStart;
        set
        {
            restDayStart = value;
            RaisePropertyChanged(nameof(RestDayStart));
        }
    }
    private string restDayEnd = " 23:59:59.999";
    public string RestDayEnd
    {
        get => restDayEnd;
        set
        {
            restDayEnd = value;
            RaisePropertyChanged(nameof(RestDayEnd));
        }
    }

    private string? dtLastTwoDayNames;
    public string? DtLastTwoDayNames
    {
        get => dtLastTwoDayNames;
        set
        {
            dtLastTwoDayNames = value;
            RaisePropertyChanged(nameof(DtLastTwoDayNames));
        }
    }
    private string? dtLastThreeDayNames;
    public string? DtLastThreeDayNames
    {
        get => dtLastThreeDayNames;
        set
        {
            dtLastThreeDayNames = value;
            RaisePropertyChanged(nameof(DtLastThreeDayNames));
        }
    }

    private string? dtToday;
    public string? DtToday
    {
        get => dtToday;
        set
        {
            dtToday = value;
            RaisePropertyChanged(nameof(DtToday));
        }
    }

    private string? dtYesterday;
    public string? DtYesterday
    {
        get => dtYesterday;
        set
        {
            dtYesterday = value;
            RaisePropertyChanged(nameof(DtYesterday));
        }
    }
    private string? dtLastFriday;
    public string? DtLastFriday
    {
        get => dtLastFriday;
        set
        {
            dtLastFriday = value;
            RaisePropertyChanged(nameof(DtLastFriday));
        }
    }
    private string? dtThisMonday;
    public string? DtThisMonday
    {
        get => dtThisMonday;
        set
        {
            dtThisMonday = value;
            RaisePropertyChanged(nameof(DtThisMonday));
        }
    }
    private string? dtLastWeekFriday;
    public string? DtLastWeekFriday
    {
        get => dtLastWeekFriday;
        set
        {
            dtLastWeekFriday = value;
            RaisePropertyChanged(nameof(DtLastWeekFriday));
        }
    }
    private string? dtLastWeekMonday;
    public string? DtLastWeekMonday
    {
        get => dtLastWeekMonday;
        set
        {
            dtLastWeekMonday = value;
            RaisePropertyChanged(nameof(DtLastWeekMonday));
        }
    }
    private string? dtLastWeekSunday;
    public string? DtLastWeekSunday
    {
        get => dtLastWeekSunday;
        set
        {
            dtLastWeekSunday = value;
            RaisePropertyChanged(nameof(DtLastWeekSunday));
        }
    }
    private string? dtOneMonthBack;
    public string? DtOneMonthBack
    {
        get => dtOneMonthBack;
        set
        {
            dtOneMonthBack = value;
            RaisePropertyChanged(nameof(DtOneMonthBack));
        }
    }
    private string? dtTwoMonthsBack;
    public string? DtTwoMonthsBack
    {
        get => dtTwoMonthsBack;
        set
        {
            dtTwoMonthsBack = value;
            RaisePropertyChanged(nameof(DtTwoMonthsBack));
        }
    }
    private string? dtLastTwoDays;
    public string? DtLastTwoDays
    {
        get => dtLastTwoDays;
        set
        {
            dtLastTwoDays = value;
            RaisePropertyChanged(nameof(DtLastTwoDays));
        }
    }
    private string? dtLastThreeDays;
    public string? DtLastThreeDays
    {
        get => dtLastThreeDays;
        set
        {
            dtLastThreeDays = value;
            RaisePropertyChanged(nameof(DtLastThreeDays));
        }
    }
    private string? dtLastSevenDays;
    public string? DtLastSevenDays
    {
        get => dtLastSevenDays;
        set
        {
            dtLastSevenDays = value;
            RaisePropertyChanged(nameof(DtLastSevenDays));
        }
    }
    #endregion BuildingUpDates properties

    private bool allowToShowArchivesProgressBar = true;
    public bool AllowToShowArchivesProgressBar
    {
        get => allowToShowArchivesProgressBar;
        set
        {
            allowToShowArchivesProgressBar = value;
            RaisePropertyChanged(nameof(AllowToShowArchivesProgressBar));
        }
    }

    private bool exportingZipArchiveNow = true;
    public bool ExportingZipArchiveNow
    {
        get => exportingZipArchiveNow;
        set
        {
            exportingZipArchiveNow = value;
            RaisePropertyChanged(nameof(ExportingZipArchiveNow));
        }
    }


    private bool isLabnextLookupIsOpen = false;
    public bool IsLabnextLookupIsOpen
    {
        get => isLabnextLookupIsOpen;
        set
        {
            isLabnextLookupIsOpen = value;
            RaisePropertyChanged(nameof(IsLabnextLookupIsOpen));
        }
    }


    private string labnextCaseID = "";
    public string LabnextCaseID
    {
        get => labnextCaseID;
        set
        {
            labnextCaseID = value;
            RaisePropertyChanged(nameof(LabnextCaseID));
        }
    }

    private bool allowToShowProgressBar = true;
    public bool AllowToShowProgressBar
    {
        get => allowToShowProgressBar;
        set
        {
            allowToShowProgressBar = value;
            RaisePropertyChanged(nameof(AllowToShowProgressBar));
        }
    }

    private bool is3ShapeTabSelected = true;
    public bool Is3ShapeTabSelected
    {
        get => is3ShapeTabSelected;
        set
        {
            is3ShapeTabSelected = value;
            RaisePropertyChanged(nameof(Is3ShapeTabSelected));
        }
    }

    private bool allowThreeShapeOrderListUpdates = true;
    public bool AllowThreeShapeOrderListUpdates
    {
        get => allowThreeShapeOrderListUpdates;
        set
        {
            allowThreeShapeOrderListUpdates = value;
            RaisePropertyChanged(nameof(AllowThreeShapeOrderListUpdates));
        }
    }

    private bool digiCase = true;
    public bool DigiCase
    {
        get => digiCase;
        set
        {
            digiCase = value;
            RaisePropertyChanged(nameof(DigiCase));
        }
    }

    private bool searchOnlyInFileNames = false;
    public bool SearchOnlyInFileNames
    {
        get => searchOnlyInFileNames;
        set
        {
            searchOnlyInFileNames = value;
            RaisePropertyChanged(nameof(SearchOnlyInFileNames));
        }
    }

    private int serverID;
    public int ServerID
    {
        get => serverID;
        set
        {
            serverID = value;
            RaisePropertyChanged(nameof(ServerID));
        }
    }

    private string orderBeingWatched = "";
    public string OrderBeingWatched
    {
        get => orderBeingWatched;
        set
        {
            orderBeingWatched = value;
            RaisePropertyChanged(nameof(OrderBeingWatched));
        }
    }


    private string lastDCASUpdate = "";
    public string LastDCASUpdate
    {
        get => lastDCASUpdate;
        set
        {
            lastDCASUpdate = value;
            RaisePropertyChanged(nameof(LastDCASUpdate));
        }
    }

    private string labnextLabID = "804598";
    public string LabnextLabID
    {
        get => labnextLabID;
        set
        {
            labnextLabID = value;
            RaisePropertyChanged(nameof(LabnextLabID));
        }
    }

    private string labnextUrl = "";
    public string LabnextUrl
    {
        get => labnextUrl;
        set
        {
            labnextUrl = value;
            RaisePropertyChanged(nameof(LabnextUrl));
        }
    }

    private bool isDCASIsActive = false;
    public bool IsDCASIsActive
    {
        get => isDCASIsActive;
        set
        {
            isDCASIsActive = value;
            RaisePropertyChanged(nameof(IsDCASIsActive));
        }
    }




    private int orderCount = 0;
    public int OrderCount
    {
        get => orderCount;
        set
        {
            orderCount = value;
            RaisePropertyChanged(nameof(OrderCount));
        }
    }

    private string orderCountText = "";
    public string OrderCountText
    {
        get => orderCountText;
        set
        {
            orderCountText = value;
            RaisePropertyChanged(nameof(OrderCountText));
            if (value.StartsWith("0"))
                _MainWindow.pb3ShapeProgressBar.Value = 0;
        }
    }

    private int archivesCount = 0;
    public int ArchivesCount
    {
        get => archivesCount;
        set
        {
            archivesCount = value;
            RaisePropertyChanged(nameof(ArchivesCount));
        }
    }

    private string archivesCountText = "";
    public string ArchivesCountText
    {
        get => archivesCountText;
        set
        {
            archivesCountText = value;
            RaisePropertyChanged(nameof(ArchivesCountText));
            if (value.StartsWith("0"))
                _MainWindow.pbArchivesProgressBar.Value = 0;
        }
    }

    private string totalOrdersInArchivesDatastore = "";
    public string TotalOrdersInArchivesDatastore
    {
        get => totalOrdersInArchivesDatastore;
        set
        {
            totalOrdersInArchivesDatastore = value;
            RaisePropertyChanged(nameof(TotalOrdersInArchivesDatastore));
        }
    }

    private string ordersInArchivesDatastoreBetweenDates = "";
    public string OrdersInArchivesDatastoreBetweenDates
    {
        get => ordersInArchivesDatastoreBetweenDates;
        set
        {
            ordersInArchivesDatastoreBetweenDates = value;
            RaisePropertyChanged(nameof(OrdersInArchivesDatastoreBetweenDates));
        }
    }

    private string lastArchivesDatastoreRebuildDate = "";
    public string LastArchivesDatastoreRebuildDate
    {
        get => lastArchivesDatastoreRebuildDate;
        set
        {
            lastArchivesDatastoreRebuildDate = value;
            RaisePropertyChanged(nameof(LastArchivesDatastoreRebuildDate));
        }
    }

    private bool workingOnExportingZipArchive = false;
    public bool WorkingOnExportingZipArchive
    {
        get => workingOnExportingZipArchive;
        set
        {
            workingOnExportingZipArchive = value;
            RaisePropertyChanged(nameof(WorkingOnExportingZipArchive));
        }
    }


    private string generatingZippedOrderString = "Generating Zipped Order";
    public string GeneratingZippedOrderString
    {
        get => generatingZippedOrderString;
        set
        {
            generatingZippedOrderString = value;
            RaisePropertyChanged(nameof(GeneratingZippedOrderString));
        }
    }


    private string searchStringGlobal = "";
    public string SearchStringGlobal
    {
        get => searchStringGlobal;
        set
        {
            searchStringGlobal = value;
            RaisePropertyChanged(nameof(SearchStringGlobal));
        }
    }

    private bool labnextCanReload = false;
    public bool LabnextCanReload
    {
        get => labnextCanReload;
        set
        {
            labnextCanReload = value;
            RaisePropertyChanged(nameof(LabnextCanReload));
        }
    }

    private int paymentIssueCount = 0;
    public int PaymentIssueCount
    {
        get => paymentIssueCount;
        set
        {
            paymentIssueCount = value;
            RaisePropertyChanged(nameof(PaymentIssueCount));
        }
    }

    private List<DesignerPaymentSummary> ordersWithIssuesList = [];
    public List<DesignerPaymentSummary> OrdersWithIssuesList
    {
        get => ordersWithIssuesList;
        set
        {
            ordersWithIssuesList = value;
            RaisePropertyChanged(nameof(OrdersWithIssuesList));
        }
    }

    private DesignerPaymentSummary? selectedDesignerPaymentSummary;
    public DesignerPaymentSummary? SelectedDesignerPaymentSummary
    {
        get => selectedDesignerPaymentSummary;
        set
        {
            selectedDesignerPaymentSummary = value;
            RaisePropertyChanged(nameof(SelectedDesignerPaymentSummary));
            if (value is not null)
                LookUpOrdersWithIssuesForDesigner();
        }
    }

    private List<LabnextIssueModel> paymentCasesIssueListForDesigner = [];
    public List<LabnextIssueModel> PaymentCasesIssueListForDesigner
    {
        get => paymentCasesIssueListForDesigner;
        set
        {
            paymentCasesIssueListForDesigner = value;
            RaisePropertyChanged(nameof(PaymentCasesIssueListForDesigner));
        }
    }

    private LabnextIssueModel? selectedPaymentIssueForDesigner;
    public LabnextIssueModel? SelectedPaymentIssueForDesigner
    {
        get => selectedPaymentIssueForDesigner;
        set
        {
            selectedPaymentIssueForDesigner = value;
            RaisePropertyChanged(nameof(SelectedPaymentIssueForDesigner));
            if (value is not null)
                PaymentIssueSelected();
        }
    }

    private int foundPanNumberSx = 0;
    public int FoundPanNumberSx
    {
        get => foundPanNumberSx;
        set
        {
            foundPanNumberSx = value;
            RaisePropertyChanged(nameof(FoundPanNumberSx));
        }
    }
    
    private bool searchOnlyForSameDesigner = true;
    public bool SearchOnlyForSameDesigner
    {
        get => searchOnlyForSameDesigner;
        set
        {
            searchOnlyForSameDesigner = value;
            RaisePropertyChanged(nameof(SearchOnlyForSameDesigner));
            PaymentIssueSelected();
        }
    }

    private bool showCaseFromCloseDateRangeOnly = true;
    public bool ShowCaseFromCloseDateRangeOnly
    {
        get => showCaseFromCloseDateRangeOnly;
        set
        {
            showCaseFromCloseDateRangeOnly = value;
            RaisePropertyChanged(nameof(ShowCaseFromCloseDateRangeOnly));
            PaymentIssueSelected();
        }
    }

    private ObservableCollection<ThreeShapeOrdersModel> possibleOrdersFrom3ShapeForLabnextMatch = [];
    public ObservableCollection<ThreeShapeOrdersModel> PossibleOrdersFrom3ShapeForLabnextMatch
    {
        get => possibleOrdersFrom3ShapeForLabnextMatch;
        set
        {
            possibleOrdersFrom3ShapeForLabnextMatch = value;
            RaisePropertyChanged(nameof(PossibleOrdersFrom3ShapeForLabnextMatch));
        }
    }

    private ObservableCollection<ThreeShapeOrdersModel> possibleOrdersFromArchivesForLabnextMatch = [];
    public ObservableCollection<ThreeShapeOrdersModel> PossibleOrdersFromArchivesForLabnextMatch
    {
        get => possibleOrdersFromArchivesForLabnextMatch;
        set
        {
            possibleOrdersFromArchivesForLabnextMatch = value;
            RaisePropertyChanged(nameof(PossibleOrdersFromArchivesForLabnextMatch));
        }
    }

    private List<DesignerPaymentSummary> designerPaymentSummaryList = [];
    public List<DesignerPaymentSummary> DesignerPaymentSummaryList
    {
        get => designerPaymentSummaryList;
        set
        {
            designerPaymentSummaryList = value;
            RaisePropertyChanged(nameof(DesignerPaymentSummaryList));
        }
    }

    private List<DoublePaidOrdersModel> doublePaidOrdersList = [];
    public List<DoublePaidOrdersModel> DoublePaidOrdersList
    {
        get => doublePaidOrdersList;
        set
        {
            doublePaidOrdersList = value;
            RaisePropertyChanged(nameof(DoublePaidOrdersList));
        }
    }

    private List<PaidToWrongPersonOrdersModel> paidToWrongPersonOrdersList = [];
    public List<PaidToWrongPersonOrdersModel> PaidToWrongPersonOrdersList
    {
        get => paidToWrongPersonOrdersList;
        set
        {
            paidToWrongPersonOrdersList = value;
            RaisePropertyChanged(nameof(PaidToWrongPersonOrdersList));
        }
    }

    private List<WrongfulPaymentsModel> wrongfullyPaidCasesList = [];
    public List<WrongfulPaymentsModel> WrongfullyPaidCasesList
    {
        get => wrongfullyPaidCasesList;
        set
        {
            wrongfullyPaidCasesList = value;
            RaisePropertyChanged(nameof(WrongfullyPaidCasesList));
        }
    }

    private string previousSearchStringGlobal = "";
    public string PreviousSearchStringGlobal
    {
        get => previousSearchStringGlobal;
        set
        {
            previousSearchStringGlobal = value;
            RaisePropertyChanged(nameof(PreviousSearchStringGlobal));
        }
    }

    private int resultIn3Shape = 0;
    public int ResultIn3Shape
    {
        get => resultIn3Shape;
        set
        {
            resultIn3Shape = value;
            RaisePropertyChanged(nameof(ResultIn3Shape));
        }
    }

    private int resultInArchives = 0;
    public int ResultInArchives
    {
        get => resultInArchives;
        set
        {
            resultInArchives = value;
            RaisePropertyChanged(nameof(ResultInArchives));
        }
    }

    private int resultInArchivesOnArchivePage = 0;
    public int ResultInArchivesOnArchivePage
    {
        get => resultInArchivesOnArchivePage;
        set
        {
            resultInArchivesOnArchivePage = value;
            RaisePropertyChanged(nameof(ResultInArchivesOnArchivePage));
        }
    }

    private int archiveResultOffset = 0;
    public int ArchiveResultOffset
    {
        get => archiveResultOffset;
        set
        {
            archiveResultOffset = value;
            RaisePropertyChanged(nameof(ArchiveResultOffset));
        }
    }

    private int archiveResultOffsetOnArchivePage = 0;
    public int ArchiveResultOffsetOnArchivePage
    {
        get => archiveResultOffsetOnArchivePage;
        set
        {
            archiveResultOffsetOnArchivePage = value;
            RaisePropertyChanged(nameof(ArchiveResultOffsetOnArchivePage));
        }
    }


    private List<DesignerUnitCountModel> designerUnitCountsList = [];
    public List<DesignerUnitCountModel> DesignerUnitCountsList
    {
        get => designerUnitCountsList;
        set
        {
            designerUnitCountsList = value;
            RaisePropertyChanged(nameof(DesignerUnitCountsList));
        }
    }


    private List<string> filterYearItems = [];
    public List<string> FilterYearItems
    {
        get => filterYearItems;
        set
        {
            filterYearItems = value;
            RaisePropertyChanged(nameof(FilterYearItems));
        }
    }

    private string filterYearItemSelected = "All time";
    public string FilterYearItemSelected
    {
        get => filterYearItemSelected;
        set
        {
            filterYearItemSelected = value;
            RaisePropertyChanged(nameof(FilterYearItemSelected));
        }
    }

    private List<string> filterMonthItems = ["All months", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    public List<string> FilterMonthItems
    {
        get => filterMonthItems;
        set
        {
            filterMonthItems = value;
            RaisePropertyChanged(nameof(FilterMonthItems));
        }
    }

    private string filterMonthItemSelected = "All months";
    public string FilterMonthItemSelected
    {
        get => filterMonthItemSelected;
        set
        {
            filterMonthItemSelected = value;
            RaisePropertyChanged(nameof(FilterMonthItemSelected));
        }
    }

    private ObservableCollection<GlobalSearchModel> globalSearchResult = [];
    public ObservableCollection<GlobalSearchModel> GlobalSearchResult
    {
        get => globalSearchResult;
        set
        {
            globalSearchResult = value;
            RaisePropertyChanged(nameof(GlobalSearchResult));
        }
    }

    private GlobalSearchModel selectedGlobalSearchResult;
    public GlobalSearchModel SelectedGlobalSearchResult
    {
        get => selectedGlobalSearchResult;
        set
        {
            selectedGlobalSearchResult = value;
            RaisePropertyChanged(nameof(SelectedGlobalSearchResult));
        }
    }

    private ObservableCollection<GlobalSearchModel> globalSearchResultArchives = [];
    public ObservableCollection<GlobalSearchModel> GlobalSearchResultArchives
    {
        get => globalSearchResultArchives;
        set
        {
            globalSearchResultArchives = value;
            RaisePropertyChanged(nameof(GlobalSearchResultArchives));
        }
    }

    private GlobalSearchModel selectedGlobalSearchResultArchives;
    public GlobalSearchModel SelectedGlobalSearchResultArchives
    {
        get => selectedGlobalSearchResultArchives;
        set
        {
            selectedGlobalSearchResultArchives = value;
            RaisePropertyChanged(nameof(SelectedGlobalSearchResultArchives));
        }
    }

    private ObservableCollection<GlobalSearchModel> globalSearchResult3Shape = [];
    public ObservableCollection<GlobalSearchModel> GlobalSearchResult3Shape
    {
        get => globalSearchResult3Shape;
        set
        {
            globalSearchResult3Shape = value;
            RaisePropertyChanged(nameof(GlobalSearchResult3Shape));
        }
    }

    private GlobalSearchModel selectedGlobalSearchResult3Shape;
    public GlobalSearchModel SelectedGlobalSearchResult3Shape
    {
        get => selectedGlobalSearchResult3Shape;
        set
        {
            selectedGlobalSearchResult3Shape = value;
            RaisePropertyChanged(nameof(SelectedGlobalSearchResult3Shape));
        }
    }

    private string searchString = "";
    public string SearchString
    {
        get => searchString;
        set
        {
            searchString = value;
            RaisePropertyChanged(nameof(SearchString));
            if (string.IsNullOrEmpty(SearchString))
            {
                ArchiveResultOffsetOnArchivePage = 0;
                ArchiveResultOffset = 0;
                GlobalSearchResultArchives = [];
                GlobalSearchResult3Shape = [];
            }
        }
    }

    private string customerSearchString = "";
    public string CustomerSearchString
    {
        get => customerSearchString;
        set
        {
            customerSearchString = value;
            RaisePropertyChanged(nameof(CustomerSearchString));
        }
    }

    private string searchStringArchives = "";
    public string SearchStringArchives
    {
        get => searchStringArchives;
        set
        {
            searchStringArchives = value;
            RaisePropertyChanged(nameof(SearchStringArchives));
        }
    }

    private string previousSearchStringArchives = "";
    public string PreviousSearchStringArchives
    {
        get => previousSearchStringArchives;
        set
        {
            previousSearchStringArchives = value;
            RaisePropertyChanged(nameof(PreviousSearchStringArchives));
        }
    }

    private List<string> searchHistory = [];
    public List<string> SearchHistory
    {
        get => searchHistory;
        set
        {
            searchHistory = value;
            RaisePropertyChanged(nameof(SearchHistory));
        }
    }

    private List<string> searchHistoryArchives = [];
    public List<string> SearchHistoryArchives
    {
        get => searchHistoryArchives;
        set
        {
            searchHistoryArchives = value;
            RaisePropertyChanged(nameof(SearchHistoryArchives));
        }
    }

    private List<MenuItem> searchHistoryForContextMenu = [];
    public List<MenuItem> SearchHistoryForContextMenu
    {
        get => searchHistoryForContextMenu;
        set
        {
            searchHistoryForContextMenu = value;
            RaisePropertyChanged(nameof(SearchHistoryForContextMenu));
        }
    }

    private string selectedItemInSearchHistory = "";
    public string SelectedItemInSearchHistory
    {
        get => selectedItemInSearchHistory;
        set
        {
            selectedItemInSearchHistory = value;
            RaisePropertyChanged(nameof(SelectedItemInSearchHistory));
            if (!string.IsNullOrEmpty(SelectedItemInSearchHistory))
            {
                SearchString = value;
                SearchFieldKeyDownOnHome();
            }
        }
    }

    private string[] groupBy = ["None", "Customer", "Scan Source", "Case Status", "Last touched By"];
    public string[] GroupBy
    {
        get => groupBy;
        set
        {
            groupBy = value;
            RaisePropertyChanged(nameof(GroupBy));
        }
    }

    private string selectedGroupByItem = "None";
    public string SelectedGroupByItem
    {
        get => selectedGroupByItem;
        set
        {
            selectedGroupByItem = value;
            RaisePropertyChanged(nameof(SelectedGroupByItem));
        }
    }

    private ThreeShapeOrdersModel? threeShapeObject;
    public ThreeShapeOrdersModel? ThreeShapeObject
    {
        get => threeShapeObject;
        set
        {
            threeShapeObject = value;
            RaisePropertyChanged(nameof(ThreeShapeObject));
        }
    }

    private ThreeShapeOrdersModel? selectedItem;
    public ThreeShapeOrdersModel? SelectedItem
    {
        get => selectedItem;
        set
        {
            selectedItem = value;
            RaisePropertyChanged(nameof(SelectedItem));
        }
    }

    private ArchivesOrdersModel? selectedArchiveItem;
    public ArchivesOrdersModel? SelectedArchiveItem
    {
        get => selectedArchiveItem;
        set
        {
            selectedArchiveItem = value;
            RaisePropertyChanged(nameof(SelectedArchiveItem));
        }
    }

    private ICollectionView? dataView;
    private ICollectionView? DataView
    {
        get => dataView;
        set
        {
            dataView = value;
            RaisePropertyChanged(nameof(DataView));
        }
    }

    private Dictionary<string, bool> expandStates = [];
    public Dictionary<string, bool> ExpandStates
    {
        get => expandStates;
        set
        {
            expandStates = value;
            RaisePropertyChanged(nameof(ExpandStates));
        }
    }

    private Dictionary<string, Brush> itemBackground = [];
    public Dictionary<string, Brush> ItemBackground
    {
        get => itemBackground;
        set
        {
            itemBackground = value;
            RaisePropertyChanged(nameof(ItemBackground));
        }
    }

    private Dictionary<Brush, bool> digiSystemColors = [];
    public Dictionary<Brush, bool> DigiSystemColors
    {
        get => digiSystemColors;
        set
        {
            digiSystemColors = value;
            RaisePropertyChanged(nameof(DigiSystemColors));
        }
    }

    private int newTriosCaseInInboxCount = 0;
    public int NewTriosCaseInInboxCount
    {
        get => newTriosCaseInInboxCount;
        set
        {
            newTriosCaseInInboxCount = value;
            RaisePropertyChanged(nameof(NewTriosCaseInInboxCount));
        }
    }

    private int newDigiCaseArrivedCount = 0;
    public int NewDigiCaseArrivedCount
    {
        get => newDigiCaseArrivedCount;
        set
        {
            newDigiCaseArrivedCount = value;
            RaisePropertyChanged(nameof(NewDigiCaseArrivedCount));
        }
    }

    private int totalNewDigiCaseWithoutInHouseCases = 0;
    public int TotalNewDigiCaseWithoutInHouseCases
    {
        get => totalNewDigiCaseWithoutInHouseCases;
        set
        {
            totalNewDigiCaseWithoutInHouseCases = value;
            RaisePropertyChanged(nameof(TotalNewDigiCaseWithoutInHouseCases));
        }
    }


    #region PRESCRIPTION MAKER PROPERTIES
    private FileStream documentStreamPixelCheck;
    public FileStream DocumentStreamPixelCheck
    {
        get => documentStreamPixelCheck;
        set
        {
            documentStreamPixelCheck = value;
            RaisePropertyChanged(nameof(DocumentStreamPixelCheck));
        }
    }

    private FileStream documentStreamFinalPrescription;
    public FileStream DocumentStreamFinalPrescription
    {
        get => documentStreamFinalPrescription;
        set
        {
            documentStreamFinalPrescription = value;
            RaisePropertyChanged(nameof(DocumentStreamFinalPrescription));
        }
    }

    private Bitmap prescriptionImageForProcess;
    public Bitmap PrescriptionImageForProcess
    {
        get => prescriptionImageForProcess;
        set
        {
            prescriptionImageForProcess = value;
            RaisePropertyChanged(nameof(PrescriptionImageForProcess));
        }
    }


    private Stream documentStreamUser;
    public Stream DocumentStreamUser
    {
        get => documentStreamUser;
        set
        {
            documentStreamUser = value;
            RaisePropertyChanged(nameof(DocumentStreamUser));
        }
    }


    private List<PMEventModel> prescriptionMakerEventsList = [];
    public List<PMEventModel> PrescriptionMakerEventsList
    {
        get => prescriptionMakerEventsList;
        set
        {
            prescriptionMakerEventsList = value;
            RaisePropertyChanged(nameof(PrescriptionMakerEventsList));
        }
    }

    private List<string> prescriptionMakerEventsListReversed = [];
    public List<string> PrescriptionMakerEventsListReversed
    {
        get => prescriptionMakerEventsListReversed;
        set
        {
            prescriptionMakerEventsListReversed = value;
            RaisePropertyChanged(nameof(PrescriptionMakerEventsListReversed));
        }
    }

    private List<InconsistencyModel> prescriptionInconsistencys = [];
    public List<InconsistencyModel> PrescriptionInconsistencys
    {
        get => prescriptionInconsistencys;
        set
        {
            prescriptionInconsistencys = value;
            RaisePropertyChanged(nameof(PrescriptionInconsistencys));
        }
    }

    private List<InconsistencyModel> ignoredPrescriptionInconsistencys = [];
    public List<InconsistencyModel> IgnoredPrescriptionInconsistencys
    {
        get => ignoredPrescriptionInconsistencys;
        set
        {
            ignoredPrescriptionInconsistencys = value;
            RaisePropertyChanged(nameof(IgnoredPrescriptionInconsistencys));
        }
    }

    private List<InconsistencyModel> prescriptionWithNoInconsistencys = [];
    public List<InconsistencyModel> PrescriptionWithNoInconsistencys
    {
        get => prescriptionWithNoInconsistencys;
        set
        {
            prescriptionWithNoInconsistencys = value;
            RaisePropertyChanged(nameof(PrescriptionWithNoInconsistencys));
        }
    }

    private bool inconsistencyPanelShows = false;
    public bool InconsistencyPanelShows
    {
        get => inconsistencyPanelShows;
        set
        {
            inconsistencyPanelShows = value;
            RaisePropertyChanged(nameof(InconsistencyPanelShows));
        }
    }

    private string ignoreInconsistencyOrderID = "";
    public string IgnoreInconsistencyOrderID
    {
        get => ignoreInconsistencyOrderID;
        set
        {
            ignoreInconsistencyOrderID = value;
            RaisePropertyChanged(nameof(IgnoreInconsistencyOrderID));
        }
    }

    private double pmLastPrescriptionSize = 0;
    public double PmLastPrescriptionSize
    {
        get => pmLastPrescriptionSize;
        set
        {
            pmLastPrescriptionSize = value;
            RaisePropertyChanged(nameof(PmLastPrescriptionSize));
        }
    }

    private int pmFilesCountIniTeroFolder = 0;
    public int PmFilesCountIniTeroFolder
    {
        get => pmFilesCountIniTeroFolder;
        set
        {
            pmFilesCountIniTeroFolder = value;
            RaisePropertyChanged(nameof(PmFilesCountIniTeroFolder));
        }
    }

    private string pmNextPanNumberInList = "";
    public string PmNextPanNumberInList
    {
        get => pmNextPanNumberInList;
        set
        {
            pmNextPanNumberInList = value;
            RaisePropertyChanged(nameof(PmNextPanNumberInList));
        }
    }

    private string lastIteroZipFileId = "";
    public string LastIteroZipFileId
    {
        get => lastIteroZipFileId;
        set
        {
            lastIteroZipFileId = value;
            RaisePropertyChanged(nameof(LastIteroZipFileId));
        }
    }

    private string pmLastTakenPanNumber = "";
    public string PmLastTakenPanNumber
    {
        get => pmLastTakenPanNumber;
        set
        {
            pmLastTakenPanNumber = value;
            RaisePropertyChanged(nameof(PmLastTakenPanNumber));
        }
    }


    private string lastUsedPanNumber = "";
    public string LastUsedPanNumber
    {
        get => lastUsedPanNumber;
        set
        {
            lastUsedPanNumber = value;
            RaisePropertyChanged(nameof(LastUsedPanNumber));
        }
    }

    private DateTime lastUsedPanNumbersDate = DateTime.Now;
    public DateTime LastUsedPanNumbersDate
    {
        get => lastUsedPanNumbersDate;
        set
        {
            lastUsedPanNumbersDate = value;
            RaisePropertyChanged(nameof(LastUsedPanNumbersDate));
        }
    }

    private DateTime lastASConnectIDDate = DateTime.Now;
    public DateTime LastASConnectIDDate
    {
        get => lastASConnectIDDate;
        set
        {
            lastASConnectIDDate = value;
            RaisePropertyChanged(nameof(LastASConnectIDDate));
        }
    }

    private string lastUsedPanNumber_oneBefore = "";
    public string LastUsedPanNumber_oneBefore
    {
        get => lastUsedPanNumber_oneBefore;
        set
        {
            lastUsedPanNumber_oneBefore = value;
            RaisePropertyChanged(nameof(LastUsedPanNumber_oneBefore));
        }
    }

    private string nextPanNumberGlobal = "";
    public string NextPanNumberGlobal
    {
        get => nextPanNumberGlobal;
        set
        {
            nextPanNumberGlobal = value;
            RaisePropertyChanged(nameof(NextPanNumberGlobal));
        }
    }

    private Visibility processingDigiPrescriptionNow = Visibility.Collapsed;
    public Visibility ProcessingDigiPrescriptionNow
    {
        get => processingDigiPrescriptionNow;
        set
        {
            processingDigiPrescriptionNow = value;
            RaisePropertyChanged(nameof(ProcessingDigiPrescriptionNow));
        }
    }

    private Visibility showingTakeANumberPanel = Visibility.Hidden;
    public Visibility ShowingTakeANumberPanel
    {
        get => showingTakeANumberPanel;
        set
        {
            showingTakeANumberPanel = value;
            RaisePropertyChanged(nameof(ShowingTakeANumberPanel));
        }
    }

    private Visibility showingPrescriptionPreviewPanel = Visibility.Collapsed;
    public Visibility ShowingPrescriptionPreviewPanel
    {
        get => showingPrescriptionPreviewPanel;
        set
        {
            showingPrescriptionPreviewPanel = value;
            RaisePropertyChanged(nameof(ShowingPrescriptionPreviewPanel));
        }
    }


    private Visibility showingFiltersPanel = Visibility.Collapsed;
    public Visibility ShowingFiltersPanel
    {
        get => showingFiltersPanel;
        set
        {
            showingFiltersPanel = value;
            RaisePropertyChanged(nameof(ShowingFiltersPanel));
        }
    }

    private Visibility pmRushButtonShows = Visibility.Hidden;
    public Visibility PmRushButtonShows
    {
        get => pmRushButtonShows;
        set
        {
            pmRushButtonShows = value;
            RaisePropertyChanged(nameof(PmRushButtonShows));
        }
    }

    private Visibility pmSendToButtonShows = Visibility.Hidden;
    public Visibility PmSendToButtonShows
    {
        get => pmSendToButtonShows;
        set
        {
            pmSendToButtonShows = value;
            RaisePropertyChanged(nameof(PmSendToButtonShows));
        }
    }

    private Visibility pmMissingButtonShows = Visibility.Hidden;
    public Visibility PmMissingButtonShows
    {
        get => pmMissingButtonShows;
        set
        {
            pmMissingButtonShows = value;
            RaisePropertyChanged(nameof(PmMissingButtonShows));
        }
    }

    private List<string> pmMissingList = ["-", "NEED SCAN BODY INFO", "NEED IMPLANT INFO", "NEED SCAN BODY / IMPLANT INFO", "WRONG SCAN", "WRONG SCAN BODY", "NOT ENOUGH INFO", "INCORRECT INFO", "NO SCAN, ONLY PRESCRIPTION CAME", "NO PREP, NO INFO", "NO PREOP SCAN", "NO STUDY MODEL", "NO OPPOSING"];
    public List<string> PmMissingList
    {
        get => pmMissingList;
    }

    private string pmSelectedMissing = "";
    public string PmSelectedMissing
    {
        get => pmSelectedMissing;
        set
        {
            pmSelectedMissing = value;
            RaisePropertyChanged(nameof(PmSelectedMissing));
        }
    }

    private List<string> pmSendToList = [];
    public List<string> PmSendToList
    {
        get => pmSendToList;
        set
        {
            pmSendToList = value;
            RaisePropertyChanged(nameof(PmSendToList));
        }
    }

    private string pmNewSentToName = "";
    public string PmNewSentToName
    {
        get => pmNewSentToName;
        set
        {
            pmNewSentToName = value;
            RaisePropertyChanged(nameof(PmNewSentToName));
        }
    }

    private string pmSelectedSentTo = "";
    public string PmSelectedSentTo
    {
        get => pmSelectedSentTo;
        set
        {
            pmSelectedSentTo = value;
            RaisePropertyChanged(nameof(PmSelectedSentTo));
        }
    }

    private string pmSelectedSendToEntry = "";
    public string PmSelectedSendToEntry
    {
        get => pmSelectedSendToEntry;
        set
        {
            pmSelectedSendToEntry = value;
            RaisePropertyChanged(nameof(PmSelectedSendToEntry));
        }
    }

    private List<string> pmPanNumberList = [];
    public List<string> PmPanNumberList
    {
        get => pmPanNumberList;
        set
        {
            pmPanNumberList = value;
            RaisePropertyChanged(nameof(PmPanNumberList));
            if (value.Count > 0)
                PmNextPanNumberInList = value[0];
        }
    }

    private ImageSource? pmSavedPrescription;
    public ImageSource PmSavedPrescription
    {
        get => pmSavedPrescription!;
        set
        {
            pmSavedPrescription = value;
            RaisePropertyChanged(nameof(PmSavedPrescription));
        }
    }

    private string pmWatchedPdfFolder = "Click here to browse..";
    public string PmWatchedPdfFolder
    {
        get => pmWatchedPdfFolder;
        set
        {
            pmWatchedPdfFolder = value;
            RaisePropertyChanged(nameof(PmWatchedPdfFolder));
        }
    }

    private string pmAddNewNumber = "";
    public string PmAddNewNumber
    {
        get => pmAddNewNumber;
        set
        {
            pmAddNewNumber = value;
            RaisePropertyChanged(nameof(PmAddNewNumber));
        }
    }


    private string fullPathGlobal = "";
    public string FullPathGlobal
    {
        get => fullPathGlobal;
        set
        {
            fullPathGlobal = value;
            RaisePropertyChanged(nameof(FullPathGlobal));
        }
    }

    private string sironaOrderNumber = "";
    public string SironaOrderNumber
    {
        get => sironaOrderNumber;
        set
        {
            sironaOrderNumber = value;
            RaisePropertyChanged(nameof(SironaOrderNumber));
        }
    }

    private int globalFileLockCount = 0;
    public int GlobalFileLockCount
    {
        get => globalFileLockCount;
        set
        {
            globalFileLockCount = value;
            RaisePropertyChanged(nameof(GlobalFileLockCount));
        }
    }

    private bool noMorePanNumberBoxShowed = false;
    public bool NoMorePanNumberBoxShowed
    {
        get => noMorePanNumberBoxShowed;
        set
        {
            noMorePanNumberBoxShowed = value;
            RaisePropertyChanged(nameof(NoMorePanNumberBoxShowed));
        }
    }

    private string pDFTemp = "";
    public string PDFTemp
    {
        get => pDFTemp;
        set
        {
            pDFTemp = value;
            RaisePropertyChanged(nameof(PDFTemp));
        }
    }

    private bool pmOpenUpPrescriptionsBool = false;
    public bool PmOpenUpPrescriptionsBool
    {
        get => pmOpenUpPrescriptionsBool;
        set
        {
            pmOpenUpPrescriptionsBool = value;
            RaisePropertyChanged(nameof(PmOpenUpPrescriptionsBool));
        }
    }

    private string pageHeaderIsHigh = "";
    public string PageHeaderIsHigh
    {
        get => pageHeaderIsHigh;
        set
        {
            pageHeaderIsHigh = value;
            RaisePropertyChanged(nameof(PageHeaderIsHigh));
        }
    }

    private string pdfPageCount = "";
    public string PdfPageCount
    {
        get => pdfPageCount;
        set
        {
            pdfPageCount = value;
            RaisePropertyChanged(nameof(PdfPageCount));
        }
    }

    private bool isItSironaPrescription = false;
    public bool IsItSironaPrescription
    {
        get => isItSironaPrescription;
        set
        {
            isItSironaPrescription = value;
            RaisePropertyChanged(nameof(IsItSironaPrescription));
        }
    }

    private bool isItASConnectPrescription = false;
    public bool IsItASConnectPrescription
    {
        get => isItASConnectPrescription;
        set
        {
            isItASConnectPrescription = value;
            RaisePropertyChanged(nameof(IsItASConnectPrescription));
        }
    }

    private string aSConnectOrderID = "";
    public string ASConnectOrderID
    {
        get => aSConnectOrderID;
        set
        {
            aSConnectOrderID = value;
            RaisePropertyChanged(nameof(ASConnectOrderID));
        }
    }

    private string pmFinalPrescriptionsFolder = "Click here to browse..";
    public string PmFinalPrescriptionsFolder
    {
        get => pmFinalPrescriptionsFolder;
        set
        {
            pmFinalPrescriptionsFolder = value;
            RaisePropertyChanged(nameof(PmFinalPrescriptionsFolder));
        }
    }

    private string pmSironaScansFolder = "Click here to browse..";
    public string PmSironaScansFolder
    {
        get => pmSironaScansFolder;
        set
        {
            pmSironaScansFolder = value;
            RaisePropertyChanged(nameof(PmSironaScansFolder));
        }
    }

    private string pmIteroExportFolder = "Click here to browse..";
    public string PmIteroExportFolder
    {
        get => pmIteroExportFolder;
        set
        {
            pmIteroExportFolder = value;
            RaisePropertyChanged(nameof(PmIteroExportFolder));
        }
    }

    private string pmDownloadFolder = "Click here to browse..";
    public string PmDownloadFolder
    {
        get => pmDownloadFolder;
        set
        {
            pmDownloadFolder = value;
            RaisePropertyChanged(nameof(PmDownloadFolder));
        }
    }

    private double notificationProgressBarValue = 0;
    public double NotificationProgressBarValue
    {
        get => notificationProgressBarValue;
        set
        {
            notificationProgressBarValue = value;
            RaisePropertyChanged(nameof(NotificationProgressBarValue));
        }
    }


    #endregion PRESCRIPTION MAKER PROPERTIES

    private OrderIssueModel selectedOrderIssue;
    public OrderIssueModel SelectedOrderIssue
    {
        get => selectedOrderIssue;
        set
        {
            selectedOrderIssue = value;
            RaisePropertyChanged(nameof(SelectedOrderIssue));
        }
    }


    #region PAN COLOR CHECKER PROPERTIES
    private string pcPanColor = "#f7f4e6";
    public string PcPanColor
    {
        get => pcPanColor;
        set
        {
            pcPanColor = value;
            RaisePropertyChanged(nameof(PcPanColor));
        }
    }

    private string pcPanColorFriendlyName = "Check pan color";
    public string PcPanColorFriendlyName
    {
        get => pcPanColorFriendlyName;
        set
        {
            pcPanColorFriendlyName = value;
            RaisePropertyChanged(nameof(PcPanColorFriendlyName));
        }
    }

    private string pcPanNumber = "";
    public string PcPanNumber
    {
        get => pcPanNumber;
        set
        {
            pcPanNumber = value;
            RaisePropertyChanged(nameof(PcPanNumber));
        }
    }

    private string previousPcPanNumber = "";
    public string PreviousPcPanNumber
    {
        get => previousPcPanNumber;
        set
        {
            previousPcPanNumber = value;
            RaisePropertyChanged(nameof(PreviousPcPanNumber));
        }
    }

    private string originalRgbColor = "";
    public string OriginalRgbColor
    {
        get => originalRgbColor;
        set
        {
            originalRgbColor = value;
            RaisePropertyChanged(nameof(OriginalRgbColor));
        }
    }

    private Visibility panColorShowsNow = Visibility.Collapsed;
    public Visibility PanColorShowsNow
    {
        get => panColorShowsNow;
        set
        {
            panColorShowsNow = value;
            RaisePropertyChanged(nameof(PanColorShowsNow));
        }
    }

    private Visibility noNumberRegisteredShowsNow = Visibility.Collapsed;
    public Visibility NoNumberRegisteredShowsNow
    {
        get => noNumberRegisteredShowsNow;
        set
        {
            noNumberRegisteredShowsNow = value;
            RaisePropertyChanged(nameof(NoNumberRegisteredShowsNow));
        }
    }


    private bool isItDarkColor = true;
    public bool IsItDarkColor
    {
        get => isItDarkColor;
        set
        {
            isItDarkColor = value;
            RaisePropertyChanged(nameof(IsItDarkColor));
        }
    }

    #endregion PAN COLOR CHECKER PROPERTIES


    public RelayCommand ChangeColorCommand { get; set; }
    public RelayCommand AddNewNumberCommand { get; set; }

    public RelayCommand RegisterApplicationCommand { get; set; }
    public RelayCommand GetCaseInfoByLabnextIDCommand { get; set; }


    #region Settings Tab Properties
    private bool cbSettingGlassyEffect = true;
    public bool CbSettingGlassyEffect
    {
        get => cbSettingGlassyEffect;
        set
        {
            cbSettingGlassyEffect = value;
            RaisePropertyChanged(nameof(CbSettingGlassyEffect));
        }
    }

    private bool cbSettingKeepUserLoggedInLabnext = false;
    public bool CbSettingKeepUserLoggedInLabnext
    {
        get => cbSettingKeepUserLoggedInLabnext;
        set
        {
            cbSettingKeepUserLoggedInLabnext = value;
            RaisePropertyChanged(nameof(CbSettingKeepUserLoggedInLabnext));
        }
    }


    private bool cbSettingStartAppMinimized = false;
    public bool CbSettingStartAppMinimized
    {
        get => cbSettingStartAppMinimized;
        set
        {
            cbSettingStartAppMinimized = value;
            RaisePropertyChanged(nameof(CbSettingStartAppMinimized));
        }
    }

    private bool cbSettingShowEmptyPanCount = true;
    public bool CbSettingShowEmptyPanCount
    {
        get => cbSettingShowEmptyPanCount;
        set
        {
            cbSettingShowEmptyPanCount = value;
            RaisePropertyChanged(nameof(CbSettingShowEmptyPanCount));
        }
    }

    private bool cbSettingShowDigiCases = true;
    public bool CbSettingShowDigiCases
    {
        get => cbSettingShowDigiCases;
        set
        {
            cbSettingShowDigiCases = value;
            RaisePropertyChanged(nameof(CbSettingShowDigiCases));
        }
    }

    private bool cbSettingShowPendingDigiCases = false;
    public bool CbSettingShowPendingDigiCases
    {
        get => cbSettingShowPendingDigiCases;
        set
        {
            cbSettingShowPendingDigiCases = value;
            RaisePropertyChanged(nameof(CbSettingShowPendingDigiCases));
        }
    }

    private bool cbSettingIncludePendingDigiCasesInNewlyArrived = true;
    public bool CbSettingIncludePendingDigiCasesInNewlyArrived
    {
        get => cbSettingIncludePendingDigiCasesInNewlyArrived;
        set
        {
            cbSettingIncludePendingDigiCasesInNewlyArrived = value;
            RaisePropertyChanged(nameof(CbSettingIncludePendingDigiCasesInNewlyArrived));
        }
    }

    private bool cbSettingShowDigiPrescriptionsCount = true;
    public bool CbSettingShowDigiPrescriptionsCount
    {
        get => cbSettingShowDigiPrescriptionsCount;
        set
        {
            cbSettingShowDigiPrescriptionsCount = value;
            RaisePropertyChanged(nameof(CbSettingShowDigiPrescriptionsCount));
        }
    }


    private bool cbSettingShowDigiCasesIn3ShapeTodayCount = true;
    public bool CbSettingShowDigiCasesIn3ShapeTodayCount
    {
        get => cbSettingShowDigiCasesIn3ShapeTodayCount;
        set
        {
            cbSettingShowDigiCasesIn3ShapeTodayCount = value;
            RaisePropertyChanged(nameof(CbSettingShowDigiCasesIn3ShapeTodayCount));
        }
    }

    private bool cbSettingModuleFolderSubscription = false;
    public bool CbSettingModuleFolderSubscription
    {
        get => cbSettingModuleFolderSubscription;
        set
        {
            cbSettingModuleFolderSubscription = value;
            RaisePropertyChanged(nameof(CbSettingModuleFolderSubscription));
        }
    }

    private bool cbSettingModuleAccountInfos = false;
    public bool CbSettingModuleAccountInfos
    {
        get => cbSettingModuleAccountInfos;
        set
        {
            cbSettingModuleAccountInfos = value;
            RaisePropertyChanged(nameof(CbSettingModuleAccountInfos));
        }
    }

    private bool cbSettingModuleLabnext = false;
    public bool CbSettingModuleLabnext
    {
        get => cbSettingModuleLabnext;
        set
        {
            cbSettingModuleLabnext = value;
            RaisePropertyChanged(nameof(CbSettingModuleLabnext));
        }
    }

    private bool cbSettingShowOtherUsersPanNumbers = false;
    public bool CbSettingShowOtherUsersPanNumbers
    {
        get => cbSettingShowOtherUsersPanNumbers;
        set
        {
            cbSettingShowOtherUsersPanNumbers = value;
            RaisePropertyChanged(nameof(CbSettingShowOtherUsersPanNumbers));
        }
    }

    private bool cbSettingModuleSmartOrderNames = false;
    public bool CbSettingModuleSmartOrderNames
    {
        get => cbSettingModuleSmartOrderNames;
        set
        {
            cbSettingModuleSmartOrderNames = value;
            RaisePropertyChanged(nameof(CbSettingModuleSmartOrderNames));
        }
    }

    private bool cbSettingModuleDebug = false;
    public bool CbSettingModuleDebug
    {
        get => cbSettingModuleDebug;
        set
        {
            cbSettingModuleDebug = value;
            RaisePropertyChanged(nameof(CbSettingModuleDebug));
        }
    }

    private bool cbSettingModulePrescriptionMaker = false;
    public bool CbSettingModulePrescriptionMaker
    {
        get => cbSettingModulePrescriptionMaker;
        set
        {
            cbSettingModulePrescriptionMaker = value;
            RaisePropertyChanged(nameof(CbSettingModulePrescriptionMaker));
        }
    }

    private bool cbSettingModulePendingDigitals = false;
    public bool CbSettingModulePendingDigitals
    {
        get => cbSettingModulePendingDigitals;
        set
        {
            cbSettingModulePendingDigitals = value;
            RaisePropertyChanged(nameof(CbSettingModulePendingDigitals));
        }
    }

    //private bool cbSettingShowDigiDetails = true;
    //public bool CbSettingShowDigiDetails
    //{
    //    get => cbSettingShowDigiDetails;
    //    set
    //    {
    //        cbSettingShowDigiDetails = value;
    //        RaisePropertyChanged(nameof(CbSettingShowDigiDetails));
    //    }
    //}

    private bool cbSettingWatchFolderPrescriptionMaker = true;
    public bool CbSettingWatchFolderPrescriptionMaker
    {
        get => cbSettingWatchFolderPrescriptionMaker;
        set
        {
            cbSettingWatchFolderPrescriptionMaker = value;
            RaisePropertyChanged(nameof(CbSettingWatchFolderPrescriptionMaker));
        }
    }

    private bool cbSettingOpenUpSironaScanFolder = true;
    public bool CbSettingOpenUpSironaScanFolder
    {
        get => cbSettingOpenUpSironaScanFolder;
        set
        {
            cbSettingOpenUpSironaScanFolder = value;
            RaisePropertyChanged(nameof(CbSettingOpenUpSironaScanFolder));
        }
    }

    private bool cbSettingExtractIteroZipFiles = true;
    public bool CbSettingExtractIteroZipFiles
    {
        get => cbSettingExtractIteroZipFiles;
        set
        {
            cbSettingExtractIteroZipFiles = value;
            RaisePropertyChanged(nameof(CbSettingExtractIteroZipFiles));
        }
    }

    #endregion Settings Tab Properties

    private bool labnextIconCanShowOn3ShapeListView = false;
    public bool LabnextIconCanShowOn3ShapeListView
    {
        get => labnextIconCanShowOn3ShapeListView;
        set
        {
            labnextIconCanShowOn3ShapeListView = value;
            RaisePropertyChanged(nameof(LabnextIconCanShowOn3ShapeListView));
        }
    }

    private string windowBackground = "#c9bf97";
    public string WindowBackground
    {
        get => windowBackground;
        set
        {
            windowBackground = value;
            RaisePropertyChanged(nameof(WindowBackground));
        }
    }

    private string classicColorSchemeWindowBackground = "#c9bf97";
    public string ClassicColorSchemeWindowBackground
    {
        get => classicColorSchemeWindowBackground;
        set
        {
            classicColorSchemeWindowBackground = value;
            RaisePropertyChanged(nameof(ClassicColorSchemeWindowBackground));
        }
    }

    private string colorSchemeWindowBackground = "#c9bf97";
    public string ColorSchemeWindowBackground
    {
        get => colorSchemeWindowBackground;
        set
        {
            colorSchemeWindowBackground = value;
            RaisePropertyChanged(nameof(ColorSchemeWindowBackground));
        }
    }

    private string modernColorSchemeWindowBackground = "#EEEEEE";
    public string ModernColorSchemeWindowBackground
    {
        get => modernColorSchemeWindowBackground;
        set
        {
            modernColorSchemeWindowBackground = value;
            RaisePropertyChanged(nameof(ModernColorSchemeWindowBackground));
        }
    }

    private List<AccountInfoModel> accountInfoList = [];
    public List<AccountInfoModel> AccountInfoList
    {
        get => accountInfoList;
        set
        {
            accountInfoList = value;
            RaisePropertyChanged(nameof(AccountInfoList));
        }
    }

    private string searchInAccountInfos = "";
    public string SearchInAccountInfos
    {
        get => searchInAccountInfos;
        set
        {
            searchInAccountInfos = value;
            RaisePropertyChanged(nameof(SearchInAccountInfos));
            SearchInAccountInfosMethod();
        }
    }

    private Dictionary<string, string> bgBorderColors = [];
    public Dictionary<string, string> BgBorderColors
    {
        get => bgBorderColors;
        set
        {
            bgBorderColors = value;
            RaisePropertyChanged(nameof(BgBorderColors));
        }
    }

    private string selectedAccountInfoCategory = "All";
    public string SelectedAccountInfoCategory
    {
        get => selectedAccountInfoCategory;
        set
        {
            selectedAccountInfoCategory = value;
            RaisePropertyChanged(nameof(SelectedAccountInfoCategory));
            GetAccountInfos();
        }
    }

    private Visibility mainMenuOpen = Visibility.Hidden;
    public Visibility MainMenuOpen
    {
        get => mainMenuOpen;
        set
        {
            mainMenuOpen = value;
            RaisePropertyChanged(nameof(MainMenuOpen));
        }
    }

    private List<string> accountInfoCategories = [];
    public List<string> AccountInfoCategories
    {
        get => accountInfoCategories;
        set
        {
            accountInfoCategories = value;
            RaisePropertyChanged(nameof(AccountInfoCategories));
        }
    }

    private List<OrderIssueModel> orderIssuesList = [];
    public List<OrderIssueModel> OrderIssuesList
    {
        get => orderIssuesList;
        set
        {
            orderIssuesList = value;
            RaisePropertyChanged(nameof(OrderIssuesList));
        }
    }

    private List<DuplicatePanNumberOrdersModel> panNrDuplicatesList = [];
    public List<DuplicatePanNumberOrdersModel> PanNrDuplicatesList
    {
        get => panNrDuplicatesList;
        set
        {
            panNrDuplicatesList = value;
            RaisePropertyChanged(nameof(PanNrDuplicatesList));

            if (PanNrDuplicatesList.Count > PanNrDuplicatesCount)
            {
                ShowWarningOfNewDuplicatedPanNumberUse();
            }

            PanNrDuplicatesCount = PanNrDuplicatesList.Count;
        }
    }

    private string panNrDuplicatesFontColor = "Red";
    public string PanNrDuplicatesFontColor
    {
        get => panNrDuplicatesFontColor;
        set
        {
            panNrDuplicatesFontColor = value;
            RaisePropertyChanged(nameof(PanNrDuplicatesFontColor));
        }
    }

    private SmartOrderNames2Page smartOrderNamesWindow = new();
    public SmartOrderNames2Page SmartOrderNamesWindow
    {
        get => smartOrderNamesWindow;
        set
        {
            smartOrderNamesWindow = value;
            RaisePropertyChanged(nameof(SmartOrderNamesWindow));
        }
    }


    private string labNextWebViewStatusText = "";
    public string LabNextWebViewStatusText
    {
        get => labNextWebViewStatusText;
        set
        {
            labNextWebViewStatusText = value;
            RaisePropertyChanged(nameof(LabNextWebViewStatusText));

            // Check login status whenever the status text changes
            CheckLabnextLoginStatus();

            if (!string.IsNullOrEmpty(LabNextWebViewStatusText) && _MainWindow is not null)
            {
                LabnextKeepAliveTimer.Stop();
                LabnextKeepAliveTimer.Start();
            }
        }
    }

    private bool parseLabnextHtml = false;
    public bool ParseLabnextHtml
    {
        get => parseLabnextHtml;
        set
        {
            parseLabnextHtml = value;
            RaisePropertyChanged(nameof(ParseLabnextHtml));
        }
    }

    private bool labnextWebviewIsLookingUpPanNumber = false;
    public bool LabnextWebviewIsLookingUpPanNumber
    {
        get => labnextWebviewIsLookingUpPanNumber;
        set
        {
            labnextWebviewIsLookingUpPanNumber = value;
            RaisePropertyChanged(nameof(LabnextWebviewIsLookingUpPanNumber));
        }
    }

    #endregion Properties


    #region RelayCommands

    #region Settings Tab RelayCommands
    public RelayCommand CbSettingGlassyEffectCommand { get; set; }
    public RelayCommand CbSettingShowAvailablePanCountCommand { get; set; }
    public RelayCommand CbSettingStartAppMinimizedCommand { get; set; }
    //public RelayCommand CbSettingShowBottomInfoBarCommand { get; set; }
    public RelayCommand CbSettingShowDigiCasesCommand { get; set; }
    //public RelayCommand CbSettingShowDigiDetailsCommand { get; set; }
    public RelayCommand CbSettingShowEmptyPanCountCommand { get; set; }
    public RelayCommand CbSettingWatchFolderPrescriptionMakerCommand { get; set; }
    public RelayCommand CbSettingOpenUpSironaScanFolderCommand { get; set; }
    public RelayCommand CbSettingExtractIteroZipFilesCommand { get; set; }
    public RelayCommand CbSettingShowPendingDigiCasesCommand { get; set; }
    public RelayCommand CbSettingKeepUserLoggedInLabnextCommand { get; set; }
    public RelayCommand CbSettingIncludePendingDigiCasesInNewlyArrivedCommand { get; set; }
    public RelayCommand CbSettingShowDigiPrescriptionsCountCommand { get; set; }
    public RelayCommand CbSettingShowDigiCasesIn3ShapeTodayCountCommand { get; set; }
    public RelayCommand CbSettingModuleFolderSubscriptionCommand { get; set; }
    public RelayCommand CbSettingModuleAccountInfosCommand { get; set; }
    public RelayCommand CbSettingModuleLabnextCommand { get; set; }
    public RelayCommand CbSettingShowOtherUsersPanNumbersCommand { get; set; }
    public RelayCommand CbSettingModuleSmartOrderNamesCommand { get; set; }
    public RelayCommand CbSettingModuleDebugCommand { get; set; }
    public RelayCommand CbSettingModulePrescriptionMakerCommand { get; set; }
    public RelayCommand CbSettingModulePendingDigitalsCommand { get; set; }


    public RelayCommand AddNewCustomerSuggestionCommand { get; set; }
    public RelayCommand DeleteCSCustomerCommand { get; set; }
    public RelayCommand DeleteCSSuggestionCommand { get; set; }


    #endregion Settings Tab RelayCommands



    public RelayCommand FocusOnDoctorsFieldCommand { get; set; }

    public RelayCommand FocusOnSsearchFieldCommand { get; set; }
    public RelayCommand FocusOnYearFieldCommand { get; set; }
    public RelayCommand FocusOnMonthFieldCommand { get; set; }

    public RelayCommand AssignOrderToLabnextCaseCommand { get; set; }



    public RelayCommand HideZipArchiveIconCommand { get; set; }
    public RelayCommand OpeniTeroExportFolderCommand { get; set; }

    public RelayCommand LookForUpdateCommand { get; set; }
    public RelayCommand OpenCloseMenuCommand { get; set; }
    public RelayCommand StartProgramUpdateCommand { get; set; }
    public RelayCommand FilterMenuItemCommand { get; set; }
    public RelayCommand HistoryMenuItemCommand { get; set; }
    public RelayCommand JumpToCasesOpenedForDesignNowCommand { get; set; }
    public RelayCommand ExpanderLoadedCommand { get; set; }
    public RelayCommand ExpanderCollapsedCommand { get; set; }
    public RelayCommand ItemClickedCommand { get; set; }
    public RelayCommand PanNumberClickedCommand { get; set; }
    public RelayCommand ArchivesItemClickedCommand { get; set; }
    public RelayCommand ArchivesItemClickedOnGlobalSearchCommand { get; set; }
    public RelayCommand ArchivesBaseFolderItemClickedCommand { get; set; }
    public RelayCommand ArchivesBaseFolderItemClickedOnGlobalSearchCommand { get; set; }
    public RelayCommand ItemRightClickedCommand { get; set; }
    //public RelayCommand GetInfoOn3ShapeOrderCommand { get; set; }
    public RelayCommand ClearSelectedDesignerNameAtIssuesCommand { get; set; }

    public RelayCommand GroupBySelectionChangedCommand { get; set; }
    public RelayCommand SearchLimitSelectionChangedCommand { get; set; }
    public RelayCommand ClearSearchStringCommand { get; set; }
    public RelayCommand SearchForTextCommand { get; set; }
    public RelayCommand SearchFieldClickedCommand { get; set; }
    public RelayCommand SearchFieldKeyDownCommand { get; set; }
    public RelayCommand SearchFieldArchivesClickedCommand { get; set; }
    public RelayCommand SearchFieldArchivesKeyDownCommand { get; set; }
    public RelayCommand Next50ResultOnArchivesSearchCommand { get; set; }
    public RelayCommand Previous50ResultOnArchivesSearchCommand { get; set; }
    public RelayCommand Next50ResultOnArchivesSearchOnArchivePageCommand { get; set; }
    public RelayCommand Previous50ResultOnArchivesSearchOnArchivePageCommand { get; set; }
    public RelayCommand SearchFieldKeyDownOnHomeCommand { get; set; }
    public RelayCommand SearchFieldKeyDownOnTabsCommand { get; set; }
    public RelayCommand SearchForCustomerFieldKeyUpOnTabsCommand { get; set; }
    public RelayCommand SearchFieldEnterKeyDownOnHomeCommand { get; set; }
    public RelayCommand ClearAllSearchCriteriaCommand { get; set; }
    public RelayCommand ClearCustomerCriteriaCommand { get; set; }
    public RelayCommand ClearYearCriteriaCommand { get; set; }
    public RelayCommand HideNotificationCommand { get; set; }

    public RelayCommand OpenUpOrderInfoWindowCommand { get; set; }
    public RelayCommand SearchForOrderByOrderIssueClickCommand { get; set; }
    public RelayCommand GenerateStCopyCommand { get; set; }
    public RelayCommand OpenUpRenameOrderWindowCommand { get; set; }
    public RelayCommand SelectTargetFolderCommand { get; set; }

    public RelayCommand PmAddNewPanNumberCommand { get; set; }
    public RelayCommand PmSelectTargetFolderCommand { get; set; }
    public RelayCommand PmOpenUpPrescriptionsCommand { get; set; }
    public RelayCommand PmMarkCaseAsRushCommand { get; set; }
    public RelayCommand PmAddToSentToListCommand { get; set; }
    public RelayCommand PmRemoveFromSentToListCommand { get; set; }
    public RelayCommand PmAddSendToLabelCommand { get; set; }
    public RelayCommand PmAddMissingLabelCommand { get; set; }
    public RelayCommand TakeAPanNumberCommand { get; set; }
    public RelayCommand PmCancelTakingANumberCommand { get; set; }
    public RelayCommand GrabAPanNumberCommand { get; set; }
    public RelayCommand ClickOnPanNumberCommand { get; set; }
    public RelayCommand HidePrescriptionPreviewPanelCommand { get; set; }
    public RelayCommand ShowPrescriptionPreviewPanelCommand { get; set; }

    public RelayCommand InconsistencyItemClickedCommand { get; set; }
    public RelayCommand CancelIgnoreInconsistencyOrderIDCommand { get; set; }
    public RelayCommand HitIgnoreInconsistencyOrderIDCommand { get; set; }

    public RelayCommand ShowFilterPanelCommand { get; set; }
    public RelayCommand HideFilterPanelCommand { get; set; }

    #region COMMENT RULES IN SETTINGS
    public RelayCommand AddNewCommentRuleCommand { get; set; }
    public RelayCommand DeleteSelectedCommentRuleCommand { get; set; }
    public RelayCommand CRCheckingRadioButtonCommand { get; set; }
    #endregion COMMENT RULES IN SETTINGS

    public RelayCommand PcCheckPanColorCommand { get; set; }

    #region Folder Subscription RelayCommands
    public RelayCommand FsCreateTodayFolderCommand { get; set; }
    public RelayCommand FsSearchFoldersCommand { get; set; }
    public RelayCommand ForceUpdatePendingDigiNumberListCommand { get; set; }
    public RelayCommand FsItemClickedCommand { get; set; }
    public RelayCommand LabnextIdClickedCommand { get; set; }
    public RelayCommand FsHideNotificationCommand { get; set; }
    public RelayCommand FsCopyFolderOverCommand { get; set; }
    public RelayCommand FsOpenFolderCommand { get; set; }
    public RelayCommand FsTriggerUpdateRequestCommand { get; set; }
    #endregion Folder Subscription RelayCommands

    public RelayCommand GsItemClickedCommand { get; set; }

    public RelayCommand BlinkWindowCommand { get; set; }
    public RelayCommand RunNotificationProgressCommand { get; set; }

    public RelayCommand SwitchToPrescriptionMakerTabCommand { get; set; }
    public RelayCommand SwitchToServerLogTabCommand { get; set; }
    public RelayCommand SwitchToAccountInfosTabCommand { get; set; }
    public RelayCommand SwitchToSettingsTabCommand { get; set; }
    public RelayCommand SwitchToPaymentTabCommand { get; set; }
    public RelayCommand SwitchToPanNrDuplicatesTabCommand { get; set; }
    public RelayCommand SwitchToOrderIssuesTabCommand { get; set; }
    public RelayCommand SwitchToFolderSubscriptionTabCommand { get; set; }
    public RelayCommand SwitchToPendingDigiCasesTabCommand { get; set; }
    public RelayCommand SwitchToDebugMessagesTabCommand { get; set; }
    public RelayCommand SwitchTo3ShapeOrdersTabCommand { get; set; }
    public RelayCommand MoveLabnextViewToFolderSubscriptionTabCommand { get; set; }
    public RelayCommand MoveLabnextViewToLabnextTabCommand { get; set; }
    public RelayCommand SwitchToArchivesTabCommand { get; set; }
    public RelayCommand SwitchToLabnextTabCommand { get; set; }
    public RelayCommand SwitchToHomeTabCommand { get; set; }
    public RelayCommand SwitchToSentOutCasesTabCommand { get; set; }
    public RelayCommand RequestDCASUpdateCommand { get; set; }
    public RelayCommand Refresh3ShapeListCommand { get; set; }
    public RelayCommand FocusOnSearchFieldCommand { get; set; }

    #region AccountInfos RelayCommands
    public RelayCommand OpenWebsiteCommand { get; set; }
    public RelayCommand StartApplicationCommand { get; set; }
    public RelayCommand CopyUserNameToClipboardCommand { get; set; }
    public RelayCommand CopyPasswordToClipboardCommand { get; set; }
    public RelayCommand ShowPasswordCommand { get; set; }
    public RelayCommand ClearAccInfoSearchCommand { get; set; }

    #endregion AccountInfos RelayCommands


    public RelayCommand ClickCommand { get; set; }

    public RelayCommand CancelLabnetLookupWindowCommand { get; set; }
    public RelayCommand LookupInLabnextByPanNumberCommand { get; set; }
    public RelayCommand LookupInLabnextByPtNameCommand { get; set; }

    public RelayCommand SearchForPanNumberInLabnextForFolderSubscriptionCommand { get; set; }
    public RelayCommand ReloadLabnextWebViewCommand { get; set; }
    public RelayCommand GoBackOnLabnextWebViewCommand { get; set; }
    public RelayCommand GoHomeOnLabnextWebViewCommand { get; set; }
    public RelayCommand ResetArchivesResultsCommand { get; set; }

    public RelayCommand ResetQuickSearchOnHomeTabCommand { get; set; }

    #endregion RelayCommands



    private readonly DispatcherTimer GeneralTimer = new();
    private readonly DispatcherTimer PrescriptionPreviewTimer = new();
    private readonly DispatcherTimer listUpdateTimer = new();
    private readonly DispatcherTimer notificationTimer = new();
    private readonly DispatcherTimer fsNotificationTimer = new();
    private readonly DispatcherTimer UpdateCheckTimer = new();
    private readonly DispatcherTimer ZipArchiveIconShowTimer = new();
    private readonly DispatcherTimer LabnextPanLookupTimer = new();
    private readonly DispatcherTimer LabnextLoadingHiderTimer = new();
    private readonly DispatcherTimer LabnextKeepAliveTimer = new();

    private static readonly BackgroundWorker bwZippingOrderArchives = new();
    private static readonly BackgroundWorker bwListCasesGlobal = new();
    private static readonly BackgroundWorker bwListCasesForPaymentIssueMatching = new();
    private static readonly BackgroundWorker bwListCases = new();
    private static readonly BackgroundWorker bwListArchivesCases = new();
    private static readonly BackgroundWorker bwBackgroundTasks = new();
    private static readonly BackgroundWorker bwGetSentOutIssues = new();
    private static readonly BackgroundWorker bwInitialTasks = new();
    private static readonly FileSystemWatcher fswPrescriptionMaker = new();
    private static readonly FileSystemWatcher fswTriosFolderWatcher = new();
    private static readonly FileSystemWatcher fswIteroZipFileWhatcher = new();
    private PdfToImageConverter imageConverter = new();


    public MainViewModel()
    {
        Instance = this;

        ClickCommand = new RelayCommand(o => TestCommandMethod(o));

        // classic color scheme
        //WindowBackground = "#c9bf97";
        //Modern color scheme

        ColorSchemeWindowBackground = ModernColorSchemeWindowBackground;
        WindowBackground = ColorSchemeWindowBackground;

        GeneralTimer.Tick += GeneralTimer_Tick;
        GeneralTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        GeneralTimer.Start();

        PrescriptionPreviewTimer.Tick += PrescriptionPreviewTimer_Tick;
        PrescriptionPreviewTimer.Interval = new TimeSpan(0, 0, 10);

        ZipArchiveIconShowTimer.Tick += ZipArchiveIconShowTimer_Tick;
        ZipArchiveIconShowTimer.Interval = new TimeSpan(0, 0, 6);

        listUpdateTimer.Tick += ListUpdateTimer_Tick;
        listUpdateTimer.Interval = new TimeSpan(0, 0, 30);
        listUpdateTimer.Start();

        LabnextPanLookupTimer.Tick += LabnextPanLookupTimer_Tick;
        LabnextPanLookupTimer.Interval = new TimeSpan(0, 0, 2);

        LabnextLoadingHiderTimer.Tick += LabnextLoadingHiderTimer_Tick;
        LabnextLoadingHiderTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);

        LabnextKeepAliveTimer.Tick += LabnextKeepAliveTimer_Tick;
        LabnextKeepAliveTimer.Interval = new TimeSpan(0, 3, 0);

        notificationTimer.Tick += NotificationTimer_Tick;
        notificationTimer.Interval = new TimeSpan(0, 0, 10);

        fsNotificationTimer.Tick += FsNotificationTimer_Tick;
        fsNotificationTimer.Interval = new TimeSpan(0, 0, 30);

        UpdateCheckTimer.Tick += UpdateCheckTimer_Tick;
        UpdateCheckTimer.Interval = new TimeSpan(0, 0, 6);
        UpdateCheckTimer.Start();

        int thisYear = DateTime.Now.Year;
        FilterYearItems.Add("All time");
        for (int i = thisYear; i > thisYear - 10; i--)
        {
            FilterYearItems.Add(i.ToString());
        }

        FilterYearItemSelected = "All time";

        FilterMonthItemSelected = "All months";

        LookForUpdateCommand = new RelayCommand(o =>
        {
            if (!LookingForUpdateNow)
                LookForUpdate();
        });

        OpenCloseMenuCommand = new RelayCommand(o =>
        {
            if (MainMenuOpen == Visibility.Hidden)
            {
                MainMenuOpen = Visibility.Visible;
                MainMenu.StaticInstance.FocusOnMainMenu();
            }
            else
                MainMenuOpen = Visibility.Hidden;
        });


        HideZipArchiveIconCommand = new RelayCommand(o => HideZipArchiveIcon());
        FocusOnDoctorsFieldCommand = new RelayCommand(o => FocusOnDoctorsField());
        FocusOnSearchFieldCommand = new RelayCommand(o => FocusOnSearchField());
        FocusOnYearFieldCommand = new RelayCommand(o => FocusOnYearField());
        FocusOnMonthFieldCommand = new RelayCommand(o => FocusOnMonthField());






        OpeniTeroExportFolderCommand = new RelayCommand(o => OpeniTeroExportFolderMethod());
        StartProgramUpdateCommand = new RelayCommand(o => StartProgramUpdate());
        FilterMenuItemCommand = new RelayCommand(o => FilterMenuItemClicked(o));
        HistoryMenuItemCommand = new RelayCommand(o => HistoryMenuItemClicked(o));
        JumpToCasesOpenedForDesignNowCommand = new RelayCommand(o => JumpToCasesOpenedForDesignNow());
        ExpanderLoadedCommand = new RelayCommand(o => ExpanderLoaded(o));
        ExpanderCollapsedCommand = new RelayCommand(o => ExpanderCollapsed(o));
        ItemClickedCommand = new RelayCommand(o => ItemClicked(o));
        PanNumberClickedCommand = new RelayCommand(o => PanNumberClicked(o));
        AssignOrderToLabnextCaseCommand = new RelayCommand(o => AssignOrderToLabnextCase(o));
        ArchivesItemClickedCommand = new RelayCommand(o => ArchivesItemClicked(o));
        ArchivesItemClickedOnGlobalSearchCommand = new RelayCommand(o => ArchivesItemClickedOnGlobalSearch(o));
        ArchivesBaseFolderItemClickedCommand = new RelayCommand(o => ArchivesBaseFolderItemClicked(o));
        ArchivesBaseFolderItemClickedOnGlobalSearchCommand = new RelayCommand(o => ArchivesBaseFolderItemClickedOnGlobalSearch(o));
        ItemRightClickedCommand = new RelayCommand(o => ItemRightClicked(o));
        ClearSelectedDesignerNameAtIssuesCommand = new RelayCommand(o => ClearSelectedDesignerNameAtIssues());
        GroupBySelectionChangedCommand = new RelayCommand(o => GroupList());
        SearchLimitSelectionChangedCommand = new RelayCommand(o => SearchLimitSelectionChanged());
        ClearSearchStringCommand = new RelayCommand(o => SearchString = "");
        SearchForTextCommand = new RelayCommand(o => SearchForText());
        SearchFieldClickedCommand = new RelayCommand(o => _MainWindow.tbSearch.Focus());
        SearchFieldKeyDownCommand = new RelayCommand(o => SearchFieldKeyDown());
        SearchFieldArchivesClickedCommand = new RelayCommand(o => _MainWindow.tbSearchArchives.Focus());
        SearchFieldArchivesKeyDownCommand = new RelayCommand(o => SearchFieldArchivesKeyDown());
        Next50ResultOnArchivesSearchCommand = new RelayCommand(o => Next50ResultOnArchivesSearch());
        Previous50ResultOnArchivesSearchCommand = new RelayCommand(o => Previous50ResultOnArchivesSearch());
        Next50ResultOnArchivesSearchOnArchivePageCommand = new RelayCommand(o => Next50ResultOnArchivesSearchOnArchivePage());
        Previous50ResultOnArchivesSearchOnArchivePageCommand = new RelayCommand(o => Previous50ResultOnArchivesSearchOnArchivePage());
        SearchFieldKeyDownOnHomeCommand = new RelayCommand(o => SearchFieldKeyDownOnHome());
        SearchFieldKeyDownOnTabsCommand = new RelayCommand(o => SearchFieldKeyDownOnTabs());
        SearchForCustomerFieldKeyUpOnTabsCommand = new RelayCommand(o => SearchFieldKeyDownOnTabs());
        SearchFieldEnterKeyDownOnHomeCommand = new RelayCommand(o => SearchFieldEnterKeyDownOnHome());
        ClearAllSearchCriteriaCommand = new RelayCommand(o => ClearAllSearchCriteria());
        ClearCustomerCriteriaCommand = new RelayCommand(o => ClearCustomerCriteria());
        ClearYearCriteriaCommand = new RelayCommand(o => ClearYearCriteria());
        HideNotificationCommand = new RelayCommand(o => HideNotification());

        OpenUpOrderInfoWindowCommand = new RelayCommand(o => OpenUpOrderInfoWindow());
        SearchForOrderByOrderIssueClickCommand = new RelayCommand(o => SearchForOrderByOrderIssueClick());
        GenerateStCopyCommand = new RelayCommand(o => GenerateStCopy());
        OpenUpRenameOrderWindowCommand = new RelayCommand(o => OpenUpRenameOrderWindow());

        PmAddNewPanNumberCommand = new RelayCommand(o => PmAddNewPanNumber(o));
        PmSelectTargetFolderCommand = new RelayCommand(o => PmSelectTargetFolder(o));
        PmOpenUpPrescriptionsCommand = new RelayCommand(o => PmOpenUpPrescriptions());
        PmMarkCaseAsRushCommand = new RelayCommand(o => PmMarkCaseAsRush());
        PmAddToSentToListCommand = new RelayCommand(o => PmAddToSentToList());
        PmRemoveFromSentToListCommand = new RelayCommand(o => PmRemoveFromSentToList());
        PmAddSendToLabelCommand = new RelayCommand(o => PmMarkCaseWithLabelSendTo());
        PmAddMissingLabelCommand = new RelayCommand(o => PmMarkCaseWithLabelMissing());
        TakeAPanNumberCommand = new RelayCommand(o => TakeAPanNumber());
        ClickOnPanNumberCommand = new RelayCommand(o => ClickOnPanNumber(o));
        HidePrescriptionPreviewPanelCommand = new RelayCommand(o => HidePrescriptionPreviewPanel());
        ShowPrescriptionPreviewPanelCommand = new RelayCommand(o => ShowPrescriptionPreviewPanel());

        OpenWebsiteCommand = new RelayCommand(o => OpenWebsite(o));
        StartApplicationCommand = new RelayCommand(o => StartApplication(o));
        CopyUserNameToClipboardCommand = new RelayCommand(o => CopyUserNameToClipboard(o));
        CopyPasswordToClipboardCommand = new RelayCommand(o => CopyPasswordToClipboard(o));
        ShowPasswordCommand = new RelayCommand(o => ShowPassword(o));
        ClearAccInfoSearchCommand = new RelayCommand(o => ClearAccInfoSearch());

        ShowFilterPanelCommand = new RelayCommand(o => ShowFilterPanelMethod());
        HideFilterPanelCommand = new RelayCommand(o => HideFilterPanelMethod());

        PmCancelTakingANumberCommand = new RelayCommand(o => { ShowingTakeANumberPanel = Visibility.Hidden; });
        GrabAPanNumberCommand = new RelayCommand(o =>
        {
            ShowingTakeANumberPanel = Visibility.Visible;
            PmSavedPrescription = null;
        });

        ChangeColorCommand = new RelayCommand(o => ChangeColor());
        AddNewNumberCommand = new RelayCommand(o => AddNewNumber());
        
        RegisterApplicationCommand = new RelayCommand(o => RegisterApplication());
        GetCaseInfoByLabnextIDCommand = new RelayCommand(o => GetCaseInfoByLabnextID(o));

        CancelLabnetLookupWindowCommand = new RelayCommand(o => { IsLabnextLookupIsOpen = false; ListUpdateable = true; });
        LookupInLabnextByPanNumberCommand = new RelayCommand(o => LookupInLabnextByPanNumber());
        LookupInLabnextByPtNameCommand = new RelayCommand(o => LookupInLabnextByPtName(o));

        SearchForPanNumberInLabnextForFolderSubscriptionCommand = new RelayCommand(o => SearchForPanNumberInLabnextForFolderSubscription());
        ReloadLabnextWebViewCommand = new RelayCommand(o => ReloadLabnextWebView());
        GoBackOnLabnextWebViewCommand = new RelayCommand(o => _MainWindow.webviewLabnext.GoBack());
        GoHomeOnLabnextWebViewCommand = new RelayCommand(o => _MainWindow.webviewLabnext.Source = new Uri(LabnextUrl));

        ResetArchivesResultsCommand = new RelayCommand(o => ResetArchivesResults());

        ResetQuickSearchOnHomeTabCommand = new RelayCommand(o => ResetQuickSearchOnHomeTab());

        #region PAYMENT ORDER LISTS COMMANDS
        SwitchTo30DaysCommand = new RelayCommand(o => SwitchTo30Days(o));
        SwitchTo60DaysCommand = new RelayCommand(o => SwitchTo60Days(o));
        SwitchTo90DaysCommand = new RelayCommand(o => SwitchTo90Days(o));
        OpenLabnextUrlCommand = new RelayCommand(o => OpenLabnextUrl(o));
        LookupInLabnextByPtNameExternalCommand = new RelayCommand(o => LookupInLabnextByPtNameExternal(o));

        HistoricalFirstPageCommand = new RelayCommand(o => HistoricalFirstPage(o));
        HistoricalPreviousPageCommand = new RelayCommand(o => HistoricalPreviousPage(o));
        HistoricalNextPageCommand = new RelayCommand(o => HistoricalNextPage(o));
        HistoricalLastPageCommand = new RelayCommand(o => HistoricalLastPage(o));

        UnpaidFirstPageCommand = new RelayCommand(o => UnpaidFirstPage(o));
        UnpaidPreviousPageCommand = new RelayCommand(o => UnpaidPreviousPage(o));
        UnpaidNextPageCommand = new RelayCommand(o => UnpaidNextPage(o));
        UnpaidLastPageCommand = new RelayCommand(o => UnpaidLastPage(o));

        LoadStatisticsCommand = new RelayCommand(o => LoadPayPeriodStatistics(o));

        // Initialize BackgroundWorkers
        bwLoadHistoricalCases.DoWork += BwLoadHistoricalCases_DoWork;
        bwLoadHistoricalCases.RunWorkerCompleted += BwLoadHistoricalCases_RunWorkerCompleted;

        bwLoadUnpaidCases.DoWork += BwLoadUnpaidCases_DoWork;
        bwLoadUnpaidCases.RunWorkerCompleted += BwLoadUnpaidCases_RunWorkerCompleted;
        #endregion

        StatsServersComputerName = ReadStatsSetting("ServerComputerName");

        #region COMMENT RULES IN SETTINGS
        AddNewCommentRuleCommand = new RelayCommand(o => AddNewCommentRuleMethod());
        DeleteSelectedCommentRuleCommand = new RelayCommand(o => DeleteSelectedCommentRuleMethod());
        CRCheckingRadioButtonCommand = new RelayCommand(o => CRCheckingRadioButtonMethod(o));
        #endregion COMMENT RULES IN SETTINGS

        #region Folder Subscription RelayCommands
        SelectTargetFolderCommand = new RelayCommand(o => SelectTargetFolder());
        FsCreateTodayFolderCommand = new RelayCommand(o => FsCreateTodayFolder());
        FsSearchFoldersCommand = new RelayCommand(o => FsSearchFolders());
        FsItemClickedCommand = new RelayCommand(o => FsItemClicked(o));
        ForceUpdatePendingDigiNumberListCommand = new RelayCommand(o => FillUpPendingDigiCaseNumberList(true));
        FsHideNotificationCommand = new RelayCommand(o => FsHideNotification());
        FsCopyFolderOverCommand = new RelayCommand(o => FsCopyFolderOver(o));
        FsOpenFolderCommand = new RelayCommand(o => FsOpenFolder(o));
        FsTriggerUpdateRequestCommand = new RelayCommand(o => FsTriggerUpdateRequest());
        #endregion Folder Subscription RelayCommands

        GsItemClickedCommand = new RelayCommand(o => GsItemClicked(o));
        LabnextIdClickedCommand = new RelayCommand(o => LabnextIdClicked(o));

        InconsistencyItemClickedCommand = new RelayCommand(o => InconsistencyItemClicked(o));
        CancelIgnoreInconsistencyOrderIDCommand = new RelayCommand(o => CancelIgnoreInconsistencyOrderIDMethod());
        HitIgnoreInconsistencyOrderIDCommand = new RelayCommand(o => HitIgnoreInconsistencyOrderIDMethod());

        CbSettingGlassyEffectCommand = new RelayCommand(o => CbSettingGlassyEffectMethod());

        CbSettingStartAppMinimizedCommand = new RelayCommand(o => CbSettingStartAppMinimizedMethod());
        //CbSettingShowBottomInfoBarCommand = new RelayCommand(o => CbSettingShowBottomInfoBarMethod());
        CbSettingShowDigiCasesCommand = new RelayCommand(o => CbSettingShowDigiCasesMethod());
        //CbSettingShowDigiDetailsCommand = new RelayCommand(o => CbSettingShowDigiDetailsMethod());
        CbSettingWatchFolderPrescriptionMakerCommand = new RelayCommand(o => CbSettingWatchFolderPrescriptionMakerMethod());
        CbSettingOpenUpSironaScanFolderCommand = new RelayCommand(o => CbSettingOpenUpSironaScanFolderMethod());
        CbSettingExtractIteroZipFilesCommand = new RelayCommand(o => CbSettingExtractIteroZipFilesMethod());
        CbSettingShowPendingDigiCasesCommand = new RelayCommand(o => CbSettingShowPendingDigiCasesMethod());
        CbSettingKeepUserLoggedInLabnextCommand = new RelayCommand(o => CbSettingKeepUserLoggedInLabnextMethod());
        CbSettingIncludePendingDigiCasesInNewlyArrivedCommand = new RelayCommand(o => CbSettingIncludePendingDigiCasesInNewlyArrivedMethod());
        CbSettingShowEmptyPanCountCommand = new RelayCommand(o => CbSettingShowEmptyPanCountMethod());
        CbSettingShowDigiPrescriptionsCountCommand = new RelayCommand(o => CbSettingShowDigiPrescriptionsCountMethod());

        CbSettingShowDigiCasesIn3ShapeTodayCountCommand = new RelayCommand(o => CbSettingShowDigiCasesIn3ShapeTodayCountMethod());
        CbSettingModuleFolderSubscriptionCommand = new RelayCommand(o => CbSettingModuleFolderSubscriptionMethod());
        CbSettingModuleAccountInfosCommand = new RelayCommand(o => CbSettingModuleAccountInfosMethod());
        CbSettingModuleLabnextCommand = new RelayCommand(o => CbSettingModuleLabnextMethod());
        CbSettingShowOtherUsersPanNumbersCommand = new RelayCommand(o => CbSettingShowOtherUsersPanNumbersMethod());
        CbSettingModuleSmartOrderNamesCommand = new RelayCommand(o => CbSettingModuleSmartOrderNamesMethod());
        CbSettingModuleDebugCommand = new RelayCommand(o => CbSettingModuleDebugMethod());
        CbSettingModulePrescriptionMakerCommand = new RelayCommand(o => CbSettingModulePrescriptionMakerMethod());
        CbSettingModulePendingDigitalsCommand = new RelayCommand(o => CbSettingModulePendingDigitalsMethod());


        AddNewCustomerSuggestionCommand = new RelayCommand(o => AddNewCustomerSuggestionMethod());
        DeleteCSCustomerCommand = new RelayCommand(o => DeleteCSCustomerMethod());
        DeleteCSSuggestionCommand = new RelayCommand(o => DeleteCSSuggestionMethod());



        BlinkWindowCommand = new RelayCommand(o => BlinkWindow(o));
        RunNotificationProgressCommand = new RelayCommand(o => BlinkWindow());
        SwitchToSettingsTabCommand = new RelayCommand(o => SwitchToSettingsTab());
        SwitchToPaymentTabCommand = new RelayCommand(o => SwitchToPaymentTab());
        SwitchToPrescriptionMakerTabCommand = new RelayCommand(o => SwitchToPrescriptionMakerTab());
        SwitchToServerLogTabCommand = new RelayCommand(o => SwitchToServerLogTab());
        SwitchToAccountInfosTabCommand = new RelayCommand(o => SwitchToAccountInfosTab());
        SwitchToPanNrDuplicatesTabCommand = new RelayCommand(o => SwitchToPanNrDuplicatesTab());
        SwitchToOrderIssuesTabCommand = new RelayCommand(o => SwitchToOrderIssuesTab());
        SwitchToFolderSubscriptionTabCommand = new RelayCommand(o => SwitchToFolderSubscriptionTab());
        SwitchToDebugMessagesTabCommand = new RelayCommand(o => SwitchToDebugMessagesTab());
        SwitchTo3ShapeOrdersTabCommand = new RelayCommand(o => SwitchTo3ShapeOrdersTab());
        MoveLabnextViewToFolderSubscriptionTabCommand = new RelayCommand(o => MoveLabnextViewToFolderSubscriptionTab());
        MoveLabnextViewToLabnextTabCommand = new RelayCommand(o => MoveLabnextViewToLabnextTab());

        SwitchToArchivesTabCommand = new RelayCommand(o => SwitchToArchivesTab());
        SwitchToLabnextTabCommand = new RelayCommand(o => SwitchToLabnextTab());
        SwitchToHomeTabCommand = new RelayCommand(o => SwitchToHomeTab());
        Refresh3ShapeListCommand = new RelayCommand(o => Refresh3ShapeList());
        SwitchToSentOutCasesTabCommand = new RelayCommand(o => SwitchToSentOutCasesTab());
        SwitchToPendingDigiCasesTabCommand = new RelayCommand(o => SwitchToPendingDigiCasesTab());

        RequestDCASUpdateCommand = new RelayCommand(o => RequestDCASUpdate());


        PcCheckPanColorCommand = new RelayCommand(o => PcCheckPanColor());


        //SMessageButtonClickCommand = new RelayCommand(o => SMessageButtonClick(o));


        bwInitialTasks.DoWork += InitialTasksAtApplicationStartup_DoWork;
        bwInitialTasks.RunWorkerCompleted += InitialTasksAtApplicationStartup_RunWorkerCompleted;

        bwListCases.DoWork += ListCases_DoWork;
        bwListCases.RunWorkerCompleted += ListCases_RunWorkerCompleted;
        bwListCases.WorkerSupportsCancellation = true;

        bwListArchivesCases.DoWork += ListArchivesCases_DoWork;
        bwListArchivesCases.RunWorkerCompleted += ListArchivesCases_RunWorkerCompleted;
        bwListArchivesCases.WorkerSupportsCancellation = true;

        bwListCasesGlobal.DoWork += ListCasesGlobal_DoWork;
        bwListCasesGlobal.RunWorkerCompleted += ZippingOrderArchives_RunWorkerCompleted;
        bwListCasesGlobal.WorkerSupportsCancellation = true;

        bwListCasesForPaymentIssueMatching.DoWork += ListCasesForPaymentIssueMatching_DoWork;
        bwListCasesForPaymentIssueMatching.RunWorkerCompleted += ListCasesForPaymentIssueMatching_RunWorkerCompleted;
        bwListCasesForPaymentIssueMatching.WorkerSupportsCancellation = true;


        bwZippingOrderArchives.DoWork += ZippingOrderArchives_DoWork;
        bwZippingOrderArchives.RunWorkerCompleted += ZippingOrderArchives_RunWorkerCompleted;
        bwZippingOrderArchives.WorkerSupportsCancellation = true;

        bwBackgroundTasks.DoWork += BwBackgroundTasks_DoWork;
        bwBackgroundTasks.RunWorkerCompleted += BwBackgroundTasks_RunWorkerCompleted;

        bwGetSentOutIssues.DoWork += BwGetSentOutIssues_DoWork;
        bwGetSentOutIssues.RunWorkerCompleted += BwGetSentOutIssues_RunWorkerCompleted;
        bwGetSentOutIssues.WorkerSupportsCancellation = true;


        DataView = CollectionViewSource.GetDefaultView(Current3ShapeOrderList);

        #region accountinfo bordercolors by category
        // for accountinfo bordercolors by category
        bgBorderColors.TryAdd("#466f69", "");
        bgBorderColors.TryAdd("#78804f", "");
        bgBorderColors.TryAdd("#7d5f48", "");
        bgBorderColors.TryAdd("#7a504c", "");
        bgBorderColors.TryAdd("#485c7a", "");
        bgBorderColors.TryAdd("#46596F", "");
        bgBorderColors.TryAdd("#7a4076", "");
        bgBorderColors.TryAdd("#67467d", "");
        bgBorderColors.TryAdd("#7a464f", "");

        bgBorderColors.TryAdd("#466f68", "");
        bgBorderColors.TryAdd("#78804e", "");
        bgBorderColors.TryAdd("#7d5f47", "");
        bgBorderColors.TryAdd("#7a504b", "");
        bgBorderColors.TryAdd("#485c79", "");
        bgBorderColors.TryAdd("#46596E", "");
        bgBorderColors.TryAdd("#7a4075", "");
        bgBorderColors.TryAdd("#67467c", "");
        bgBorderColors.TryAdd("#7a464d", "");

        bgBorderColors.TryAdd("#466f67", "");
        bgBorderColors.TryAdd("#78804d", "");
        bgBorderColors.TryAdd("#7d5f46", "");
        bgBorderColors.TryAdd("#7a504c", "");
        bgBorderColors.TryAdd("#485c78", "");
        bgBorderColors.TryAdd("#46596d", "");
        bgBorderColors.TryAdd("#7a4074", "");
        bgBorderColors.TryAdd("#67467b", "");
        bgBorderColors.TryAdd("#7a464c", "");

        bgBorderColors.TryAdd("#466f66", "");
        bgBorderColors.TryAdd("#78804c", "");
        bgBorderColors.TryAdd("#7d5f45", "");
        bgBorderColors.TryAdd("#7a504b", "");
        bgBorderColors.TryAdd("#485c77", "");
        bgBorderColors.TryAdd("#46596c", "");
        bgBorderColors.TryAdd("#7a4073", "");
        bgBorderColors.TryAdd("#67467a", "");
        bgBorderColors.TryAdd("#7a464b", "");

        bgBorderColors.TryAdd("#466f65", "");
        bgBorderColors.TryAdd("#78804b", "");
        bgBorderColors.TryAdd("#7d5f44", "");
        bgBorderColors.TryAdd("#7a504c", "");
        bgBorderColors.TryAdd("#485c76", "");
        bgBorderColors.TryAdd("#46596b", "");
        bgBorderColors.TryAdd("#7a4072", "");
        bgBorderColors.TryAdd("#674679", "");
        bgBorderColors.TryAdd("#7a464b", "");
        #endregion accountinfo bordercolors by category


        BuildCustomerSuggestionsList();
    }

    private async void GetCaseInfoByLabnextID(object o)
    {
        int? labnextID = (int?)o;

        var config = new DdxConfig
        {
            DdxHost = "www.ddxdental.com",
            AppPublicKey = "32b9aa6c9b1eef69e637f032405ba6697a1321d9189fbb91bc3c665c15fdd531",
            AppPrivateKey = "70d36154d363fb5f9cf7842605d518fce21e6bba5521bb4f6daee5dd8755a1d7"
        };



        using var client = new DdxClient(config);

       
        try
        {
            int caseId = 18732;
            await client.GetApplicationInfoAsync();
            CaseInfoResponse caseInfo = await client.GetCaseInfoByIdAsync(caseId);
            Debug.WriteLine(caseInfo.ToString());

            var account = await client.GetAccountAsync(caseInfo.PracticeId.ToString());
                Debug.WriteLine(account.Name);           // "Mike's Dental Practice"
                Debug.WriteLine(account.Primary?.Email); // "mcaplan@labnet.net"
            }
        catch (DdxApiException ex) when (ex.ErrorCode == 1)
        {
            // Error 1 on case_get usually means the case ID doesn't exist,
            // or it doesn't belong to this practice
            Debug.WriteLine($"Full message: {ex.Message}");
        }
        catch (DdxApiException ex)
        {
            Debug.WriteLine($"DDX error {ex.ErrorCode}: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Network error: {ex.Message}");
        }

        //try
        //{
        //    // Verify keys and get application info
        //    var app = await client.GetApplicationInfoAsync();
        //    config.PracticeId = app.ParentId;   // set it once here
        //    Debug.WriteLine("================================");
        //    Debug.WriteLine(app.Name);
        //    Debug.WriteLine("================================");
        //    // Get case info
        //    int caseId = 18732;
        //    var caseInfo = await client.GetCaseInfoAsync(caseId);
        //    Debug.WriteLine(caseInfo.ToString());
        //    Debug.WriteLine("================================");
        //}
        //catch (DdxApiException ex) when (ex.ErrorCode == 2)
        //{
        //    Debug.WriteLine("Invalid signature. Check your AppPrivateKey.");
        //}
        //catch (DdxApiException ex) when (ex.ErrorCode == 3)
        //{
        //    Debug.WriteLine("Invalid application key. Check your AppPublicKey.");
        //}
        //catch (DdxApiException ex) when (ex.ErrorCode == 4)
        //{
        //    Debug.WriteLine("Invalid authentication data. Check your DdxConfig.");
        //}
        //catch (DdxApiException ex) when (ex.ErrorCode == 6)
        //{
        //    Debug.WriteLine($"API method not found. Check the endpoint name. ({ex.Message})");
        //}
        //catch (DdxApiException ex) when (ex.ErrorCode == 12)
        //{
        //    Debug.WriteLine("DDX requires HTTPS. Check your DdxHost.");
        //}
        //catch (DdxApiException ex)
        //{
        //    Debug.WriteLine($"DDX error {ex.ErrorCode}: {ex.Message}");
        //}
        //catch (HttpRequestException ex)
        //{
        //    Debug.WriteLine($"Network error: {ex.Message}");
        //}
        //catch (TaskCanceledException)
        //{
        //    Debug.WriteLine("Request timed out.");
        //}
    }

    private void RegisterApplication()
    {
        var config = new DdxConfig
        {
            DdxHost = "www.ddxdental.com"
        };

        using var client = new DdxClient(config);

        string url = client.GetRegistrationUrl(
                appName: "Stats2027",
                role: "LAB",
                scopes: new[] { "lab.case.read", "lab.account.read" });

        // Open it in the browser
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
        //```

        //The method builds a URL like:
        //```
        //https://www.ddxdental.com/application_register?name=MyApp&role=PRACTICE
        //
        //The remote application "Stats2027" has been approved to use your DDX account on your behalf.

        //        To finalize the registration process, you will need to supply the following details to your remote application:

        //Remote Application Public Key
        //948c367accc97589b44150f8e74c5a5ff79eb6004ac58566cfbe6ffdda968363
        //Remote Application Private Key
        //38c4bdd6919184305d3b6291fc5c783682b602728761e6a95c0a200321fe78f3
    }

    private void ClearSelectedDesignerNameAtIssues()
    {
        SelectedDesignerPaymentSummary = null;
        PaymentCasesIssueListForDesigner.Clear();
        SearchOnlyForSameDesigner = true;
        ShowCaseFromCloseDateRangeOnly = true;
        PossibleOrdersFrom3ShapeForLabnextMatch.Clear();
    }

    private void SearchForText()
    {
        Search(SearchString);
        ClearAllSearchCriteria();
        SwitchTo3ShapeOrdersTab();
    }

    private void ClearAllSearchCriteria()
    {
        SearchString = string.Empty;
        CustomerSearchString = string.Empty;
        FilterYearItemSelected = "All time";
        FilterMonthItemSelected = "All months";
        SearchFieldKeyDownOnTabs();
        GlobalSearchResultArchives = [];
        GlobalSearchResult3Shape = [];
    }

    private void ClearCustomerCriteria()
    {
        CustomerSearchString = string.Empty;
        SearchFieldKeyDownOnTabs();
    }
    private void ClearYearCriteria()
    {
        FilterYearItemSelected = "All time";
        FilterMonthItemSelected = "All months";
        SearchFieldKeyDownOnTabs();
    }

    private void ReloadLabnextWebView()
    {
        if (!_MainWindow.webviewLabnext.IsInitialized)
        {
            return;
        }

        if (_MainWindow.FolderSubscriptionTabPanel.Children.Contains(_MainWindow.LabnextView))
        {
            _MainWindow.webviewLabnext.ZoomFactor = 0.75;
        }

        _MainWindow.webviewLabnext.Reload();
    }

    private void LabnextKeepAliveTimer_Tick(object? sender, EventArgs e)
    {
        if (LabnextCanReload && _MainWindow.webviewLabnext.IsInitialized && CbSettingKeepUserLoggedInLabnext)
        {
            try
            {
                ReloadLabnextWebView();
            }
            catch { }
        }
    }

    private void ResetQuickSearchOnHomeTab()
    {
        SelectedGlobalSearchResult = new();
        SearchStringGlobal = "";
        GlobalSearchResult.Clear();
    }

    private void LabnextLoadingHiderTimer_Tick(object? sender, EventArgs e)
    {
        if (CbSettingModuleLabnext)
        {
            if (LabNextWebViewStatusText.Contains("/cases/pan/id/"))
                LabnextWebviewIsLookingUpPanNumber = true;
            else
                LabnextWebviewIsLookingUpPanNumber = false;
        }
        else
            LabnextLoadingHiderTimer.Stop();
    }

    private async void LabnextPanLookupTimer_Tick(object? sender, EventArgs e)
    {
        if (LabNextWebViewStatusText.Contains("/cases/pan/id/"))
        {
            if (_MainWindow.webviewLabnext.CanGoBack)
                _MainWindow.webviewLabnext.GoBack();
            else
                _MainWindow.webviewLabnext.Source = new Uri(LabnextUrl);

            await Task.Delay(500);
            SearchForPanNumberInLabnextForFolderSubscription();
        }
    }

    private void ResetArchivesResults()
    {
        ArchiveResultOffset = 0;
        ArchiveResultOffsetOnArchivePage = 0;
        ArchivesCount = 0;
        ArchivesCountText = "";
        CurrentArchivesList.Clear();
        _MainWindow.listViewArchives.ItemsSource = CurrentArchivesList;
        _MainWindow.listViewArchives.Items.Refresh();
    }

    private void LookupInLabnextByPtName(object obj)
    {
        string commandParam = obj as string;
        if (SelectedItem is not null && CbSettingModuleLabnext)
        {
            string firstName = "";
            string lastName = "";
            if (SelectedItem.Patient_FirstName is not null)
            {
                firstName = $"{SelectedItem.Patient_FirstName.Trim()}";
                firstName = RemoveNumbers().Replace(firstName, string.Empty).Trim();
                firstName = firstName.ToUpper()
                                     .Replace("_", "")
                                     .Replace(",", "")
                                     .Replace("%25", "")
                                     //.Replace(" ", "+")
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
                if (firstName.Equals('-'))
                    firstName = "";
            }

            if (SelectedItem.Patient_LastName is not null)
            {
                lastName = $"{SelectedItem.Patient_LastName.Trim()}";
                lastName = RemoveNumbers().Replace(lastName, string.Empty).Trim();
                lastName = lastName.ToUpper()
                                   .Replace("_", "")
                                   .Replace(",", "")
                                   .Replace("%25", "")
                                   //.Replace(" ", "+")
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

                if (lastName.Equals('-'))
                    lastName = "";
            }

            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            {
                ShowMessageBox("Error", "Cannot lookup this case by patient name.", SMessageBoxButtons.Close, NotificationIcon.Error, 10, _MainWindow);
                return;
            }

            string searcString = "";
            

            if (commandParam == "firstname")
            {
                if (!string.IsNullOrEmpty(firstName.Trim()))
                    searcString = Uri.EscapeDataString($"{firstName}");
            }
            else if (commandParam == "lastname")
            {
                if (!string.IsNullOrEmpty(lastName.Trim()))
                    searcString = Uri.EscapeDataString($"{lastName}");
            }
            else
            {
                searcString = Uri.EscapeDataString($"{firstName} {lastName}").Trim();
            }

            if (string.IsNullOrEmpty(searcString.Trim()))
                return;

            Uri link = new(HttpUtility.UrlPathEncode($"{LabnextUrl}default/search/?q=" + searcString + "&search_type=all"), UriKind.Absolute);

            _MainWindow.webviewLabnext.Source = link;
            LabNextWebViewStatusText = link.ToString().Replace($"https://{LabnextLabID}.labnext.net/lab", "");
            SwitchToLabnextTab();
        }

        IsLabnextLookupIsOpen = false;
        ListUpdateable = true;
    }

    private async void LookupInLabnextByPanNumber()
    {
        if (SelectedItem is not null && CbSettingModuleLabnext)
        {
            string caseId = await LookUpCaseIdInLabnextByPanNumber(SelectedItem.PanNumber);

            if (caseId == "notloggedin")
            {
                IsLabnextLookupIsOpen = false;
                ListUpdateable = true;
                return;
            }

            if (string.IsNullOrEmpty(caseId))
            {
                IsLabnextLookupIsOpen = false;
                ListUpdateable = true;
                return;
            }

            Uri link = new(HttpUtility.UrlPathEncode($"{LabnextUrl}cases/case/id/{caseId}"), UriKind.Absolute);

            _MainWindow.webviewLabnext.Source = link;
            LabNextWebViewStatusText = link.ToString().Replace($"https://{LabnextLabID}.labnext.net/lab", "");
            SwitchToLabnextTab();
        }
        IsLabnextLookupIsOpen = false;
        ListUpdateable = true;
    }

    private async Task<string> LookUpCaseIdInLabnextByPanNumber(string panNumber)
    {
        LabnextPanLookupTimer.Start();
        string url = $"{LabnextUrl}cases/pan/id/{panNumber}";
        string caseId = "";
        _MainWindow.webviewLabnext.Source = new Uri(url);
        LabNextWebViewStatusText = url.Replace($"https://{LabnextLabID}.labnext.net/lab", "");

        await Task.Delay(1000);
        caseId = LabnextCaseID;


        if (!string.IsNullOrEmpty(caseId))
            LabnextPanLookupTimer.Stop();

        return caseId;
    }

    private void CoreWebView2_WebResourceResponseReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        var result = e.Response.GetContentAsync().GetAwaiter();
        result.OnCompleted(() =>
        {
            try
            {
                var res = result.GetResult();
                StreamReader reader = new(res);
                string json = reader.ReadToEnd();
                if (!json.StartsWith('{'))
                    return;

                LabNextObjectResponse? model = JsonConvert.DeserializeObject<LabNextObjectResponse>(json);
                if (model is not null)
                {
                    if (model.caseId is not null)
                        LabnextCaseID = model.caseId;

                    if (model.success is not null)
                    {
                        if (model.success == "false")
                            _MainWindow.webviewLabnext.GoBack();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e + " Error: " + e.LineNumber());
            }
        });
    }

    private void ZippingOrderArchives_DoWork(object? sender, DoWorkEventArgs e)
    {
        var data = (FolderData)e.Argument!;
        string ExportPath = data.FolderName!;
        string orderId = data.OrderId!;
        string sourceFolder = data.SourcePath!;

        sourceFolder = Path.Combine(sourceFolder, orderId);

        if (File.Exists(ExportPath + orderId + ".zip"))
            File.Delete(ExportPath + orderId + ".zip");

        ExportingZipArchiveNow = true;

        try
        {
            ZipFile.CreateFromDirectory(sourceFolder, ExportPath + "\\" + orderId + ".zip", CompressionLevel.Optimal, true);
            ExportingZipArchiveNow = false;
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ShowMessageBox("Error", $"An error occured during export: {ex.Message}", SMessageBoxButtons.Close, NotificationIcon.Error, 10, _MainWindow);
            }));
        }

        Thread.Sleep(1500);
    }

    private void Next50ResultOnArchivesSearch()
    {
        if (_MainWindow.mainTabControl.SelectedItem == _MainWindow.ThreeShapeTab)
        {
            ArchiveResultOffset++;
            SearchFieldKeyDownOnTabs();
        }
        else
        {
            ArchiveResultOffset++;
            SearchFieldKeyDownOnHome();
        }
    }

    private void Previous50ResultOnArchivesSearch()
    {
        if (_MainWindow.mainTabControl.SelectedItem == _MainWindow.ThreeShapeTab)
        {
            if (ArchiveResultOffset > 0)
            {
                ArchiveResultOffset--;
                SearchFieldKeyDownOnTabs();
            }
        }
        else
        {
            if (ArchiveResultOffset > 0)
            {
                ArchiveResultOffset--;
                SearchFieldKeyDownOnHome();
            }
        }
    }

    private void Next50ResultOnArchivesSearchOnArchivePage()
    {
        if (_MainWindow.mainTabControl.SelectedItem == _MainWindow.ThreeShapeTab)
        {
            ArchiveResultOffsetOnArchivePage++;
            SearchFieldKeyDownOnTabs();
        }
        else
        {
            ArchiveResultOffsetOnArchivePage++;
            SearchFieldArchivesKeyDown();
        }
    }

    private void Previous50ResultOnArchivesSearchOnArchivePage()
    {
        if (_MainWindow.mainTabControl.SelectedItem == _MainWindow.ThreeShapeTab)
        {
            if (ArchiveResultOffsetOnArchivePage > 0)
            {
                ArchiveResultOffsetOnArchivePage--;
                SearchFieldKeyDownOnTabs();
            }
        }
        else
        {
            if (ArchiveResultOffsetOnArchivePage > 0)
            {
                ArchiveResultOffsetOnArchivePage--;
                SearchFieldArchivesKeyDown();
            }
        }
    }

    private void OpeniTeroExportFolderMethod()
    {
        OpenUpFolder("iteroFolder");
    }

    private void ShowFilterPanelMethod()
    {
        ShowingFiltersPanel = Visibility.Visible;
    }

    private void HideFilterPanelMethod()
    {
        ShowingFiltersPanel = Visibility.Collapsed;
    }


    private void TestCommandMethod(object obj)
    {
        Debug.WriteLine("clicked");
        Debug.WriteLine(obj);
    }

    private void SearchLimitSelectionChanged()
    {
        WriteLocalSetting("SearchLimit", SearchLimit);
    }




    #region SMessageBox Metods

    public SMessageBoxResult ShowMessageBox(string Title, string Message, SMessageBoxButtons Buttons,
                                              NotificationIcon MessageBoxIcon,
                                              double DismissAfterSeconds = 300,
                                              Window? Owner = null)
    {
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (!MessageBoxPresent)
            {
                MessageBoxPresent = true;
                SMessageBox sMessageBox = new(Title, Message, Buttons, MessageBoxIcon, DismissAfterSeconds);
                if (Owner is null)
                    sMessageBox.Owner = MainWindow.Instance;
                else
                    sMessageBox.Owner = Owner;

                sMessageBox.ShowDialog();
                MessageBoxPresent = false;
            }
        }));

        return SMessageBoxxResult;
    }



    #endregion SMessageBox Metods


    #region Settings / Customer Suggestions Tab
    private async void BuildCustomerSuggestionsList()
    {
        CustomerSuggestionsCusNamesList = await GetCustomerSuggestionsCustomerNamesList();
    }

    private async void BuildCustomerSuggestionReplacementList()
    {
        CustomerSuggestionsReplacementsList = await GetCustomerSuggestionsReplacementList(SelectedCustomerName);
    }



    #endregion Settings / Customer Suggestions Tab

    #region Settings / Comment Rules Tab
    private async void BuildCommentRuleList()
    {
        CommentRulesList = await GetCommentRulesList();
    }

    #endregion Settings / Comment Rules Tab






    #region AccountInfos
    private void SearchInAccountInfosMethod()
    {
        List<AccountInfoModel> accountInfoModels = AccountInfoList;
        if (!string.IsNullOrEmpty(SearchInAccountInfos))
            AccountInfoList = accountInfoModels.Where(x => x.FriendlyName!.Contains(SearchInAccountInfos, StringComparison.CurrentCultureIgnoreCase) ||
                                                           x.SubCategory!.Contains(SearchInAccountInfos, StringComparison.CurrentCultureIgnoreCase)).ToList();
        else
            GetAccountInfos();
    }

    private void ClearAccInfoSearch()
    {
        SearchInAccountInfos = "";
        _MainWindow.tbSearchAccInfos.Focus();
    }


    private async void GetAccountInfos()
    {
        AccountInfoCategories = await GetAccountInfoCategories();

        List<AccountInfoModel> list = await GetAccountInfoList(BgBorderColors);

        if (string.IsNullOrEmpty(SelectedAccountInfoCategory) || SelectedAccountInfoCategory == "All")
            AccountInfoList = list;
        else
            AccountInfoList = list.Where(x => x.Category == SelectedAccountInfoCategory).ToList();
    }

    private void OpenWebsite(object obj)
    {
        string url = (string)obj;
        Debug.WriteLine(url);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private void StartApplication(object obj)
    {
        string appPath = (string)obj;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = appPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private void StartApplication(string appPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = appPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private void CopyUserNameToClipboard(object obj)
    {
        string userName = (string)obj;

        Clipboard.SetText(userName);

        var item = new System.Windows.Forms.NotifyIcon()
        {
            Visible = true,
            Icon = System.Drawing.SystemIcons.Information
        };
        item.ShowBalloonTip(40000, "", $"Username was copied to clipboard!", System.Windows.Forms.ToolTipIcon.Info);

    }

    private void CopyPasswordToClipboard(object obj)
    {
        string password = (string)obj;

        Clipboard.SetText(password);

        var item = new System.Windows.Forms.NotifyIcon()
        {
            Visible = true,
            Icon = System.Drawing.SystemIcons.Information
        };
        item.ShowBalloonTip(40000, "", "Password was copied to clipboard!", System.Windows.Forms.ToolTipIcon.Info);

    }

    private void ShowPassword(object obj)
    {
        Border border = (Border)obj;
        Debug.WriteLine(border.Tag.ToString());
    }
    #endregion AccountInfos



    private void ChangeColor()
    {
        SetPanColorWindow setPanColorWindow = new(PreviousPcPanNumber, OriginalRgbColor)
        {
            Owner = _mainWindow
        };
        setPanColorWindow.ShowDialog();
    }


    private void AddNewNumber()
    {
        SetPanColorWindow setPanColorWindow = new(PreviousPcPanNumber, "0-0-0")
        {
            Owner = _mainWindow
        };

        setPanColorWindow.ShowDialog();
    }

    private void RequestDCASUpdate()
    {
        if (LastDCASUpdate.Contains("minute"))
        {
            WriteStatsSetting("dcas_CheckForEmails", "true");
            ShowNotificationMessage("Success", "Request for DCAS update sent!", NotificationIcon.Success, false);
        }
    }


    private void SwitchToSettingsTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            MoveLabnextViewToLabnextTab();
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.infoTab;
        });
    }
    private void SwitchToPaymentTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.paymentsTab;
        });
    }

    private void SwitchToPrescriptionMakerTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.prescriptionMakerTab;
        });
    }

    private void SwitchToServerLogTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.serverLogsTab;
        });
    }

    private void SwitchToAccountInfosTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.tabAccountInfos;
        });
    }

    private void SwitchToPanNrDuplicatesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.duplicatedPanNrTab;
        });
    }

    private void SwitchToOrderIssuesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.orderIssuesTab;
        });
    }

    private void SwitchToFolderSubscriptionTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (CbSettingModuleLabnext)
            {
                LabnextKeepAliveTimer.Stop();
                LabnextKeepAliveTimer.Start();
                LabnextCanReload = true;
                ClearAllSearchCriteria();
                MoveLabnextViewToFolderSubscriptionTab();
                SearchForPanNumberInLabnextForFolderSubscription();
            }
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.folderSubscriptionTab;
        });
    }


    private void SwitchToDebugMessagesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();

            _MainWindow.mainTabControl.SelectedItem = _MainWindow.infoTab;
            _MainWindow.infoTabControl.SelectedItem = _MainWindow.settingsTab;
            _MainWindow.settingsTabControl.SelectedItem = _MainWindow.debugTab;

        });
    }

    private void SwitchTo3ShapeOrdersTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            //if (CbSettingModuleLabnext) 
            MoveLabnextViewToLabnextTab();
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.tbSearch.Focus();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.ThreeShapeTab;
            _MainWindow.tbSearch.Focus();
        });
    }

    private void SwitchToLabnextTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextKeepAliveTimer.Stop();
            LabnextKeepAliveTimer.Start();
            IsLabnextLookupIsOpen = false;
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            MoveLabnextViewToLabnextTab();
            if (_MainWindow.webviewLabnext.IsInitialized)
            {
                _MainWindow.webviewLabnext.ZoomFactor = 1.0;
            }
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.tabLabnext;
        });
    }

    private void SwitchToSentOutCasesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.SentOutCasesTab;
        });
    }

    private void SwitchToHomeTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            HomeButtonShows = Visibility.Collapsed;
            RefreshButtonShows = Visibility.Collapsed;
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.HomeTab;
            ClearAllSearchCriteria();
        });
    }

    private void Refresh3ShapeList()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            listUpdateTimer.Stop();
            if (ListUpdateable && AllowThreeShapeOrderListUpdates)
            {
                AllowToShowProgressBar = false;
                if (!string.IsNullOrEmpty(ActiveFilterInUse))
                    Search(ActiveFilterInUse, true);
                if (!string.IsNullOrEmpty(ActiveSearchString))
                    Search(ActiveSearchString);
            }
            listUpdateTimer.Start();
        });
    }




    private void SwitchToPendingDigiCasesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.pendingDigiCasesTab;
        });
    }

    private void SwitchTo3ShapeTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.ThreeShapeTab;
        });
    }

    private void SwitchToArchivesTab()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LabnextCanReload = true;
            ClearAllSearchCriteria();
            _MainWindow.mainTabControl.SelectedItem = _MainWindow.ArchivesTab;
        });
    }

    private async Task BlinkWindow(string color = "yellow")
    {
        string FinalColor = "#c9bf97";

        FinalColor = ColorSchemeWindowBackground;

        if (color == "yellow")
        {
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
            WindowBackground = "#66595F";
            await Task.Delay(50);
            WindowBackground = "#76795F";
            await Task.Delay(50);
            WindowBackground = "#86895F";
            await Task.Delay(50);
            WindowBackground = "#96995F";
            await Task.Delay(50);
            WindowBackground = "#A6A95F";
            await Task.Delay(50);
            WindowBackground = "#96995F";
            await Task.Delay(50);
            WindowBackground = "#86895F";
            await Task.Delay(50);
            WindowBackground = "#76795F";
            await Task.Delay(50);
            WindowBackground = "#66695F";
            await Task.Delay(50);
        }

        if (color == "green")
        {
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
            WindowBackground = "#56695F";
            await Task.Delay(50);
            WindowBackground = "#56795F";
            await Task.Delay(50);
            WindowBackground = "#56895F";
            await Task.Delay(50);
            WindowBackground = "#56995F";
            await Task.Delay(50);
            WindowBackground = "#56A95F";
            await Task.Delay(50);
            WindowBackground = "#56995F";
            await Task.Delay(50);
            WindowBackground = "#56895F";
            await Task.Delay(50);
            WindowBackground = "#56795F";
            await Task.Delay(50);
            WindowBackground = "#56695F";
            await Task.Delay(50);
        }

        if (color == "red")
        {
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
            WindowBackground = "#66595F";
            await Task.Delay(50);
            WindowBackground = "#76595F";
            await Task.Delay(50);
            WindowBackground = "#86595F";
            await Task.Delay(50);
            WindowBackground = "#96595F";
            await Task.Delay(50);
            WindowBackground = "#A6595F";
            await Task.Delay(50);
            WindowBackground = "#86595F";
            await Task.Delay(50);
            WindowBackground = "#76595F";
            await Task.Delay(50);
            WindowBackground = "#66595F";
            await Task.Delay(50);
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
        }

        WindowBackground = FinalColor;
    }
    private async Task BlinkWindow(object obj)
    {
        string color = obj.ToString()!;

        string FinalColor = "#c9bf97";

        FinalColor = ColorSchemeWindowBackground;

        if (color == "yellow")
        {
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
            WindowBackground = "#66595F";
            await Task.Delay(50);
            WindowBackground = "#76795F";
            await Task.Delay(50);
            WindowBackground = "#86895F";
            await Task.Delay(50);
            WindowBackground = "#96995F";
            await Task.Delay(50);
            WindowBackground = "#A6A95F";
            await Task.Delay(50);
            WindowBackground = "#96995F";
            await Task.Delay(50);
            WindowBackground = "#86895F";
            await Task.Delay(50);
            WindowBackground = "#76795F";
            await Task.Delay(50);
            WindowBackground = "#66695F";
            await Task.Delay(50);
        }

        if (color == "green")
        {
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
            WindowBackground = "#56695F";
            await Task.Delay(50);
            WindowBackground = "#56795F";
            await Task.Delay(50);
            WindowBackground = "#56895F";
            await Task.Delay(50);
            WindowBackground = "#56995F";
            await Task.Delay(50);
            WindowBackground = "#56A95F";
            await Task.Delay(50);
            WindowBackground = "#56995F";
            await Task.Delay(50);
            WindowBackground = "#56895F";
            await Task.Delay(50);
            WindowBackground = "#56795F";
            await Task.Delay(50);
            WindowBackground = "#56695F";
            await Task.Delay(50);
        }

        if (color == "red")
        {
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
            WindowBackground = "#66595F";
            await Task.Delay(50);
            WindowBackground = "#76595F";
            await Task.Delay(50);
            WindowBackground = "#86595F";
            await Task.Delay(50);
            WindowBackground = "#96595F";
            await Task.Delay(50);
            WindowBackground = "#A6595F";
            await Task.Delay(50);
            WindowBackground = "#86595F";
            await Task.Delay(50);
            WindowBackground = "#76595F";
            await Task.Delay(50);
            WindowBackground = "#66595F";
            await Task.Delay(50);
            WindowBackground = "#c9bf97";
            await Task.Delay(50);
        }

        WindowBackground = FinalColor;
    }


    private async void PcCheckPanColor()
    {
        string panNumber = PcPanNumber;
        if (panNumber.Length < 1)
            return;

        if (!int.TryParse(panNumber, out int num))
        {
            PcPanNumber = "";
            return;
        }

        PreviousPcPanNumber = PcPanNumber;

        string rgbColor = GetPanColorByNumber(num);
        if (rgbColor == "0-0-0")
        {
            NoNumberRegisteredShowsNow = Visibility.Visible;

            PcPanColor = "Black";
            PcPanColorFriendlyName = "Number not found!";
            PcPanNumber = "";

            await Task.Delay(300);
            PcPanColor = "#c9bf97";
            await Task.Delay(300);
            PcPanColor = "Black";
            await Task.Delay(300);
            PcPanColor = "#c9bf97";
            await Task.Delay(300);
            PcPanColor = "Black";
            await Task.Delay(300);

            PcPanColor = "#1a040a";
            await Task.Delay(100);

            PcPanColor = "#2a140a";
            await Task.Delay(100);

            PcPanColor = "#3a240a";
            await Task.Delay(100);

            PcPanColor = "#4a340a";
            await Task.Delay(100);

            PcPanColor = "#5a440a";
            await Task.Delay(100);

            PcPanColor = "#6a540a";
            await Task.Delay(100);

            PcPanColor = "#7a640a";
            await Task.Delay(100);

            PcPanColor = "#8a741a";
            await Task.Delay(100);

            PcPanColor = "#9a842a";
            await Task.Delay(100);

            PcPanColor = "#aa943a";




            await Task.Delay(500);
            PcPanColor = "#f7f4e6";
            PcPanColorFriendlyName = "Check pan color";
            NoNumberRegisteredShowsNow = Visibility.Collapsed;
        }
        else
        {
            IsItDarkColor = CheckIfItsDarkColor(rgbColor);

            PanColorShowsNow = Visibility.Visible;

            string[] panColorParts = rgbColor.Split('-');

            _ = int.TryParse(panColorParts[0], out int colorR);
            _ = int.TryParse(panColorParts[1], out int colorG);
            _ = int.TryParse(panColorParts[2], out int colorB);

            Brush panColor = new SolidColorBrush(Color.FromArgb(255, (byte)colorR, (byte)colorG, (byte)colorB));
            PcPanColor = panColor.ToString();
            OriginalRgbColor = PcPanColor;
            PcPanColorFriendlyName = GetPanColorNameByNumber(num);
            PcPanNumber = "";

            await Task.Delay(3500);
            PcPanColor = "#f7f4e6";
            PcPanColorFriendlyName = "Check pan color";
            PanColorShowsNow = Visibility.Collapsed;

            IsItDarkColor = true;
        }
    }








    #region FOLDER SUBSCRIPTION & PENDING DIGI CASES METHODS

    private void FsTriggerUpdateRequest()
    {
        WriteStatsSetting("fs_RescanNow", "true");
    }

    private void FsOpenFolder(object obj)
    {
        try
        {
            string folder = (string)obj!;
            Process.Start("explorer.exe", "\"" + folder + "\"");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private async void FsCopyFolderOver(object obj)
    {
        string number = obj.ToString()!;
        FsHideNotification();


        if (await Task.Run(() => CopyDirectory(FsSelectedFolderObject.Path!, $@"{FsubscrTargetFolder}{number}-{FsSelectedFolderObject.FolderName}")))
        {
            ShowNotificationMessage("Success", $"Folder with name: '{number}-{FsSelectedFolderObject.FolderName}' copied over successfully", NotificationIcon.Success, true, 35);
            FsSearchString = "";
            await Task.Run(() => MarkPanNumberAsCollected(number));
            FillUpPendingDigiCaseNumberList(true);
        }
        else
            ShowNotificationMessage("Error", "Error occured during the copy process!", NotificationIcon.Error, true, 35);
    }


    private void FsSearchFolders()
    {
        if (FsSearchString.Length < 1)
            return;

        FolderSubscriptionList = GetFolderSubscriptions(FsSearchString);
        FolderSubscriptionList.Sort((x, y) => x.AgeForSorting!.CompareTo(y.AgeForSorting));
        FsSearchString = "";
    }

    private void FsCreateTodayFolder()
    {
        try
        {
            _ = int.TryParse(FsubscrTargetFolder.AsSpan(FsubscrTargetFolder.Length - 3, 2), out int FolderCheck);
            string parentDir = "";

            if (FolderCheck > 0)
                parentDir = Directory.GetParent(Directory.GetParent(FsubscrTargetFolder)!.ToString())!.ToString() + "\\";
            else
                parentDir = FsubscrTargetFolder;

            string TDDir = parentDir + DateTime.Now.ToString("MM-dd");
            Directory.CreateDirectory(TDDir);
            Directory.CreateDirectory(@$"{TDDir}\DN");
            FsubscrTargetFolder = TDDir + "\\";
            WriteLocalSetting("SubscriptionCopyFolder", TDDir + "\\");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
        }

        try
        {
            Process.Start("explorer.exe", "\"" + FsubscrTargetFolder + "\"");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private void SelectTargetFolder()
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select the target folder where you want the app to copy over scan files"
        };

        if (folderDialog.ShowDialog() == true)
        {
            var folderName = folderDialog.FolderName;
            FsubscrTargetFolder = folderName + @"\";
            WriteLocalSetting("SubscriptionCopyFolder", folderName + @"\");
        }
    }

    private void FsHideNotification()
    {
        FsNotificationTimer_Tick(null, null);
    }

    private void FsNotificationTimer_Tick(object? sender, EventArgs e)
    {
        FsCustomNumber = "";
        FsCopyPanelShows = false;
        fsNotificationTimer.Stop();
    }

    private async void FsItemClicked(object obj)
    {
        try
        {
            if (await Task.Run(() => Directory.Exists(((FolderSubscriptionModel)obj).Path)))
            {
                FsSelectedFolderObject = (FolderSubscriptionModel)obj;
                FsCopyPanelShows = true;
            }
            else
            {
                ShowNotificationMessage("Folder not found", "The folder you're selected does not exist anymore", NotificationIcon.Error, true, 35);
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowNotificationMessage("No access", "The folder you're selected is not accessible!", NotificationIcon.Error, true);
        }
    }


    #endregion FOLDER SUBSCRIPTION & PENDING DIGI CASES METHODS


    #region PRESCRIPTION MAKER METHODS

    private void InconsistencyItemClicked(object obj)
    {
        try
        {
            string orderID = (string)obj;
            IgnoreInconsistencyOrderID = orderID;
            InconsistencyPanelShows = true;
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private void CancelIgnoreInconsistencyOrderIDMethod()
    {
        IgnoreInconsistencyOrderID = "";
        InconsistencyPanelShows = false;
    }

    private void HitIgnoreInconsistencyOrderIDMethod()
    {
        IgnoredPrescriptionInconsistencys.Add(PrescriptionInconsistencys.FirstOrDefault(x => x.OrderID == IgnoreInconsistencyOrderID)!);
        AddOrderToIgnoredListLocalDB(PrescriptionInconsistencys.FirstOrDefault(x => x.OrderID == IgnoreInconsistencyOrderID)!.OrderID!);
        InconsistencyPanelShows = false;
    }

    private async Task FillUpIngnoredOrdersInInconsistencyList()
    {


        List<InconsistencyModel> list = await GetBackAllOrderToBeIgnoredFromLocalDB();
        foreach (var item in list)
        {
            if (IgnoredPrescriptionInconsistencys.Count > 0)
            {
                if (IgnoredPrescriptionInconsistencys[0] is null)
                    IgnoredPrescriptionInconsistencys.RemoveAt(0);

                try
                {
                    if (!IgnoredPrescriptionInconsistencys.Any(x => x.OrderID == item.OrderID))
                    {
                        IgnoredPrescriptionInconsistencys.Add(PrescriptionInconsistencys.FirstOrDefault(x => x.OrderID == item.OrderID)!);
                    }
                }
                catch
                {
                }
            }
            else
            {
                try
                {
                    IgnoredPrescriptionInconsistencys.Add(PrescriptionInconsistencys.FirstOrDefault(x => x.OrderID == item.OrderID)!);
                }
                catch
                {
                }
            }
        }
    }


    private async void ReadBackAllEvent()
    {
        PrescriptionMakerEventsList = await GetBackAllEventFromLocalDB();
    }

    private void PmAddNewPanNumber(object obj)
    {
        string number = obj.ToString()!;
        _ = int.TryParse(number, out int num);

        if (GetPanColorByNumber(num) != "0-0-0")
        {
            if (!PmPanNumberList.Contains(number))
            {
                AddNewPanNumber(number);
                FillUpEmptyPanNumberPanel(false, number);

                RaisePropertyChanged(nameof(PmPanNumberList));
            }
            else
            {
                ShowNotificationMessage("Already Exists", $"{number} is already present in the list above!", NotificationIcon.Info);
            }
            PmAddNewNumber = "";
        }
        else
        {
            ShowNotificationMessage("Not registered", $"{number} is not registered within the system. Please enter a valid pan number!", NotificationIcon.Warning);
            PmAddNewNumber = "";
        }
    }

    private void TakeAPanNumber()
    {
        if (PmPanNumberList.Count > 0 && PmNextPanNumberInList is not null)
        {
            foreach (var item in _MainWindow.pmPanelPanek.Children.OfType<Button>().ToList())
            {
                if (item.Tag == PmNextPanNumberInList)
                    _MainWindow.pmPanelPanek.Children.Remove(item);
            }

            LastUsedPanNumbersDate = DateTime.Now;
            LastUsedPanNumber = PmNextPanNumberInList;
            PmLastTakenPanNumber = PmNextPanNumberInList;
            PmPanNumberList.Remove(PmNextPanNumberInList);
            RaisePropertyChanged(nameof(PmPanNumberList));
            RemovePanNumberFromAvailablePans(PmNextPanNumberInList);
            FillUpEmptyPanNumberPanel();
            ShowingTakeANumberPanel = Visibility.Hidden;
            PmRushButtonShows = Visibility.Hidden;
            PmSendToButtonShows = Visibility.Hidden;
            PmMissingButtonShows = Visibility.Hidden;

            try
            {
                Directory.CreateDirectory(PmFinalPrescriptionsFolder + DateTime.Now.ToString("MM-dd"));
            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
            }

            if (PmPanNumberList.Count == 0)
                PmNextPanNumberInList = "";

            //might need to delete 04-09-2025
            if (NextPanNumberGlobal != "")
                NextPanNumberGlobal = "";

            AddEventToEventListLocalDB($"Grabbed a pan number: {LastUsedPanNumber}", "Yellow");
            ReadBackAllEvent();
        }
    }

    private void HidePrescriptionPreviewPanel()
    {
        ShowingPrescriptionPreviewPanel = Visibility.Hidden;
    }

    private void ShowPrescriptionPreviewPanel()
    {
        ShowingPrescriptionPreviewPanel = Visibility.Visible;
        PrescriptionPreviewTimer.Start();
    }

    private void ClickOnPanNumber(object obj)
    {
        string panNumberStr = (string)obj;

        SMessageBoxResult res = ShowMessageBox("Delete pan", $"Are you sure you want to delete this pan number:\n                                                               {panNumberStr} ?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);

        if (res == SMessageBoxResult.Yes)
        {
            foreach (var item in _MainWindow.pmPanelPanek.Children.OfType<Button>().ToList())
            {
                if (item.Tag == panNumberStr)
                    _MainWindow.pmPanelPanek.Children.Remove(item);
            }

            PmPanNumberList.Remove(panNumberStr);
            RaisePropertyChanged(nameof(PmPanNumberList));
            RemovePanNumberFromAvailablePans(panNumberStr);
            FillUpEmptyPanNumberPanel();
            ShowingTakeANumberPanel = Visibility.Hidden;

            if (PmPanNumberList.Count == 0)
                PmNextPanNumberInList = null;
        }
    }

    private void FillUpEmptyPanNumberPanel(bool clearBeforeAdd = true, string panNumbr = "")
    {

        if (clearBeforeAdd)
        {
            PmPanNumberList = GetPanNumbers();
            MainWindow.Instance.pmPanelPanek.Children.Clear();

            foreach (string number in PmPanNumberList)
            {
                try
                {
                    _ = int.TryParse(number, out int num);
                    string[] panColorParts = GetPanColorByNumber(num).Split('-');
                    _ = int.TryParse(panColorParts[0], out int colorR);
                    _ = int.TryParse(panColorParts[1], out int colorG);
                    _ = int.TryParse(panColorParts[2], out int colorB);

                    Brush panColor = new SolidColorBrush(Color.FromArgb(255, (byte)colorR, (byte)colorG, (byte)colorB));

                    Button btn = new()
                    {
                        Tag = number,
                        Margin = new Thickness(0),
                        Background = Brushes.Transparent,
                        Padding = new Thickness(0),
                        BorderThickness = new Thickness(0),
                        Width = 76,
                        Height = 42,
                        Command = ClickOnPanNumberCommand,
                        CommandParameter = number,
                        //Style = Application.Current.Resources["panBoxStyle"] as Style
                    };

                    Border panBack = new()
                    {
                        CornerRadius = new CornerRadius(2),
                        Margin = new Thickness(3),
                        Background = panColor,
                        Width = 70,
                        Height = 36,
                        ClipToBounds = true,
                        BorderBrush = Brushes.DimGray,
                        BorderThickness = new Thickness(0.5),
                    };

                    panBack.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 320,
                        ShadowDepth = 3,
                        Opacity = 0.5,
                        BlurRadius = 5
                    };

                    Border stickerBorder = new()
                    {
                        BorderBrush = Brushes.Silver,
                        BorderThickness = new Thickness(0.5),
                    };

                    Grid panSticker = new()
                    {
                        Background = Brushes.White,
                        Height = 20,
                        Margin = new Thickness(8, 6, 8, 6),
                    };

                    TextBlock panNumber = new()
                    {
                        Text = number,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.Black,
                        Cursor = Cursors.Hand,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(4, 0, 4, 0),
                        Margin = new Thickness(0, -2, 0, 0),
                    };


                    stickerBorder.Child = panNumber;
                    panSticker.Children.Add(stickerBorder);
                    panBack.Child = panSticker;
                    btn.Content = panBack;

                    MainWindow.Instance.pmPanelPanek.Children.Add(btn);
                }
                catch (Exception ex)
                {
                    AddDebugLine(ex);
                }
            }
        }
        else if (panNumbr.Length > 0)
        {
            try
            {
                _ = int.TryParse(panNumbr, out int num);
                string[] panColorParts = GetPanColorByNumber(num).Split('-');
                _ = int.TryParse(panColorParts[0], out int colorR);
                _ = int.TryParse(panColorParts[1], out int colorG);
                _ = int.TryParse(panColorParts[2], out int colorB);

                Brush panColor = new SolidColorBrush(Color.FromArgb(255, (byte)colorR, (byte)colorG, (byte)colorB));

                Button btn = new()
                {
                    Tag = panNumbr,
                    Margin = new Thickness(0),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    Width = 76,
                    Height = 42,
                    ClipToBounds = true,
                    Command = ClickOnPanNumberCommand,
                    CommandParameter = panNumbr,
                };

                Border panBack = new()
                {
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(3),
                    Background = panColor,
                    Width = 70,
                    Height = 36,
                    ClipToBounds = true,
                    BorderBrush = Brushes.DimGray,
                    BorderThickness = new Thickness(0.5)
                };

                panBack.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 320,
                    ShadowDepth = 3,
                    Opacity = 0.5,
                    BlurRadius = 5
                };

                Border stickerBorder = new()
                {
                    BorderBrush = Brushes.Silver,
                    BorderThickness = new Thickness(0.5),
                };

                Grid panSticker = new()
                {
                    Background = Brushes.White,
                    Height = 20,
                    Margin = new Thickness(8, 6, 8, 6),
                };

                TextBlock panNumber = new()
                {
                    Text = panNumbr,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 0, 4, 0),
                    Margin = new Thickness(0, -2, 0, 0),
                };

                stickerBorder.Child = panNumber;
                panSticker.Children.Add(stickerBorder);
                panBack.Child = panSticker;
                btn.Content = panBack;

                MainWindow.Instance.pmPanelPanek.Children.Add(btn);

                PmPanNumberList.Add(panNumbr);
            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
            }
        }

        if (PmPanNumberList.Count > 0)
            PmNextPanNumberInList = PmPanNumberList[0];
    }



    private void PmRemoveFromSentToList()
    {
        if (PmSelectedSendToEntry is not null)
            RemoveNameFromSentToList(PmSelectedSendToEntry);

        PmSendToList = GetAllSendToEnties();
    }

    private void PmAddToSentToList()
    {
        if (PmNewSentToName.Trim().Length > 0)
        {
            AddNameToSentToList(PmNewSentToName.Trim());
            PmNewSentToName = "";
            PmSendToList = GetAllSendToEnties();
        }
    }



    private async void FswPrescriptionMaker_Created(object sender, FileSystemEventArgs e)
    {
        if (CbSettingWatchFolderPrescriptionMaker)
        {
            if (PmFinalPrescriptionsFolder != "" && !PmFinalPrescriptionsFolder.StartsWith("Click"))
            {
                Directory.CreateDirectory(PmFinalPrescriptionsFolder + DateTime.Now.ToString("MM-dd"));
                CleanTempFolder();

                try
                {
                    if (PmPanNumberList.Count > 0)
                    {
                        int i = 0;
                        FileInfo file = new(e.FullPath);
                        while (IsFileLocked(file) || i > 10)
                        {
                            await Task.Delay(1000);
                            i++;
                        }
                        PmLastTakenPanNumber = "";
                    }
                    else
                    {
                        if (!NoMorePanNumberBoxShowed)
                        {
                            SystemSounds.Beep.Play();
                            SwitchToPrescriptionMakerTab();
                            ClearAllSearchCriteria();
                            await BlinkWindow("red");
                            SystemSounds.Beep.Play();
                            ShowMessageBox("No more pan numbers", $"No more available pan numbers!", SMessageBoxButtons.Ok, NotificationIcon.Warning, 20, MainWindow.Instance);
                            NoMorePanNumberBoxShowed = true;
                            if (File.Exists(e.FullPath))
                                File.Delete(e.FullPath);
                        }
                        SironaOrderNumber = "";
                        IsItSironaPrescription = false;
                    }
                }
                catch (Exception ex)
                {
                    AddDebugLine(ex);
                    SironaOrderNumber = "";
                    IsItSironaPrescription = false;
                    return;
                }
            }

        }
    }

    private async void FswPrescriptionMaker_Changed(object sender, FileSystemEventArgs e)
    {
        if (CbSettingWatchFolderPrescriptionMaker)
        {
            if (PmFinalPrescriptionsFolder != "" && !PmFinalPrescriptionsFolder.StartsWith("Click"))
            {
                Directory.CreateDirectory(PmFinalPrescriptionsFolder + DateTime.Now.ToString("MM-dd"));
                CleanTempFolder();

                try
                {
                    if (PmPanNumberList.Count > 0)
                    {
                        int i = 0;
                        FileInfo file = new(e.FullPath);
                        while (IsFileLocked(file) || i > 10)
                        {
                            await Task.Delay(1000);
                            i++;
                        }

                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            ProcessingDigiPrescriptionNow = Visibility.Visible;
                            PmSavedPrescription = null;
                            NextPanNumberGlobal = PmPanNumberList[0].ToString();

                            //LastUsedPanNumber = NextPanNumberGlobal;

                            //might need to delete 04-09-2025
                            if (PmLastTakenPanNumber != "")
                                PmLastTakenPanNumber = "";

                            FullPathGlobal = e.FullPath;
                            DocumentStreamPixelCheck = new FileStream(e.FullPath, FileMode.OpenOrCreate);

                            PmRushButtonShows = Visibility.Visible;
                            PmSendToButtonShows = Visibility.Visible;
                            PmMissingButtonShows = Visibility.Visible;

                            //Load the PDF document as a stream
                            using FileStream inputStream = DocumentStreamPixelCheck;
                            imageConverter.Load(inputStream);
                            //Convert PDF to Image.
                            using (Stream outputStreamForPixelCheck = imageConverter.Convert(0, false, false))
                            {
                                PrescriptionImageForProcess = new(outputStreamForPixelCheck);
                            }
                            Stream[] outputStream = imageConverter.Convert(0, imageConverter.PageCount - 1, 200, 200, false, false);

                        }));

                        if (e.Name!.Contains("Workticket_"))
                            IsItSironaPrescription = true;

                        SironaOrderNumber = "";

                        StartProcessingPrescription();
                    }
                    else
                    {
                        if (!NoMorePanNumberBoxShowed)
                        {
                            SystemSounds.Beep.Play();
                            SwitchToPrescriptionMakerTab();
                            ClearAllSearchCriteria();
                            await BlinkWindow("red");
                            SystemSounds.Beep.Play();
                            ShowMessageBox("No more pan numbers", $"No more available pan numbers!", SMessageBoxButtons.Ok, NotificationIcon.Warning, 20, MainWindow.Instance);
                            NoMorePanNumberBoxShowed = true;
                            if (File.Exists(e.FullPath))
                                File.Delete(e.FullPath);
                        }
                        else
                            NoMorePanNumberBoxShowed = false;

                        SironaOrderNumber = "";
                        IsItSironaPrescription = false;
                    }
                }
                catch (Exception ex)
                {
                    AddDebugLine(ex);
                    SironaOrderNumber = "";
                    IsItSironaPrescription = false;
                    return;
                }
            }

        }
    }

    private bool IsFileLocked(FileInfo file)
    {
        FileStream stream = null;

        try
        {
            if (!file.Exists)
                return false;

            stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException io)
        {
            GlobalFileLockCount++;
            if (!io.Message.Contains("Could not find file", StringComparison.CurrentCultureIgnoreCase))
            {
                stream?.Close();
                return false;
            }

            if (!io.Message.Contains("The process cannot access the file", StringComparison.CurrentCultureIgnoreCase))
                AddDebugLine(io);
            //the file is unavailable because it is:
            //still being written to
            //or being processed by another thread
            //or does not exist (has already been processed)
            return true;
        }
        finally
        {
            stream?.Close();
        }

        //file is not locked
        return false;
    }

    private async void StartProcessingPrescription()
    {
        await Task.Run(() => IsPixelisWhite(PrescriptionImageForProcess));
    }

    private async Task<bool> IsPixelisWhite(Bitmap img)
    {
        Debug.WriteLine("########################");
        Debug.WriteLine("W" + img.Width);
        Debug.WriteLine("H" + img.Height);
        Debug.WriteLine("########################");


        string PixelColor = img.GetPixel(10, 56).ToString()
                                                    .Replace("Color [", "")
                                                    .Replace("]", "")
                                                    .Replace("A=", "")
                                                    .Replace(" R=", "")
                                                    .Replace(" G=", "")
                                                    .Replace(" B=", "")
                                                    .Replace(",", "");
        Debug.WriteLine("PixelColor:" + PixelColor);

        string PixelIS3DColor = img.GetPixel(2, 30).ToString()
                                                    .Replace("Color [", "")
                                                    .Replace("]", "")
                                                    .Replace("A=", "")
                                                    .Replace(" R=", "")
                                                    .Replace(" G=", "")
                                                    .Replace(" B=", "")
                                                    .Replace(",", "");

        Debug.WriteLine("PixelIS3DColor:" + PixelIS3DColor);

        string PixelASConnectColor = img.GetPixel(400, 50).ToString()
                                                    .Replace("Color [", "")
                                                    .Replace("]", "")
                                                    .Replace("A=", "")
                                                    .Replace(" R=", "")
                                                    .Replace(" G=", "")
                                                    .Replace(" B=", "")
                                                    .Replace(",", "");

        Debug.WriteLine("PixelIS3DColor:" + PixelASConnectColor);

        string PixelDSCoreColor = img.GetPixel(12, 12).ToString()
                                                    .Replace("Color [", "")
                                                    .Replace("]", "")
                                                    .Replace("A=", "")
                                                    .Replace(" R=", "")
                                                    .Replace(" G=", "")
                                                    .Replace(" B=", "")
                                                    .Replace(",", "");

        Debug.WriteLine("PixelDSCoreColor:" + PixelDSCoreColor);

        string PixelSironaColor = img.GetPixel(750, 40).ToString()
                                                    .Replace("Color [", "")
                                                    .Replace("]", "")
                                                    .Replace("A=", "")
                                                    .Replace(" R=", "")
                                                    .Replace(" G=", "")
                                                    .Replace(" B=", "")
                                                    .Replace(",", "");
        Debug.WriteLine("PixelSironaColor:" + PixelSironaColor);

        string PixelMeditColor = img.GetPixel(687, 32).ToString()
                                                    .Replace("Color [", "")
                                                    .Replace("]", "")
                                                    .Replace("A=", "")
                                                    .Replace(" R=", "")
                                                    .Replace(" G=", "")
                                                    .Replace(" B=", "")
                                                    .Replace(",", "");
        Debug.WriteLine("PixelMeditColor:" + PixelMeditColor);


        //255246249254
        //25546112234

        LastUsedPanNumbersDate = DateTime.Now;
        LastUsedPanNumber = NextPanNumberGlobal;

        if (PixelMeditColor == "255107154240" || PixelMeditColor == "255246249254" || PixelMeditColor == "25546112234") // Medit
        {
            PageHeaderIsHigh = "5";
            await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
            return false;
        }
        else if (PixelSironaColor == "2552441671") // Sirona
        {
            PageHeaderIsHigh = "4";
            await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
            return false;
        }
        else if (PixelIS3DColor == "25585135255") // IS3D
        {
            PageHeaderIsHigh = "3";
            await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
            return false;
        }
        else if (PixelDSCoreColor == "255858789") // DSCore
        {
            PageHeaderIsHigh = "6";
            await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
            return false;
        }
        else if (PixelASConnectColor == "255067153") // ASConnect
        {
            PageHeaderIsHigh = "7";
            await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
            return false;
        }
        else
        {
            if (PixelColor == "255255255255") // iTero
            {
                PageHeaderIsHigh = "0";
                await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
                return true;
            }
            else
            {
                PageHeaderIsHigh = "1";
                await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal));
                return false;
            }
        }
    }


    private async void TriggerSironaFolderRename(string PanNumber, string FinalLocation)
    {
        string image = FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + PanNumber + ".png";

        try
        {
            if (!File.Exists(DataBaseFolder + "eng.traineddata"))
                await WriteResourceToFile("eng.traineddata", DataBaseFolder + "eng.traineddata");

            using var engine = new Engine(DataBaseFolder, Language.English, EngineMode.Default);
            using var img = TesseractOCR.Pix.Image.LoadFromFile(image);
            using var page = engine.Process(img);
            string text = page.Text;
            if (text.Contains("Connect Case Center", StringComparison.CurrentCultureIgnoreCase))
            {
                IsItSironaPrescription = true;
                SironaOrderNumber = text.Substring(text.IndexOf("Order Number:"), 23).Replace("Order Number:", "").Trim();
            }
            Debug.WriteLine(SironaOrderNumber);

            if (text.Contains("CORE", StringComparison.CurrentCultureIgnoreCase) && text.Contains("Case Sheet", StringComparison.CurrentCultureIgnoreCase))
            {
                string DSCoreOrderNumber = text.Substring(text.IndexOf("Order Number:"), 48)
                                               .Replace("Order Number:", "")
                                               .Replace("Order Date:", "")
                                               .Replace("Date Due:", "")
                                               .Trim();

                if (DSCoreOrderNumber.Contains(' '))
                    DSCoreOrderNumber = DSCoreOrderNumber[..DSCoreOrderNumber.IndexOf(' ')];


                string PatientNameRaw = "";
                string PatientName = "";

                foreach (var str in text.Substring(text.IndexOf("Patient"), 100).Replace("Patient", "").Trim().Split("\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    if (PatientNameRaw == "")
                        PatientNameRaw = str;
                }

                string[] patientNameParts = PatientNameRaw.Split(" ");

                PatientName = patientNameParts[^2] + " " + patientNameParts[^1];

                SendSironaInfoToServer(PanNumber, PatientName, DSCoreOrderNumber, "DSCORE");
            }

            // for ASConnect case, getting the order id
            if (text.Contains("Treatment Report", StringComparison.CurrentCultureIgnoreCase))
            {
                IsItASConnectPrescription = true;
                ASConnectOrderID = text.Substring(text.IndexOf("Order ID:"), 54).Replace("Order ID:", "").Replace("Medical License:\n", "").Replace(" ", "").Replace("\n", "").Replace("—", "").Replace("--", "").Trim();
                ASConnectOrderID = GetNumbers(ASConnectOrderID);
                LastASConnectIDDate = DateTime.Now;
            }

            // for Dexis case, getting the order id
            if (text.Contains("CASE INFORMATION (", StringComparison.CurrentCultureIgnoreCase))
            {
                IsItASConnectPrescription = true;
                ASConnectOrderID = text.Substring(text.IndexOf("CASE INFORMATION ("), 26).Replace("CASE INFORMATION (", "").Replace(")", "").Trim();
                LastASConnectIDDate = DateTime.Now;
            }
            Debug.WriteLine(ASConnectOrderID);
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }

        if (IsItSironaPrescription && !string.IsNullOrEmpty(SironaOrderNumber))
        {
            string sironaFolder = ReadLocalSetting("SironaScansFolder");
            string PatientName = "";
            if (Directory.Exists(sironaFolder))
            {
                string finalFolderName = "";
                var directories = Directory.GetDirectories(sironaFolder, "*", SearchOption.TopDirectoryOnly);
                string currentDirectory = directories.FirstOrDefault(x => x.Contains($"{SironaOrderNumber}_"))!;

                if (File.Exists($@"{currentDirectory}\DentalCase.xml"))
                {
                    _ = bool.TryParse(ReadLocalSetting("OpenUpSironaScanFolder"), out bool OpenUpFolder);

                    XmlDocument doc = new();
                    doc.Load($@"{currentDirectory}\DentalCase.xml");

                    XmlElement root = doc.DocumentElement!;
                    List<XmlNode> nodes = [];
                    List<XmlNode> childNodes = [];

                    foreach (XmlNode node in root.ChildNodes)
                    {
                        if (!nodes.Contains(node))
                            nodes.Add(node);
                    }

                    XmlNode xmlNode = nodes.FirstOrDefault(x => x.Name.Equals("Patient"))!;

                    foreach (XmlNode node in xmlNode.ChildNodes)
                    {
                        if (!childNodes.Contains(node))
                            childNodes.Add(node);
                    }

                    foreach (XmlNode node in childNodes)
                    {
                        if (node.Name.Equals("FullName"))
                        {
                            PatientName = node.InnerText;

                            if (PatientName.Contains('(') && PatientName.Contains(')'))
                            {
                                string firstPart = PatientName.Substring(0, PatientName.IndexOf("("));
                                string secondPart = PatientName.Substring(PatientName.IndexOf(")") + 1);

                                PatientName = firstPart.Trim() + " " + secondPart.Trim();
                                PatientName = PatientName.TrimEnd().TrimStart().Replace(" ", "_");
                            }
                        }
                    }

                    finalFolderName = $"{PanNumber}-{PatientName}_{SironaOrderNumber}";



                    try
                    {
                        Directory.Move($@"{currentDirectory}", $@"{sironaFolder}{finalFolderName}");

                        try
                        {
                            if (OpenUpFolder)
                                Process.Start("explorer.exe", "\"" + $@"{sironaFolder}{finalFolderName}\" + "\"");

                            if (Directory.Exists($@"{sironaFolder}{finalFolderName}\"))
                            {
                                Clipboard.SetText($@"{sironaFolder}{finalFolderName}\");

                                var item = new System.Windows.Forms.NotifyIcon()
                                {
                                    Visible = true,
                                    Icon = System.Drawing.SystemIcons.Information
                                };
                                item.ShowBalloonTip(40000, "", $"Scan path was copied to clipboard!", System.Windows.Forms.ToolTipIcon.Info);
                            }
                        }
                        catch (Exception ex)
                        {
                            AddDebugLine(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddDebugLine(ex);
                        ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 20, MainWindow.Instance);
                    }
                }
            }

            SendSironaInfoToServer(PanNumber, PatientName, SironaOrderNumber, "SIRONA");
        }



        IsItSironaPrescription = false;
    }

    private static string GetNumbers(string input)
    {
        return new string(input.Where(c => char.IsDigit(c)).ToArray());
    }

    private async void EditPDF(string FilePath, string NextPanNumber = "", bool MarkAsRush = false, bool MarkAsSentTo = false, string SentTo = "", bool MarkAsMissing = false, string MissingText = "")
    {
        string SavedPDF = "";
        string SavedPDFCopy;

        try
        {
            // INJECTING PAN NUMBER TO PDF
            if (!MarkAsRush && !MarkAsSentTo && !MarkAsMissing && !string.IsNullOrEmpty(NextPanNumber))
            {
                //Load a PDF document.
                PdfLoadedDocument doc = new(FilePath);
                Thread.Sleep(600);
                //Get first page from document.
                PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
                //Create PDF graphics for the page
                PdfGraphics graphics = page.Graphics;

                //Set the standard font.
                PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 40, PdfFontStyle.Bold);
                //Draw the text.
                if (PageHeaderIsHigh == "1")
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(500, 55));
                else if (PageHeaderIsHigh == "0")
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(500, 38));
                else if (PageHeaderIsHigh == "3") // blue header Intelliscan / IS3D / Shining 3D type of prescr
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(130, 22));
                else if (PageHeaderIsHigh == "4") // Sirona type of prescr
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(210, 40));
                else if (PageHeaderIsHigh == "5") // Medit type of prescr
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(310, 25));
                else if (PageHeaderIsHigh == "6") // DSCore type of prescr
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(250, 30));
                else if (PageHeaderIsHigh == "7") // ASConnect type of prescr
                    graphics.DrawString(NextPanNumber, font, PdfBrushes.Black, new System.Drawing.PointF(470, 60));

                //Save the document.
                SavedPDF = PDFTemp + "\\" + NextPanNumber + ".pdf";
                SavedPDFCopy = PDFTemp + "\\" + NextPanNumber + "-copy.pdf";

                int i = 0;
                FileInfo file = new(SavedPDF);
                while (IsFileLocked(file) || i > 10)
                {
                    await Task.Delay(1000);
                    i++;

                    if (GlobalFileLockCount > 10)
                    {
                        GlobalFileLockCount = 0;
                        break;
                    }
                    //last change here -- need to delete this comment later
                }

                doc.Save(SavedPDF);
                doc.Save(SavedPDFCopy);

                doc.Close(true);


                WriteLocalSetting("BaseFile", FilePath.Replace("'", "|"));
                WriteLocalSetting("LastFile", PDFTemp + "\\" + NextPanNumber + ".pdf");
                WriteLocalSetting("LastPanNumber", NextPanNumber);


                SavePrescriptionFromPdfToImage(SavedPDFCopy);
            }
            // INJECTING RUSH TO PDF
            else if (MarkAsRush)
            {
                string lastPanNr = ReadLocalSetting("LastPanNumber");
                string LastFile = ReadLocalSetting("LastFile");


                //Load a PDF document.
                PdfLoadedDocument doc = new(LastFile);
                Thread.Sleep(600);
                //Get first page from document.
                PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
                //Create PDF graphics for the page
                PdfGraphics graphics = page.Graphics;

                //Set the standard font.
                PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 40, PdfFontStyle.Bold);
                //Draw the text.
                if (PageHeaderIsHigh == "1")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(120, 219));
                else if (PageHeaderIsHigh == "0")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(120, 201));
                else if (PageHeaderIsHigh == "3")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(306, 22));
                else if (PageHeaderIsHigh == "4")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(210, 92));
                else if (PageHeaderIsHigh == "5")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(310, 92));
                else if (PageHeaderIsHigh == "6")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(375, 30));
                else if (PageHeaderIsHigh == "7")
                    graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, new System.Drawing.PointF(420, 120));

                //Save the document.
                SavedPDF = PDFTemp + "\\" + lastPanNr + ".pdf";
                SavedPDFCopy = PDFTemp + "\\" + lastPanNr + "-copy.pdf";

                doc.Save(SavedPDF);
                doc.Save(SavedPDFCopy);

                doc.Close(true);

                SavePrescriptionFromPdfToImage(SavedPDFCopy, true);
            }
            // INJECTING SENT TO LABEL TO PDF
            else if (MarkAsSentTo && !string.IsNullOrEmpty(SentTo))
            {
                string lastPanNr = ReadLocalSetting("LastPanNumber");
                string LastFile = ReadLocalSetting("LastFile");


                //Load a PDF document.
                PdfLoadedDocument doc = new(LastFile);
                Thread.Sleep(600);
                //Get first page from document.
                PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
                //Create PDF graphics for the page
                PdfGraphics graphics = page.Graphics;

                //Set the standard font.
                PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 30, PdfFontStyle.Bold);
                //Draw the text.
                if (PageHeaderIsHigh == "1")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(260, 225));
                else if (PageHeaderIsHigh == "0")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(260, 205));
                else if (PageHeaderIsHigh == "3")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(120, 383));
                else if (PageHeaderIsHigh == "4")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(210, 310));
                else if (PageHeaderIsHigh == "5")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(190, 235));
                else if (PageHeaderIsHigh == "6")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(190, 205));
                else if (PageHeaderIsHigh == "7")
                    graphics.DrawString("Sent to " + SentTo + " " + DateTime.Now.ToString("MM/dd"), font, PdfBrushes.Black, new System.Drawing.PointF(35, 625));


                //Save the document.
                SavedPDF = PDFTemp + "\\" + lastPanNr + ".pdf";
                SavedPDFCopy = PDFTemp + "\\" + lastPanNr + "-copy.pdf";

                doc.Save(SavedPDF);
                doc.Save(SavedPDFCopy);

                doc.Close(true);

                PmSelectedSentTo = "-";

                SavePrescriptionFromPdfToImage(SavedPDFCopy, true);
            }
            // INJECTING MISSING LABEL TO PDF
            else if (MarkAsMissing && !string.IsNullOrEmpty(MissingText))
            {
                string lastPanNr = ReadLocalSetting("LastPanNumber");
                string LastFile = ReadLocalSetting("LastFile");


                //Load a PDF document.
                PdfLoadedDocument doc = new(LastFile);
                Thread.Sleep(600);
                //Get first page from document.
                PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
                //Create PDF graphics for the page
                PdfGraphics graphics = page.Graphics;

                //Set the standard font.
                float fontSize = 30;

                if (MissingText.Length > 20)
                    fontSize = 20;

                PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, fontSize, PdfFontStyle.Bold);
                //Draw the text.
                if (PageHeaderIsHigh == "1") // iTero high header
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(220, 225));
                else if (PageHeaderIsHigh == "0") // iTero low header
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(220, 205));
                else if (PageHeaderIsHigh == "3") // blue header Intelliscan / IS3D / Shining 3D type of prescr
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(120, 383));
                else if (PageHeaderIsHigh == "4") // Sirona type of prescr
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(180, 312));
                else if (PageHeaderIsHigh == "5") // Medit type of prescr
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(190, 235));
                else if (PageHeaderIsHigh == "6") // DSCore type of prescr
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(190, 205));
                else if (PageHeaderIsHigh == "7") // ASConnect type of prescr
                    graphics.DrawString(MissingText, font, PdfBrushes.Black, new System.Drawing.PointF(35, 635));


                //Save the document.
                SavedPDF = PDFTemp + "\\" + lastPanNr + ".pdf";
                SavedPDFCopy = PDFTemp + "\\" + lastPanNr + "-copy.pdf";

                doc.Save(SavedPDF);
                doc.Save(SavedPDFCopy);

                doc.Close(true);

                PmSelectedMissing = "-";
                SavePrescriptionFromPdfToImage(SavedPDFCopy, true);
            }

        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }



        try
        {

            if (PmOpenUpPrescriptionsBool && File.Exists(SavedPDF))
            {
                var p = new Process();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = $"/c start msedge {SavedPDF}";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 20, MainWindow.Instance);
        }


    }

    private async void PmMarkCaseAsRush()
    {
        SMessageBoxResult res = ShowMessageBox("Marking case rush", $"Sure you want to mark the case RUSH?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);

        if (res == SMessageBoxResult.Yes)
        {
            PmRushButtonShows = Visibility.Hidden;
            await Task.Run(() => EditPDF("", "", true));
        }
        else
            return;
    }

    private async void PmMarkCaseWithLabelSendTo()
    {
        if (PmSelectedSentTo is null || PmSelectedSentTo == "" || PmSelectedSentTo == "-")
        {
            ShowMessageBox("Error", $"Please choose one option from the dropdow first", SMessageBoxButtons.Ok, NotificationIcon.Warning, 15, MainWindow.Instance);
            return;
        }

        SMessageBoxResult res = ShowMessageBox("Adding label", $"Sure you want to add label '{PmSelectedSentTo}' to prescription?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);

        if (res == SMessageBoxResult.Yes)
        {
            PmSendToButtonShows = Visibility.Hidden;
            PmMissingButtonShows = Visibility.Hidden;
            await Task.Run(() => EditPDF("", "", false, true, PmSelectedSentTo));
            PmSelectedSentTo = "-";
        }
        else
            return;
    }

    private async void PmMarkCaseWithLabelMissing()
    {
        if (PmSelectedMissing is null || PmSelectedMissing == "" || PmSelectedMissing == "-")
        {
            ShowMessageBox("Error", $"Please choose one option from the dropdow first", SMessageBoxButtons.Ok, NotificationIcon.Warning, 15, MainWindow.Instance);
            return;
        }

        SMessageBoxResult res = ShowMessageBox("Adding label", $"Sure you want to add label '{PmSelectedMissing}' to prescription?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);

        if (res == SMessageBoxResult.Yes)
        {
            PmMissingButtonShows = Visibility.Hidden;
            PmSendToButtonShows = Visibility.Hidden;
            await Task.Run(() => EditPDF("", "", false, false, "", true, PmSelectedMissing));
            PmSelectedMissing = "-";
        }
        else
            return;
    }



    private void SavePrescriptionFromPdfToImage(string savedPDFCopy, bool IgnoreExistingImage = false)
    {
        string FinalLocation = ReadLocalSetting("FinalPrescriptionsFolder");
        string NextPanNumber = ReadLocalSetting("LastPanNumber");
        string BaseFile = ReadLocalSetting("BaseFile");

        string FinalFileNameWithPath = "";

        Application.Current.Dispatcher.Invoke(new Action(async () =>
        {
            DocumentStreamFinalPrescription = new FileStream(savedPDFCopy, FileMode.OpenOrCreate);

            try
            {
                Stream[] outputStream;
                //Load the PDF document as a stream
                using (FileStream inputStream = DocumentStreamFinalPrescription)
                {
                    imageConverter.Load(inputStream);
                    outputStream = imageConverter.Convert(0, imageConverter.PageCount - 1, 200, 200, false, false);
                }
                //Convert PDF to Image.

                for (int i = 0; i < outputStream.Length; i++)
                {
                    Bitmap image = new(outputStream[i]);
                    image.Save($@"{DataBaseFolder}Temp\{NextPanNumber}-{i}.png");
                    image.Dispose();
                }

                //if (!PwRush)
                //buttonMakeItRush.Visible = true;


                int count = outputStream.Length;
                PdfPageCount = count.ToString();



                /// till checked

                // checking if any empty pages in it
                bool LastPageIsEmpty = false;
                if (count > 1)
                {
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            string SavedPDF = ReadLocalSetting("LastFile");
                            //Load the PDF document.
                            PdfLoadedDocument loadedDocument = new(SavedPDF);
                            //Gets the page.
                            PdfPageBase loadedPage = loadedDocument.Pages[i] as PdfPageBase;
                            //Get the page is blank or not.
                            LastPageIsEmpty = loadedPage.IsBlank;

                            //Close the document.
                            loadedDocument.Close(true);
                            // END
                        }
                        catch (Exception ex)
                        {
                            AddDebugLine(ex);
                            ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
                        }

                    }

                    // decreasing count if last page is empty page
                    if (LastPageIsEmpty)
                        count--;
                }

                if (count == 1)
                {
                    if (FinalLocation != "")
                    {
                        if (NextPanNumber.Length > 0)
                        {
                            try
                            {
                                Bitmap image = new(outputStream[0]);
                                // Save the image.

                                if (!File.Exists(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                {
                                    PngBitmapEncoder encoder = new();

                                    string photolocation = FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png";
                                    image.Save(photolocation, System.Drawing.Imaging.ImageFormat.Png);
                                    image.Dispose();

                                    FinalFileNameWithPath = photolocation;

                                    // if after questionary we choose that the current prescription is the same as the last one, deleting the newly made paper and returning the used pan number as new
                                    if (await Task.Run(() => CheckIfCurrentPrescriptionIsSameAsLastOne(photolocation)))
                                    {
                                        PmAddNewPanNumber(NextPanNumber);
                                        File.Delete(photolocation);
                                        return;
                                    }

                                    Bitmap img;
                                    using (var bmpTemp = new Bitmap(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                    {
                                        img = new Bitmap(bmpTemp);
                                    }

                                    ImageSource imageSource = ImageSourceFromBitmap(img);

                                    PmSavedPrescription = imageSource;

                                    //if (IsItSironaPrescription)
                                    TriggerSironaFolderRename(NextPanNumber, FinalLocation);
                                }
                                else
                                {
                                    if (IgnoreExistingImage)
                                    {
                                        try
                                        {
                                            File.Delete(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png");
                                        }
                                        catch (Exception ex)
                                        {
                                            AddDebugLine(ex);
                                        }

                                        PngBitmapEncoder encoder = new();

                                        string photolocation = FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png";
                                        image.Save(photolocation, System.Drawing.Imaging.ImageFormat.Png);
                                        image.Dispose();

                                        FinalFileNameWithPath = photolocation;

                                        // if after questionary we choose that the current prescription is the same as the last one, deleting the newly made paper and returning the used pan number as new
                                        if (await Task.Run(() => CheckIfCurrentPrescriptionIsSameAsLastOne(photolocation)))
                                        {
                                            PmAddNewPanNumber(NextPanNumber);
                                            File.Delete(photolocation);
                                            return;
                                        }

                                        Bitmap img;
                                        using (var bmpTemp = new Bitmap(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                        {
                                            img = new Bitmap(bmpTemp);
                                        }

                                        ImageSource imageSource = ImageSourceFromBitmap(img);

                                        PmSavedPrescription = imageSource;

                                        //if (IsItSironaPrescription)
                                        TriggerSironaFolderRename(NextPanNumber, FinalLocation);
                                    }
                                    else
                                    {
                                        SMessageBoxResult res = ShowMessageBox("Adding label", $"This number is already used for a prescription, would you like to overwrite the original file?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);

                                        if (res == SMessageBoxResult.Yes)
                                        {
                                            PngBitmapEncoder encoder = new();

                                            string photolocation = FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png";
                                            image.Save(photolocation, System.Drawing.Imaging.ImageFormat.Png);
                                            image.Dispose();


                                            FinalFileNameWithPath = photolocation;

                                            // if after questionary we choose that the current prescription is the same as the last one, deleting the newly made paper and returning the used pan number as new
                                            if (await Task.Run(() => CheckIfCurrentPrescriptionIsSameAsLastOne(photolocation)))
                                            {
                                                PmAddNewPanNumber(NextPanNumber);
                                                File.Delete(photolocation);
                                                return;
                                            }

                                            Bitmap img;
                                            using (var bmpTemp = new Bitmap(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                            {
                                                img = new Bitmap(bmpTemp);
                                            }

                                            ImageSource imageSource = ImageSourceFromBitmap(img);

                                            PmSavedPrescription = imageSource;

                                            //if (IsItSironaPrescription)
                                            TriggerSironaFolderRename(NextPanNumber, FinalLocation);
                                        }
                                        else
                                            return;
                                    }

                                }
                            }
                            catch (Exception ex)
                            {
                                AddDebugLine(ex);
                                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
                            }


                            if (!IgnoreExistingImage)
                            {
                                foreach (Border item in _MainWindow.pmPanelPanek.Children.OfType<Border>().ToList())
                                {
                                    if (item.Tag == NextPanNumber)
                                        _MainWindow.pmPanelPanek.Children.Remove(item);
                                }

                                PmPanNumberList.Remove(NextPanNumber);
                                RemovePanNumberFromAvailablePans(NextPanNumber);
                                FillUpEmptyPanNumberPanel();
                            }
                        }
                    }
                }
                else
                {
                    if (FinalLocation != "")
                    {
                        if (NextPanNumber.Length > 0)
                        {
                            List<string> files = [];
                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    Bitmap image = new(outputStream[i]);

                                    Bitmap bitImage = Crop(image, PageHeaderIsHigh);
                                    // Save the image.
                                    string file = DataBaseFolder + "Temp\\" + NextPanNumber + "-" + i.ToString() + ".png";
                                    bitImage.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                                    files.Add(file);
                                    bitImage.Dispose();
                                    image.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    AddDebugLine(ex);
                                    ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
                                }
                            }

                            try
                            {
                                if (!File.Exists(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                {
                                    await Task.Run(() => CombineImages(files).Save(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png", System.Drawing.Imaging.ImageFormat.Png));

                                    Bitmap img;
                                    using (var bmpTemp = new Bitmap(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                    {
                                        img = new Bitmap(bmpTemp);
                                    }

                                    // if after questionary we choose that the current prescription is the same as the last one, deleting the newly made paper and returning the used pan number as new
                                    if (CheckIfCurrentPrescriptionIsSameAsLastOne(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                    {
                                        PmAddNewPanNumber(NextPanNumber);
                                        File.Delete(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png");
                                        return;
                                    }

                                    ImageSource imageSource = ImageSourceFromBitmap(img);
                                    //ImageSource imageSource = new BitmapImage(new Uri(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"));
                                    PmSavedPrescription = imageSource;

                                    TriggerSironaFolderRename(NextPanNumber, FinalLocation);
                                }
                                else
                                {
                                    if (IgnoreExistingImage)
                                    {
                                        try
                                        {
                                            File.Delete(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png");
                                        }
                                        catch (Exception ex)
                                        {
                                            AddDebugLine(ex);
                                        }

                                        await Task.Run(() => CombineImages(files).Save(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png", System.Drawing.Imaging.ImageFormat.Png));

                                        Bitmap img;
                                        using (var bmpTemp = new Bitmap(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                        {
                                            img = new Bitmap(bmpTemp);
                                        }

                                        // if after questionary we choose that the current prescription is the same as the last one, deleting the newly made paper and returning the used pan number as new
                                        if (await Task.Run(() => CheckIfCurrentPrescriptionIsSameAsLastOne(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png")))
                                        {
                                            PmAddNewPanNumber(NextPanNumber);
                                            File.Delete(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png");
                                            return;
                                        }

                                        ImageSource imageSource = ImageSourceFromBitmap(img);
                                        //ImageSource imageSource = new BitmapImage(new Uri(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"));
                                        PmSavedPrescription = imageSource;
                                        //if (IsItSironaPrescription)
                                        TriggerSironaFolderRename(NextPanNumber, FinalLocation);
                                    }
                                    else
                                    {
                                        SMessageBoxResult dlg = ShowMessageBox("Number already used", $"This number is already used for a prescription, would you like to overwrite the original file?", SMessageBoxButtons.YesNo, NotificationIcon.Warning, 15, MainWindow.Instance);

                                        if (dlg == SMessageBoxResult.Yes)
                                        {
                                            await Task.Run(() => CombineImages(files).Save(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png", System.Drawing.Imaging.ImageFormat.Png));


                                            // if after questionary we choose that the current prescription is the same as the last one, deleting the newly made paper and returning the used pan number as new
                                            if (await Task.Run(() => CheckIfCurrentPrescriptionIsSameAsLastOne(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png")))
                                            {
                                                PmAddNewPanNumber(NextPanNumber);
                                                File.Delete(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png");
                                                return;
                                            }

                                            Bitmap img;
                                            using (var bmpTemp = new Bitmap(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
                                            {
                                                img = new Bitmap(bmpTemp);
                                            }

                                            ImageSource imageSource = ImageSourceFromBitmap(img);
                                            //ImageSource imageSource = new BitmapImage(new Uri(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"));
                                            PmSavedPrescription = imageSource;
                                            //if (IsItSironaPrescription)
                                            TriggerSironaFolderRename(NextPanNumber, FinalLocation);
                                        }
                                        else
                                            return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AddDebugLine(ex);
                                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
                            }

                            if (!IgnoreExistingImage)
                            {

                                foreach (Border item in _MainWindow.pmPanelPanek.Children.OfType<Border>().ToList())
                                {
                                    if (item.Tag == NextPanNumber)
                                        _MainWindow.pmPanelPanek.Children.Remove(item);
                                }

                                PmPanNumberList.Remove(NextPanNumber);
                                RemovePanNumberFromAvailablePans(NextPanNumber);

                                FillUpEmptyPanNumberPanel();

                                if (PmPanNumberList.Contains(LastUsedPanNumber))
                                {
                                    ShowMessageBox("Error", $"Removing number {LastUsedPanNumber}, was unsuccesful!", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
            }

            try
            {
                ProcessingDigiPrescriptionNow = Visibility.Collapsed;
                ShowingPrescriptionPreviewPanel = Visibility.Visible;
                StartTimerForPrescriptionPreviewPanel();
                DocumentStreamPixelCheck?.Dispose();
                //need to replace apostrophe to | for Medit type of prescription names..
                File.Delete(BaseFile.Replace("|", "'"));
            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
            }

            SystemSounds.Beep.Play();
            await BlinkWindow("yellow");

            // checking if there was a PNG image saved or not
            if (!File.Exists(FinalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + NextPanNumber + ".png"))
            {
                ShowNotificationMessage("Image was not saved!", $"There was no image saved of this prescription! Please check..", NotificationIcon.Error);
                AddEventToEventListLocalDB($"Image was not saved! ({NextPanNumber})", "Red");
                ReadBackAllEvent();
                SystemSounds.Beep.Play();
                await BlinkWindow("red");
            }
            else
            {
                ShowNotificationMessage("Image was saved!", $"Prescription image successfully saved!", NotificationIcon.Success);
                AddEventToEventListLocalDB($"Prescription image successfully saved: {NextPanNumber}", "Green");
                ReadBackAllEvent();
                await BlinkWindow("yellow");
            }
        }));
    }

    private void PrescriptionPreviewTimer_Tick(object? sender, EventArgs e)
    {
        ShowingPrescriptionPreviewPanel = Visibility.Collapsed;
        PrescriptionPreviewTimer.Stop();
    }


    private void StartTimerForPrescriptionPreviewPanel()
    {
        PrescriptionPreviewTimer.Start();
    }

    private bool CheckIfCurrentPrescriptionIsSameAsLastOne(string photolocation)
    {
        double ActualSize = new FileInfo(photolocation).Length;

        if (PmLastPrescriptionSize == ActualSize)
        {
            SMessageBoxResult dlg = ShowMessageBox("Number already used", $"This case's prescription might be a duplicate..\nWould you like to open up last saved prescription to see if they are the same?", SMessageBoxButtons.YesNo, NotificationIcon.Warning, 15, MainWindow.Instance);

            if (dlg == SMessageBoxResult.Yes)
            {
                try
                {
                    //TODO: change this to open with hidden cmd window
                    Process.Start("cmd.exe", "/C \"" + photolocation + "\"");
                }
                catch (Exception ex)
                {
                    AddDebugLine(ex);
                }

                SMessageBoxResult res = ShowMessageBox("Is it same?", $"Is it same?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);
                if (res == SMessageBoxResult.Yes)
                {
                    PmSavedPrescription = null;
                    return true;
                }

            }
        }

        PmLastPrescriptionSize = ActualSize;
        return false;
    }

    private void PmOpenUpPrescriptions()
    {
        WriteLocalSetting("PmOpenUpPrescriptions", PmOpenUpPrescriptionsBool.ToString());
    }

    private static void PmSelectTargetFolder(object obj)
    {
        string folderIdentifier = obj.ToString()!;

        var folderDialog = new OpenFolderDialog
        {
            Title = "Please select a folder"
        };

        if (folderDialog.ShowDialog() == true)
        {
            var folderName = folderDialog.FolderName;
            WriteLocalSetting(folderIdentifier, folderName + @"\");

            StartInitialTasks();
        }
    }
    #endregion PRESCRIPTION MAKER METHODS

    private void FillUpPendingDigiCaseNumberList(bool forceUpdate = false)
    {
        List<ProcessedPanNumberModel> list = GetAllNotCollectedNumbers();
        if (CbSettingShowPendingDigiCases)
            PendingDigiNumbersWaitingToCollectInt = list.Count;
        else
            PendingDigiNumbersWaitingToCollectInt = 0;

        if (!forceUpdate)
        {
            if (list.Count != PendingDigiNumbersWaitingToCollect.Count)
            {
                PendingDigiNumbersWaitingToCollect = [];
                foreach (ProcessedPanNumberModel item in list)
                    PendingDigiNumbersWaitingToCollect.Add(item.PanNumber!);

                MainWindow.Instance.listviewPendingDigiNumbers.ItemsSource = PendingDigiNumbersWaitingToCollect;
                MainWindow.Instance.listviewPendingDigiNumbers.Items.Refresh();

                if (list.Count > 0)
                {
                    SelectedPendingDigiNumber = PendingDigiNumbersWaitingToCollect[0];
                    MainWindow.Instance.listviewPendingDigiNumbers.SelectedIndex = 0;
                }
            }
        }
        else
        {
            PendingDigiNumbersWaitingToCollect = [];
            foreach (ProcessedPanNumberModel item in list)
                PendingDigiNumbersWaitingToCollect.Add(item.PanNumber!);

            MainWindow.Instance.listviewPendingDigiNumbers.ItemsSource = PendingDigiNumbersWaitingToCollect;
            MainWindow.Instance.listviewPendingDigiNumbers.Items.Refresh();


            if (list.Count > 0)
            {
                SelectedPendingDigiNumber = PendingDigiNumbersWaitingToCollect[0];
                MainWindow.Instance.listviewPendingDigiNumbers.SelectedIndex = 0;
            }
        }

        List<ProcessedPanNumberModel> allPendingDigi = GetAllPendingDigiNumbersInLast30Days();
        PendingDigiNumbersWaitingToProcess = [];

        foreach (ProcessedPanNumberModel item in allPendingDigi)
        {
            ProcessedPanNumberModel model = new()
            {
                PanNumber = item.PanNumber,
                Comment = item.Comment,
                Id = item.Id,
            };

            if (item.IsProcessed == "true")
                model.IsProcessed = "✓";
            else
                model.IsProcessed = "";

            if (item.IsCollected == "true")
                model.IsCollected = "✓";
            else
                model.IsCollected = "";

            _ = DateTime.TryParse(item.PostedTime!, out DateTime postedDateTime);

            string postedTime = postedDateTime.ToString("M/d - h:mm tt");

            if (postedDateTime.ToString("yyyy-MM-dd") == DateTime.Now.ToString("yyyy-MM-dd"))
                postedTime = $"Today - {postedDateTime:h:mm tt}";
            else if (postedDateTime.ToString("yyyy-MM-dd") == DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"))
                postedTime = $"Yesterday - {postedDateTime:h:mm tt}";

            string processedTime;
            if (DateTime.TryParse(item.ProcessedTime!, out DateTime processedDateTime))
            {
                processedTime = processedDateTime.ToString("M/d - h:mm tt");
                if (processedDateTime.ToString("yyyy-MM-dd") == DateTime.Now.ToString("yyyy-MM-dd"))
                    processedTime = $"Today - {processedDateTime:h:mm tt}";
                else if (processedDateTime.ToString("yyyy-MM-dd") == DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"))
                    processedTime = $"Yesterday - {processedDateTime:h:mm tt}";
            }
            else
                processedTime = "-";

            model.ProcessedTime = processedTime;
            model.PostedTime = postedTime;
            model.ProcessedBy = ReadComputerName(item.ProcessedBy!);
            model.PostedBy = ReadComputerName(item.PostedBy!);
            model.PostedTimeForSorting = item.PostedTimeForSorting;

            if ((postedDateTime.ToString("yyyy-MM-dd") == DateTime.Now.ToString("yyyy-MM-dd")) || string.IsNullOrEmpty(model.IsProcessed))
            {
                if (!string.IsNullOrEmpty(model.IsProcessed))
                    model.LineColor = "LightGreen"; // Today - processed
                else
                {
                    if (string.IsNullOrEmpty(model.IsCollected))
                        model.LineColor = "#fa91c5"; // Today - not collected yet
                    else
                        model.LineColor = "Yellow"; // Today - collected but not processed yet
                }
            }
            else if (postedDateTime.ToString("yyyy-MM-dd") == DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"))
                model.LineColor = "#c2effc"; // Yesterday


            PendingDigiNumbersWaitingToProcess.Add(model);

        }
        PendingDigiNumbersWaitingToProcess = PendingDigiNumbersWaitingToProcess.OrderBy(x => x.IsProcessed).ThenByDescending(x => x.PostedTimeForSorting).ToList();
    }

    private void FswTriosFolderWatcher_Created(object sender, FileSystemEventArgs e)
    {
        CountTriosCases();
    }

    private void CountTriosCases()
    {
        int directoryCount = Directory.GetDirectories(TriosInboxFolder).Length;
        NewTriosCaseInInboxCount = directoryCount;
    }

    private void FswTriosFolderWatcher_Deleted(object sender, FileSystemEventArgs e)
    {
        int directoryCount = Directory.GetDirectories(TriosInboxFolder).Length;
        NewTriosCaseInInboxCount = directoryCount;
    }

    private void GeneralTimer_Tick(object? sender, EventArgs e)
    {
        DateTime dtime = DateTime.Now;
        int hours = dtime.Hour;
        int minutes = dtime.Minute;
        int seconds = dtime.Second;


        //if (hours > 12)
        //    _ = 12;

        if (PanNrDuplicatesList.Count > 0)
        {
            if (PanNrDuplicatesFontColor == "Red")
                PanNrDuplicatesFontColor = "Yellow";
            else
                PanNrDuplicatesFontColor = "Red";
        }

        // Run background tasks
        if (!bwBackgroundTasks.IsBusy && AppIsFullyLoaded)
            bwBackgroundTasks.RunWorkerAsync(argument: minutes.ToString() + "|" + seconds.ToString());
    }

    #region SETTINGS TAB METHODS






    public void OpenUpOrderInfoWindow()
    {
        if (ThreeShapeObject is null)
            return;

        bool archiveSelection = !string.IsNullOrWhiteSpace(ThreeShapeObject.OrderFolderPath) ||
                                !string.IsNullOrWhiteSpace(ThreeShapeObject.XmlFilePath);

        if (archiveSelection)
        {
            string xmlFilePath = ResolveXmlFilePath(ThreeShapeObject.XmlFilePath, ThreeShapeObject.OrderFolderPath ?? string.Empty, ThreeShapeObject.IntOrderID ?? string.Empty);

            if (string.IsNullOrWhiteSpace(xmlFilePath) || !File.Exists(xmlFilePath))
            {
                ShowMessageBox("No access", "Cannot open Order Info. Archive XML file is missing or inaccessible.", SMessageBoxButtons.Ok, NotificationIcon.Warning, 10, _MainWindow);
                return;
            }

            ThreeShapeObject.XmlFilePath = xmlFilePath;
        }

        OrderInfoWindow orderInfoWindow = new(ThreeShapeObject)
        {
            Owner = _MainWindow
        };
        orderInfoWindow.ShowDialog();
    }

    public void SearchForOrderByOrderIssueClick()
    {
        if (SelectedOrderIssue is null)
            return;
        Search(SelectedOrderIssue.OrderID!);
        SwitchTo3ShapeTab();
        ClearAllSearchCriteria();
    }


    public void GsItemClicked(object obj)
    {
        if (obj is null)
            return;
        string orderid = ((GlobalSearchModel)obj).IntOrderId!;

        ClearAllSearchCriteria();

        if (((GlobalSearchModel)obj).Source == "3Shape")
        {
            Search(orderid);
            SwitchTo3ShapeTab();
        }
        if (((GlobalSearchModel)obj).Source == "Archives")
        {
            SearchArchives(orderid);
            SwitchToArchivesTab();
        }

        SelectedGlobalSearchResult = new();
        SearchStringGlobal = "";
        GlobalSearchResult.Clear();
    }

    public void LabnextIdClicked(object obj)
    {
        if (obj is null)
            return;
        string labnextId = ((int)obj)!.ToString();

        try
        {
            Process.Start(new ProcessStartInfo($"{LabnextUrl}cases/case/id/{labnextId}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }

    }




    public async void OpenUpArchiveExportWindow(ArchivesOrdersModel model)
    {
        if (File.Exists(model.XMLFile))
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Please select the target folder"
            };

            GeneratingZippedOrderString = "Generating Zipped Order";

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                WorkingOnExportingZipArchive = true;
                if (bwZippingOrderArchives.IsBusy != true)
                {
                    bwZippingOrderArchives.RunWorkerAsync(new FolderData
                    {
                        FolderName = folderName,
                        OrderId = model.OrderID,
                        SourcePath = model.BaseFolder
                    });
                }
                else
                {
                    bwZippingOrderArchives.CancelAsync();
                }
            }
        }
        else
        {
            ShowMessageBox("No access", "Cannot access the file in Archives DataStore!", SMessageBoxButtons.Ok, NotificationIcon.Error, 10, _MainWindow);
        }
    }

    public async void OpenUpArchiveExportWindow(GlobalSearchModel model)
    {
        if (File.Exists(model.XMLFile))
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Please select the target folder"
            };

            GeneratingZippedOrderString = "Generating Zipped Order";

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                WorkingOnExportingZipArchive = true;
                if (bwZippingOrderArchives.IsBusy != true)
                {
                    bwZippingOrderArchives.RunWorkerAsync(new FolderData
                    {
                        FolderName = folderName,
                        OrderId = model.IntOrderId,
                        SourcePath = model.BaseFolder
                    });
                }
                else
                {
                    bwZippingOrderArchives.CancelAsync();
                }
            }
        }
        else
        {
            ShowMessageBox("No access", "Cannot access the file in Archives DataStore!", SMessageBoxButtons.Ok, NotificationIcon.Error, 10, _MainWindow);
        }
    }

    public async void OpenUpRenameOrderWindow()
    {
        OrderRenameWindow orderRenameWindow = new(ThreeShapeObject!)
        {
            Owner = _MainWindow
        };
        await LockOrderIn3Shape(ThreeShapeObject!.IntOrderID!);
        orderRenameWindow.ShowDialog();
        ListUpdateTimer_Tick(null, null);
    }


    private void CbSettingGlassyEffectMethod()
    {
        WriteLocalSetting("GlassyEffect", CbSettingGlassyEffect.ToString());
    }



    private void CbSettingStartAppMinimizedMethod()
    {
        WriteLocalSetting("StartAppMinimized", CbSettingStartAppMinimized.ToString());
    }


    //private void CbSettingShowBottomInfoBarMethod()
    //{
    //    WriteLocalSetting("ShowBottomInfoBar", ShowBottomInfoBar.ToString());
    //    if (ShowBottomInfoBar)
    //        BottomBarSize = 120;
    //    else
    //    {
    //        CbSettingShowDigiDetails = false;
    //        WriteLocalSetting("ShowDigiDetails", CbSettingShowDigiDetails.ToString());
    //        BottomBarSize = 35;
    //    }
    //}

    private void CbSettingShowDigiCasesMethod()
    {
        WriteLocalSetting("ShowDigiCases", CbSettingShowDigiCases.ToString());

    }

    private void CbSettingShowPendingDigiCasesMethod()
    {
        WriteLocalSetting("ShowPendingDigiCases", CbSettingShowPendingDigiCases.ToString());
    }

    private void CbSettingKeepUserLoggedInLabnextMethod()
    {
        WriteLocalSetting("KeepUserLoggedInLabnext", CbSettingKeepUserLoggedInLabnext.ToString());
    }

    private void CbSettingIncludePendingDigiCasesInNewlyArrivedMethod()
    {
        WriteLocalSetting("IncludePendingDigiCases", CbSettingIncludePendingDigiCasesInNewlyArrived.ToString());
    }

    //private void CbSettingShowDigiDetailsMethod()
    //{
    //    if (!ShowBottomInfoBar)
    //    {
    //        CbSettingShowDigiDetails = false;
    //        ShowNotificationMessage("Cannot activate!", "This option only works when the \"Show bottom info bar\" option is active", NotificationIcon.Warning);
    //    }
    //    WriteLocalSetting("ShowDigiDetails", CbSettingShowDigiDetails.ToString());
    //}

    private void CbSettingWatchFolderPrescriptionMakerMethod()
    {
        WriteLocalSetting("ActivePrescriptionMaker", CbSettingWatchFolderPrescriptionMaker.ToString());
        if (!string.IsNullOrEmpty(fswPrescriptionMaker.Path) && Directory.Exists(fswPrescriptionMaker.Path))
        {
            if (CbSettingWatchFolderPrescriptionMaker)
            {
                fswPrescriptionMaker.Path = PmWatchedPdfFolder;
                fswPrescriptionMaker.Filter = "*.pdf";
                fswPrescriptionMaker.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                fswPrescriptionMaker.Created += new FileSystemEventHandler(FswPrescriptionMaker_Created);
                fswPrescriptionMaker.Changed += new FileSystemEventHandler(FswPrescriptionMaker_Changed);
                fswPrescriptionMaker.EnableRaisingEvents = true;
            }

            fswPrescriptionMaker.EnableRaisingEvents = CbSettingWatchFolderPrescriptionMaker;
        }
        else
            fswPrescriptionMaker.EnableRaisingEvents = CbSettingWatchFolderPrescriptionMaker;
    }

    private void CbSettingOpenUpSironaScanFolderMethod()
    {
        WriteLocalSetting("OpenUpSironaScanFolder", CbSettingOpenUpSironaScanFolder.ToString());
    }

    private void CbSettingShowEmptyPanCountMethod()
    {
        WriteLocalSetting("ShowEmptyPanCount", CbSettingShowEmptyPanCount.ToString());
    }

    private void CbSettingShowDigiPrescriptionsCountMethod()
    {
        WriteLocalSetting("ShowDigiPrescriptionsCount", CbSettingShowDigiPrescriptionsCount.ToString());
    }


    private void CbSettingShowDigiCasesIn3ShapeTodayCountMethod()
    {
        WriteLocalSetting("ShowDigiCasesIn3ShapeTodayCount", CbSettingShowDigiCasesIn3ShapeTodayCount.ToString());
    }

    private void CbSettingModuleFolderSubscriptionMethod()
    {
        WriteLocalSetting("ModuleFolderSubscription", CbSettingModuleFolderSubscription.ToString());
    }

    private void CbSettingModuleAccountInfosMethod()
    {
        WriteLocalSetting("ModuleAccountInfos", CbSettingModuleAccountInfos.ToString());
        if (CbSettingModuleAccountInfos)
            GetAccountInfos();
    }

    private void CbSettingModuleLabnextMethod()
    {
        WriteLocalSetting("ModuleLabnext", CbSettingModuleLabnext.ToString());
    }

    private void CbSettingShowOtherUsersPanNumbersMethod()
    {
        WriteLocalSetting("ShowOtherUsersPanNumbers", CbSettingShowOtherUsersPanNumbers.ToString());
    }

    private void CbSettingModuleSmartOrderNamesMethod()
    {
        WriteLocalSetting("ModuleSmartOrderNames", CbSettingModuleSmartOrderNames.ToString());

        if (MainMenuViewModel.StaticInstance is not null)
        {
            if (CbSettingModuleSmartOrderNames)
                MainMenuViewModel.StaticInstance.ShowSmartRenameMenuItem();
            else
                MainMenuViewModel.StaticInstance.HideSmartRenameMenuItem();
        }
    }

    private void CbSettingModuleDebugMethod()
    {
        WriteLocalSetting("ModuleDebug", CbSettingModuleDebug.ToString());
    }

    private void CbSettingModulePrescriptionMakerMethod()
    {
        WriteLocalSetting("ModulePrescriptionMaker", CbSettingModulePrescriptionMaker.ToString());

        if (!CbSettingModulePrescriptionMaker)
        {
            CbSettingWatchFolderPrescriptionMaker = false;
            CbSettingExtractIteroZipFiles = false;
            CbSettingShowEmptyPanCount = false;
            CbSettingWatchFolderPrescriptionMakerMethod();
            CbSettingExtractIteroZipFilesMethod();
            CbSettingShowEmptyPanCountMethod();
        }
    }

    private void CbSettingModulePendingDigitalsMethod()
    {
        WriteLocalSetting("ModulePendingDigitals", CbSettingModulePendingDigitals.ToString());
    }

    private async void DeleteCSCustomerMethod()
    {
        if (string.IsNullOrEmpty(SelectedCustomerName))
            return;

        SMessageBoxResult result = ShowMessageBox("Question", $"Are you sure you want to delete the selected customer?", SMessageBoxButtons.YesNo, NotificationIcon.Warning, 150, MainWindow.Instance);

        if (result == SMessageBoxResult.Yes)
        {
            if (await DeleteCustomer(SelectedCustomerName))
            {
                ShowNotificationMessage("Customer", "Customer deleted!", NotificationIcon.Success);
                BuildCustomerSuggestionsList();
                SelectedCustomerName = "";
            }
            else
                ShowNotificationMessage("Customer", "Customer was not deleted!", NotificationIcon.Error);
        }
    }

    private async void DeleteCSSuggestionMethod()
    {
        if (string.IsNullOrEmpty(SelectedCustomerName) || string.IsNullOrEmpty(SelectedCustomerSuggestion))
            return;

        SMessageBoxResult result = ShowMessageBox("Question", $"Are you sure you want to delete the selected customer suggestion / replacement?", SMessageBoxButtons.YesNo, NotificationIcon.Warning, 15, MainWindow.Instance);
        if (result == SMessageBoxResult.Yes)
        {
            if (await DeleteCustomerSuggestion(SelectedCustomerName, SelectedCustomerSuggestion))
            {
                ShowNotificationMessage("Customer suggestion", "Customer suggestion deleted!", NotificationIcon.Success);
                if (!string.IsNullOrEmpty(SelectedCustomerName))
                    BuildCustomerSuggestionReplacementList();
            }
            else
                ShowNotificationMessage("Customer suggestion", "Customer suggestion was not deleted!", NotificationIcon.Error);
        }
    }


    private async void AddNewCustomerSuggestionMethod()
    {
        if (string.IsNullOrEmpty(CSNewCustomer.Trim()) || string.IsNullOrEmpty(CleanUpCustomerName(CSNewReplacement)))
            return;

        if (await AddNewCustomerSuggestion(CSNewCustomer.Trim(), CleanUpCustomerName(CSNewReplacement)))
        {
            CSNewReplacement = "";
            BuildCustomerSuggestionsList();
            if (!string.IsNullOrEmpty(SelectedCustomerName))
                BuildCustomerSuggestionReplacementList();
            else
                CSNewCustomer = "";

            ShowNotificationMessage("Customer suggestion", "New customer suggestion added!", NotificationIcon.Success);
        }
        else
            ShowNotificationMessage("Customer suggestion", "The new suggestion was not added!", NotificationIcon.Error);
    }

    private void CRCheckingRadioButtonMethod(object obj)
    {
        CRSelectedItemToBeContained = (string)obj;
    }

    private async void DeleteSelectedCommentRuleMethod()
    {
        if (SelectedCommentRule is null)
            return;

        SMessageBoxResult result = ShowMessageBox("Question", $"Are you sure you want to delete the selected comment rule?", SMessageBoxButtons.YesNo, NotificationIcon.Warning, 15, MainWindow.Instance);
        if (result == SMessageBoxResult.Yes)
        {
            if (await DeleteCommentRule(SelectedCommentRule))
            {
                ShowNotificationMessage("Comment rule", "Comment Rule is now deleted!", NotificationIcon.Success);
                BuildCommentRuleList();
                SelectedCommentRule = null;
                SelectedCommentRule = new();
            }
            else
                ShowNotificationMessage("Comment rule", "Comment Rule was not deleted!", NotificationIcon.Error);
        }
    }

    private async void AddNewCommentRuleMethod()
    {
        if (string.IsNullOrEmpty(CRNewCustomer.Trim()) || string.IsNullOrEmpty(CRNewRuleName.Trim()) || string.IsNullOrEmpty(CRNewCommentToBeInserted.Trim()) || string.IsNullOrEmpty(CRSelectedItemToBeContained.Trim()))
            return;

        if (await AddNewCommentRule(CRNewRuleName.Trim(), CRNewCustomer.Trim(), CRNewCommentToBeInserted.Trim(), CRSelectedItemToBeContained))
        {
            CRNewCustomer = "";
            CRNewRuleName = "";
            CRNewCommentToBeInserted = "";
            CRSelectedItemToBeContained = "";
            SelectedCommentRule = null;
            SelectedCommentRule = new();
            BuildCommentRuleList();

            ShowNotificationMessage("Comment Rule", "New comment rule added!", NotificationIcon.Success);
        }
        else
            ShowNotificationMessage("Comment Rule", "The new comment rule was not added!", NotificationIcon.Error);

        _MainWindow.crItemsRadioButtonEmpty.IsChecked = true;
    }

    private void CbSettingExtractIteroZipFilesMethod()
    {
        WriteLocalSetting("ExtractIteroZipFiles", CbSettingExtractIteroZipFiles.ToString());

        if (fswIteroZipFileWhatcher.Path is null)
        {
            if (CbSettingExtractIteroZipFiles && Directory.Exists(PmDownloadFolder) && Directory.Exists(PmIteroExportFolder))
            {
                fswIteroZipFileWhatcher.Path = PmDownloadFolder;
                fswIteroZipFileWhatcher.Filter = "iTero_Export_*.zip";
                fswIteroZipFileWhatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                fswIteroZipFileWhatcher.Created += new FileSystemEventHandler(FswIteroZipFileWhatcher_Created);
                fswIteroZipFileWhatcher.Changed += new FileSystemEventHandler(FswIteroZipFileWhatcher_Created);
                fswIteroZipFileWhatcher.EnableRaisingEvents = true;
            }
        }
    }

    #endregion SETTINGS TAB METHODS

    private void HideNotification()
    {
        NotificationTimer_Tick(null, null);
    }


    private async void NotificationTimer_Tick(object? sender, EventArgs e)
    {
        DoubleAnimation da = new(0.01, TimeSpan.FromMilliseconds(500));
        MainWindow.Instance.notificationMessagePanel.BeginAnimation(FrameworkElement.OpacityProperty, da);


        await Task.Delay(1000);

        NotificationMessageTitle = "";
        NotificationMessageBody = "";
        NotificationMessageIcon = @"\Images\MessageIcons\Info.png";
        NotificationMessagePosition = new Thickness(-500, 0, 0, -500);
        NotificationMessageVisibility = Visibility.Collapsed;
        notificationTimer.Stop();
    }


    public void ExploreOrderFolder()
    {
        string SelectedOrderID = ThreeShapeObject!.IntOrderID!;
        if (string.IsNullOrEmpty(SelectedOrderID))
            return;

        if (Directory.Exists($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\"))
        {
            try
            {
                Process.Start("explorer.exe", $@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\");
            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
            }
        }
    }


    public void SetShadeClick(string shade)
    {
        string SelectedOrderID = ThreeShapeObject!.IntOrderID!;
        if (string.IsNullOrEmpty(SelectedOrderID))
            return;

        if (!Directory.Exists($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\History"))
            Directory.CreateDirectory($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\History");

        SMessageBoxResult res = ShowMessageBox("Set shade", $"Sure you want to set the shade to: {shade}?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, MainWindow.Instance);

        if (res == SMessageBoxResult.No)
            return;

        File.WriteAllText($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\shade", shade);
        File.WriteAllText($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\History\LastShadeSetBy", $"{Environment.MachineName} - {DateTime.Now:M/d/yyyy h:mm:ss tt}");

        ListUpdateTimer_Tick(null, null);
    }

    public void GenerateStCopy()
    {
        string SelectedOrderID = ThreeShapeObject!.IntOrderID!;
        if (string.IsNullOrEmpty(SelectedOrderID))
            return;

        try
        {
            bool regenerate = false;
            if ((ThreeShapeDirectoryHelper.Length > 1) && CheckFolderIsWritable(ThreeShapeDirectoryHelper + SelectedOrderID)) // if 3Shape dir not set it up or it is setted up but the case folder not writable (maybe doesn't exist)
            {
                if (!File.Exists($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\Manufacturers.stCopy"))
                    File.Copy($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\Manufacturers.3ml", $@"{ThreeShapeDirectoryHelper}\{SelectedOrderID}\Manufacturers.stCopy");
                else
                {
                    File.Move($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\Manufacturers.stCopy", $@"{ThreeShapeDirectoryHelper}\{SelectedOrderID}\Manufacturers.stCopy{DateTime.Now:HHmmss}");
                    File.Copy($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\Manufacturers.3ml", $@"{ThreeShapeDirectoryHelper}\{SelectedOrderID}\Manufacturers.stCopy");
                    regenerate = true;
                }

                if (!File.Exists($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\{SelectedOrderID}.stCopy"))
                    File.Copy($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\{SelectedOrderID}.xml", $@"{ThreeShapeDirectoryHelper}\{SelectedOrderID}\{SelectedOrderID}.stCopy");
                else
                {
                    File.Move($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\{SelectedOrderID}.stCopy", $@"{ThreeShapeDirectoryHelper}\{SelectedOrderID}\{SelectedOrderID}.stCopy{DateTime.Now:HHmmss}");
                    File.Copy($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\{SelectedOrderID}.xml", $@"{ThreeShapeDirectoryHelper}\{SelectedOrderID}\{SelectedOrderID}.stCopy");
                    regenerate = true;
                }

                if (!File.Exists($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\client.info"))
                    File.WriteAllText($@"{ThreeShapeDirectoryHelper}{SelectedOrderID}\client.info", ThreeShapeObject.Customer);
            }

            if (regenerate)
                ShowNotificationMessage("StCopy", "StCopy successfully regenerated!", NotificationIcon.Info);
            else
                ShowNotificationMessage("StCopy", "StCopy successfully generated!", NotificationIcon.Info);
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowNotificationMessage("Error", ex.Message, NotificationIcon.Error);
        }
    }

    private void ListUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (ListUpdateable && AllowThreeShapeOrderListUpdates)
        {
            AllowToShowProgressBar = false;
            if (!string.IsNullOrEmpty(ActiveFilterInUse))
                Search(ActiveFilterInUse, true);
            if (!string.IsNullOrEmpty(ActiveSearchString))
                Search(ActiveSearchString);
        }
    }

    private async void SearchFieldArchivesKeyDown()
    {
        if (SearchStringArchives == " ")
        {
            CurrentArchivesList.Clear();
            SearchStringArchives = SearchStringArchives.Trim();
        }



        if (SearchStringArchives.Length > 0)
        {
            _MainWindow.pbArchivesProgressBar.Value = 0;
            CurrentArchivesList.Clear();
            _MainWindow.listViewArchives.ItemsSource = CurrentArchivesList;
            _MainWindow.listViewArchives.Items.Refresh();
            //GroupList();

            if (PreviousSearchStringArchives != SearchStringArchives)
                ArchiveResultOffsetOnArchivePage = 0;

            PreviousSearchStringArchives = SearchStringArchives;
            SearchArchives(SearchStringArchives.Trim());
            SearchStringArchives = "";

            //SearchHistoryArchives = await GetBackAllSearchHistoryFromLocalDB();

            //UpdateSearchHistorryContextMenu();
        }

        else if (PreviousSearchStringArchives.Length > 0)
        {
            SearchArchives(PreviousSearchStringArchives.Trim());
        }
    }

    private async void SearchFieldKeyDown()
    {
        ShowingFiltersPanel = Visibility.Collapsed;
        if (SearchString.Length > 0)
        {
            _MainWindow.pb3ShapeProgressBar.Value = 0;
            Current3ShapeOrderList.Clear();
            _MainWindow.listView3ShapeOrders.ItemsSource = Current3ShapeOrderList;
            _MainWindow.listView3ShapeOrders.Items.Refresh();
            GroupList();

            Search(SearchString.Trim());
            AddStringToSearchHistoryLocalDB(SearchString.Trim());
            ActiveFilterInUse = "";
            ActiveSearchString = SearchString;
            SearchString = "";
            SearchHistory = await GetBackAllSearchHistoryFromLocalDB();

            //UpdateSearchHistorryContextMenu();
        }
    }



    private async void SearchFieldKeyDownOnTabs()
    {
        if (SearchString.Length > 1)
        {
            ListUpdateable = true;
            BuildingUpDates();
            if (bwListCasesGlobal.IsBusy != true)
            {
                bwListCasesGlobal.RunWorkerAsync(new SearchData
                {
                    FilterInUse = false,
                    KeyWordOrFilter = SearchString
                });
            }
            else
            {
                bwListCasesGlobal.CancelAsync();
            }
        }
        else
            GlobalSearchResult.Clear();
    }

    private async void SearchFieldKeyDownOnHome()
    {
        if (SearchStringGlobal.Length > 1)
        {
            ListUpdateable = true;
            BuildingUpDates();
            if (bwListCasesGlobal.IsBusy != true)
            {
                bwListCasesGlobal.RunWorkerAsync(new SearchData
                {
                    FilterInUse = false,
                    KeyWordOrFilter = SearchStringGlobal
                });
            }
            else
            {
                bwListCasesGlobal.CancelAsync();
            }
        }
        else
            GlobalSearchResult.Clear();
    }

    private void SearchFieldEnterKeyDownOnHome()
    {
        SearchString = SearchStringGlobal;
        SearchStringGlobal = "";
        ResultIn3Shape = 0;
        ResultInArchives = 0;
        SearchFieldKeyDown();
        SwitchTo3ShapeOrdersTab();
        GlobalSearchResult.Clear();
    }


    private async void UpdateOrderIssuesList()
    {
        OrderIssuesList = await GetAllSentOutIssues();
    }


    private async void UpdatePanNrDuplicatesList()
    {
        PanNrDuplicatesList = await GetAllPanNrDuplicates();
    }



    private void ShowWarningOfNewDuplicatedPanNumberUse()
    {
        Application.Current.Dispatcher.Invoke(new Action(async () =>
        {
            await BlinkWindow("red");
            ShowNotificationMessage("Duplicated Pan Number found", $"Possible duplicate use of pan number found!", NotificationIcon.Warning);
            ShowMessageBox("Duplicated Pan Number found", "Possible duplicate use of pan number found!", SMessageBoxButtons.Close, NotificationIcon.Warning, 250, _MainWindow);


        }));
    }



    private void ItemClicked(object obj)
    {
        bool isArchiveSelection = false;
        ThreeShapeOrdersModel? model = obj as ThreeShapeOrdersModel;

        if (model is null && obj is ArchivesOrdersModel archiveModel)
        {
            isArchiveSelection = true;
            model = CreateOrderInfoModelFromArchiveOrder(archiveModel);
        }

        if (model is null && obj is GlobalSearchModel globalSearchModel)
        {
            if (string.Equals(globalSearchModel.Source, "3Shape", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(globalSearchModel.IntOrderId))
            {
                model = Current3ShapeOrderList.FirstOrDefault(x =>
                    string.Equals(x.IntOrderID, globalSearchModel.IntOrderId, StringComparison.OrdinalIgnoreCase));
            }

            isArchiveSelection = string.Equals(globalSearchModel.Source, "Archives", StringComparison.OrdinalIgnoreCase);
            model ??= CreateOrderInfoModelFromGlobalSearch(globalSearchModel);
        }

        if (model is null)
        {
            return;
        }

        Debug.WriteLine(model.IntOrderID);
        FocusOnSearchField();

        OrderBeingWatched = model.IntOrderID ?? string.Empty;
        ThreeShapeObject = model;

        if (ThreeShapeObject is null)
            return;

        if (!isArchiveSelection)
        {
            SelectedItem = ThreeShapeObject;
            TurnOnOffToolBarButtons(ThreeShapeObject);
        }

    }

    private static ThreeShapeOrdersModel? CreateOrderInfoModelFromGlobalSearch(GlobalSearchModel globalSearchModel)
    {
        if (string.IsNullOrWhiteSpace(globalSearchModel.IntOrderId))
        {
            return null;
        }

        bool isArchive = string.Equals(globalSearchModel.Source, "Archives", StringComparison.OrdinalIgnoreCase);

        if (isArchive)
        {
            ArchivesOrdersModel? archiveModel = ReadArchiveOrderByOrderId(globalSearchModel.IntOrderId);
            if (archiveModel is not null)
            {
                return CreateOrderInfoModelFromArchiveOrder(archiveModel);
            }
        }

        string orderFolder = ResolveOrderFolder(globalSearchModel.OrderFolder, globalSearchModel.BaseFolder, globalSearchModel.IntOrderId, globalSearchModel.XMLFile);
        string xmlFilePath = ResolveXmlFilePath(globalSearchModel.XMLFile, orderFolder, globalSearchModel.IntOrderId);

        var createdDate = !string.IsNullOrWhiteSpace(globalSearchModel.CreateDate)
            ? globalSearchModel.CreateDate
            : globalSearchModel.CreateDateLong;

        string panNumber = DeterminePanNumber(globalSearchModel.IntOrderId, globalSearchModel.Patient_LastName ?? string.Empty, globalSearchModel.Patient_FirstName ?? string.Empty);
        string processStatus = "psClosed";
        string processLock = "plReady";
        string scanSource = "ssUnknown";
        string imageSource = @"\Images\ListViewIcons\" + IconSelect(processStatus, scanSource, processLock) + ".png";
        string panColor = GetBackPanColorHEX(panNumber);
        if (string.IsNullOrWhiteSpace(panNumber) || panColor == "#FFFFFF")
            panColor = "Transparent";

        return new ThreeShapeOrdersModel
        {
            IntOrderID = globalSearchModel.IntOrderId,
            PanNumber = panNumber,
            Patient_FirstName = globalSearchModel.Patient_FirstName ?? string.Empty,
            Patient_LastName = globalSearchModel.Patient_LastName ?? string.Empty,
            Customer = globalSearchModel.Customer ?? string.Empty,
            Items = globalSearchModel.Items ?? string.Empty,
            MaxCreateDate = createdDate ?? string.Empty,
            CacheMaxScanDate = string.Empty,
            ProcessStatusID = processStatus,
            MaxProcessStatusID = processStatus,
            ProcessLockID = processLock,
            ScanSource = scanSource,
            ScanSourceFriendlyName = GetScanner(scanSource),
            TraySystemType = "stNone",
            ManufName = string.Empty,
            CacheMaterialName = string.Empty,
            OrderComments = globalSearchModel.ReasonIsDead ?? string.Empty,
            ImageSource = imageSource,
            PanColor = panColor,
            IsCaseWereDesigned = false,
            OrderFolderPath = isArchive ? orderFolder : string.Empty,
            XmlFilePath = isArchive ? xmlFilePath : string.Empty,
            ArchiveBaseFolderPath = isArchive ? globalSearchModel.BaseFolder ?? string.Empty : string.Empty
        };
    }

    private static ThreeShapeOrdersModel? CreateOrderInfoModelFromArchiveOrder(ArchivesOrdersModel archiveModel)
    {
        if (string.IsNullOrWhiteSpace(archiveModel.OrderID))
        {
            return null;
        }

        string orderFolder = ResolveOrderFolder(null, archiveModel.BaseFolder, archiveModel.OrderID, archiveModel.XMLFile);
        string xmlFilePath = ResolveXmlFilePath(archiveModel.XMLFile, orderFolder, archiveModel.OrderID);

        string processStatus = string.IsNullOrWhiteSpace(archiveModel.ProcessStatusID) ? "psClosed" : archiveModel.ProcessStatusID;
        string processLock = string.IsNullOrWhiteSpace(archiveModel.ProcessLockID) ? "plReady" : archiveModel.ProcessLockID;
        string scanSource = string.IsNullOrWhiteSpace(archiveModel.ScanSource) ? "ssUnknown" : archiveModel.ScanSource;

        string panNumber = archiveModel.PanNumber ?? string.Empty;
        if (string.IsNullOrWhiteSpace(panNumber))
            panNumber = DeterminePanNumber(archiveModel.OrderID, archiveModel.Patient_LastName ?? string.Empty, archiveModel.Patient_FirstName ?? string.Empty);

        string iconSource = @"\Images\ListViewIcons\" + IconSelect(processStatus, scanSource, processLock) + ".png";
        string panColor = GetBackPanColorHEX(panNumber);
        if (string.IsNullOrWhiteSpace(panNumber) || panColor == "#FFFFFF")
            panColor = "Transparent";

        string material = (archiveModel.CacheMaterialName ?? string.Empty).Replace("\"", "").Trim();

        return new ThreeShapeOrdersModel
        {
            IntOrderID = archiveModel.OrderID,
            PanNumber = panNumber,
            Patient_FirstName = archiveModel.Patient_FirstName ?? string.Empty,
            Patient_LastName = archiveModel.Patient_LastName ?? string.Empty,
            Customer = archiveModel.Customer ?? string.Empty,
            Items = archiveModel.Items ?? string.Empty,
            MaxCreateDate = archiveModel.Registered ?? string.Empty,
            CacheMaxScanDate = archiveModel.CacheMaxScanDate ?? string.Empty,
            ProcessStatusID = processStatus,
            MaxProcessStatusID = processStatus,
            ProcessLockID = processLock,
            ScanSource = scanSource,
            ScanSourceFriendlyName = GetScanner(scanSource),
            TraySystemType = "stNone",
            ManufName = archiveModel.ManufName ?? string.Empty,
            CacheMaterialName = material,
            OrderComments = archiveModel.OrderComments ?? archiveModel.ReasonIsDead ?? string.Empty,
            OriginalOrderID = archiveModel.OriginalOrderID ?? string.Empty,
            DesignerID = archiveModel.DesignerID ?? string.Empty,
            DesignerName = archiveModel.DesignerName ?? string.Empty,
            ImageSource = iconSource,
            PanColor = panColor,
            IsCaseWereDesigned = false,
            OrderFolderPath = orderFolder,
            XmlFilePath = xmlFilePath,
            ArchiveBaseFolderPath = archiveModel.BaseFolder ?? string.Empty
        };
    }

    private static string ResolveOrderFolder(string? orderFolder, string? baseFolder, string? orderId, string? xmlFile)
    {
        if (IsValidArchivePath(orderFolder))
        {
            return orderFolder!.Trim();
        }

        if (IsValidArchivePath(baseFolder) && !string.IsNullOrWhiteSpace(orderId))
        {
            return Path.Combine(baseFolder!.Trim(), orderId.Trim());
        }

        if (IsValidArchivePath(xmlFile))
        {
            string? folderFromXml = Path.GetDirectoryName(xmlFile!.Trim());
            return folderFromXml ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ResolveXmlFilePath(string? xmlFile, string orderFolder, string orderId)
    {
        if (IsValidArchivePath(xmlFile))
        {
            return xmlFile!.Trim();
        }

        if (IsValidArchivePath(orderFolder) && !string.IsNullOrWhiteSpace(orderId))
        {
            return Path.Combine(orderFolder.Trim(), orderId + ".xml");
        }

        return string.Empty;
    }

    private static bool IsValidArchivePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        return !string.Equals(trimmed, "%%", StringComparison.Ordinal) &&
               !string.Equals(trimmed, "-", StringComparison.Ordinal) &&
               !string.Equals(trimmed, "Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static ArchivesOrdersModel? ReadArchiveOrderByOrderId(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return null;

        try
        {
            string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();
            string safeOrderId = orderId.Replace("'", "''");
            string query = $@"SELECT TOP 1 * FROM dbo.Archives WHERE OrderID = '{safeOrderId}' ORDER BY LastUdated DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return new ArchivesOrdersModel
            {
                OrderID = reader["OrderID"].ToString(),
                PanNumber = reader["PanNumber"].ToString(),
                Patient_FirstName = reader["Patient_FirstName"].ToString(),
                Patient_LastName = reader["Patient_LastName"].ToString(),
                Registered = reader["Registered"].ToString(),
                Customer = reader["Customer"].ToString(),
                XMLFile = reader["XMLFile"].ToString(),
                BaseFolder = reader["BaseFolder"].ToString(),
                HostingComputer = reader["HostingComputer"].ToString(),
                LastUdated = reader["LastUdated"].ToString(),
                Items = reader["Items"].ToString(),
                ItemsDetailed = reader["ItemsDetailed"].ToString(),
                OrderComments = reader["OrderComments"].ToString(),
                Icon = reader["Icon"].ToString(),
                ProcessStatusID = reader["ProcessStatusID"].ToString(),
                ProcessLockID = reader["ProcessLockID"].ToString(),
                ScanSource = reader["ScanSource"].ToString(),
                DesignModuleID = reader["DesignModuleID"].ToString(),
                DentalVersion = reader["DentalVersion"].ToString(),
                OriginalOrderID = reader["OriginalOrderID"].ToString(),
                ManufName = reader["ManufName"].ToString(),
                CacheMaterialName = reader["CacheMaterialName"].ToString(),
                CreateDate = reader["CreateDate"].ToString(),
                CacheMaxScanDate = reader["CacheMaxScanDate"].ToString(),
                IsStillAlive = reader["IsStillAlive"].ToString(),
                ReasonIsDead = reader["ReasonIsDead"].ToString(),
                DesignerID = reader["DesignerID"].ToString(),
                DesignerName = reader["DesignerName"].ToString()
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return null;
        }
    }

    public void ArchivesBaseFolderItemClicked(object obj)
    {
        ArchivesOrdersModel model = (ArchivesOrdersModel)obj;
        SelectedArchiveItem = model;

        try
        {
            if (!string.IsNullOrEmpty(model.BaseFolder) && Directory.Exists(model.BaseFolder))
                Process.Start("explorer.exe", "\"" + model.BaseFolder + model.OrderID + "\"");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    public void ArchivesBaseFolderItemClickedOnGlobalSearch(object obj)
    {
        GlobalSearchModel model = (GlobalSearchModel)obj;

        try
        {
            if (!string.IsNullOrEmpty(model.OrderFolder) && Directory.Exists(model.OrderFolder))
                Process.Start("explorer.exe", "\"" + model.OrderFolder + "\"");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    public void ArchivesItemClicked(object obj)
    {
        ArchivesOrdersModel model = (ArchivesOrdersModel)obj;
        SelectedArchiveItem = model;
        OpenUpArchiveExportWindow(model);
    }
    public void ArchivesItemClickedOnGlobalSearch(object obj)
    {
        GlobalSearchModel model = (GlobalSearchModel)obj;
        OpenUpArchiveExportWindow(model);
    }

    private async void FocusOnDoctorsField()
    {
        await Task.Delay(150);
        _MainWindow.tbDoctor.Focus();
    }
    private async void FocusOnYearField()
    {
        await Task.Delay(150);
        _MainWindow.cbYear.Focus();
    }
    private async void FocusOnMonthField()
    {
        await Task.Delay(150);
        _MainWindow.cbMonth.Focus();
    }

    private async void FocusOnSearchField()
    {
        await Task.Delay(150);
        _MainWindow.tbSearch.Focus();
    }

    private void ItemRightClicked(object obj)
    {
        ThreeShapeOrdersModel model = (ThreeShapeOrdersModel)obj;
        Debug.WriteLine(model.IntOrderID);
        FocusOnSearchField();

        OrderBeingWatched = model.IntOrderID!;
        ThreeShapeObject = model;

        if (ThreeShapeObject is null)
            return;

        SelectedItem = ThreeShapeObject;
        TurnOnOffToolBarButtons(ThreeShapeObject);
        OpenUpOrderInfoWindow();
    }


    private void TurnOnOffToolBarButtons(ThreeShapeOrdersModel? threeShapeObject)
    {
        ShowingFiltersPanel = Visibility.Collapsed;


        if (threeShapeObject is null)
            return;

        string SelectedOrderID = threeShapeObject.IntOrderID!;

        #region Rename
        bool isTheFilesAccessible = false;
        bool itIsACopiedCase = false;
        // checking if the folder for that case are exist and writable (in 3Shape orders folder)
        if ((ThreeShapeDirectoryHelper.Length < 1) || !CheckFolderIsWritable(ThreeShapeDirectoryHelper + SelectedOrderID)) // if 3Shape dir not set it up or it is setted up but the case folder not writable (maybe doesn't exist)
        {
            isTheFilesAccessible = false;
        }
        else
        {

            isTheFilesAccessible = true;

            string XMLFile = ThreeShapeDirectoryHelper + SelectedOrderID + @"\" + SelectedOrderID + ".xml";
            string originalFileName = "";
            try
            {
                string[] lines = File.ReadAllLines(XMLFile);
                foreach (string line in lines.Where(l => l.Contains("OriginalOrderID")))
                    originalFileName = line.Replace("<Property name=\"OriginalOrderID\" value=\"", "").Replace("\"", "").Replace("/>", "").Trim();

                if (originalFileName != "")
                    itIsACopiedCase = true;
                else
                    itIsACopiedCase = false;
            }
            catch (Exception ex)
            {
                AddDebugLine(ex);
                itIsACopiedCase = false;
            }
        }


        ThreeShapeOrderInspectionModel inspectedOrder = InspectThreeShapeOrder(SelectedOrderID);

        string caseStatus = inspectedOrder.CaseStatus!;



        #endregion Rename

    }



    private void GetInfoOn3ShapeOrder(string OrderID)
    {
        OrderBeingWatched = OrderID;
        ThreeShapeObject = Current3ShapeOrderList!.FirstOrDefault(x => x.IntOrderID == OrderBeingWatched)!;
        if (ThreeShapeObject is null)
            return;
        //orderDetailsWindow = new OrderDetailsWindow();
        //orderDetailsWindow.ShowDialog(this, ThreeShapeObject);
        OrderBeingWatched = "";
    }


    private void ExpanderCollapsed(object obj)
    {
        var expander = (Expander)obj;
        var dc = (CollectionViewGroup)expander.DataContext;
        var groupName = dc.Name.ToString();
        ExpandStates[groupName!] = expander.IsExpanded;
    }

    private void ExpanderLoaded(object obj)
    {
        var expander = (Expander)obj;
        var dc = (CollectionViewGroup)expander.DataContext;
        var groupName = dc.Name.ToString();
        if (ExpandStates.TryGetValue(groupName!, out var value))
            expander.IsExpanded = value;
    }

    private async void ListCases_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        try
        {

            _MainWindow.listView3ShapeOrders.ItemsSource = Current3ShapeOrderList;
            _MainWindow.listView3ShapeOrders.Items.Refresh();
            _MainWindow.pb3ShapeProgressBar.Value = 0;
            await Task.Run(() => GroupList());

            // Order count in list
            OrderCountText = Current3ShapeOrderList.Count == 1 ? Current3ShapeOrderList.Count + " order" : Current3ShapeOrderList.Count + " orders";
            OrderCount = Current3ShapeOrderList.Count;
            AllowToShowProgressBar = true;


            if (OrderBeingWatched.Length > 0)
            {
                ThreeShapeObject = Current3ShapeOrderList.FirstOrDefault(x => x.IntOrderID == OrderBeingWatched)!;
                //if (!IsTheSame(orderDetailsWindow.OrderObject, ThreeShapeObject))
                //{
                //    orderDetailsWindow.OrderObject = ThreeShapeObject;
                //    orderDetailsWindow.UpdateForm();
                //}
            }
        }
        catch (Exception ex)
        {
        }
    }

    private async void ListArchivesCases_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        try
        {

            _MainWindow.listViewArchives.ItemsSource = CurrentArchivesList;
            _MainWindow.listViewArchives.Items.Refresh();
            _MainWindow.pbArchivesProgressBar.Value = 0;
            await Task.Run(() => GroupList());

            // Order count in list
            ArchivesCountText = CurrentArchivesList.Count == 1 ? CurrentArchivesList.Count + " order" : CurrentArchivesList.Count + " orders";
            ArchivesCount = CurrentArchivesList.Count;
            AllowToShowArchivesProgressBar = true;
        }
        catch (Exception ex)
        {
        }
    }

    public void GroupList()
    {
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            try
            {


                _MainWindow.pb3ShapeProgressBar.Value = 0;

                _MainWindow.listView3ShapeOrders.Items.GroupDescriptions.Clear();
                //var property = _MainWindow.GroupBy.SelectedItem as string;
                var property = SelectedGroupByItem;


                if (FilterString != "MyRecent" && FilterString != "MyRecent30")
                    WriteLocalSetting("GroupBy", property!);



                if (property == "None" || property is null)
                {
                    if (DataView is not null)
                    {
                        DataView.SortDescriptions.Clear();
                        SortDescription sd;

                        if (FilterString == "MyRecent" || FilterString == "MyRecent30")
                            sd = new("LastModificationForSorting", ListSortDirection.Descending);
                        else
                            sd = new("IntOrderID", ListSortDirection.Ascending);

                        DataView.SortDescriptions.Add(sd);
                        DataView.Refresh();
                    }
                    return;
                }

                property = property.Replace(" ", "");

                if (property == "LasttouchedBy")
                {
                    property = "LastModifiedComputerName";
                }

                if (property == "ScanSource")
                {
                    property = "ScanSourceFriendlyName";
                }

                if (DataView is not null)
                {
                    PropertyGroupDescription groupDescription = new(property);
                    DataView.GroupDescriptions.Add(groupDescription);

                    DataView.SortDescriptions.Clear();
                    SortDescription sd;
                    sd = new SortDescription(property, ListSortDirection.Ascending);
                    DataView.SortDescriptions.Add(sd);

                    sd = new SortDescription("LastModificationForSorting", ListSortDirection.Descending);
                    DataView.SortDescriptions.Add(sd);

                    sd = new SortDescription("CreateDateForSorting", ListSortDirection.Descending);
                    DataView.SortDescriptions.Add(sd);
                    DataView.Refresh();

                }
            }
            catch (Exception ex)
            {
                AddDebugLine(ex, ex.Message);
            }
        }));
    }

    private bool IsTheSame(ThreeShapeOrdersModel FirstObject, ThreeShapeOrdersModel? SecondObject)
    {
        if (FirstObject == null || SecondObject == null)
            return false;

        if (FirstObject.IntOrderID != SecondObject.IntOrderID) return false;
        if (FirstObject.Patient_FirstName != SecondObject.Patient_FirstName) return false;
        if (FirstObject.Patient_LastName != SecondObject.Patient_LastName) return false;
        if (FirstObject.OrderComments != SecondObject.OrderComments) return false;
        if (FirstObject.Items != SecondObject.Items) return false;
        if (FirstObject.OperatorName != SecondObject.OperatorName) return false;
        if (FirstObject.Customer != SecondObject.Customer) return false;
        if (FirstObject.ManufName != SecondObject.ManufName) return false;
        if (FirstObject.CacheMaterialName != SecondObject.CacheMaterialName) return false;
        if (FirstObject.ScanSource != SecondObject.ScanSource) return false;
        if (FirstObject.CacheMaxScanDate != SecondObject.CacheMaxScanDate) return false;
        if (FirstObject.TraySystemType != SecondObject.TraySystemType) return false;
        if (FirstObject.MaxCreateDate != SecondObject.MaxCreateDate) return false;
        if (FirstObject.MaxProcessStatusID != SecondObject.MaxProcessStatusID) return false;
        if (FirstObject.ProcessStatusID != SecondObject.ProcessStatusID) return false;
        if (FirstObject.AltProcessStatusID != SecondObject.AltProcessStatusID) return false;
        if (FirstObject.ProcessLockID != SecondObject.ProcessLockID) return false;
        if (FirstObject.WasSent != SecondObject.WasSent) return false;
        if (FirstObject.ModificationDate != SecondObject.ModificationDate) return false;
        if (FirstObject.ImageSource != SecondObject.ImageSource) return false;
        if (FirstObject.ListViewGroup != SecondObject.ListViewGroup) return false;
        if (FirstObject.PanColor != SecondObject.PanColor) return false;
        if (FirstObject.PanColorName != SecondObject.PanColorName) return false;
        if (FirstObject.CaseStatus != SecondObject.CaseStatus) return false;
        if (FirstObject.PanNumber != SecondObject.PanNumber) return false;
        if (FirstObject.LastModificationForSorting != SecondObject.LastModificationForSorting) return false;
        if (FirstObject.LastModifiedComputerName != SecondObject.LastModifiedComputerName) return false;
        if (FirstObject.CreateDateForSorting != SecondObject.CreateDateForSorting) return false;
        if (FirstObject.ScanSourceFriendlyName != SecondObject.ScanSourceFriendlyName) return false;
        if (FirstObject.CacheMaxScanDateFriendly != SecondObject.CacheMaxScanDateFriendly) return false;
        if (FirstObject.MaxCreateDateFriendly != SecondObject.MaxCreateDateFriendly) return false;
        if (FirstObject.IsCaseWereDesigned != SecondObject.IsCaseWereDesigned) return false;
        if (FirstObject.IsLocked != SecondObject.IsLocked) return false;
        if (FirstObject.IsCheckedOut != SecondObject.IsCheckedOut) return false;

        return true;
    }

    private async void ListCasesGlobal_DoWork(object? sender, DoWorkEventArgs e)
    {
        if (SearchString.Length < 2)
        {
            ClearAllSearchCriteria();
            GlobalSearchResultArchives = [];
            GlobalSearchResult3Shape = [];
            return;
        }

        if (SearchStringGlobal != PreviousSearchStringGlobal)
        {
            PreviousSearchStringGlobal = SearchStringGlobal;
            ArchiveResultOffset = 0;
        }

        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (_MainWindow.mainTabControl.SelectedItem == _MainWindow.tabLabnext || _MainWindow.mainTabControl.SelectedItem == _MainWindow.folderSubscriptionTab)
            {
                LabnextCanReload = true;
                HomeButtonShows = Visibility.Collapsed;
                RefreshButtonShows = Visibility.Collapsed;
                _MainWindow.mainTabControl.SelectedItem = _MainWindow.HomeTab;
            }
        }));

        ThreeShapeServerIsDown = false;
        var data = (SearchData)e.Argument!;
        string keyword = data.KeyWordOrFilter!;
        string queryString = "";
        int id = 0;
        int idA = 0;
        ResultIn3Shape = 0;
        ResultInArchives = 0;

        keyword = keyword.Replace("'", "").Replace("%", "").Trim();
        ObservableCollection<GlobalSearchModel> listArchives = [];
        ObservableCollection<GlobalSearchModel> list3Shape = [];

        bool searchingForPanNumber = false;

        if (PanNumberRegex().IsMatch(keyword))
            searchingForPanNumber = true;



        string searchQueryStr = "";
        string searchForYear = "";
        string searchForMonthFrom = "01";
        string searchForMonthTo = "12";

        if (FilterYearItemSelected == "All time")
            searchForYear = "";
        else
            searchForYear = FilterYearItemSelected;


        if (FilterMonthItemSelected == "All months")
        {
            searchForMonthFrom = "01";
            searchForMonthTo = "12";
        }
        else
        {
            switch (FilterMonthItemSelected)
            {
                case "Jan": searchForMonthFrom = "01"; searchForMonthTo = "01"; break;
                case "Feb": searchForMonthFrom = "02"; searchForMonthTo = "02"; break;
                case "Mar": searchForMonthFrom = "03"; searchForMonthTo = "03"; break;
                case "Apr": searchForMonthFrom = "04"; searchForMonthTo = "04"; break;
                case "May": searchForMonthFrom = "05"; searchForMonthTo = "05"; break;
                case "Jun": searchForMonthFrom = "06"; searchForMonthTo = "06"; break;
                case "Jul": searchForMonthFrom = "07"; searchForMonthTo = "07"; break;
                case "Aug": searchForMonthFrom = "08"; searchForMonthTo = "08"; break;
                case "Sep": searchForMonthFrom = "09"; searchForMonthTo = "09"; break;
                case "Oct": searchForMonthFrom = "10"; searchForMonthTo = "10"; break;
                case "Nov": searchForMonthFrom = "11"; searchForMonthTo = "11"; break;
                case "Dec": searchForMonthFrom = "12"; searchForMonthTo = "12"; break;
                default: searchForMonthFrom = "01"; searchForMonthTo = "12"; break;
            }
        }

        #region FOR 3Shape ONLY
        string yearSearchQueryStr = "";
        string daySearchQuery;

        if (searchForMonthTo == "01" || searchForMonthTo == "03" || searchForMonthTo == "05" || searchForMonthTo == "07" || searchForMonthTo == "08" || searchForMonthTo == "10" || searchForMonthTo == "12")
            daySearchQuery = "31";
        else if (searchForMonthTo == "02")
            daySearchQuery = "29";
        else
            daySearchQuery = "30";


        if (!string.IsNullOrEmpty(searchForYear))
        {
            yearSearchQueryStr = $"AND (MaxCreateDate >= '{searchForYear}-{searchForMonthFrom}-01' AND MaxCreateDate <= '{searchForYear}-{searchForMonthTo}-{daySearchQuery}')";
        }


        if (!string.IsNullOrEmpty(CustomerSearchString))
        {
            if (string.IsNullOrEmpty(yearSearchQueryStr))
            // if we don't filter for year
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"(IntOrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%'";
                else
                    searchQueryStr = $"(IntOrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%'";
            }
            else
            // if we filter for year too
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"((IntOrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%') {yearSearchQueryStr}";
                else
                    searchQueryStr = $"((IntOrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%') {yearSearchQueryStr}";
            }
            ArchiveResultOffsetOnArchivePage = 0;
            ArchiveResultOffset = 0;
        }
        else
        {
            if (string.IsNullOrEmpty(yearSearchQueryStr))
            // if we don't filter for year
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"IntOrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%'";
                else
                    searchQueryStr = $"IntOrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%' OR Customer LIKE '%{keyword}%'";
            }
            else
            // if we filter for year too
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"(IntOrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%') {yearSearchQueryStr}";
                else
                    searchQueryStr = $"(IntOrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%' OR Customer LIKE '%{keyword}%') {yearSearchQueryStr}";
            }
        }
        #endregion FOR 3Shape ONLY


        // search in 3Shape

        try
        {
            string connectionString = DatabaseConnection.ConnectionStrFor3Shape();


            queryString = $@"SELECT TOP 50 IntOrderID, 
                                 Patient_FirstName, 
                                 Patient_LastName,
                                 o.ExtOrderID, 
                                 Items, 
                                 Customer, 
                                 MaxCreateDate,
								 MaxProcessStatusID
                             FROM Orders o
                             FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
                             WHERE {searchQueryStr}
                             Order by MaxCreateDate DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                id++;
                ResultIn3Shape++;
                _ = DateTime.TryParse(reader["MaxCreateDate"].ToString()!, out DateTime dresult);
                string createDate = dresult.ToString("M/d h:mm tt");
                string createDateLong = dresult.ToString("MMM d");
                string createYear = dresult.ToString("yyyy");

                string panNumber = "";
                #region >> Determining the pan number



                string orderIDHelpr = reader["IntOrderID"].ToString()!;
                List<string> orderIDDarabolt = [];
                orderIDDarabolt = [.. orderIDHelpr.Split('-')];

                bool foundPanNumber = int.TryParse(orderIDDarabolt[0].ToString(), out int panNr);

                if (foundPanNumber)
                {
                    panNumber = panNr.ToString();
                }
                else
                {
                    // checking if we can find any pan number in the patient name section
                    string orderIDHelprFromPtName = reader["Patient_LastName"].ToString()!;
                    List<string> orderIDHelprFromPtNameDarabolt = [];
                    orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                    bool foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out int panNr2);
                    if (foundPanNumber2)
                    {
                        panNumber = panNr2.ToString();
                    }
                    else
                    {
                        orderIDHelprFromPtName = reader["Patient_FirstName"].ToString()!;
                        orderIDHelprFromPtNameDarabolt = [];
                        orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                        panNr2 = 0;
                        foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out panNr2);

                        if (foundPanNumber2)
                        {
                            panNumber = panNr2.ToString();
                        }
                        else
                            panNumber = "";
                    }

                }
                #endregion


                string ptLastName = reader["Patient_LastName"].ToString()!;
                string ptFirstName = reader["Patient_FirstName"].ToString()!;

                if (ptLastName == $"{panNumber}-")
                    ptLastName = "";

                if (ptFirstName == $"{panNumber}-")
                    ptFirstName = "";

                string items = reader["Items"].ToString()!.Replace("Unsectioned model, Unsectioned model", "Model")
                                                          .Replace("Unsectioned model, Antagonist model", "Model")
                                                          .Replace("Sectioned model, Antagonist model", "Model")
                                                          .Replace("Sectioned model, Unsectioned model", "Model")
                                                          .Replace("Unsectioned model, Sectioned model", "Model")
                                                          .Replace("Sectioned model, Sectioned model", "Model")
                                                          .Replace("Sectioned model,", "Model,")
                                                          .Replace("Unsectioned model,", "Model,")
                                                          .Replace("Sectioned model", "Model")
                                                          .Replace("Unsectioned model", "Model")
                                                          .Replace("Soft tissue,", "")
                                                          .Replace("Soft tissue", "");
                string unitsFromItems = $"#{RemoveAllButUnitNumbers(reader["Items"].ToString()!)}";

                string icon = "/Images/Other/threeshape.png";

                switch (reader["MaxProcessStatusID"].ToString()!)
                {
                    case "psCreated": icon = "/Images/ListViewIcons/psCreated.png"; break;
                    case "psModelled": icon = "/Images/ListViewIcons/psModelled.png"; break;
                    case "psModelling": icon = "/Images/ListViewIcons/psModelling.png"; break;
                    case "psScanned": icon = "/Images/ListViewIcons/psScanned.png"; break;
                    case "psScanning": icon = "/Images/ListViewIcons/psScanning.png"; break;
                    case "psSent": icon = "/Images/ListViewIcons/psSent.png"; break;
                    case "psCheckedOut": icon = "/Images/ListViewIcons/checkedOut.png"; break;
                    default: icon = "/Images/Other/threeshape.png"; break;
                }


                list3Shape.Add(new GlobalSearchModel
                {
                    Id = id,
                    IntOrderId = reader["IntOrderID"].ToString()!,
                    PanNumber = panNumber,
                    Patient_FirstName = ptFirstName,
                    Patient_LastName = ptLastName,
                    Customer = reader["Customer"].ToString()!,
                    Items = items,
                    CreateDate = createDate,
                    CreateDateLong = createDateLong,
                    CreateYear = createYear,
                    Designer = reader["ExtOrderID"].ToString()!,
                    Icon = icon,
                    Background = "White",
                    Source = "3Shape",
                    UnitsFromItems = unitsFromItems,
                });
            }
        }
        catch (Exception ex)
        {
        }




        #region FOR ARCHIVES ONLY
        yearSearchQueryStr = "";
        if (!string.IsNullOrEmpty(searchForYear) && !searchForYear.Equals("All time"))
        {
            _ = int.TryParse(searchForYear, out int syear);
            _ = int.TryParse(searchForMonthFrom, out int smonthFrom);
            _ = int.TryParse(searchForMonthTo, out int smonthTo);

            var baseDate = new DateTime(1970, 01, 01);
            var fromDateInt = new DateTime(syear, smonthFrom, 01);

            DateTime toDateInt;

            if (smonthTo == 1 || smonthTo == 3 || smonthTo == 5 || smonthTo == 7 || smonthTo == 8 || smonthTo == 10 || smonthTo == 12)
            {
                toDateInt = new DateTime(syear, smonthTo, 31);
            }
            else if (smonthTo == 2)
            {
                try
                {
                    toDateInt = new DateTime(syear, smonthTo, 29);
                }
                catch (Exception)
                {
                    toDateInt = new DateTime(syear, smonthTo, 28);
                }
            }
            else
            {
                toDateInt = new DateTime(syear, smonthTo, 30);
            }


            var fromResult = fromDateInt.Subtract(baseDate).TotalSeconds;
            var toResult = toDateInt.Subtract(baseDate).TotalSeconds;


            yearSearchQueryStr = $"AND (CreateDate >= '{fromResult}' AND CreateDate <= '{toResult}')";
        }


        if (!string.IsNullOrEmpty(CustomerSearchString))
        {
            if (string.IsNullOrEmpty(yearSearchQueryStr))
            // if we don't filter for year
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"(OrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%'";
                else
                    searchQueryStr = $"(OrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%'";
            }
            else
            // if we filter for year too
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"((OrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%') {yearSearchQueryStr}";
                else
                    searchQueryStr = $"((OrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%') AND Customer LIKE '%{CustomerSearchString.Replace("'", "").Replace("%", "").Trim()}%') {yearSearchQueryStr}";
            }
            ArchiveResultOffsetOnArchivePage = 0;
            ArchiveResultOffset = 0;
        }
        else
        {
            if (string.IsNullOrEmpty(yearSearchQueryStr))
            // if we don't filter for year
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"OrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%'";
                else
                    searchQueryStr = $"OrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%' OR Customer LIKE '%{keyword}%'";
            }
            else
            // if we filter for year too
            {
                if (searchingForPanNumber)
                    searchQueryStr = $"(OrderID LIKE '{keyword}%' OR Patient_FirstName LIKE '{keyword}%') {yearSearchQueryStr}";
                else
                    searchQueryStr = $"(OrderID LIKE '%{keyword}%' OR Patient_FirstName LIKE '%{keyword}%' OR Patient_LastName LIKE '%{keyword}%' OR Customer LIKE '%{keyword}%') {yearSearchQueryStr}";
            }
        }
        #endregion FOR ARCHIVES ONLY


        // search in Archives
        try
        {
            string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();

            queryString = $@"SELECT OrderID, 
                                 PanNumber,                                 
                                 Patient_FirstName, 
                                 Patient_LastName,
                                 Items, 
                                 Customer, 
                                 DesignerName,
                                 CreateDate,
                                 ReasonIsDead,
                                 Registered,
                                 BaseFolder,
                                 XMLFile
                             FROM Archives
                             WHERE {searchQueryStr}
                             Order by CreateDate DESC
                             OFFSET {ArchiveResultOffset * 50} ROWS FETCH NEXT 50 ROWS ONLY";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                idA++;
                ResultInArchives++;
                // Unix timestamp is seconds past epoch
                _ = int.TryParse(reader["CreateDate"].ToString()!, out int unixTimeStamp);
                DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                DateTime dresult = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
                string createDate = dresult.ToString("M/d h:mm tt");
                string createDateLong = dresult.ToString("MMM d");
                string createYear = dresult.ToString("yyyy");

                _ = DateTime.TryParse(reader["Registered"].ToString()!, out DateTime regiDate);


                string orderFolder = reader["XMLFile"].ToString()!;

                if (!string.IsNullOrEmpty(orderFolder))
                    orderFolder = orderFolder.Replace($"{reader["OrderID"]!}.xml", "");

                string panNumber = reader["PanNumber"].ToString()!;
                string ptLastName = reader["Patient_LastName"].ToString()!;
                string ptFirstName = reader["Patient_FirstName"].ToString()!;

                if (ptLastName == $"{panNumber}-")
                    ptLastName = "";

                if (ptFirstName == $"{panNumber}-")
                    ptFirstName = "";


                string items = reader["Items"].ToString()!.Replace("Unsectioned model, Unsectioned model", "Model")
                                                          .Replace("Unsectioned model, Antagonist model", "Model")
                                                          .Replace("Sectioned model, Antagonist model", "Model")
                                                          .Replace("Sectioned model, Unsectioned model", "Model")
                                                          .Replace("Unsectioned model, Sectioned model", "Model")
                                                          .Replace("Sectioned model, Sectioned model", "Model")
                                                          .Replace("Sectioned model,", "Model,")
                                                          .Replace("Unsectioned model,", "Model,")
                                                          .Replace("Sectioned model", "Model")
                                                          .Replace("Unsectioned model", "Model")
                                                          .Replace("Soft tissue,", "")
                                                          .Replace("Soft tissue", "");

                string unitsFromItems = $"#{RemoveAllButUnitNumbers(reader["Items"].ToString()!)}";



                listArchives.Add(new GlobalSearchModel
                {
                    Id = idA,
                    IntOrderId = reader["OrderID"].ToString()!,
                    PanNumber = panNumber,
                    Patient_FirstName = ptFirstName,
                    Patient_LastName = ptLastName,
                    Customer = reader["Customer"].ToString()!,
                    Items = items,
                    CreateDate = createDate,
                    CreateDateLong = createDateLong,
                    CreateYear = createYear,
                    Designer = reader["DesignerName"].ToString()!,
                    Icon = "/Images/Other/archives.png",
                    Background = "LightYellow",
                    Source = "Archives",
                    ReasonIsDead = reader["ReasonIsDead"].ToString()!,
                    AddedToDatastore = regiDate.ToString("MM/dd/yyyy"),
                    BaseFolder = reader["BaseFolder"].ToString()!,
                    XMLFile = reader["XMLFile"].ToString()!,
                    OrderFolder = orderFolder,
                    UnitsFromItems = unitsFromItems,
                });
            }
        }
        catch (Exception ex)
        {
        }


        GlobalSearchResultArchives = listArchives;
        GlobalSearchResult3Shape = list3Shape;
        //Application.Current.Dispatcher.Invoke(new Action(() =>
        //{
        //    _MainWindow.tbFlyingSearch.Focus();
        //    _MainWindow.tbFlyingSearch.CaretIndex = _MainWindow.tbFlyingSearch.Text.Length;
        //}));
    }

    private static string RemoveAllButUnitNumbers(string items)
    {
        string result = RemoveNumbersAndDash().Replace(items, "");
        result = result.Replace(",,", ",").Replace("--", "-");

        if (result.StartsWith(',') || result.StartsWith('-'))
            result = result[1..];

        if (result.Contains(','))
        {
            string[] parts = result.Split(',');
            string[] newArray = [.. parts.Distinct()];

            //Array.Sort(newArray);


            result = "";
            foreach (string item in newArray)
                result += $"{item},";

            if (result.EndsWith(','))
                result = result[..^1];
        }

        return result.Replace(",,", ",").Replace("#,", "#").Replace("#,", "#");
    }

    private void ZippingOrderArchives_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        WorkingOnExportingZipArchive = false;
        ExportingZipArchiveNow = true;
    }

    private async void ListCases_DoWork(object? sender, DoWorkEventArgs e)
    {
        ThreeShapeServerIsDown = false;
        var data = (SearchData)e.Argument!;
        string keyWordOrFilter = data.KeyWordOrFilter!;
        bool FilterInUse = data.FilterInUse;

        string keyWord = "";
        string Filter = "";


        if (FilterInUse)
            Filter = keyWordOrFilter;
        else
            keyWord = keyWordOrFilter;


        string sFilter = "";
        string sOrderBy;
        string panNumber;

        //sOrderBy = "i.MaxCreateDate DESC, oh.ModificationDate DESC, i.MaxProcessStatusID DESC ";
        //sOrderBy = "IntOrderID ASC, oh.ModificationDate DESC ";
        sOrderBy = "oh.ModificationDate DESC, MaxCreateDate DESC, IntOrderID ASC ";


        #region >> searching for string / keyword
        if (!FilterInUse)
        {
            sFilter = "WHERE ( ";


            if (keyWord.StartsWith('@') && !keyWord.Contains('+'))
            {
                sFilter += $@" (o.Patient_RefNo LIKE '%{keyWord.Replace("@", "").Trim()}%' OR
                                o.ExtOrderID LIKE '%{keyWord.Replace("@", "").Trim()}%') ";
            }
            else if (keyWord.Contains('+') && keyWord.Length > 2 && !keyWord.StartsWith('+') && !keyWord.EndsWith('+'))
            {
                string[] keyWordPart = keyWord.Split('+');

                if (keyWordPart.Length > 5)
                {
                    ShowNotificationMessage("Error", "You can only use 5 keywords at once", NotificationIcon.Error);
                    return;
                }

                if (keyWordPart.Length == 2)
                    sFilter += $@"
                                (o.IntOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[0].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[0].Trim()}%') 
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[1].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[1].Trim()}%') 
                                ";

                if (keyWordPart.Length == 3)
                    sFilter += $@"
                                (o.IntOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[0].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[0].Trim()}%') 
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[1].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[1].Trim()}%')
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[2].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[2].Trim()}%') 
                                ";

                if (keyWordPart.Length == 4)
                    sFilter += $@"
                                (o.IntOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[0].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[0].Trim()}%') 
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[1].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[1].Trim()}%')
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[2].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[2].Trim()}%') 
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[3].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[3].Trim()}%') 
                                ";

                if (keyWordPart.Length == 5)
                    sFilter += $@"
                                (o.IntOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[0].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[0].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[0].Trim()}%') 
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[1].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[1].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[1].Trim()}%')
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[2].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[2].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[2].Trim()}%') 
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[3].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[3].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[3].Trim()}%')  
                                AND
                                (o.IntOrderID LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.Patient_FirstName LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.Patient_LastName LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.Patient_RefNo LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.ExtOrderID LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.OrderComments LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.Items LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.Customer LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 o.ManufName LIKE '%{keyWordPart[4].Trim()} % ' OR 
                                 o.CacheMaterialName LIKE '%{keyWordPart[4].Trim()}%' OR 
                                 i.MaxCreateDate LIKE '%{keyWordPart[4].Trim()}%') 
                                ";
            }
            else if (!keyWord.Contains('+'))
            {
                if (SearchOnlyInFileNames)
                {
                    sFilter += "o.IntOrderID LIKE '%" + keyWord + "%' OR " +
                              "o.Patient_RefNo LIKE '%" + keyWord + "%' OR " +
                              "o.ExtOrderID LIKE '%" + keyWord + "%' OR " +
                              "o.Patient_FirstName LIKE '%" + keyWord + "%' OR " +
                              "o.Patient_LastName LIKE '%" + keyWord + "%' ";
                }
                else
                {
                    sFilter += " o.IntOrderID LIKE '%" + keyWord + "%' OR " +
                            "o.Patient_FirstName LIKE '%" + keyWord + "%' OR " +
                            "o.Patient_LastName LIKE '%" + keyWord + "%' OR " +
                            "o.Patient_RefNo LIKE '%" + keyWord + "%' OR " +
                            "o.ExtOrderID LIKE '%" + keyWord + "%' OR " +
                            "o.OrderComments LIKE '%" + keyWord + "%' OR " +
                            "o.Items LIKE '%" + keyWord + "%' OR " +
                            "o.Customer LIKE '%" + keyWord + "%' OR " +
                            "o.ManufName LIKE '%" + keyWord + "%' OR " +
                            "o.CacheMaterialName LIKE '%" + keyWord + "%' OR " +
                            "i.MaxCreateDate LIKE '%" + keyWord + "%' ";
                }
            }


            sFilter += ")";


        }
        #endregion

        #region >> searching by Filter

        if (FilterInUse)
        {

            string sManufacturingFilterWithoutThisSite;
            string sOpenedForDesignFilter = "";





            sManufacturingFilterWithoutThisSite = "AND " +
                                   "  ( " +
                                   "    (o.ManufName NOT LIKE '" + ThisSite + "' AND o.CacheMaterialName NOT LIKE '%Vulcan Zirconia%') " +
                                   "    OR " +
                                   "    ( " +
                                   "      ( " +
                                   "        o.CacheMaterialName LIKE '%Neoss_Ti%' OR " +
                                   "        o.CacheMaterialName LIKE '%,Ti%' OR " +
                                   "        o.CacheMaterialName LIKE '%Ti,%' OR " +
                                   "        o.CacheMaterialName LIKE '%Tita%' OR " +
                                   "        o.CacheMaterialName LIKE '%CoCr%' OR " +
                                   "        o.CacheMaterialName LIKE '%TAN%' OR " +
                                   "        o.CacheMaterialName LIKE '%BellaTek%' OR " +
                                   "        o.CacheMaterialName LIKE 'Ti' " +
                                   "      ) " +
                                   "     AND " +
                                   "      ( " +
                                   "        o.Items LIKE '%Abutment%' " +
                                   "      ) " +
                                   "    ) " +
                                   "    OR " +
                                   "    ( " +
                                   "        o.Items LIKE '%Abutment%' " +
                                   "      AND " +
                                   "        o.CacheMaterialName NOT LIKE '%Bruxzir%' " +
                                   "    ) " +
                                   "    OR " +
                                   "    ( " +
                                   "        o.Items LIKE '%Post and Core%' " +
                                   "    ) " +
                                   "    OR " +
                                   "    ( " +
                                   "        o.CacheMaterialName LIKE '%Argen%' " +
                                   "    ) " +
                                   "    OR " +
                                   "    ( " +
                                   "        o.CacheMaterialName LIKE '%Gold%' " +
                                   "    ) " +
                                   "    OR " +
                                   "    ( " +
                                   "      ( " +
                                   "        o.CacheMaterialName LIKE '%Argen%' OR " +
                                   "        o.CacheMaterialName LIKE '%Wax%' " +
                                   "      ) AND " +
                                   "        (o.OrderComments LIKE '%gold%' OR o.OrderComments LIKE '%Gold%' OR o.OrderComments LIKE '%GOLD%') " +
                                   "    ) " +
                                   "  ) ";



            switch (Filter)
            {
                case "MyRecent":
                    sFilter = $"WHERE(UserID = '{Environment.MachineName}') ";
                    TempSearchLimitIgnore = false;
                    MyRecent30 = false;
                    break;

                case "MyRecent30":
                    sFilter = $"WHERE(UserID = '{Environment.MachineName}') ";
                    MyRecent30 = true;
                    TempSearchLimitIgnore = false;
                    break;

                case "Today":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtToday + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') " +
                              sOpenedForDesignFilter;
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    TodayCasesCount = DatabaseOperations.GetBackTodayCasesCount();
                    break;

                case "Yesterday":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtYesterday + RestDayStart + "' AND i.MaxCreateDate < '" + DtYesterday + RestDayEnd + "') ";
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    break;

                case "LastTwoDays":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtYesterday + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') " +
                              sOpenedForDesignFilter;
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    break;

                case "ThisWeek":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtThisMonday + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') ";
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    break;

                case "ThisAndLastWeek":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtLastWeekMonday + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') ";
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    break;

                case "LastMonth":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtOneMonthBack + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') ";
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    break;

                case "LastTwoMonths":
                    sFilter = "WHERE(i.MaxCreateDate > '" + DtTwoMonthsBack + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') ";
                    TempSearchLimitIgnore = true;
                    MyRecent30 = false;
                    break;


                case "Created":
                    sFilter = "WHERE i.MaxProcessStatusID LIKE 'psCreated' ";
                    break;

                case "Scanned":
                    sFilter = "WHERE (i.MaxProcessStatusID LIKE 'psScanned' OR i.MaxProcessStatusID LIKE 'psScanning') ";
                    break;

                case "NotCheckedOut":
                    sFilter = "WHERE (i.MaxProcessStatusID LIKE 'psScanned' AND me.ProcessLockID <> 'plCheckedOut' AND me.ProcessLockID <> 'plLocked') ";
                    break;


                case "ImpressionScans":
                    sFilter = "WHERE (i.MaxProcessStatusID LIKE 'psScanned' OR i.MaxProcessStatusID LIKE 'psScanning') AND (o.TraySystemType NOT LIKE 'stNone') ";
                    break;

                case "ModelScans":
                    sFilter = "WHERE (i.MaxProcessStatusID LIKE 'psScanned' OR i.MaxProcessStatusID LIKE 'psScanning') AND (o.TraySystemType LIKE 'stNone') AND " +
                              "(" +
                              "(o.ScanSource NOT LIKE 'ssImportThirdPartySTL')  AND " +
                              "(o.ScanSource NOT LIKE 'ssImportPLY')  AND " +
                              "(o.ScanSource NOT LIKE 'ssImport')  AND " +
                              "(o.ScanSource NOT LIKE 'ssItero')  AND " +
                              "(o.ScanSource NOT LIKE 'ssTRIOS')  AND " +
                              "(o.ScanSource NOT LIKE 'ssImport3ShapeSTL')" +
                              ") ";
                    break;

                case "DigitalScans":
                    sFilter = "WHERE (i.MaxProcessStatusID LIKE 'psScanned' OR i.MaxProcessStatusID LIKE 'psScanning') AND " +
                              "(" +
                              "(o.ScanSource LIKE 'ssImportThirdPartySTL')  OR " +
                              "(o.ScanSource LIKE 'ssImportPLY')  OR " +
                              "(o.ScanSource LIKE 'ssImport')  OR " +
                              "(o.ScanSource LIKE 'ssItero')  OR " +
                              "(o.ScanSource LIKE 'ssTRIOS')  OR " +
                              "(o.ScanSource LIKE 'ssImport3ShapeSTL')" +
                              ") ";
                    break;



                case "Designed":
                    sFilter = "WHERE i.MaxProcessStatusID LIKE 'psModelled' ";
                    break;

                case "Designing":
                    sFilter = "WHERE i.MaxProcessStatusID LIKE 'psModelling' ";
                    break;

                case "Sent":
                    sFilter = "WHERE (i.MaxProcessStatusID LIKE 'psSent' OR i.MaxProcessStatusID LIKE 'psAccepted' OR i.MaxProcessStatusID LIKE 'psRejected') ";
                    break;

                case "Closed":
                    sFilter = "WHERE i.MaxProcessStatusID LIKE 'psClosed' ";
                    break;

                case "CheckedOut":
                    sFilter = "WHERE me.ProcessLockID LIKE 'plCheckedOut' ";
                    break;





                case "ImpressionAll":
                    sFilter = "WHERE (o.TraySystemType NOT LIKE 'stNone') ";
                    break;

                case "ModelAll":
                    sFilter = "WHERE (o.TraySystemType LIKE 'stNone') AND " +
                              "(" +
                              "(o.ScanSource NOT LIKE 'ssImportThirdPartySTL')  AND " +
                              "(o.ScanSource NOT LIKE 'ssImportPLY')  AND " +
                              "(o.ScanSource NOT LIKE 'ssImport')  AND " +
                              "(o.ScanSource NOT LIKE 'ssItero')  AND " +
                              "(o.ScanSource NOT LIKE 'ssTRIOS')  AND " +
                              "(o.ScanSource NOT LIKE 'ssImport3ShapeSTL')" +
                              ") ";
                    break;

                case "DigitalAll":
                    sFilter = "WHERE " +
                              "(" +
                              "(o.ScanSource LIKE 'ssImportThirdPartySTL')  OR " +
                              "(o.ScanSource LIKE 'ssImportPLY')  OR " +
                              "(o.ScanSource LIKE 'ssImport')  OR " +
                              "(o.ScanSource LIKE 'ssItero')  OR " +
                              "(o.ScanSource LIKE 'ssTRIOS')  OR " +
                              "(o.ScanSource LIKE 'ssImport3ShapeSTL')" +
                              ") ";
                    break;


                case "DigitalModels":
                    sFilter = "WHERE (o.Items LIKE '%model%') ";
                    break;


                case "NeverSentAbutments":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtLastSevenDays + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') AND (i.MaxProcessStatusID NOT LIKE 'psCreated' AND i.MaxProcessStatusID NOT LIKE 'psScanned' AND i.MaxProcessStatusID NOT LIKE 'psScanning' AND i.MaxProcessStatusID NOT LIKE 'psClosed') " + //AND (o.Items LIKE '%Abutment%') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "i.MaxCreateDate DESC, o.ManufName DESC ";
                    break;





                case "ManufToday":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtToday + RestDayStart + "' AND i.MaxCreateDate < '" + DtToday + RestDayEnd + "') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "o.ManufName DESC, i.MaxCreateDate DESC ";
                    break;

                case "ManufYesterday":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtYesterday + RestDayStart + "' AND i.MaxCreateDate < '" + DtYesterday + RestDayEnd + "') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "o.ManufName DESC, i.MaxCreateDate DESC ";
                    break;

                case "ManufLastTwoDays":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtLastTwoDays + RestDayStart + "' AND i.MaxCreateDate < '" + DtYesterday + RestDayEnd + "') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "i.MaxCreateDate DESC, o.ManufName DESC ";
                    break;

                case "ManufLastThreeDays":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtLastThreeDays + RestDayStart + "' AND i.MaxCreateDate < '" + DtYesterday + RestDayEnd + "') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "i.MaxCreateDate DESC, o.ManufName DESC ";
                    break;



                case "ManufLastSevenDays":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtLastSevenDays + RestDayStart + "' AND i.MaxCreateDate < '" + DtYesterday + RestDayEnd + "') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "i.MaxCreateDate DESC, o.ManufName DESC ";
                    break;


                case "ManufLast30Days":
                    sFilter = "WHERE (i.MaxCreateDate > '" + DtOneMonthBack + RestDayStart + "' AND i.MaxCreateDate < '" + DtYesterday + RestDayEnd + "') " +
                              sManufacturingFilterWithoutThisSite;
                    sOrderBy = "i.MaxCreateDate DESC, o.ManufName DESC ";
                    break;

            }
        }
        #endregion


        string queryString = "SELECT ";

        if (!TempSearchLimitIgnore && (Filter != "NeverSentAbutments"))
            queryString += " TOP (" + SearchLimit + @") ";

        if (EaseUpSearch)
        {
            queryString += "  IntOrderID, " +
                        "  Patient_FirstName, " +
                        "  Patient_LastName, " +
                        "  Patient_RefNo, " +
                        "  o.ExtOrderID, " +
                        "  o.OriginalOrderID, " +
                        "  OrderComments, " +
                        "  o.Items, " +
                        "  OperatorName, " +
                        "  Customer, " +
                        "  o.ManufName, " +
                        "  o.CacheMaterialName, " +
                        "  me.ModelHeight, " +
                        "  ScanSource, " +
                        "  CacheMaxScanDate, " +
                        "  TraySystemType, " +
                        "  MaxCreateDate, " +
                        "  MaxProcessStatusID, " +
                        "  ModificationDate, " +
                        "  UserID ";

            if (Filter != "NeverSentAbutments")
            {
                queryString += ",  ModificationDate, " +
                               "   UserID ";
            }

            queryString += "FROM Orders o " +
                        "FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID " +
                        "FULL OUTER JOIN OrderHistory oh ON oh.OrderID = o.IntOrderID " +

                        sFilter;

            if (Filter != "NeverSentAbutments")
            {
                queryString += "GROUP BY " +
                             "  IntOrderID, " +
                             "  Patient_FirstName, " +
                             "  Patient_LastName, " +
                             "  Patient_RefNo, " +
                             "  o.ExtOrderID, " +
                             "  o.OriginalOrderID, " +
                             "  OrderComments, " +
                             "  o.Items, " +
                             "  OperatorName, " +
                             "  Customer, " +
                             "  o.ManufName, " +
                             "  o.CacheMaterialName, " +
                             "  me.ModelHeight, " +
                             "  ScanSource, " +
                             "  CacheMaxScanDate, " +
                             "  TraySystemType, " +
                             "  MaxCreateDate, " +
                             "  MaxProcessStatusID, " +
                             "  ModificationDate, " +
                             "  UserID ";
            }
        }
        else
        {

            queryString += "  IntOrderID, " +
                        "  Patient_FirstName, " +
                        "  Patient_LastName, " +
                        "  Patient_RefNo, " +
                        "  o.ExtOrderID, " +
                        "  o.OriginalOrderID, " +
                        "  OrderComments, " +
                        "  o.Items, " +
                        "  OperatorName, " +
                        "  Customer, " +
                        "  o.ManufName, " +
                        "  o.CacheMaterialName, " +
                        "  me.ModelHeight, " +
                        "  ScanSource, " +
                        "  CacheMaxScanDate, " +
                        "  TraySystemType, " +
                        "  MaxCreateDate, " +
                        "  MaxProcessStatusID, " +
                        "  ProcessStatusID, " +
                        "  AltProcessStatusID, " +
                        "  ProcessLockID,  " +
                        "  WasSent, " +
                        "  ModificationDate, " +
                        "  UserID ";


            queryString += "FROM Orders o " +
                        "FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID " +
                        "FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID " +
                        "FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID " +
                        "FULL OUTER JOIN OrderHistory oh ON oh.OrderID = o.IntOrderID " +

                        sFilter;

            if (Filter != "NeverSentAbutments")
            {
                queryString += "GROUP BY " +
                        "  IntOrderID, " +
                        "  Patient_FirstName, " +
                        "  Patient_LastName, " +
                        "  Patient_RefNo, " +
                        "  o.ExtOrderID, " +
                        "  o.OriginalOrderID, " +
                        "  OrderComments, " +
                        "  o.Items, " +
                        "  OperatorName, " +
                        "  Customer, " +
                        "  o.ManufName, " +
                        "  o.CacheMaterialName, " +
                        "  me.ModelHeight, " +
                        "  ScanSource, " +
                        "  CacheMaxScanDate, " +
                        "  TraySystemType, " +
                        "  MaxCreateDate, " +
                        "  MaxProcessStatusID, " +
                        "  ProcessStatusID, " +
                        "  AltProcessStatusID, " +
                        "  ProcessLockID,  " +
                        "  WasSent, " +
                        "  ModificationDate, " +
                        "  UserID ";
            }

        }


        queryString += "ORDER BY " + sOrderBy;


        //if (!TempSearchLimitIgnore)
        //    queryString += " OFFSET 0 ROWS FETCH FIRST " + SearchLimit.ToString() + @" ROWS ONLY;";


        string countingQueryString = "select count(*) " +
                                    "from " +
                                    "( " +
                                    "select count(IntOrderID) tot " +
                                    "FROM Orders o " +
                                    "FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID " +
                                    "FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID " +
                                    "FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID " +
                                    "FULL OUTER JOIN OrderHistory oh ON oh.OrderID = o.IntOrderID " +
                                    sFilter +
                                    "  group by IntOrderID " +
                                    ")  src;";
        Debug.WriteLine(countingQueryString);
        int countedResults = DatabaseOperations.Counting_result(countingQueryString);

        _ = int.TryParse(SearchLimit, out int srchLimit);

        if (!TempSearchLimitIgnore && countedResults > srchLimit && srchLimit > 0 && !MyRecent30)
        {
            // if the result is higher than the searchLimit, then counting the units on the first "searchLimit" amount of cases, to reflect the real amount of case 

            int totalEntryLinesInDatabase = DatabaseOperations.GetBackTotalEntryLinesCount(srchLimit, sFilter);

            countedResults = srchLimit;
            //countedResults = totalEntryLinesInDatabase;
            queryString = queryString.Replace(@$"TOP ({SearchLimit})", @$"TOP ({totalEntryLinesInDatabase})");
        }

        if (countedResults > 30 && MyRecent30)
        {
            // if the result is higher than the searchLimit, then counting the units on the first "searchLimit" amount of cases, to reflect the real amount of case 

            int totalEntryLinesInDatabase = DatabaseOperations.GetBackTotalEntryLinesCount(30, sFilter);

            countedResults = 30;
            //countedResults = totalEntryLinesInDatabase;
            queryString = queryString.Replace(@$"TOP ({SearchLimit})", @$"TOP ({totalEntryLinesInDatabase})");
        }

        if (countedResults < 1)
        {
            countedResults = 1;
        }

        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            _MainWindow.pb3ShapeProgressBar.Maximum = countedResults;
        }));

        //CountedResultsInt = countedResults;


        string connectionString = DatabaseConnection.ConnectionStrFor3Shape();

        List<string> list = [];


        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            FilterString = keyWordOrFilter.Trim();
            //if (FilterInUse)
            //    tbFilterString.Foreground = Brushes.DarkGreen;
            //else
            //    tbFilterString.Foreground = Brushes.SteelBlue;

            _MainWindow.pb3ShapeProgressBar.Value = 0;
            if (AllowToShowProgressBar)
                _MainWindow.pb3ShapeProgressBar.Visibility = Visibility.Visible;
        }));

        TempSearchLimitIgnore = false;

        Current3ShapeOrderList.Clear();

        try
        {

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (Filter == "NeverSentAbutments" && reader["ProcessLockID"].ToString() == "plSent")
                {
                    if (!list.Contains(reader["IntOrderID"].ToString()!))
                    {
                        list.Add(reader["IntOrderID"].ToString()!);
                    }

                    if (Current3ShapeOrderList.Where(x => x.IntOrderID == reader["IntOrderID"].ToString()).FirstOrDefault() != null)
                    {
                        if (Current3ShapeOrderList.Where(x => x.IntOrderID == reader["IntOrderID"].ToString()).FirstOrDefault()!.IntOrderID == reader["IntOrderID"].ToString())
                            Current3ShapeOrderList.RemoveAt(Current3ShapeOrderList.IndexOf(Current3ShapeOrderList.Where(x => x.IntOrderID == reader["IntOrderID"].ToString()).FirstOrDefault()!));
                    }
                    continue;
                }

                if (!list.Contains(reader["IntOrderID"].ToString()!))
                {
                    list.Add(reader["IntOrderID"].ToString()!);

                    string AlternateColoring = "";

                    string MaxProcessStatusID = reader["MaxProcessStatusID"].ToString()!;
                    string ScanSource = reader["ScanSource"].ToString()!;
                    string ProcessStatusID = "";
                    string ProcessLockID = "";
                    string AltProcessStatusID = "";
                    string WasSent = "";

                    if (!EaseUpSearch)
                    {
                        ProcessLockID = reader["ProcessLockID"].ToString()!;
                        ProcessStatusID = reader["ProcessStatusID"].ToString()!;
                        AltProcessStatusID = reader["AltProcessStatusID"].ToString()!;
                        WasSent = reader["WasSent"].ToString()!;
                    }




                    #region >> Determining the pan number

                    string orderIDHelpr = reader["IntOrderID"].ToString()!;
                    List<string> orderIDDarabolt = [];
                    orderIDDarabolt = [.. orderIDHelpr.Split('-')];

                    bool foundPanNumber = int.TryParse(orderIDDarabolt[0].ToString(), out int panNr);

                    if (foundPanNumber)
                    {
                        panNumber = panNr.ToString();
                    }
                    else
                    {
                        // checking if we can find any pan number in the patient name section
                        string orderIDHelprFromPtName = reader["Patient_LastName"].ToString()!;
                        List<string> orderIDHelprFromPtNameDarabolt = [];
                        orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                        bool foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out int panNr2);
                        if (foundPanNumber2)
                        {
                            panNumber = panNr2.ToString();
                        }
                        else
                        {
                            orderIDHelprFromPtName = reader["Patient_FirstName"].ToString()!;
                            orderIDHelprFromPtNameDarabolt = [];
                            orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                            panNr2 = 0;
                            foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out panNr2);

                            if (foundPanNumber2)
                            {
                                panNumber = panNr2.ToString();
                            }
                            else
                                panNumber = "";
                        }

                    }
                    #endregion

                    bool isAbutmentCase = false;







                    string CaseStatus = CaseStatusSelect(MaxProcessStatusID, ScanSource, ProcessLockID);
                    string ImageSource = @"\Images\ListViewIcons\" + IconSelect(MaxProcessStatusID, ScanSource, ProcessLockID) + ".png";
                    string PanColorName = GetBackPanColorName(panNumber);
                    string PanColor = GetBackPanColorHEX(panNumber);

                    if (panNumber == "" || PanColor == "#FFFFFF")
                        PanColor = "Transparent";

                    string Patient_FirstName = reader["Patient_FirstName"].ToString()!.Trim();
                    string Patient_LastName = reader["Patient_LastName"].ToString()!.Trim();
                    if (Patient_FirstName == "")
                        Patient_FirstName = "-";

                    string manufName = "";
                    if (ThisSite.Length > 0)
                    {
                        manufName = reader["ManufName"].ToString()!
                                            .Replace(ThisSite + "/", "")
                                            .Replace("/" + ThisSite, "")
                                            .Replace(ThisSite, "");
                    }
                    else
                    {
                        manufName = reader["ManufName"].ToString()!;
                    }


                    _ = DateTime.TryParse(reader["ModificationDate"].ToString(), out DateTime LastModificationForSortingDateTime);
                    string LastModificationForSorting = "";
                    if (reader["ModificationDate"].ToString() != "")
                        LastModificationForSorting = LastModificationForSortingDateTime.ToString("yyyy-MM-dd-HHmmss");

                    _ = DateTime.TryParse(reader["MaxCreateDate"].ToString(), out DateTime CreateDateForSortingDateTime);
                    string CreateDateForSorting = "";
                    CreateDateForSorting = CreateDateForSortingDateTime.ToString("yyyy-MM-dd-HHmmss");

                    string ScanSourceFriendlyName = GetScanner(ScanSource);




                    string CacheMaxScanDate = reader["CacheMaxScanDate"].ToString()!;
                    string CacheMaxScanDateFriendly = CacheMaxScanDate;
                    if (IsItToday(CacheMaxScanDate))
                    {
                        _ = DateTime.TryParse(CacheMaxScanDate, out DateTime CacheMaxScanDateDT);
                        CacheMaxScanDateFriendly = CacheMaxScanDateDT.ToString("h:mm tt");
                    }
                    else if (IsItThisYear(CacheMaxScanDate))
                    {
                        _ = DateTime.TryParse(CacheMaxScanDate, out DateTime CacheMaxScanDateDT);
                        CacheMaxScanDateFriendly = CacheMaxScanDateDT.ToString("MM/dd - h:mm tt");
                    }

                    if (CacheMaxScanDateFriendly.StartsWith("000"))
                        CacheMaxScanDateFriendly = CacheMaxScanDate;




                    string MaxCreateDate = reader["MaxCreateDate"].ToString()!;
                    string MaxCreateDateFriendly = MaxCreateDate;
                    if (IsItToday(MaxCreateDate))
                    {
                        _ = DateTime.TryParse(MaxCreateDate, out DateTime MaxCreateDateDT);
                        MaxCreateDateFriendly = MaxCreateDateDT.ToString("h:mm tt");
                    }
                    else if (IsItThisYear(MaxCreateDate))
                    {
                        _ = DateTime.TryParse(MaxCreateDate, out DateTime MaxCreateDateDT);
                        MaxCreateDateFriendly = MaxCreateDateDT.ToString("MM/dd - h:mm tt");
                    }







                    string ModificationDate = reader["ModificationDate"].ToString()!;
                    _ = DateTime.TryParse(ModificationDate, out DateTime ModificationDateDT);
                    _ = DateTime.TryParse(DtThisMonday, out DateTime dtLastWeekSundayDT);
                    dtLastWeekSundayDT = dtLastWeekSundayDT.AddDays(-1);

                    if (IsItToday(ModificationDate))
                    {
                        ModificationDate = ModificationDateDT.ToString("h:mm tt");
                    }
                    else if (ModificationDateDT > dtLastWeekSundayDT)
                    {
                        ModificationDate = ModificationDateDT.ToString("dddd - h:mm tt");
                    }
                    else if (IsItThisYear(ModificationDate))
                    {
                        ModificationDate = ModificationDateDT.ToString("MM/dd - h:mm tt");
                    }

                    string CacheMaterialName = reader["CacheMaterialName"].ToString()!.Replace("\"", "");

                    string LastModifiedComputerName = ReadComputerName(reader["UserID"].ToString()!);

                    string Items = RemoveChineseCharacters(reader["Items"].ToString()!);

                    string[] CaseStatusByManufacturerParts = manufName.Split('/');
                    string CaseStatusByManufacturer = CaseStatusByManufacturerParts[0];
                    if (CaseStatusByManufacturer == "")
                    {
                        if (Items.Contains("Abutment") && CacheMaterialName.Contains("Ti"))
                            CaseStatusByManufacturer = "Abutments (3rd Party)";
                        else
                            CaseStatusByManufacturer = "Miscellaneous";
                    }
                    if (Filter == "NeverSentAbutments")
                        CaseStatus = CaseStatusByManufacturer;


                    if (Items.Contains("Abutment") &&
                                        !IsItEncodeUnit(reader["ManufName"].ToString()!, reader["CacheMaterialName"].ToString()!) &&
                                                MaxProcessStatusID == "psModelled" && ProcessLockID != "plLocked")
                        isAbutmentCase = true;


                    string shade = "";

                    shade = DetermininingShade(reader["IntOrderID"].ToString()!);
                    if (string.IsNullOrEmpty(shade))
                        AlternateColoring = "noshade";

                    // alternate coloring
                    if (PanColor == "#FFFFFF")
                        AlternateColoring = "nopancolor";

                    if (CacheMaterialName.Contains("NO MATERIAL"))
                    {
                        AlternateColoring = "encode";
                        isAbutmentCase = false;
                    }





                    string ExtOrderID = reader["ExtOrderID"].ToString()!;
                    var isNumeric = int.TryParse(ExtOrderID, out _);
                    if (isNumeric)
                        ExtOrderID = "";


                    bool hasAnyImage = false;


                    hasAnyImage = CheckForImageInOrderFolder(reader["IntOrderID"].ToString()!);

                    #region Context MenuItems Visibility

                    //Visibility ToolBarButton_exploreOrderCAM = Visibility.Collapsed;
                    //Visibility ToolBarButton_secureAbutmentDesign = Visibility.Collapsed;
                    //Visibility ToolBarButton_removeSecureAbutmentDesign = Visibility.Collapsed;
                    //Visibility ToolBarButton_renameOrder = Visibility.Collapsed;


                    //if (MaxProcessStatusID == "psModelled")
                    //    ToolBarButton_exploreOrderCAM = Visibility.Visible;

                    //if (isAbutmentCase)
                    //    ToolBarButton_secureAbutmentDesign = Visibility.Visible;

                    //if (isAbutmentCase)
                    //    ToolBarButton_removeSecureAbutmentDesign = Visibility.Visible;

                    bool isTheFilesAccessible = true;
                    bool generateStCopy = true;
                    bool hasDesignerHistory = false;
                    bool IsLocked = false;
                    bool IsCheckedOut = false;
                    bool IsCaseWereDesigned = false;
                    bool previouslyDesigned = false;

                    if (reader["ProcessLockID"].ToString() == "plLocked")
                        IsLocked = true;

                    // checking if case is checked out
                    if (reader["ProcessLockID"].ToString() == "plCheckedOut")
                        IsCheckedOut = true;

                    // checking if the folder for that case are exist and writable (in 3Shape orders folder)
                    if (ThreeShapeDirectoryHelper.Length < 1 ||
                        !CheckFolderIsWritable(ThreeShapeDirectoryHelper + reader["IntOrderID"].ToString()) ||// if 3Shape dir not set it up or it is setted up but the case folder not writable (maybe doesn't exist)
                        IsCheckedOut
                        )
                    {
                        isTheFilesAccessible = false;
                        generateStCopy = false;
                    }

                    List<DesignerHistoryModel> designerHistory = [];
                    string designedByFile = @$"{ThreeShapeDirectoryHelper}{reader["IntOrderID"]}\History\DesignedBy";
                    if (File.Exists(designedByFile))
                    {
                        try
                        {
                            File.ReadAllLines(designedByFile).ToList().ForEach(x =>
                            {
                                string[] parts = x.Split(']');

                                _ = DateTime.TryParse(parts[0].Replace("[", ""), out DateTime dtTime);

                                string designr = parts[1].Trim();
                                designerHistory.Add(new DesignerHistoryModel()
                                {
                                    Year = $"[{dtTime:yyyy}]",
                                    Day = dtTime.ToString("ddd"),
                                    Date = dtTime.ToString("M/d"),
                                    Time = dtTime.ToString("h:mm tt"),
                                    DesignerName = designr
                                });
                            });

                            if (designerHistory.Count > 0)
                                designerHistory.Reverse();

                            hasDesignerHistory = true;
                        }
                        catch (Exception ex)
                        {
                            if (!ex.Message.Contains("The media is write protected", StringComparison.CurrentCultureIgnoreCase))
                                AddDebugLine(ex);
                        }
                    }
                    else if (File.Exists(designedByFile.Replace(@"\History\", @"\")))
                    {
                        designedByFile = designedByFile.Replace(@"\History\", @"\");

                        try
                        {
                            File.ReadAllLines(designedByFile).ToList().ForEach(x =>
                            {
                                string[] parts = x.Split(']');

                                _ = DateTime.TryParse(parts[0].Replace("[", ""), out DateTime dtTime);

                                //string dTime = dtTime.ToString("[yyyy] ddd M/d h:mm tt");
                                string designr = parts[1].Trim();
                                //designerHistory.Add($"{dTime} - {designr}");
                                designerHistory.Add(new DesignerHistoryModel()
                                {
                                    Year = $"[{dtTime.ToString("yyyy")}]",
                                    Day = dtTime.ToString("ddd"),
                                    Date = dtTime.ToString("M/d"),
                                    Time = dtTime.ToString("h:mm tt"),
                                    DesignerName = designr
                                });
                            });

                            if (designerHistory.Count > 0)
                                designerHistory.Reverse();

                            hasDesignerHistory = true;
                        }
                        catch (Exception ex)
                        {
                            AddDebugLine(ex);
                        }
                    }



                    if (reader["ModelHeight"].ToString() != "0" && string.IsNullOrEmpty(reader["OriginalOrderID"].ToString()))
                        IsCaseWereDesigned = true;

                    bool canBeRenamed = false;
                    if (((MaxProcessStatusID == "psCreated" && !IsCaseWereDesigned) ||
                         (MaxProcessStatusID == "psScanned" && !IsCaseWereDesigned) ||
                         (MaxProcessStatusID == "psModelled" && !string.IsNullOrEmpty(reader["OriginalOrderID"].ToString()))) &&
                         !IsLocked && !IsCheckedOut && isTheFilesAccessible)
                    {
                        canBeRenamed = true;
                    }


                    if (MaxProcessStatusID != "psModelled"
                     && MaxProcessStatusID != "psClosed"
                     && MaxProcessStatusID != "psSent"
                     && IsCaseWereDesigned)
                        previouslyDesigned = true;

                    #endregion


#pragma warning disable CS8604 // Possible null reference argument.
                    Current3ShapeOrderList.Add(new ThreeShapeOrdersModel
                    {
                        IntOrderID = reader["IntOrderID"].ToString(),
                        Patient_FirstName = Patient_FirstName,
                        Patient_LastName = Patient_LastName,
                        Patient_RefNo = reader["Patient_RefNo"].ToString(),
                        ExtOrderID = ExtOrderID,
                        OrderComments = reader["OrderComments"].ToString(),
                        Items = Items,
                        OperatorName = reader["OperatorName"].ToString(),
                        Customer = reader["Customer"].ToString(),
                        ManufName = manufName,
                        CacheMaterialName = CacheMaterialName,
                        ScanSource = ScanSource,
                        CacheMaxScanDate = CacheMaxScanDate,
                        TraySystemType = reader["TraySystemType"].ToString(),
                        MaxCreateDate = MaxCreateDate,
                        MaxProcessStatusID = MaxProcessStatusID,
                        ProcessStatusID = ProcessStatusID,
                        AltProcessStatusID = AltProcessStatusID,
                        ProcessLockID = ProcessLockID,
                        WasSent = WasSent,
                        ModificationDate = ModificationDate,
                        ImageSource = ImageSource,
                        ListViewGroup = "",
                        PanColor = PanColor,
                        PanColorName = PanColorName,
                        CaseStatus = CaseStatus,
                        PanNumber = panNumber,
                        Shade = shade,
                        LastModificationForSorting = LastModificationForSorting,
                        LastModifiedComputerName = LastModifiedComputerName,
                        CreateDateForSorting = CreateDateForSorting,
                        ScanSourceFriendlyName = ScanSourceFriendlyName,
                        CacheMaxScanDateFriendly = CacheMaxScanDateFriendly,
                        MaxCreateDateFriendly = MaxCreateDateFriendly,
                        CaseStatusByManufacturer = CaseStatusByManufacturer,
                        AlternateColoring = AlternateColoring,
                        OriginalOrderID = reader["OriginalOrderID"].ToString(),

                        IsCaseWereDesigned = IsCaseWereDesigned,
                        IsLocked = IsLocked,
                        IsCheckedOut = IsCheckedOut,
                        CanBeRenamed = canBeRenamed,
                        CanGenerateStCopy = generateStCopy,
                        HasDesignerHistory = hasDesignerHistory,
                        DesignerHistory = designerHistory,
                        PreviouslyDesigned = previouslyDesigned,
                        HasAnyImage = hasAnyImage,
                    });
#pragma warning restore CS8604 // Possible null reference argument.

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        OrderCount = Current3ShapeOrderList.Count;
                        OrderCountText = Current3ShapeOrderList.Count.ToString() + " orders";
                        if (AllowToShowProgressBar)
                            _MainWindow.pb3ShapeProgressBar.Value += 1;
                        else
                            _MainWindow.pb3ShapeProgressBar.Value = 0;
                    }));
                }
            }

        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (ex.Message.Contains("A network-related or instance-specific error", StringComparison.CurrentCultureIgnoreCase))
                    ThreeShapeServerIsDown = true;
                else if (ex.Message.Contains("The value's length for key 'data source'", StringComparison.CurrentCultureIgnoreCase))
                    ThreeShapeServerIsDown = true;
                else
                {
                    if (!ex.Message.Contains("Incorrect syntax near ')'"))
                        ShowMessage(MainWindow.Instance, "Exception", "Exception occured", ex.Message, Ookii.Dialogs.Wpf.TaskDialogIcon.Error, Buttons.Ok);
                }
            }));
        }

        SearchOnlyInFileNames = false;

        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            _MainWindow.pb3ShapeProgressBar.Value = 0;
        }));
    }



    private async void ListArchivesCases_DoWork(object? sender, DoWorkEventArgs e)
    {
        var data = (SearchData)e.Argument!;
        string keyWord = data.KeyWordOrFilter!;


        if (SearchStringArchives != PreviousSearchStringArchives && !string.IsNullOrEmpty(SearchStringArchives))
        {
            PreviousSearchStringArchives = SearchStringArchives;
            ArchiveResultOffsetOnArchivePage = 0;
        }

        ResultInArchivesOnArchivePage = 0;

        string queryString = $@"SELECT * FROM dbo.Archives
                             WHERE OrderID LIKE '%{keyWord}%' OR Patient_FirstName LIKE '%{keyWord}%' OR Patient_LastName LIKE '%{keyWord}%' OR Customer LIKE '%{keyWord}%'
                             Order by CreateDate DESC
							 OFFSET {ArchiveResultOffsetOnArchivePage * 50} ROWS FETCH NEXT 50 ROWS ONLY";



        string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();

        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            FilterString = keyWord.Trim();

            _MainWindow.pbArchivesProgressBar.Value = 0;
            _MainWindow.pbArchivesProgressBar.Visibility = Visibility.Visible;
        }));

        CurrentArchivesList.Clear();

        try
        {
            int id = 0;
            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                id++;
                ResultInArchivesOnArchivePage++;
                // Unix timestamp is seconds past epoch
                _ = int.TryParse(reader["CreateDate"].ToString()!, out int unixTimeStamp);
                DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                DateTime dresult = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
                string createDate = dresult.ToString("M/d h:mm tt");
                string createYear = dresult.ToString("yyyy");

                string iconPath = $"/Images/ArchivesIcons/{reader["Icon"]}.png";
                string items = reader["Items"].ToString()!.Replace("Unsectioned model, Antagonist model", "Model")
                                                          .Replace("Unsectioned model, Unsectioned model", "Model")
                                                          .Replace("Sectioned model, Unsectioned model", "Model")
                                                          .Replace("Unsectioned model, Sectioned model", "Model")
                                                          .Replace("Sectioned model, Antagonist model", "Model")
                                                          .Replace("Sectioned model", "Model")
                                                          .Replace("Unsectioned model", "Model");

                string designModuleId = reader["DesignModuleID"].ToString()!;
                if (designModuleId == "DentalDesigner")
                    designModuleId = $"DD{reader["DentalVersion"].ToString()!.Replace("-1", "")}";

#pragma warning disable CS8604 // Possible null reference argument.
                CurrentArchivesList.Add(new ArchivesOrdersModel
                {
                    Id = id,
                    OrderID = reader["OrderID"].ToString(),
                    PanNumber = reader["PanNumber"].ToString(),
                    Patient_FirstName = reader["Patient_FirstName"].ToString(),
                    Patient_LastName = reader["Patient_LastName"].ToString(),
                    Registered = reader["Registered"].ToString(),
                    Customer = reader["Customer"].ToString(),
                    XMLFile = reader["XMLFile"].ToString(),
                    BaseFolder = reader["BaseFolder"].ToString(),
                    HostingComputer = reader["HostingComputer"].ToString(),

                    LastUdated = reader["LastUdated"].ToString(),
                    Items = items,
                    ItemsDetailed = reader["ItemsDetailed"].ToString(),
                    OrderComments = reader["OrderComments"].ToString(),
                    Icon = iconPath,
                    ProcessStatusID = reader["ProcessStatusID"].ToString(),
                    ProcessLockID = reader["ProcessLockID"].ToString(),
                    ScanSource = reader["ScanSource"].ToString(),

                    DesignModuleID = designModuleId,
                    DentalVersion = reader["DentalVersion"].ToString(),

                    OriginalOrderID = reader["OriginalOrderID"].ToString(),
                    ManufName = reader["ManufName"].ToString(),
                    CacheMaterialName = reader["CacheMaterialName"].ToString(),


                    CreateDate = createDate,
                    CacheMaxScanDate = reader["CacheMaxScanDate"].ToString(),
                    IsStillAlive = reader["IsStillAlive"].ToString(),
                    ReasonIsDead = reader["ReasonIsDead"].ToString(),
                    DesignerID = reader["DesignerID"].ToString(),
                    DesignerName = reader["DesignerName"].ToString(),
                    CreateYear = createYear,
                });
#pragma warning restore CS8604 // Possible null reference argument.

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    ArchivesCount = CurrentArchivesList.Count;
                    ArchivesCountText = CurrentArchivesList.Count.ToString() + " orders";
                    _MainWindow.pbArchivesProgressBar.Value += 1;

                }));
            }


        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (ex.Message.Contains("A network-related or instance-specific error", StringComparison.CurrentCultureIgnoreCase))
                    ThreeShapeServerIsDown = true;
                else if (ex.Message.Contains("The value's length for key 'data source'", StringComparison.CurrentCultureIgnoreCase))
                    ThreeShapeServerIsDown = true;
                else
                {
                    if (!ex.Message.Contains("Incorrect syntax near ')'"))
                        ShowMessage(MainWindow.Instance, "Exception", "Exception occured", ex.Message, Ookii.Dialogs.Wpf.TaskDialogIcon.Error, Buttons.Ok);
                }
            }));
        }

        SearchOnlyInFileNames = false;

        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            _MainWindow.pbArchivesProgressBar.Value = 0;
        }));
    }

    private bool CheckForImageInOrderFolder(string orderID)
    {
        string path = @$"{ThreeShapeDirectoryHelper}{orderID}\Images";

        if (Directory.Exists(path))
        {
            var fileCount = Directory.EnumerateFiles(path).Count();
            if (fileCount > 0)
                return true;
        }
        return false;
    }


    private void JumpToCasesOpenedForDesignNow()
    {
        FilterMenuItemClicked("Designing");
        SwitchTo3ShapeOrdersTab();
    }

    private void MoveLabnextViewToFolderSubscriptionTab()
    {
        if (_MainWindow.LabnextTabPanel.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
        {
            _MainWindow.LabnextTabPanel.Children.Remove(_MainWindow.LabnextView);
            _MainWindow.FolderSubscriptionTabPanel.Children.Add(_MainWindow.LabnextView);
        }

        if (_MainWindow.paymentIssuePanelLabnextView.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
        {
            _MainWindow.paymentIssuePanelLabnextView.Children.Remove(_MainWindow.LabnextView);
            _MainWindow.FolderSubscriptionTabPanel.Children.Add(_MainWindow.LabnextView);
        }

        if (_MainWindow.webviewLabnext.IsInitialized)
        {
            _MainWindow.webviewLabnext.ZoomFactor = 0.75;
        }
    }

    private void MoveLabnextViewToLabnextTab()
    {
        if (_MainWindow.FolderSubscriptionTabPanel.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
        {
            _MainWindow.FolderSubscriptionTabPanel.Children.Remove(_MainWindow.LabnextView);
            _MainWindow.LabnextTabPanel.Children.Add(_MainWindow.LabnextView);
        }

        if (_MainWindow.paymentIssuePanelLabnextView.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
        {
            _MainWindow.paymentIssuePanelLabnextView.Children.Remove(_MainWindow.LabnextView);
            _MainWindow.LabnextTabPanel.Children.Add(_MainWindow.LabnextView);
        }
    }

    private void MoveLabnextViewToPaymentIssueTab()
    {
        if ((_MainWindow.FolderSubscriptionTabPanel.Children.Contains(_MainWindow.LabnextView) || _MainWindow.LabnextTabPanel.Children.Contains(_MainWindow.LabnextView)) && CbSettingModuleLabnext)
        {
            if (_MainWindow.FolderSubscriptionTabPanel.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
            {
                _MainWindow.FolderSubscriptionTabPanel.Children.Remove(_MainWindow.LabnextView);
                _MainWindow.paymentIssuePanelLabnextView.Children.Add(_MainWindow.LabnextView);
            }

            if (_MainWindow.LabnextTabPanel.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
            {
                _MainWindow.LabnextTabPanel.Children.Remove(_MainWindow.LabnextView);
                _MainWindow.paymentIssuePanelLabnextView.Children.Add(_MainWindow.LabnextView);
            }
        }
    }

    private async void SearchForPanNumberInLabnextForFolderSubscription()
    {
        FsCreationDateOfLabnextCase = "";
        FsStatusOfLabnextCase = "";
        if (!LabNextWebViewStatusText.Contains("/login"))
        {
            if (_MainWindow.FolderSubscriptionTabPanel.Children.Contains(_MainWindow.LabnextView) && CbSettingModuleLabnext)
            {
                string caseId = await LookUpCaseIdInLabnextByPanNumber(SelectedPendingDigiNumber);

                if (caseId == "notloggedin")
                    return;

                if (string.IsNullOrEmpty(caseId))
                    return;


                Uri link = new(HttpUtility.UrlPathEncode($"{LabnextUrl}cases/case/id/{caseId}"), UriKind.Absolute);

                _MainWindow.webviewLabnext.Source = link;
                LabNextWebViewStatusText = link.ToString().Replace($"https://{LabnextLabID}.labnext.net/lab", "");
                ParseLabnextHtml = true;
            }
        }
    }


    private void FilterMenuItemClicked(object obj)
    {
        ShowingFiltersPanel = Visibility.Collapsed;
        _MainWindow.pb3ShapeProgressBar.Value = 0;
        Current3ShapeOrderList.Clear();
        _MainWindow.listView3ShapeOrders.ItemsSource = Current3ShapeOrderList;
        _MainWindow.listView3ShapeOrders.Items.Refresh();
        //GroupList();
        string filter = (string)obj;
        WriteLocalSetting("FilterUsed", filter);

        if (filter == "MyRecent" || filter == "MyRecent30")
        {
            FilterString = "MyRecent";
            SelectedGroupByItem = "None";
        }
        else
            SelectedGroupByItem = ReadLocalSetting("GroupBy");


        Search(filter, true);
    }


    private void HistoryMenuItemClicked(object obj)
    {
        ShowingFiltersPanel = Visibility.Collapsed;
        _MainWindow.pb3ShapeProgressBar.Value = 0;
        Current3ShapeOrderList.Clear();
        _MainWindow.listView3ShapeOrders.ItemsSource = Current3ShapeOrderList;
        _MainWindow.listView3ShapeOrders.Items.Refresh();
        //GroupList();
        string filter = (string)obj;
        WriteLocalSetting("FilterUsed", filter);

        SelectedGroupByItem = ReadLocalSetting("GroupBy");


        Search(filter);
    }

    private void Search(string keyWord)
    {
        WriteLocalSetting("FilterUsed", "");

        ListUpdateable = true;
        ActiveFilterInUse = "";
        ActiveSearchString = keyWord;
        BuildingUpDates();
        if (bwListCases.IsBusy != true)
        {
            bwListCases.RunWorkerAsync(new SearchData
            {
                FilterInUse = false,
                KeyWordOrFilter = keyWord
            });
        }
        else
            bwListCases.CancelAsync();
    }

    private void SearchArchives(string keyWord)
    {
        BuildingUpDates();
        if (bwListArchivesCases.IsBusy != true)
        {
            bwListArchivesCases.RunWorkerAsync(new SearchData
            {
                FilterInUse = false,
                KeyWordOrFilter = keyWord
            });
        }
        else
            bwListCases.CancelAsync();
    }

    public void Search(string Filter, bool SearchWithFilter)
    {
        _MainWindow.pb3ShapeProgressBar.Value = 0;
        ListUpdateable = true;
        ActiveFilterInUse = Filter;
        ActiveSearchString = "";
        BuildingUpDates();
        if (bwListCases.IsBusy != true)
        {
            bwListCases.RunWorkerAsync(new SearchData
            {
                FilterInUse = true,
                KeyWordOrFilter = Filter
            });
        }
        else
        {
            bwListCases.CancelAsync();
            _MainWindow.pb3ShapeProgressBar.Value = 0;
        }
    }

    private async void StartProgramUpdate()
    {
#if DEBUG
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
#endif
        UpdateAvailableText = "Starting App Updater..";
        await Task.Delay(1500);

        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            _MainWindow.Cursor = Cursors.Wait;
        }));

        var Processes = Process.GetProcesses()
                            .Where(pr => pr.ProcessName == "StatsClientUpdater");
        foreach (var process in Processes)
        {
            process.Kill();
        }

        Task.Run(DownloadUpdater).Wait();

        StartUpdaterApp();
    }

    public void BuildingUpDates()
    {
        string TodayName = DateTime.Now.ToString("dddd");
        int lstFd, thisMd;
        lstFd = 0;
        thisMd = 0;

        RestDayStart = " 0:01:00.000";
        RestDayEnd = " 23:59:59.999";

        switch (TodayName)
        {
            case "Monday": lstFd = 3; thisMd = 0; break;
            case "Tuesday": lstFd = 4; thisMd = 1; break;
            case "Wednesday": lstFd = 5; thisMd = 2; break;
            case "Thursday": lstFd = 6; thisMd = 3; break;
            case "Friday": lstFd = 7; thisMd = 4; break;
            case "Saturday": lstFd = 1; thisMd = 5; break;
            case "Sunday": lstFd = 2; thisMd = 6; break;
        }

        DtLastTwoDayNames = DateTime.Now.AddDays(-2).ToString("dddd") + " and " + DateTime.Now.AddDays(-1).ToString("dddd");
        DtLastThreeDayNames = DateTime.Now.AddDays(-3).ToString("dddd") + ", " + DateTime.Now.AddDays(-2).ToString("dddd") + " and " + DateTime.Now.AddDays(-1).ToString("dddd");

        DtToday = DateTime.Now.ToString("yyyy-MM-dd");

        DtYesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        DtLastFriday = DateTime.Now.AddDays(-lstFd).ToString("yyyy-MM-dd");
        DtThisMonday = DateTime.Now.AddDays(-thisMd).ToString("yyyy-MM-dd");
        DtLastWeekFriday = DateTime.Now.AddDays(-(lstFd + 7)).ToString("yyyy-MM-dd");
        DtLastWeekMonday = DateTime.Now.AddDays(-(lstFd + 11)).ToString("yyyy-MM-dd");
        DtLastWeekSunday = DateTime.Now.AddDays(-(lstFd + 5)).ToString("yyyy-MM-dd");
        DtOneMonthBack = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        DtTwoMonthsBack = DateTime.Now.AddDays(-60).ToString("yyyy-MM-dd");
        DtLastTwoDays = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd");
        DtLastThreeDays = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd");
        DtLastSevenDays = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
    }

    public bool CheckFolderIsWritable(string folder)
    {
        string lastCharacter = folder[^1..];

        try
        {
            if (lastCharacter != "\\")
            {
                File.WriteAllText(folder + "\\write.test", "Write test");
                File.Delete(folder + "\\write.test");
            }
            else
            {
                File.WriteAllText(folder + "write.test", "Write test");
                File.Delete(folder + "write.test");
            }
            return true;
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("The network path was not found", StringComparison.CurrentCultureIgnoreCase) || !ex.Message.Contains("Could not find the part of the path", StringComparison.CurrentCultureIgnoreCase))
                AddDebugLine(ex);

            return false;
        }
    }

    public void ShowNotificationMessage(string title, string message, NotificationIcon notificationIcon = NotificationIcon.Info,
                                        bool notificationWindowPulledIn = false,
                                        double pullUpFromBottomEdge = 50)
    {

        if (FsCopyPanelShows && notificationWindowPulledIn)
            pullUpFromBottomEdge = 135;


        //if (notificationWindowOnInfoBar)
        //{
        //    NotificationMessageGridPosition = "2";
        //    NotificationMessagePosition = new Thickness(1, 10, 0, pullUpFromBottomEdge);
        //    NotificationMessageVertAlignment = VerticalAlignment.Top;
        //}
        //else
        //{
        NotificationMessageVertAlignment = VerticalAlignment.Bottom;
        if (notificationWindowPulledIn)
            NotificationMessagePosition = new Thickness(151, 0, 0, pullUpFromBottomEdge);
        else
            NotificationMessagePosition = new Thickness(15, 0, 0, pullUpFromBottomEdge);
        NotificationMessageGridPosition = "1";
        //}

        NotificationMessageTitle = title;
        NotificationMessageBody = message;
        NotificationMessageIcon = $@"\Images\MessageIcons\{notificationIcon}.png";
        NotificationMessageVisibility = Visibility.Visible;
        DoubleAnimation da = new(1, TimeSpan.FromMilliseconds(250));
        MainWindow.Instance.notificationMessagePanel.BeginAnimation(FrameworkElement.OpacityProperty, da);

        notificationTimer.Stop();
        notificationTimer.Start();
    }

    public enum NotificationIcon
    {
        Info,
        Warning,
        Error,
        Success,
        Question
    }


    private void ResetDigiSystemColors()
    {
        DigiSystemColors.Clear();
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFfcf0cc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFfcccfc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFfcd1cc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFcce1fc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFeffccc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFcefccc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFcccefc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFfce2cc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFccfced") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFccf6fc") as SolidColorBrush)!, true);
        DigiSystemColors.Add((new BrushConverter().ConvertFromString("#FFe9ccfc") as SolidColorBrush)!, true);
        DigiSystemColors.Add(Brushes.WhiteSmoke, true);
        DigiSystemColors.Add(Brushes.LightYellow, true);
        DigiSystemColors.Add(Brushes.LightPink, true);
        DigiSystemColors.Add(Brushes.Black, true);
        DigiSystemColors.Add(Brushes.LightCoral, true);
        DigiSystemColors.Add(Brushes.Yellow, true);
        DigiSystemColors.Add(Brushes.LightSeaGreen, true);
        DigiSystemColors.Add(Brushes.CornflowerBlue, true);
        DigiSystemColors.Add(Brushes.DeepPink, true);
        DigiSystemColors.Add(Brushes.Violet, true);
    }

    private void FillUpDigiCasePanel()
    {
        if (CbSettingShowDigiCases)
        {
            double fontSize = 10;
            double fontSizeCopy = 14;

            PendingDigiNumbersWaitingToProcessInt = GetAllNotProcessedNumbers().Count;


            Dictionary<string, int> catchedEmails = GetEWCategoriesAndCounts();
            Dictionary<string, int> meditCases = GetMeditCasesWithCounts();

            ResetDigiSystemColors();

            //MainWindow.Instance.panelDigiCases.Children.Clear();
            MainWindow.Instance.panelNewlyArrivedDigitalCasesList.Children.Clear();

            int countedCases = 0;

            //if (PendingDigiNumbersWaitingToProcessInt > 0 && CbSettingIncludePendingDigiCasesInNewlyArrived)
            //{
            //    string pendingDigiName = PendingDigiCasesReplacementName.Trim();
            //    if (pendingDigiName == "")
            //        pendingDigiName = "PendingDigi";

            //    TextBlock textBlock = new()
            //    {
            //        Text = $"{pendingDigiName} ⇢ Got {PendingDigiNumbersWaitingToProcessInt} new case (In-house)",
            //        FontSize = fontSize,
            //        Foreground = new BrushConverter().ConvertFromString("#FF8ce4ff") as SolidColorBrush,
            //        FontWeight = FontWeights.SemiBold,
            //    };


            //    MainWindow.Instance.panelDigiCases.Children.Add(textBlock);
            //}

            if (NewTriosCaseInInboxCount > 0)
            {
                TextBlock textBlock = new()
                {
                    Text = $"Trios ⇢ Got {NewTriosCaseInInboxCount} new case",
                    FontSize = fontSize,
                    Foreground = Brushes.LightGreen,
                    FontWeight = FontWeights.SemiBold,
                };

                TextBlock textBlockCopy = new()
                {
                    Text = $"Trios ⇢ Got {NewTriosCaseInInboxCount} new case",
                    FontSize = fontSizeCopy,
                    Foreground = Brushes.LightGreen,
                    FontWeight = FontWeights.SemiBold,
                };

                //MainWindow.Instance.panelDigiCases.Children.Add(textBlock);

                MainWindow.Instance.panelNewlyArrivedDigitalCasesList.Children.Add(textBlockCopy);
            }


            foreach (var item in catchedEmails)
            {
                Brush textColor = Brushes.LightGreen;
                foreach (var color in DigiSystemColors)
                {
                    if (color.Value == true)
                    {
                        textColor = color.Key;
                        break;
                    }
                }

                if (textColor != null)
                    DigiSystemColors[textColor] = false;

                string digiSystem = "";
                string labIdentifierAllScan = "";
                if (item.Key.Contains('-'))
                {
                    string[] identifierParts = item.Key.Split('-');
                    digiSystem = identifierParts[0].Trim();
                    labIdentifierAllScan = identifierParts[1].Trim();
                }

                TextBlock textBlock = new()
                {
                    Text = $"{digiSystem} ⇢ Got {item.Value} new case ({labIdentifierAllScan})",
                    FontSize = fontSize,
                    Foreground = textColor!,
                    FontWeight = FontWeights.SemiBold,
                };

                TextBlock textBlockCopy = new()
                {
                    Text = $"{digiSystem} ⇢ Got {item.Value} new case ({labIdentifierAllScan})",
                    FontSize = fontSizeCopy,
                    Foreground = textColor!,
                    FontWeight = FontWeights.SemiBold,
                };

                countedCases += item.Value;
                //MainWindow.Instance.panelDigiCases.Children.Add(textBlock);

                MainWindow.Instance.panelNewlyArrivedDigitalCasesList.Children.Add(textBlockCopy);
            }


            foreach (var item in meditCases)
            {
                Brush textColor = Brushes.LightSteelBlue;
                foreach (var color in DigiSystemColors)
                {
                    if (color.Value == true)
                    {
                        textColor = color.Key;
                        break;
                    }
                }

                if (textColor != null)
                    DigiSystemColors[textColor] = false;

                string labIdentifier = GetSingleEWIdentifier(item.Key);

                TextBlock textBlock = new()
                {
                    Text = $"Medit ⇢ Got {item.Value} new case ({labIdentifier})",
                    FontSize = fontSize,
                    Foreground = textColor!,
                    FontWeight = FontWeights.SemiBold,
                };

                TextBlock textBlockCopy = new()
                {
                    Text = $"Medit ⇢ Got {item.Value} new case ({labIdentifier})",
                    FontSize = fontSizeCopy,
                    Foreground = textColor!,
                    FontWeight = FontWeights.SemiBold,
                };

                countedCases += item.Value;
                //MainWindow.Instance.panelDigiCases.Children.Add(textBlock);

                MainWindow.Instance.panelNewlyArrivedDigitalCasesList.Children.Add(textBlockCopy);
            }

            if (CbSettingIncludePendingDigiCasesInNewlyArrived)
                NewDigiCaseArrivedCount = NewTriosCaseInInboxCount + PendingDigiNumbersWaitingToProcessInt + countedCases;
            else
                NewDigiCaseArrivedCount = NewTriosCaseInInboxCount + countedCases;

            TotalNewDigiCaseWithoutInHouseCases = NewTriosCaseInInboxCount + countedCases;
        }
    }



    private void BwBackgroundTasks_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {

    }

    private async void BwBackgroundTasks_DoWork(object? sender, DoWorkEventArgs e)
    {
        string arg = (string)e.Argument!;
        string[] argParts = arg.Split('|');
        _ = int.TryParse(argParts[0], out int minute);
        _ = int.TryParse(argParts[1], out int second);

        Application.Current.Dispatcher.Invoke(new Action(async () =>
        {
            if (_MainWindow.mainTabControl.SelectedItem != _MainWindow.HomeTab)
                HomeButtonShows = Visibility.Visible;

            if (_MainWindow.mainTabControl.SelectedItem == _MainWindow.ThreeShapeTab)
                RefreshButtonShows = Visibility.Visible;
            else
                RefreshButtonShows = Visibility.Collapsed;


            //ServerStatus = GetStatsServerStatus();
            ServerIsWritingDatabase = CheckIfServerIsWritingDatabase();
            if (CbSettingModuleFolderSubscription)
                FsLastDatabaseUpdate = GetLastDatabaseUpdate();

            await Task.Run(LookForPendingTask);

            await Task.Run(UpdateDesignerUnitCounts);


            if (CbSettingModuleLabnext && !LabNextWebViewStatusText.Contains("/login"))
                LabnextIconCanShowOn3ShapeListView = true;
            else
                LabnextIconCanShowOn3ShapeListView = false;

            if (CbSettingModulePrescriptionMaker)
            {
                if (CbSettingExtractIteroZipFiles)
                    CountiTeroFolders();

                List<InconsistencyModel> listGood = await GetPrescriptionWithNoInconsistencys();
                List<InconsistencyModel> list = await GetPrescriptionInconsistencys();
                try
                {

                    if (IgnoredPrescriptionInconsistencys.Count > 0)
                    {
                        foreach (var item in IgnoredPrescriptionInconsistencys)
                        {
                            if (item is not null)
                                list.Remove(list.FirstOrDefault(x => x.OrderID == item.OrderID)!);
                        }
                    }

                    PrescriptionInconsistencys = list;


                    if (IgnoredPrescriptionInconsistencys.Count > 0)
                    {

                        foreach (var item in IgnoredPrescriptionInconsistencys)
                        {
                            if (item is not null)
                                if (!listGood.Any(x => x.OrderID == item.OrderID))
                                {
                                    InconsistencyModel model = new()
                                    {
                                        OrderID = item.OrderID,
                                        PanNumber = item.PanNumber,
                                        Ignored = true
                                    };
                                    listGood.Add(model);
                                }
                        }
                    }

                    await FillUpIngnoredOrdersInInconsistencyList();
                }
                catch (Exception ex)
                {
                    AddDebugLine(ex);
                }

                PrescriptionWithNoInconsistencys = listGood;
            }
        }));

        if (InfoTabActive)
        {
            if (DateTime.Now.Second % 2 == 0)
            {
                HealthReports = await Task.Run(GetHealthReportsAsync);
                if (IsDCASIsActive)
                    LastDCASUpdate = GetLastDCASUpdate();
            }
        }
        else
        {
            if (DateTime.Now.Second % 30 == 0)
                HealthReports = await Task.Run(GetHealthReportsAsync);
        }


        if (DateTime.Now.Minute % 5 == 0 && DateTime.Now.Second < 6)
        {
            BuildCommentRuleList();
            SearchHistory = await GetBackAllSearchHistoryFromLocalDB();

            //UpdateSearchHistorryContextMenu();

            PaymentIssueCount = await GetPaymentIssueCountFromDB();

            if (SelectedDesignerPaymentSummary is null)
                DesignerPaymentSummaryList = await GetDesignerPaymentSummaryFromDB();
            //DoublePaidOrdersList = await GetDoublePaidOrdersListFromDB();
            PaidToWrongPersonOrdersList = await GetPaidToWrongPersonsOrdersListFromDB();
        }

        // clear list once a day
        if (DateTime.Now.Hour == 0 && DateTime.Now.Minute == 0 && DateTime.Now.Second < 6)
        {
            DeleteOldOrderToIgnoredListLocalDB();
            DeleteOldPMEventsFromLocalDB();

            SearchHistory = await GetBackAllSearchHistoryFromLocalDB();
            DeleteOldSearchHistoryFromLocalDB();
        }

        if (second % 5 == 0)
        {
            Application.Current.Dispatcher.Invoke(new Action(async () =>
            {
                ImportedCasesList = await GetBackImportHistory();
                ExportedCasesList = await GetBackExportHistory();

                if (TestImportHistoryList.Count > 0)
                {
                    foreach (var item in TestImportHistoryList)
                    {
                        ImportedCasesList.Add(item);
                    }
                }
            }));
        }

        if (second % 5 == 0 || ServerLogCanBeRead)
        {
            if (ScrollServerLogToBottom && ServerLogWebViewIsInitialized)
            {
                Application.Current.Dispatcher.Invoke(new Action(async () =>
                {
                    await _MainWindow.webview.ExecuteScriptAsync("window.scroll(0,10000000)");
                }));
            }
        }



        if (second % 15 == 0 || FirstRun)
        {

            CurrentMemoryUsage = Math.Round(await GetMemoryUsage() / (1024 * 1024));

            if (FirstRun)
            {
                BuildingUpDates();
                UpdateOrderIssuesList();
                UpdatePanNrDuplicatesList();

                TotalMemoryInGiB = await GetTotalMemoryInGiB();
                TotalMemory = Math.Round(await GetTotalMemoryInMiB());
            }


            FirstRun = false;
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                int casesDesigningNow = GetOpenedForDesignCasesCount(ServerID);
                if (casesDesigningNow > 0)
                {
                    DesignerOpenToolTip = casesDesigningNow.ToString();

                    //if (MainWindow.Instance.panelDesignerOpen.Children.Count > casesDesigningNow ||
                    //    MainWindow.Instance.panelDesignerOpen.Children.Count < casesDesigningNow)
                    //{
                    //    MainWindow.Instance.panelDesignerOpen.Children.Clear();

                    //    for (int i = 0; i < casesDesigningNow; i++)
                    //    {
                    //        //Effect bitmapEffect = new DropShadowEffect
                    //        //{
                    //        //    ShadowDepth = 1,
                    //        //    Opacity = 0.55,
                    //        //    Color = Colors.Orange,
                    //        //    Direction = 270
                    //        //};


                    //        Image image = new()
                    //        {
                    //            Source = new BitmapImage(new Uri("pack://application:,,,/Images/ListViewIcons/psModelling.png")),
                    //            Width = 16,
                    //            Height = 16,
                    //            Cursor = Cursors.Hand,
                    //            ToolTip = casesDesigningNow.ToString() + " case is open for design",
                    //            Margin = new Thickness(0, 3, 1, 0), 
                    //            //Effect = bitmapEffect,
                    //        };

                    //        //Style s = new();


                    //        //Setter setterButtonBg = new()
                    //        //{
                    //        //    Property = Button.BackgroundProperty,
                    //        //    Value = Brushes.Red
                    //        //};



                    //        //s.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
                    //        //s.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
                    //        //s.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));


                    //        //Button btn = new()
                    //        //{
                    //        //    Content = image,
                    //        //    Command = JumpToCasesOpenedForDesignNowCommand,
                    //        //    //Style = Application.Current.Resources["BlankButtonNew"] as Style
                    //        //    Style = s
                    //        //};

                    //        MainWindow.Instance.panelDesignerOpen.Children.Add(image);

                    //    }
                    //}

                    DesignerOpen = true;
                }
                else
                {
                    //MainWindow.Instance.panelDesignerOpen.Children.Clear();
                    DesignerOpen = false;
                }

                FsCountedEntries = GetBackFolderSubscriptionCountedEntries();

                _ = int.TryParse(GetSentOutIssuesCount(), out int sentOutIssues);
                SentOutIssuesCount = sentOutIssues;


                if (CbSettingShowDigiPrescriptionsCount)
                    DigiPrescriptionsTodayCount = GetCurrentDigiPrescriptionCount();

                if (CbSettingShowDigiCasesIn3ShapeTodayCount)
                    DigiCasesIn3ShapeTodayCount = GetDigiCasesIn3ShapeTodayCount();

                FillUpDigiCasePanel();


                FillUpPendingDigiCaseNumberList();

                _ = bool.TryParse(ReadStatsSetting("dcas_EmailWatcherActive"), out bool isDCASIsActive);
                IsDCASIsActive = isDCASIsActive;



                //checking if server log is readable
                if (File.Exists(@$"\\{StatsServersComputerName}\StatsSystemsLogs$\StatsSystem_log_{DateTime.Now:yyyy-MM-dd}.html"))
                {
                    ServerLogCanBeRead = true;
                    ServerLogUrl = @$"\\{StatsServersComputerName}\StatsSystemsLogs$\StatsSystem_log_{DateTime.Now:yyyy-MM-dd}.html";
                    if (_MainWindow.webview.Source != new Uri(ServerLogUrl))
                        _MainWindow.webview.Source = new Uri(ServerLogUrl);
                }
                else
                    ServerLogCanBeRead = false;




            }));
        }

        if (second % 59 == 1)
        {
            UpdateOrderIssuesList();
            UpdatePanNrDuplicatesList();

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (bwGetSentOutIssues.IsBusy != true)
                {
                    bwGetSentOutIssues.RunWorkerAsync();
                }
            }));

            GC.Collect();
        }

        if (second % 30 == 1)
        {
            await ReportClientLoginToDatabase();
        }

        if (minute % 59 == 0 && second < 3)
            BuildingUpDates();


        //setting up an event handler for getting the JSON text from webview on pan number lookup
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (_MainWindow.webviewLabnext.CoreWebView2 is not null && !EventHandlerAlreadyAdded && CbSettingModuleLabnext)
            {
                _MainWindow.webviewLabnext.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
                _MainWindow.webviewLabnext.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                EventHandlerAlreadyAdded = true;
            }
        }));
    }

    private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            string html = await GetPageHtmlAsync();
            ParseHtml(html);
        }
    }


    /// <summary>
    /// Executes JavaScript to get the full HTML of the loaded page.
    /// </summary>
    private async Task<string> GetPageHtmlAsync()
    {
        try
        {
            string script = "document.documentElement.outerHTML;";
            string result = await _MainWindow.webviewLabnext.ExecuteScriptAsync(script);

            // WebView2 returns JSON-encoded string, so remove quotes and unescape
            return System.Text.Json.JsonSerializer.Deserialize<string>(result)!;
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, $"Error getting page HTML: {ex.Message}", "MVM");
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses HTML using HtmlAgilityPack.
    /// </summary>
    private void ParseHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return;

        try
        {

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (html.Contains("Closed - Invoiced"))
            {
                try
                {
                    string result = CopyStringFromAfter(Regex.Replace(doc.Text, @"\s+", " "), "Creation Date:", 75).Replace("</dt>", "").Replace("<dd>", "").Trim();
                    FsCreationDateOfLabnextCase = CopyStringTill(result, '<').Trim();

                    string result2 = CopyStringFromAfter(Regex.Replace(doc.Text, @"\s+", " "), "Status:", 75).Replace("</dt>", "").Replace("<dd>", "").Trim();
                    FsStatusOfLabnextCase = CopyStringTill(result2, '<').Trim();

                    string result3 = CopyStringFromAfter(Regex.Replace(doc.Text, @"\s+", " "), "<dt>Account:</dt>", 95).Replace("</dt>", "").Replace("<dt>", "").Replace("<dd>", "").Trim();
                    result3 = new Regex("href=\"[^\"]*\"").Replace(result3, "").Replace("<a >", "");

                    if (LookingForPanNumberForPaymentIssue)
                    {
                        string result4 = CopyStringFromAfter(Regex.Replace(doc.Text, @"\s+", " "), "<dt>Pan</dt>", 210);
                        result4 = CopyStringTill(result4, "</dd>").Trim();
                        result4 = result4.Replace("</dt>", "").Replace("<dt>", "").Replace("<dd>", "").Trim();
                        result4 = new Regex("href=\"[^\"]*\"").Replace(result4, "");
                        result4 = new Regex("id=\"[^\"]*\"").Replace(result4, "");
                        result4 = new Regex("title=\"[^\"]*\"").Replace(result4, "").Replace("<a >", "").Replace("</a>", "");
                        result4 = new Regex("class=\"[^\"]*\"").Replace(result4, "");
                        result4 = new Regex("style=\"[^\"]*\"").Replace(result4, "").Replace("<span >", "").Replace("</span>", "").Replace("&nbsp;", "");
                        result4 = result4.Replace("<span >", "").Trim();
                        result4 = KeepOnlyNumeric().Replace(result4, "");
                        if (int.TryParse(result4, out int panNr))
                        {
                            SelectedPaymentIssueForDesigner.PanNumber = panNr;
                            FoundPanNumberSx = panNr;
                        }
                        //MessageBox.Show(result4);
                        AddDebugLine(null, $"Pan number found: {result4}");
                    }

                }
                catch (Exception ex)
                {
                    // Handle parsing errors
                    AddDebugLine(ex, $"Error parsing HTML: {ex.Message}", "MVM");
                }

                //ShowMessageBox("Case is invoiced", "This case is already closed and invoiced.\nConsider checking if this is the right case\nyou looking for.", SMessageBoxButtons.Ok, NotificationIcon.Warning, 20, _MainWindow);

                LookingForPanNumberForPaymentIssue = false;
                return;
            }
            else if (!html.Contains("json-formatter-container"))
            {
                string? ptName;
                if (doc.GetElementbyId("patient_add_content") is null)
                    return;

                ptName = doc.GetElementbyId("patient_add_content").InnerText.Trim();

                try
                {
                    string result = CopyStringFromAfter(Regex.Replace(doc.Text, @"\s+", " "), "Creation Date:", 75).Replace("</dt>", "").Replace("<dd>", "").Trim();
                    FsCreationDateOfLabnextCase = CopyStringTill(result, '<').Trim();

                    string result2 = CopyStringFromAfter(Regex.Replace(doc.Text, @"\s+", " "), "Status:", 75).Replace("</dt>", "").Replace("<dd>", "").Trim();
                    FsStatusOfLabnextCase = CopyStringTill(result2, '<').Trim();
                }
                catch (Exception ex)
                {
                    // Handle parsing errors
                    AddDebugLine(ex, $"Error parsing HTML: {ex.Message}", "MVM");
                }

                string[] parts = ptName.Split(' ');
                if (parts[0].Length >= 3)
                    FsSearchString = parts[0].Substring(0, 3);
                else
                    FsSearchString = parts[0];

                if (FsSearchString.Length <= 1)
                {
                    if (parts[1].Length >= 3)
                        FsSearchString = parts[1].Substring(0, 3);
                    else
                        FsSearchString = parts[1];
                }

                FsSearchFolders();
            }
            LookingForPanNumberForPaymentIssue = false;
        }
        catch (Exception ex)
        {
            // Handle parsing errors
            AddDebugLine(ex, $"Error parsing HTML: {ex.Message}", "MVM");
        }

    }


    private void UpdateDesignerUnitCounts()
    {
        List<DesignerUnitCountModel> list = [];



        DesignerUnitCountsList = list;
    }


    //private void UpdateSearchHistorryContextMenu()
    //{
    //    Application.Current.Dispatcher.Invoke(new Action(() =>
    //    {
    //        SearchHistoryForContextMenu.Clear();
    //        SearchHistoryForContextMenu.Add(new MenuItem()
    //        {
    //            Header = "Search History",
    //            Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#e6c235")!,
    //            FontSize = 14,
    //            FontWeight = FontWeights.Bold,
    //            IsEnabled = false,
    //        });
    //        foreach (var item in SearchHistory)
    //        {
    //            SearchHistoryForContextMenu.Add(new MenuItem()
    //            {
    //                Header = item,
    //                CommandParameter = item,
    //                Command = HistoryMenuItemCommand,
    //            });
    //        }
    //    }));
    //}

    private void BwGetSentOutIssues_DoWork(object? sender, DoWorkEventArgs e)
    {
        try
        {
            string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();
            string query = @"SELECT * FROM dbo.SentOutIssues";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            IssuesWithCasesList.Clear();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {

                string IconSource = "";
                switch (reader["Level"].ToString())
                {
                    case "0": IconSource = @"\Images\IssuesIcons\info.png"; break;
                    case "1": IconSource = @"\Images\IssuesIcons\warning.png"; break;
                    case "2": IconSource = @"\Images\IssuesIcons\error.png"; break;
                }

                IssuesWithCasesList.Add(new IssuesWithCasesModel(
                    reader["Level"].ToString()!,
                    reader["OrderID"].ToString()!,
                    reader["SkipReason"].ToString()!.Replace("&apos;", "'"),
                    reader["ForeColor"].ToString()!,
                    reader["CreateDate"].ToString()!,
                    IconSource
                ));
            }

        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("A network-related or instance-specific error", StringComparison.CurrentCultureIgnoreCase))
                ThreeShapeServerIsDown = true;
            else
                AddDebugLine(ex);
        }
    }

    private void BwGetSentOutIssues_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        //listViewOrderIssues.Items.Refresh();
    }

    #region >> Initial Tasks at startup
    private void InitialTasksAtApplicationStartup_DoWork(object? sender, DoWorkEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(new Action(async () =>
        {
            SplashViewModel.Instance.LoadingText = "Gathering info from database..";
            ThisSite = DatabaseOperations.GetServerSiteName();
            ThreeShapeDirectoryHelper = DatabaseOperations.GetServerFileDirectory();
            //ServerFriendlyNameHelper = DatabaseOperations.GetServerName();

            // Initialize PaymentListCutOffDate from database settings
            InitializePaymentListCutOffDate();

            _ = bool.TryParse(ReadLocalSetting("GlassyEffect"), out bool GlassyEffect);
            _ = bool.TryParse(ReadLocalSetting("ShowAvailablePanCount"), out bool ShowAvailablePanCount);
            _ = bool.TryParse(ReadLocalSetting("StartAppMinimized"), out bool StartAppMinimized);
            //_ = bool.TryParse(ReadLocalSetting("ShowBottomInfoBar"), out bool showBottomInfoBar);
            //_ = bool.TryParse(ReadLocalSetting("ShowDigiDetails"), out bool showDigiDetails);
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

            _ = bool.TryParse(ReadLocalSetting("ModuleFolderSubscription"), out bool moduleFolderSubscription);
            _ = bool.TryParse(ReadLocalSetting("ModuleAccountInfos"), out bool moduleAccountInfos);
            _ = bool.TryParse(ReadLocalSetting("ModuleLabnext"), out bool moduleLabnext);
            _ = bool.TryParse(ReadLocalSetting("ModuleSmartOrderNames"), out bool moduleSmartOrderNames);
            _ = bool.TryParse(ReadLocalSetting("ModuleDebug"), out bool moduleDebug);
            _ = bool.TryParse(ReadLocalSetting("ModulePrescriptionMaker"), out bool modulePrescriptionMaker);
            _ = bool.TryParse(ReadLocalSetting("ModulePendingDigitals"), out bool modulePendingDigitals);

            _ = bool.TryParse(ReadStatsSetting("dcas_EmailWatcherActive"), out bool isDCASIsActive);



            if (!bool.TryParse(ReadLocalSetting("IncludePendingDigiCases"), out bool includePendingDigiCases))
                CbSettingIncludePendingDigiCasesInNewlyArrived = true;

            CbSettingGlassyEffect = GlassyEffect;


            CbSettingStartAppMinimized = StartAppMinimized;
            //ShowBottomInfoBar = showBottomInfoBar;
            //CbSettingShowDigiDetails = showDigiDetails;
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

            IsDCASIsActive = isDCASIsActive;

            TriosInboxFolder = ThreeShapeDirectoryHelper + @"3ShapeCommunicate\Inbox";

            LabnextLabID = ReadStatsSetting("LabnextLabID");
            LabnextUrl = $"https://{LabnextLabID}.labnext.net/lab/";

            if (CbSettingModuleLabnext)
                _MainWindow.webviewLabnext.Source = new Uri(LabnextUrl);

            LabNextWebViewStatusText = "/";

            string srchLimit = ReadLocalSetting("SearchLimit");
            if (!string.IsNullOrEmpty(srchLimit))
                SearchLimit = srchLimit;

            string tmOut = ReadLocalSetting("TimeoutForImportAncmnt");
            if (!string.IsNullOrEmpty(tmOut))
                TimeOut = tmOut;


            if (CbSettingModuleLabnext)
                LabnextLoadingHiderTimer.Start();

#if !DEBUG

            if (Directory.Exists(TriosInboxFolder))
            {
                fswTriosFolderWatcher.Path = TriosInboxFolder;
                fswTriosFolderWatcher.Filter = "*.*";
                fswTriosFolderWatcher.NotifyFilter = NotifyFilters.DirectoryName;
                fswTriosFolderWatcher.Created += new FileSystemEventHandler(FswTriosFolderWatcher_Created);
                fswTriosFolderWatcher.Deleted += new FileSystemEventHandler(FswTriosFolderWatcher_Deleted);
                fswTriosFolderWatcher.EnableRaisingEvents = true;
                CountTriosCases();
            }
#endif

            PendingDigiCasesReplacementName = ReadLocalSetting("PendingDigiCasesReplacementName");
            if (string.IsNullOrEmpty(PendingDigiCasesReplacementName))
                PendingDigiCasesReplacementName = "PendingDigi";

            FsubscrTargetFolder = ReadLocalSetting("SubscriptionCopyFolder");
            PmWatchedPdfFolder = ReadLocalSetting("PmWatchedPdfFolder");
            if (string.IsNullOrEmpty(PmWatchedPdfFolder))
                PmWatchedPdfFolder = "Click here to setup..";

            PmFinalPrescriptionsFolder = ReadLocalSetting("FinalPrescriptionsFolder");
            if (string.IsNullOrEmpty(PmFinalPrescriptionsFolder))
                PmFinalPrescriptionsFolder = "Click here to setup..";

            PmSironaScansFolder = ReadLocalSetting("SironaScansFolder");
            if (string.IsNullOrEmpty(PmSironaScansFolder))
                PmSironaScansFolder = "Click here to setup..";

            PmIteroExportFolder = ReadLocalSetting("IteroExportFolder");
            if (string.IsNullOrEmpty(PmIteroExportFolder))
                PmIteroExportFolder = "Click here to setup..";

            PmDownloadFolder = ReadLocalSetting("PmDownloadFolder");
            if (string.IsNullOrEmpty(PmDownloadFolder))
                PmDownloadFolder = "Click here to setup..";

            if (PmDownloadFolder.Contains("Click here to"))
            {
                PmDownloadFolder = Environment.GetEnvironmentVariable("USERPROFILE") + @"\" + @"Downloads\";
            }

            //if (ShowBottomInfoBar)
            //    BottomBarSize = 120;
            //else
            //    BottomBarSize = 35;

            SetAppVersion();

            ResetDigiSystemColors();

            FillUpEmptyPanNumberPanel();

            BuildCommentRuleList();

            if (CbSettingModuleAccountInfos)
                GetAccountInfos();

            PDFTemp = DataBaseFolder + @"PDFTemp";

            if (Directory.Exists(PDFTemp))
                Directory.Delete(PDFTemp, true);

            Directory.CreateDirectory(PDFTemp);

            if (CbSettingWatchFolderPrescriptionMaker && Directory.Exists(PmWatchedPdfFolder))
            {
                fswPrescriptionMaker.Path = PmWatchedPdfFolder;
                fswPrescriptionMaker.Filter = "*.pdf";
                fswPrescriptionMaker.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                fswPrescriptionMaker.Created += new FileSystemEventHandler(FswPrescriptionMaker_Created);
                fswPrescriptionMaker.Changed += new FileSystemEventHandler(FswPrescriptionMaker_Changed);
                fswPrescriptionMaker.EnableRaisingEvents = true;
            }

            if (CbSettingExtractIteroZipFiles && Directory.Exists(PmDownloadFolder) && Directory.Exists(PmIteroExportFolder))
            {
                fswIteroZipFileWhatcher.Path = PmDownloadFolder;
                fswIteroZipFileWhatcher.Filter = "iTero_Export_*.zip";
                fswIteroZipFileWhatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                fswIteroZipFileWhatcher.Created += new FileSystemEventHandler(FswIteroZipFileWhatcher_Created);
                fswIteroZipFileWhatcher.Changed += new FileSystemEventHandler(FswIteroZipFileWhatcher_Created);
                fswIteroZipFileWhatcher.EnableRaisingEvents = true;

            }

            PmSendToList = GetAllSendToEnties();

            if (StartAppMinimized && MainWindow.Instance is not null)
                MainWindow.Instance.WindowState = WindowState.Minimized;





            await ReportClientLoginToDatabase(true);

            // deleting old entries from local database on startup
            DeleteOldOrderToIgnoredListLocalDB();
            DeleteOldPMEventsFromLocalDB();

            SearchHistory = await GetBackAllSearchHistoryFromLocalDB();

            //UpdateSearchHistorryContextMenu();

            DeleteOldSearchHistoryFromLocalDB();

            ReadBackAllEvent();

            await FillUpIngnoredOrdersInInconsistencyList();

            TotalOrdersInArchivesDatastore = DatabaseOperations.GetTotalOrdersForArchives().ToString("N0");
            OrdersInArchivesDatastoreBetweenDates = DatabaseOperations.GetOrdersBetweenDatesForArchives();
            LastArchivesDatastoreRebuildDate = DatabaseOperations.GetLastRebuiltDateForArchives();

            PaymentIssueCount = await GetPaymentIssueCountFromDB();
            DesignerPaymentSummaryList = await GetDesignerPaymentSummaryFromDB();
            //DoublePaidOrdersList = await GetDoublePaidOrdersListFromDB(); 
            PaidToWrongPersonOrdersList = await GetPaidToWrongPersonsOrdersListFromDB();
        }));
    }


    private async void SetAppVersion()
    {
        SoftwareVersion = await GetAppVersion();
        _ = double.TryParse(SoftwareVersion, out double appVersionDouble);
        AppVersionDouble = appVersionDouble;
    }

    public void InitialTasksAtApplicationStartup_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        SplashViewModel.Instance.LoadingText = "Loading finished!";
        SplashViewModel.Instance.mainWindow!.Show();
        SplashWindow.Instance.Hide();
        AppIsFullyLoaded = true;
#if DEBUG
        AddDebugLine(null, "App started");
#endif
        GeneralTimer_Tick(sender, e);
    }


    private async void FswIteroZipFileWhatcher_Created(object sender, FileSystemEventArgs e)
    {
        if (!CbSettingExtractIteroZipFiles)
            return;

        string exportFolder = PmIteroExportFolder;

        if (string.IsNullOrEmpty(exportFolder))
            return;

        //bool success = false;
        try
        {
            if (Directory.Exists($@"{exportFolder}\{e.Name!.Replace(".zip", "")}"))
            {
                try
                {
                    Directory.Delete($@"{exportFolder}\{e.Name!.Replace(".zip", "")}", true);
                }
                catch (Exception ex)
                {
                    AddDebugLine(ex);
                }
            }

            //// blocking the thread until the file is released / dowloaded for a time of maximum 11 seconds
            //int i = 0;
            //FileInfo file = new(e.FullPath);
            //while (IsFileLocked(file) || i > 10)
            //{
            //    await Task.Delay(1000);
            //    i++;
            //}

            await Task.Run(() => ZipFile.ExtractToDirectory(e.FullPath, $@"{exportFolder}\{e.Name!.Replace(".zip", "")}", true));

            LastIteroZipFileId = e.Name!.Replace(".zip", "").Replace("iTero_Export_", "");
            //success = true;
            //if (success)
            File.Delete(e.FullPath);

            Application.Current.Dispatcher.Invoke(new Action(async () =>
            {
                ShowNotificationMessage("iTero Case Downloaded", $"There is a new Itero case placed into Export folder! Id: {LastIteroZipFileId}", NotificationIcon.Success, false);
                AddEventToEventListLocalDB($"iTero Zip file downloaded: {LastIteroZipFileId}", "SteelBlue");
                ReadBackAllEvent();
                SystemSounds.Beep.Play();
                await BlinkWindow("green");
                ZipArchiveIconGrowAnimation();
                CountiTeroFolders();
            }));
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("end of central directory record", StringComparison.CurrentCultureIgnoreCase))
            {
                Application.Current.Dispatcher.Invoke(new Action(async () =>
                {
                    ShowNotificationMessage("iTero Case Download Issue", $"There is a new Itero case downloaded but the file is CORRUPT! Please download it again! Id: {LastIteroZipFileId}", NotificationIcon.Error, false);
                    AddEventToEventListLocalDB($"iTero Zip file issue: {LastIteroZipFileId}", "#F66D06");
                    ReadBackAllEvent();
                    SystemSounds.Beep.Play();
                    await BlinkWindow("red");
                }));
                return;
            }

            if (!ex.Message.Contains("because it is being used by another process", StringComparison.CurrentCultureIgnoreCase))
                AddDebugLine(ex);
        }
    }

    private void CountiTeroFolders()
    {
        if (Directory.Exists(PmIteroExportFolder))
        {
            int count = Directory.GetDirectories(PmIteroExportFolder).Length - 1;
            if (count >= 0)
                PmFilesCountIniTeroFolder = count;
            else
                PmFilesCountIniTeroFolder = 0;

        }
    }

    public void AddDebugLine(Exception? ex = null, string? message = null, string location = "MVM")
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        string lineNumber = "";
        if (ex is not null)
        {
            lineNumber = ex.LineNumber().ToString();
            message ??= ex.Message;
        }

        if (lineNumber == "-1")
            lineNumber = "";

        Application.Current.Dispatcher.Invoke(new Action(async () =>
        {
            //DebugMessages.Add(new DebugMessagesModel()
            //{
            //    DLocation = location,
            //    DLineNumber = lineNumber,
            //    DTime = time,
            //    DMessage = message,
            //});
            DebugMessages.Insert(0, new DebugMessagesModel()
            {
                DLocation = location,
                DLineNumber = lineNumber,
                DTime = time,
                DMessage = message,
            });
        }));
    }


    internal static void StartInitialTasks()
    {
        if (!bwInitialTasks.IsBusy)
        {
            bwInitialTasks.RunWorkerAsync();
        }
    }
    #endregion >> Initial Tasks at startup

    #region Looking for Update
    private async void LookForUpdate()
    {
        LookingForUpdateNow = true;
        Debug.WriteLine("Looking for update..");
        //Application.Current.Dispatcher.Invoke(new Action(() =>
        //{
        //    if (MainWindow.Instance is not null)
        //    {
        //        BeginStoryboard? sb = MainWindow.Instance.FindResource("ProgramIconShrinkAnimation")! as BeginStoryboard;
        //        sb!.Storyboard.Completed += ProgramIconShrinkAnimation_Completed;
        //        sb!.Storyboard.Begin();
        //    }
        //}));

        double remoteVersion = 0;
        try
        {
            string result = await new HttpClient().GetStringAsync("https://raw.githubusercontent.com/aml-one/StatsClient-2027/master/StatsClient/version.txt");
            _ = double.TryParse(result, out remoteVersion);
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                string remVersion = remoteVersion.ToString();
                LatestAppVersion = remVersion;
                if (DoAForceUpdateNow)
                    AddDebugLine(null, $"Current app version: {AppVersionDouble}, Last available version: {LatestAppVersion}");
            }));
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }


        if (remoteVersion > AppVersionDouble)
        {
#if DEBUG
            return;
#endif
            UpdateAvailable = true;

            if (StartAutoUpdateCuzAppJustStarted && (remoteVersion - AppVersionDouble) > 10)
                Application.Current.Dispatcher.Invoke(new Action(StartProgramUpdate));

            if (remoteVersion.ToString().EndsWith('0') || remoteVersion.ToString().EndsWith('5'))
                Application.Current.Dispatcher.Invoke(new Action(StartProgramUpdate));


            if (DoAForceUpdateNow)
                Application.Current.Dispatcher.Invoke(new Action(StartProgramUpdate));
        }
        else
        {
            UpdateAvailable = false;
            StartAutoUpdateCuzAppJustStarted = false;
        }

        LookingForUpdateNow = false;
    }

    private void ProgramIconShrinkAnimation_Completed(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (MainWindow.Instance is not null)
            {
                BeginStoryboard? sb = MainWindow.Instance.FindResource("ProgramIconGrowAnimation")! as BeginStoryboard;
                sb!.Storyboard.Completed += ProgramIconGrowAnimation_Completed;
                sb!.Storyboard.Begin();
            }
        }));
    }

    private void ProgramIconGrowAnimation_Completed(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (LookingForUpdateNow && MainWindow.Instance is not null)
            {
                BeginStoryboard? sb = MainWindow.Instance.FindResource("ProgramIconShrinkAnimation")! as BeginStoryboard;
                sb!.Storyboard.Completed += ProgramIconShrinkAnimation_Completed;
                sb!.Storyboard.Begin();
            }
        }));
    }


    private void ZipArchiveIconShrinkAnimation()
    {
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (MainWindow.Instance is not null)
            {
                BeginStoryboard? sb = MainWindow.Instance.FindResource("ZipArchiveIconShrinkAnimation")! as BeginStoryboard;
                sb!.Storyboard.Begin();
            }
        }));
    }

    private void ZipArchiveIconGrowAnimation_Completed(object? sender, EventArgs e)
    {
        ZipArchiveIconShowTimer.Start();
    }

    private void ZipArchiveIconGrowAnimation()
    {
        Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            if (MainWindow.Instance is not null)
            {
                BeginStoryboard? sb = MainWindow.Instance.FindResource("ZipArchiveIconGrowAnimation")! as BeginStoryboard;
                sb!.Storyboard.Completed += ZipArchiveIconGrowAnimation_Completed;
                sb!.Storyboard.Begin();
            }
        }));
    }



    private void HideZipArchiveIcon()
    {
        ZipArchiveIconShrinkAnimation();
        ZipArchiveIconShowTimer.Stop();
    }

    private void ZipArchiveIconShowTimer_Tick(object? sender, EventArgs e)
    {
        HideZipArchiveIcon();
    }

    private void UpdateCheckTimer_Tick(object? sender, EventArgs e)
    {
#if DEBUG
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
#endif
        LookForUpdate();
        UpdateCheckTimer.Interval = new TimeSpan(0, 5, 0);
    }


    private async void DownloadUpdater()
    {
        Thread.Sleep(500);
        try
        {
            if (File.Exists($@"{LocalConfigFolderHelper}StatsClientUpdater.exe"))
                File.Delete($@"{LocalConfigFolderHelper}StatsClientUpdater.exe");

            Thread.Sleep(500);
            if (!File.Exists($@"{LocalConfigFolderHelper}StatsClientUpdater.exe"))
            {
                using var client = new HttpClient();
                using var s = await client.GetStreamAsync("https://raw.githubusercontent.com/aml-one/StatsClient-2027/master/StatsClient/Executable/StatsClientUpdater.exe");
                using var fs = new FileStream($@"{LocalConfigFolderHelper}StatsClientUpdater.exe", FileMode.OpenOrCreate);
                await s.CopyToAsync(fs);
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }

        Thread.Sleep(3000);
    }

    private void StartUpdaterApp()
    {
#if DEBUG
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
#endif

        Thread.Sleep(3000);
        try
        {
            var p = new Process();

            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = $"/c \"{LocalConfigFolderHelper}StatsClientUpdater.exe\"";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            Thread.Sleep(2000);
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AddDebugLine(ex);
                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
                _MainWindow.Cursor = Cursors.Arrow;
            });
        }
    }
    #endregion Looking for Update


    private async void InitiateRestart()
    {
        try
        {
            if (Environment.ProcessPath is not null)
            {
                await ResetPingDifferenceInDatabaseOnClose();
                StartApplication(Environment.ProcessPath);
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    private async void LookForPendingTask()
    {
        TaskModel task = await GetPendingTaskFromDatabase();

        if (string.IsNullOrEmpty(task.Task))
            return;

        if (!string.IsNullOrEmpty(task.ComputerName) && !task.ComputerName.Equals(Environment.MachineName))
            return;

        if ((DateTime.Now - task.Time).TotalSeconds > 7200) // if the task is older than 2 hours then skip it..
            return;

        switch (task.Task.ToLower())
        {
            case "update":
                {
                    await WriteDownLastCommandId(task.Id!);
                    DoAForceUpdateNow = true;
                    LookForUpdate();
                    break;
                }

            case "report":
                {
                    await WriteDownLastCommandId(task.Id!);
                    await ReportClientLoginToDatabase();
                    break;
                }

            case "close":
                {
                    await WriteDownLastCommandId(task.Id!);
                    await ResetPingDifferenceInDatabaseOnClose();
                    Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                    break;
                }

            case "restart":
                {
                    await WriteDownLastCommandId(task.Id!);
                    InitiateRestart();
                    break;
                }
        }

    }

    public void RunMainMenuCommand(string selectedMenuItem)
    {
        switch (selectedMenuItem)
        {
            case "lookForUpdate":
                {
                    LookForUpdate();
                    break;
                }

            case "openManufFolder":
                {
                    OpenUpFolder("manufacturing");
                    break;
                }

            case "openTriosInbox":
                {
                    OpenUpFolder("triosinbox");
                    break;
                }

            case "openSmartRenameWindw":
                {
                    SmartOrderNamesWindow.ShowDialog();
                    break;
                }

            default: break;
        }
    }

    private void OpenUpFolder(string folderDescription)
    {
        string folder = "";

        switch (folderDescription)
        {
            case "manufacturing":
                folder = ThreeShapeDirectoryHelper.Replace(@"\3Shape Dental System Orders", "") + @"3Shape Dental System Manufacturing";
                break;

            case "triosinbox":
                folder = ThreeShapeDirectoryHelper + @"3ShapeCommunicate\Inbox";
                break;

            case "iteroFolder":
                folder = PmIteroExportFolder;
                break;
        }

        Debug.WriteLine(folder);

        try
        {
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                Process.Start("explorer.exe", "\"" + folder + "\"");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }
    }

    internal void FileWasDroppedToWindow(string[] files)
    {
        if (files.Length > 1)
        {
            ShowMessageBox("Too many files..", "Too many files at once!\nOnly drop one file at the time!\nSupported formats: STL, DCM", SMessageBoxButtons.Ok, NotificationIcon.Warning, 10, _MainWindow);
            return;
        }
        else
        {
            if (files[0].EndsWith(".stl", StringComparison.CurrentCultureIgnoreCase) || files[0].EndsWith(".dcm", StringComparison.CurrentCultureIgnoreCase))
            {
                FileRenameWindow frnWindow = new();
                frnWindow.Owner = _MainWindow;
                FileRenameViewModel.Instance.OriginalFilePath = files[0];
                frnWindow.Show();
            }
            else
                ShowMessageBox("Incompatible file", "Incompatible file!\nSupported formats: STL, DCM", SMessageBoxButtons.Ok, NotificationIcon.Error, 10, _MainWindow);

        }
    }



    private async void LookUpOrdersWithIssuesForDesigner()
    {
        string? designerName = "";
        if (SelectedDesignerPaymentSummary is not null)
            designerName = SelectedDesignerPaymentSummary.DesignerName;

        if (string.IsNullOrEmpty(designerName))
            return;

        await ListAllOrdersWithIssuesForSelectedDesigner(designerName);
    }

    private async Task ListAllOrdersWithIssuesForSelectedDesigner(string designerName)
    {
        PaymentCasesIssueListForDesigner = await GetAllCasesWithIssues(designerName);
    }
    private async void PaymentIssueSelected()
    {
        if (SelectedPaymentIssueForDesigner is null)
            return;

        MoveLabnextViewToPaymentIssueTab();

        PossibleOrdersFrom3ShapeForLabnextMatch.Clear();
        await Task.Delay(300);
        int i = 0;
        while (SelectedPaymentIssueForDesigner is null)
        {
            await Task.Delay(100);
            i++;
            if (i > 25)
                return;
        }

        ListOrdersTemporary(SelectedPaymentIssueForDesigner);

        //if (bwListCasesForPaymentIssueMatching.IsBusy != true)
        //    bwListCasesForPaymentIssueMatching.RunWorkerAsync(SelectedPaymentIssueForDesigner);
        //else
        //    bwListCasesForPaymentIssueMatching.CancelAsync();


        //PossibleOrdersFrom3ShapeForLabnextMatch = await GetPossibleOrderMatchesForLabnextIssueCaseFrom3Shape(SelectedPaymentIssueForDesigner);
        //PossibleOrdersFromArchivesForLabnextMatch = await GetPossibleOrderMatchesForLabnextIssueCaseFromArchives(SelectedPaymentIssueForDesigner);
    }

    private void ListCasesForPaymentIssueMatching_DoWork(object? sender, DoWorkEventArgs e)
    {
        AddDebugLine(null, "Stepped into BG worker");

        if (sender is not LabnextIssueModel model || sender is null)
            return;

        try
        {

            string searchQueryStr = $@"IntOrderID LIKE '{model.PanNumber}-%'";
            string connectionString = DatabaseConnection.ConnectionStrFor3Shape();
            string queryString = $@"SELECT TOP 10 IntOrderID, 
                                         Patient_FirstName, 
                                         Patient_LastName,
                                         o.ExtOrderID, 
                                         Items, 
                                         Customer, 
                                         MaxCreateDate,
								         MaxProcessStatusID
                                    FROM Orders o
                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
                                    WHERE {searchQueryStr}
                                    Order by MaxCreateDate DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string panNumber = DeterminePanNumber(reader["IntOrderID"].ToString()!, reader["Patient_LastName"].ToString()!, reader["Patient_FirstName"].ToString()!);

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    PossibleOrdersFrom3ShapeForLabnextMatch.Add(new ThreeShapeOrdersModel
                    {
                        IntOrderID = reader["IntOrderID"].ToString(),
                        Patient_FirstName = reader["Patient_FirstName"].ToString(),
                        Patient_LastName = reader["Patient_LastName"].ToString(),
                        PanNumber = panNumber,
                        MaxCreateDate = reader["MaxCreateDate"].ToString(),
                        ExtOrderID = reader["ExtOrderID"].ToString(),
                        Customer = reader["Customer"].ToString(),
                        Items = reader["Items"].ToString(),
                    });
                }));
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }


    }


    private void PanNumberClicked(object obj)
    {
        try
        {
            LabnextIssueModel model = obj as LabnextIssueModel;
            model.PanNumber = SelectedPaymentIssueForDesigner.PanNumber;
            ListOrdersTemporary(model);
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }
    }

    private void ListOrdersTemporary(LabnextIssueModel model)
    {

        //try to get customer name from Labnext
        if (!LabNextWebViewStatusText.Contains("/login") && model.PanNumber == 0)
        {
            LookingForPanNumberForPaymentIssue = true;
            Uri link = new(HttpUtility.UrlPathEncode($"{LabnextUrl}cases/case/id/{SelectedPaymentIssueForDesigner.LabnextID}"), UriKind.Absolute);

            _MainWindow.webviewLabnext.Source = link;
        }
        else
            LookingForPanNumberForPaymentIssue = false;

        try
        {
            string searchQueryStr = $@"IntOrderID LIKE '{model.PanNumber}-%'";

            if (SearchOnlyForSameDesigner)
                searchQueryStr += $" AND (o.ExtOrderID = '{SelectedPaymentIssueForDesigner!.DesignerName}' OR o.ExtOrderID LIKE '{SelectedPaymentIssueForDesigner!.DesignerName} %')";

            if (ShowCaseFromCloseDateRangeOnly)
            {

                string creationDateOfLabnextCase = "";
                string invoiceDateOfLabnextCase = "";

                if (model.CreationDate is not null)
                    creationDateOfLabnextCase = model.CreationDate.Replace("EST", "").Replace("EDT", "").Trim();
                if (model.InvoiceDate is not null)
                    invoiceDateOfLabnextCase = model.InvoiceDate.Replace("EST", "").Replace("EDT", "").Trim();

                //Dec 12, 2025 5:04 PM EST
                if (DateTime.TryParseExact(creationDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateCreate))
                {
                    bool invoiceDateParsed = false;
                    if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateInvoice))
                        invoiceDateParsed = true;

                    if (!invoiceDateParsed)
                        if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mmtt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateInvoice))
                            invoiceDateParsed = true;

                    string dateTill = "";

                    if (invoiceDateParsed)
                    {
                        if (dateCreate.AddDays(+15) < dateInvoice)
                            dateTill = $"{dateInvoice.AddDays(+5).ToString("yyyy-MM-dd HH:mm:ss")}.000";
                        else
                            dateTill = $"{dateCreate.AddDays(+15).ToString("yyyy-MM-dd HH:mm:ss")}.000";
                    }
                    else
                        dateTill = $"{dateCreate.AddDays(+15).ToString("yyyy-MM-dd HH:mm:ss")}.000";

                    //2025-09-19 18:05:16.000
                    string dateFrom = $"{dateCreate.AddDays(-15).ToString("yyyy-MM-dd HH:mm:ss")}.000";

                    searchQueryStr += $" AND (MaxCreateDate > '{dateFrom}' AND MaxCreateDate < '{dateTill}')";
                }
                else
                {
                    AddDebugLine(null, $"#1: Could not parse creation date of Labnext case! Creation date string: {model.CreationDate}");
                    ShowCaseFromCloseDateRangeOnly = false;
                }
            }



            string connectionString = DatabaseConnection.ConnectionStrFor3Shape();
            string queryString = $@"SELECT TOP 10 IntOrderID, 
                                         Patient_FirstName, 
                                         Patient_LastName,
                                         o.ExtOrderID, 
                                         Items, 
                                         Customer, 
                                         ScanSource,
                                         MaxCreateDate,
								         MaxProcessStatusID
                                    FROM Orders o
                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
                                    WHERE {searchQueryStr}
                                    Order by MaxCreateDate DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string panNumber = DeterminePanNumber(reader["IntOrderID"].ToString()!, reader["Patient_LastName"].ToString()!, reader["Patient_FirstName"].ToString()!);
                string CaseStatus = CaseStatusSelect(reader["MaxProcessStatusID"].ToString()!, reader["ScanSource"].ToString()!, "plReady");
                string ImageSource = @"\Images\ListViewIcons\" + IconSelect(reader["MaxProcessStatusID"].ToString()!, reader["ScanSource"].ToString()!, "plReady") + ".png";
                string PanColorName = "Green";
                string PanColor = "#068506";


                string createDateFriendly = reader["MaxCreateDate"].ToString()!;

                if (DateTime.TryParse(reader["MaxCreateDate"].ToString(), out DateTime createDate))
                    createDateFriendly = createDate.ToString("MMM d, yyyy");

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (SelectedPaymentIssueForDesigner is not null && SelectedPaymentIssueForDesigner.TeethNumbers is not null)
                        if ((CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers}" ||
                             CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers.Replace("-", ",")}") && panNumber == SelectedPaymentIssueForDesigner.PanNumber.ToString())
                        {
                            PanColorName = "Orange";
                            PanColor = "#F46900";
                        }


                    if (!PossibleOrdersFrom3ShapeForLabnextMatch.Contains(PossibleOrdersFrom3ShapeForLabnextMatch.FirstOrDefault(x => x.IntOrderID == reader["IntOrderID"].ToString())!))
                        PossibleOrdersFrom3ShapeForLabnextMatch.Add(new ThreeShapeOrdersModel
                        {
                            IntOrderID = reader["IntOrderID"].ToString(),
                            Patient_FirstName = RemoveNumbers().Replace(reader["Patient_FirstName"].ToString()!, ""),
                            Patient_LastName = RemoveNumbers().Replace(reader["Patient_LastName"].ToString()!, ""),
                            PanNumber = panNumber,
                            MaxCreateDate = reader["MaxCreateDate"].ToString(),
                            MaxCreateDateFriendly = createDateFriendly,
                            ExtOrderID = reader["ExtOrderID"].ToString(),
                            Customer = reader["Customer"].ToString(),
                            Items = CleanLettersFromItems(reader["Items"].ToString()),
                            PanColor = PanColor,
                            PanColorName = PanColorName,
                            ImageSource = ImageSource,
                            CaseStatus = CaseStatus,
                        });
                }));
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }

        // SHOW OTHER DESIGNER CASES TOO, or show only the same designer as we looking for now
        // checkbox opcioval

        try
        {
            string? Patient_LastName = model.Patient_LastName;
            string? Patient_FirstName = model.Patient_FirstName;

            if (string.IsNullOrEmpty(Patient_LastName) || Patient_LastName.Length < 2)
                Patient_LastName = "------------";

            if (string.IsNullOrEmpty(Patient_FirstName) || Patient_FirstName.Length < 2)
                Patient_FirstName = "------------";


            string searchQueryStr = $@"(Patient_LastName LIKE '%{Patient_LastName}%' OR 
                                       Patient_FirstName LIKE '%{Patient_LastName}%' OR
                                       Patient_FirstName LIKE '%{Patient_FirstName}%' OR
                                       Patient_LastName LIKE '%{Patient_FirstName}%')
                                    ";

            if (SearchOnlyForSameDesigner)
                searchQueryStr += $" AND (o.ExtOrderID = '{SelectedPaymentIssueForDesigner!.DesignerName}' OR o.ExtOrderID LIKE '{SelectedPaymentIssueForDesigner!.DesignerName} %')";

            if (ShowCaseFromCloseDateRangeOnly)
            {

                string creationDateOfLabnextCase = "";
                string invoiceDateOfLabnextCase = "";

                if (model.CreationDate is not null)
                    creationDateOfLabnextCase = model.CreationDate.Replace("EST", "").Replace("EDT", "").Trim();
                if (model.InvoiceDate is not null)
                    invoiceDateOfLabnextCase = model.InvoiceDate.Replace("EST", "").Replace("EDT", "").Trim();

                //Dec 12, 2025 5:04 PM EST
                if (DateTime.TryParseExact(creationDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateCreate))
                {
                    bool invoiceDateParsed = false;
                    if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateInvoice))
                        invoiceDateParsed = true;

                    if (!invoiceDateParsed)
                        if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mmtt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateInvoice))
                            invoiceDateParsed = true;

                    string dateTill = "";

                    if (invoiceDateParsed)
                    {
                        if (dateCreate.AddDays(+15) < dateInvoice)
                            dateTill = $"{dateInvoice.AddDays(+5).ToString("yyyy-MM-dd HH:mm:ss")}.000";
                        else
                            dateTill = $"{dateCreate.AddDays(+15).ToString("yyyy-MM-dd HH:mm:ss")}.000";
                    }
                    else
                        dateTill = $"{dateCreate.AddDays(+15).ToString("yyyy-MM-dd HH:mm:ss")}.000";

                    //2025-09-19 18:05:16.000
                    string dateFrom = $"{dateCreate.AddDays(-15).ToString("yyyy-MM-dd HH:mm:ss")}.000";

                    searchQueryStr += $" AND (MaxCreateDate > '{dateFrom}' AND MaxCreateDate < '{dateTill}')";
                }
                else
                {
                    AddDebugLine(null, $"#2: Could not parse creation date of Labnext case! Creation date string: {model.CreationDate}");
                    ShowCaseFromCloseDateRangeOnly = false;
                }
            }

            string connectionString = DatabaseConnection.ConnectionStrFor3Shape();
            string queryString = $@"SELECT TOP 10 IntOrderID, 
                                         Patient_FirstName, 
                                         Patient_LastName,
                                         o.ExtOrderID, 
                                         Items, 
                                         Customer, 
                                         ScanSource,
                                         MaxCreateDate,
								         MaxProcessStatusID
                                    FROM Orders o
                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
                                    WHERE {searchQueryStr}
                                    Order by MaxCreateDate DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string panNumber = DeterminePanNumber(reader["IntOrderID"].ToString()!, reader["Patient_LastName"].ToString()!, reader["Patient_FirstName"].ToString()!);
                string CaseStatus = CaseStatusSelect(reader["MaxProcessStatusID"].ToString()!, reader["ScanSource"].ToString()!, "plReady");
                string ImageSource = @"\Images\ListViewIcons\" + IconSelect(reader["MaxProcessStatusID"].ToString()!, reader["ScanSource"].ToString()!, "plReady") + ".png";
                string PanColorName = "Purple";
                string PanColor = "#8D088D";

                string createDateFriendly = reader["MaxCreateDate"].ToString()!;

                if (DateTime.TryParse(reader["MaxCreateDate"].ToString(), out DateTime createDate))
                    createDateFriendly = createDate.ToString("MMM d, yyyy");

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (SelectedPaymentIssueForDesigner is not null && SelectedPaymentIssueForDesigner.TeethNumbers is not null)
                        if ((CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers}" ||
                             CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers.Replace("-", ",")}") && panNumber == SelectedPaymentIssueForDesigner.PanNumber.ToString())
                        {
                            PanColorName = "Orange";
                            PanColor = "#F46900";
                        }

                    if (!PossibleOrdersFrom3ShapeForLabnextMatch.Contains(PossibleOrdersFrom3ShapeForLabnextMatch.FirstOrDefault(x => x.IntOrderID == reader["IntOrderID"].ToString())!))
                        PossibleOrdersFrom3ShapeForLabnextMatch.Add(new ThreeShapeOrdersModel
                        {
                            IntOrderID = reader["IntOrderID"].ToString(),
                            Patient_FirstName = RemoveNumbers().Replace(reader["Patient_FirstName"].ToString()!, ""),
                            Patient_LastName = RemoveNumbers().Replace(reader["Patient_LastName"].ToString()!, ""),
                            PanNumber = panNumber,
                            MaxCreateDate = reader["MaxCreateDate"].ToString(),
                            MaxCreateDateFriendly = createDateFriendly,
                            ExtOrderID = reader["ExtOrderID"].ToString(),
                            Customer = reader["Customer"].ToString(),
                            Items = CleanLettersFromItems(reader["Items"].ToString()),
                            PanColor = PanColor,
                            PanColorName = PanColorName,
                            ImageSource = ImageSource,
                            CaseStatus = CaseStatus,
                        });
                }));
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }


        // Check In Archives

        try
        {
            string searchQueryStr = $@"OrderID LIKE '{model.PanNumber}-%'";

            if (SearchOnlyForSameDesigner)
                searchQueryStr += $" AND (DesignerName = '{SelectedPaymentIssueForDesigner!.DesignerName}' OR DesignerName LIKE '{SelectedPaymentIssueForDesigner!.DesignerName} %')";

            if (ShowCaseFromCloseDateRangeOnly)
            {
                string creationDateOfLabnextCase = "";
                string invoiceDateOfLabnextCase = "";

                if (model.CreationDate is not null)
                    creationDateOfLabnextCase = model.CreationDate.Replace("EST", "").Replace("EDT", "").Trim();
                if (model.InvoiceDate is not null)
                    invoiceDateOfLabnextCase = model.InvoiceDate.Replace("EST", "").Replace("EDT", "").Trim();

                //Dec 12, 2025 5:04 PM EST
                if (DateTime.TryParseExact(creationDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateCreate))
                {
                    bool invoiceDateParsed = false;
                    if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateInvoice))
                        invoiceDateParsed = true;

                    if (!invoiceDateParsed)
                        if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mmtt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateInvoice))
                            invoiceDateParsed = true;

                    string dateTill = "";

                    if (invoiceDateParsed)
                    {
                        if (dateCreate.AddDays(+15) < dateInvoice)
                            dateTill = GetUnixTimeStampFromDate(dateInvoice.AddDays(+5));
                        else
                            dateTill = GetUnixTimeStampFromDate(dateCreate.AddDays(+15));
                    }
                    else
                        dateTill = GetUnixTimeStampFromDate(dateCreate.AddDays(+15));

                    //2025-09-19 18:05:16.000
                    string dateFrom = GetUnixTimeStampFromDate(dateCreate.AddDays(-30));

                    searchQueryStr += $" AND (CreateDate > '{dateFrom}' AND CreateDate < '{dateTill}')";
                }
                else
                {
                    AddDebugLine(null, $"#3: Could not parse creation date of Labnext case! Creation date string: {model.CreationDate}");
                    ShowCaseFromCloseDateRangeOnly = false;
                }
            }



            string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();
            string queryString = $@"SELECT TOP 8 OrderID, 
                                         Patient_FirstName, 
                                         Patient_LastName,
                                         DesignerName, 
                                         Items, 
                                         Customer, 
                                         ScanSource,
                                         CreateDate, 
                                         PanNumber
                                    FROM Archives
                                    WHERE {searchQueryStr}
                                    Order by CreateDate DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string panNumber = reader["PanNumber"].ToString()!;

                string ImageSource = @"\Images\HomeButtons\archives.png";
                string PanColorName = "SteelBlue";
                string PanColor = "#4884B6";

                string createDateFriendly = UnixTimeStampToDateTime(reader["CreateDate"].ToString()!, false, true);

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (SelectedPaymentIssueForDesigner is not null && SelectedPaymentIssueForDesigner.TeethNumbers is not null)
                        if ((CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers}" ||
                             CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers.Replace("-", ",")}") && panNumber == SelectedPaymentIssueForDesigner.PanNumber.ToString())
                        {
                            PanColorName = "Orange";
                            PanColor = "#F46900";
                        }

                    if (!PossibleOrdersFrom3ShapeForLabnextMatch.Contains(PossibleOrdersFrom3ShapeForLabnextMatch.FirstOrDefault(x => x.IntOrderID == reader["OrderID"].ToString())!))
                        PossibleOrdersFrom3ShapeForLabnextMatch.Add(new ThreeShapeOrdersModel
                        {
                            IntOrderID = reader["OrderID"].ToString(),
                            Patient_FirstName = RemoveNumbers().Replace(reader["Patient_FirstName"].ToString()!, ""),
                            Patient_LastName = RemoveNumbers().Replace(reader["Patient_LastName"].ToString()!, ""),
                            PanNumber = panNumber,
                            MaxCreateDate = UnixTimeStampToDateTime(reader["CreateDate"].ToString()!),
                            MaxCreateDateFriendly = createDateFriendly,
                            ExtOrderID = reader["DesignerName"].ToString(),
                            Customer = reader["Customer"].ToString(),
                            Items = CleanLettersFromItems(reader["Items"].ToString()),
                            PanColor = PanColor,
                            PanColorName = PanColorName,
                            ImageSource = ImageSource,
                        });
                }));
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }


        try
        {
            string? Patient_LastName = model.Patient_LastName;
            string? Patient_FirstName = model.Patient_FirstName;

            if (string.IsNullOrEmpty(Patient_LastName) || Patient_LastName.Length < 2)
                Patient_LastName = "------------";

            if (string.IsNullOrEmpty(Patient_FirstName) || Patient_FirstName.Length < 2)
                Patient_FirstName = "------------";


            string searchQueryStr = $@"(Patient_LastName LIKE '%{Patient_LastName}%' OR 
                                       Patient_FirstName LIKE '%{Patient_LastName}%' OR
                                       Patient_FirstName LIKE '%{Patient_FirstName}%' OR
                                       Patient_LastName LIKE '%{Patient_FirstName}%')
                                    ";

            if (SearchOnlyForSameDesigner)
                searchQueryStr += $" AND (DesignerName = '{SelectedPaymentIssueForDesigner!.DesignerName}' OR DesignerName LIKE '{SelectedPaymentIssueForDesigner!.DesignerName} %')";

            if (ShowCaseFromCloseDateRangeOnly)
            {

                string creationDateOfLabnextCase = "";
                string invoiceDateOfLabnextCase = "";

                if (model.CreationDate is not null)
                    creationDateOfLabnextCase = model.CreationDate.Replace("EST", "").Replace("EDT", "").Trim();
                if (model.InvoiceDate is not null)
                    invoiceDateOfLabnextCase = model.InvoiceDate.Replace("EST", "").Replace("EDT", "").Trim();

                //Dec 12, 2025 5:04 PM EST
                if (DateTime.TryParseExact(creationDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateCreate))
                {
                    bool invoiceDateParsed = false;
                    if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dateInvoice))
                        invoiceDateParsed = true;

                    if (!invoiceDateParsed)
                        if (DateTime.TryParseExact(invoiceDateOfLabnextCase, "MMM d, yyyy h:mmtt", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateInvoice))
                            invoiceDateParsed = true;

                    string dateTill = "";

                    if (invoiceDateParsed)
                    {
                        if (dateCreate.AddDays(+15) < dateInvoice)
                            dateTill = GetUnixTimeStampFromDate(dateInvoice.AddDays(+5));
                        else
                            dateTill = GetUnixTimeStampFromDate(dateCreate.AddDays(+15));
                    }
                    else
                        dateTill = GetUnixTimeStampFromDate(dateCreate.AddDays(+15));

                    //2025-09-19 18:05:16.000
                    string dateFrom = GetUnixTimeStampFromDate(dateCreate.AddDays(-30));

                    searchQueryStr += $" AND (CreateDate > '{dateFrom}' AND CreateDate < '{dateTill}')";
                }
                else
                {
                    AddDebugLine(null, $"#4: Could not parse creation date of Labnext case! Creation date string: {model.CreationDate}");
                    ShowCaseFromCloseDateRangeOnly = false;
                }
            }



            string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();
            string queryString = $@"SELECT TOP 8 OrderID, 
                                         Patient_FirstName, 
                                         Patient_LastName,
                                         DesignerName, 
                                         Items, 
                                         Customer, 
                                         ScanSource,
                                         CreateDate, 
                                         PanNumber
                                    FROM Archives
                                    WHERE {searchQueryStr}
                                    Order by CreateDate DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string panNumber = reader["PanNumber"].ToString()!;

                string ImageSource = @"\Images\HomeButtons\archives.png";
                string PanColorName = "BlueViolet";
                string PanColor = "#902DEC";

                string createDateFriendly = UnixTimeStampToDateTime(reader["CreateDate"].ToString()!, false, true);

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (SelectedPaymentIssueForDesigner is not null && SelectedPaymentIssueForDesigner.TeethNumbers is not null)
                        if ((CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers}" ||
                             CleanLettersFromItems(reader["Items"].ToString()) == $"#{SelectedPaymentIssueForDesigner.TeethNumbers.Replace("-", ",")}") && panNumber == SelectedPaymentIssueForDesigner.PanNumber.ToString())
                        {
                            PanColorName = "Orange";
                            PanColor = "#F46900";
                        }

                    if (!PossibleOrdersFrom3ShapeForLabnextMatch.Contains(PossibleOrdersFrom3ShapeForLabnextMatch.FirstOrDefault(x => x.IntOrderID == reader["OrderID"].ToString())!))
                        PossibleOrdersFrom3ShapeForLabnextMatch.Add(new ThreeShapeOrdersModel
                        {
                            IntOrderID = reader["OrderID"].ToString(),
                            Patient_FirstName = RemoveNumbers().Replace(reader["Patient_FirstName"].ToString()!, ""),
                            Patient_LastName = RemoveNumbers().Replace(reader["Patient_LastName"].ToString()!, ""),
                            PanNumber = panNumber,
                            MaxCreateDate = UnixTimeStampToDateTime(reader["CreateDate"].ToString()!),
                            MaxCreateDateFriendly = createDateFriendly,
                            ExtOrderID = reader["DesignerName"].ToString(),
                            Customer = reader["Customer"].ToString(),
                            Items = CleanLettersFromItems(reader["Items"].ToString()),
                            PanColor = PanColor,
                            PanColorName = PanColorName,
                            ImageSource = ImageSource,
                        });
                }));
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex, ex.Message);
        }
    }

    private static string CleanLettersFromItems(string? items)
    {
        if (items is not null)
        {
            items = KeepOnlyNumeric().Replace(items, "").Replace(",,", ",").Trim();

            string[] parts = items.Split(',');
            List<string> list = [];

            foreach (string item in parts)
            {
                if (!list.Contains(item))
                    list.Add(item);
            }

            items = "#";
            foreach (string item in list)
            {
                items += item + ",";
            }

            if (items[1] == ',')
                items = string.Concat("#", items.AsSpan(2));

            if (items.EndsWith(','))
                items = items[..^1];
            return items;
        }
        else
            return items;
    }

    private void ListCasesForPaymentIssueMatching_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        AddDebugLine(null, "Finished BG worker");
    }


    public async void AssignOrderToLabnextCase(object obj)
    {
        if (obj is not ThreeShapeOrdersModel model) return;

        //confirmation window
        SMessageBoxResult result = ShowMessageBox("OrderID assignment", $"Are you sure this is the correct 3Shape Order?\n{model.IntOrderID}", SMessageBoxButtons.YesNo, NotificationIcon.Question, 10, _MainWindow);

        if (result == SMessageBoxResult.Yes)
        {
            if (await AssignOrderIDToLabnextIssueCase(SelectedPaymentIssueForDesigner!.LabnextID, model.IntOrderID))
            {
                ShowNotificationMessage("Success!", $"OrderID {model.IntOrderID} successfully assigned to Labnext case!", NotificationIcon.Success);
                UpdateLabnextIssueLists();
            }
            else
                ShowNotificationMessage("Error!", $"This OrderID is already assigned to another Labnext case! Please check.", NotificationIcon.Error);
        }
    }

    private async void UpdateLabnextIssueLists()
    {
        SearchOnlyForSameDesigner = true;
        ShowCaseFromCloseDateRangeOnly = true;

        DesignerPaymentSummary model = SelectedDesignerPaymentSummary!;
        PossibleOrdersFrom3ShapeForLabnextMatch.Clear();
        SelectedPaymentIssueForDesigner = new();
        DesignerPaymentSummaryList = await GetDesignerPaymentSummaryFromDB();

        if (PaymentCasesIssueListForDesigner.Count > 0)
        {
            if (SelectedDesignerPaymentSummary is not null)
                if (SelectedDesignerPaymentSummary.DesignerName is not null)
                    PaymentCasesIssueListForDesigner = await GetAllCasesWithIssues(SelectedDesignerPaymentSummary.DesignerName);

            //SelectedPaymentIssueForDesigner = PaymentCasesIssueListForDesigner.FirstOrDefault();
            _MainWindow.paymentIssueList.SelectedIndex = 0;
        }
        else
        {
            ClearSelectedDesignerNameAtIssues();
        }
        model.PaymentIssues = PaymentCasesIssueListForDesigner.Count;
        //model.PaymentIssues--;
        SelectedDesignerPaymentSummary = model;
        PaymentIssueCount = await GetPaymentIssueCountFromDB();
        PaidToWrongPersonOrdersList = await GetPaidToWrongPersonsOrdersListFromDB();
        if (PaymentCasesIssueListForDesigner.Count > 0)
        {
            SelectedPaymentIssueForDesigner = PaymentCasesIssueListForDesigner.FirstOrDefault();
            _MainWindow.paymentIssueList.SelectedIndex = 0;
        }


        if (PaymentCasesIssueListForDesigner.Count == 0)
            ClearSelectedDesignerNameAtIssues();

        FoundPanNumberSx = 0;
    }

    private async Task<bool> AssignOrderIDToLabnextIssueCase(int labnextID, string? intOrderID)
    {
        if (intOrderID is null)
            return false;

        if (await GetOrderIDAssignedToPaymentIssue(labnextID, intOrderID))
        {
            await AddOrUpdateLabnextManualPair(intOrderID, labnextID.ToString());
            await RemovePaymentIssueFromPaymentIssuesTable(labnextID.ToString());
            return true;
        }
        return false;
    }
    private static string DeterminePanNumber(string OrderID, string Patient_LastName, string Patient_FirstName)
    {
        string panNumber = "";
        List<string> orderIDDarabolt = [];
        orderIDDarabolt = [.. OrderID.Split('-')];

        bool foundPanNumber = int.TryParse(orderIDDarabolt[0].ToString(), out int panNr);

        if (foundPanNumber)
        {
            panNumber = panNr.ToString();
        }
        else
        {
            // checking if we can find any pan number in the patient name section

            List<string> orderIDHelprFromPtNameDarabolt = [];
            orderIDHelprFromPtNameDarabolt = [.. Patient_LastName.Split('-')];
            bool foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out int panNr2);
            if (foundPanNumber2)
            {
                panNumber = panNr2.ToString();
            }
            else
            {
                orderIDHelprFromPtNameDarabolt = [];
                orderIDHelprFromPtNameDarabolt = [.. Patient_FirstName.Split('-')];
                panNr2 = 0;
                foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out panNr2);

                if (foundPanNumber2)
                    panNumber = panNr2.ToString();
                else
                    panNumber = "";
            }
        }

        return panNumber;
    }

    private static int DeterminePanNumberToInt(string OrderID, string Patient_LastName, string Patient_FirstName)
    {
        int panNumber = 0;
        List<string> orderIDDarabolt = [];
        orderIDDarabolt = [.. OrderID.Split('-')];

        bool foundPanNumber = int.TryParse(orderIDDarabolt[0].ToString(), out int panNr);

        if (foundPanNumber)
        {
            panNumber = panNr;
        }
        else
        {
            // checking if we can find any pan number in the patient name section

            List<string> orderIDHelprFromPtNameDarabolt = [];
            orderIDHelprFromPtNameDarabolt = [.. Patient_LastName.Split('-')];
            bool foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out int panNr2);
            if (foundPanNumber2)
            {
                panNumber = panNr2;
            }
            else
            {
                orderIDHelprFromPtNameDarabolt = [];
                orderIDHelprFromPtNameDarabolt = [.. Patient_FirstName.Split('-')];
                panNr2 = 0;
                foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out panNr2);

                if (foundPanNumber2)
                    panNumber = panNr2;
                else
                    panNumber = 0;
            }
        }

        return panNumber;
    }



    [GeneratedRegex(@"[\d-]")]
    private static partial Regex RemoveNumbers();

    [GeneratedRegex(@"^[0-9]+-$")]
    private static partial Regex PanNumberRegex();

    [GeneratedRegex("[^0-9.+-+,+-]")]
    private static partial Regex RemoveNumbersAndDash();

    [GeneratedRegex("[^0-9.,-]")]
    private static partial Regex KeepOnlyNumeric();
}
