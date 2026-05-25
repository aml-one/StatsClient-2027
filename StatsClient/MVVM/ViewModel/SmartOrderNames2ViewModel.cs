using Microsoft.Data.SqlClient;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.ViewModel.MainViewModel;



namespace StatsClient.MVVM.ViewModel;

public partial class SmartOrderNames2ViewModel : ObservableObject
{
    private static SmartOrderNames2ViewModel? staticInstance;
    public static SmartOrderNames2ViewModel StaticInstance
    {
        get => staticInstance!;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    private ObservableCollection<ThreeShapeOrdersModel> newOrdersByMe = [];
    public ObservableCollection<ThreeShapeOrdersModel> NewOrdersByMe
    {
        get => newOrdersByMe;
        set
        {
            if (value != newOrdersByMe)
            {
                newOrdersByMe = value;
                RaisePropertyChanged(nameof(NewOrdersByMe));
                if (NewOrdersByMe.Count == 0 && PreviouslySelectedOrder is not null)
                {
                    ResetNameForm();
                }
            }
        }
    }

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

    private readonly List<string> digitalSystems = [
        "ABUTMENT-ONLY",
        "ATLANTIS",
        "DROPBOX",
        "DSCORE",
        "EMAIL",
        "ASCONNECT",
        "CARESTREAM",
        "DEXIS",
        "-- None --",
        "MEDIT",
        "IS3D",
        "SIRONA",
        "ITERO",
        "TRIOS",
        "HENNESSY"
        ];
    public List<string> DigitalSystems
    {
        get => digitalSystems;
    }

    private string logMessage = "";
    public string LogMessage
    {
        get => logMessage!;
        set
        {
            logMessage = value;
            RaisePropertyChanged(nameof(LogMessage));
        }
    }

    private List<string> logMessages = [];
    public List<string> LogMessages
    {
        get => logMessages!;
        set
        {
            logMessages = value;
            RaisePropertyChanged(nameof(LogMessages));
        }
    }

    private List<string> customerSuggestionsList = [];
    public List<string> CustomerSuggestionsList
    {
        get => customerSuggestionsList!;
        set
        {
            customerSuggestionsList = value;
            RaisePropertyChanged(nameof(CustomerSuggestionsList));
        }
    }

    private string selectedCustomerName;
    public string SelectedCustomerName
    {
        get => selectedCustomerName;
        set
        {
            selectedCustomerName = value;
            RaisePropertyChanged(nameof(SelectedCustomerName));
            BuildName();
            FocusOnRenameButton();
        }
    }

    private string panNumber;
    public string PanNumber
    {
        get => panNumber;
        set
        {
            panNumber = value;
            RaisePropertyChanged(nameof(PanNumber));
        }
    }

    private string aSConnectID;
    public string ASConnectID
    {
        get => aSConnectID;
        set
        {
            aSConnectID = value;
            RaisePropertyChanged(nameof(ASConnectID));
        }
    }

    private string carestreamID = "";
    public string CarestreamID
    {
        get => carestreamID;
        set
        {
            carestreamID = value;
            RaisePropertyChanged(nameof(CarestreamID));
        }
    }

    private bool namingCustomerFirst = false;
    public bool NamingCustomerFirst
    {
        get => namingCustomerFirst;
        set
        {
            namingCustomerFirst = value;
            RaisePropertyChanged(nameof(NamingCustomerFirst));
        }
    }

    private bool autoSelectFirstOrder = true;
    public bool AutoSelectFirstOrder
    {
        get => autoSelectFirstOrder;
        set
        {
            autoSelectFirstOrder = value;
            RaisePropertyChanged(nameof(AutoSelectFirstOrder));
        }
    }

    private bool smartOrderNamesModuleIsActive = true;
    public bool SmartOrderNamesModuleIsActive
    {
        get => smartOrderNamesModuleIsActive;
        set
        {
            smartOrderNamesModuleIsActive = value;
            RaisePropertyChanged(nameof(SmartOrderNamesModuleIsActive));
        }
    }

    private bool namingCustomerLast = true;
    public bool NamingCustomerLast
    {
        get => namingCustomerLast;
        set
        {
            namingCustomerLast = value;
            RaisePropertyChanged(nameof(NamingCustomerLast));
        }
    }

    private bool isScrewRetained = false;
    public bool IsScrewRetained
    {
        get => isScrewRetained;
        set
        {
            isScrewRetained = value;
            RaisePropertyChanged(nameof(IsScrewRetained));
        }
    }

    private bool showCasesWithoutNumber = true;
    public bool ShowCasesWithoutNumber
    {
        get => showCasesWithoutNumber;
        set
        {
            showCasesWithoutNumber = value;
            RaisePropertyChanged(nameof(ShowCasesWithoutNumber));
        }
    }

    

    private string selectedDigitalSystem = "";
    public string SelectedDigitalSystem
    {
        get => selectedDigitalSystem;
        set
        {
            selectedDigitalSystem = value;
            RaisePropertyChanged(nameof(SelectedDigitalSystem));
            FocusOnRenameButton();
        }
    }

    private string originalOrderID = "";
    public string OriginalOrderID
    {
        get => originalOrderID!;
        set
        {
            originalOrderID = value;
            RaisePropertyChanged(nameof(OriginalOrderID));
        }
    }

    private string selectedShade = "";
    public string SelectedShade
    {
        get => selectedShade;
        set
        {
            selectedShade = value;
            RaisePropertyChanged(nameof(SelectedShade));
        }
    }

    private string orderNamePreview = "";
    public string OrderNamePreview
    {
        get => orderNamePreview;
        set
        {
            orderNamePreview = value;
            RaisePropertyChanged(nameof(OrderNamePreview));
        }
    }

    private ThreeShapeOrdersModel? selectedOrder;
    public ThreeShapeOrdersModel? SelectedOrder
    {
        get => selectedOrder;
        set
        {
            if (value is not null)
            {
                selectedOrder = value;

                RaisePropertyChanged(nameof(SelectedOrder));
                PreviouslySelectedOrder = value;
                FocusOnPanNumberBox();

                PanNumber = "";
                CarestreamID = "";
                IsScrewRetained = false;
                SelectedDigitalSystem = "-- None --";
                SelectedShade = "";
                OrderNamePreview = string.Empty;
                CustomerSuggestionsList = [];
                
            }
        }
    }


    private ThreeShapeOrdersModel? previouslySelectedOrder;
    public ThreeShapeOrdersModel? PreviouslySelectedOrder
    {
        get => previouslySelectedOrder;
        set
        {
            previouslySelectedOrder = value;
            RaisePropertyChanged(nameof(PreviouslySelectedOrder));
        }
    }

    private bool firstOrderSelected = false;
    public bool FirstOrderSelected
    {
        get => firstOrderSelected!;
        set
        {
            firstOrderSelected = value;
            RaisePropertyChanged(nameof(FirstOrderSelected));
        }
    }

    private bool jumpToSmartOrderNamesTabWhenNewOrder = false;
    public bool JumpToSmartOrderNamesTabWhenNewOrder
    {
        get => jumpToSmartOrderNamesTabWhenNewOrder!;
        set
        {
            jumpToSmartOrderNamesTabWhenNewOrder = value;
            RaisePropertyChanged(nameof(JumpToSmartOrderNamesTabWhenNewOrder));
        }
    }

    private string threeShapeDirectoryHelper = "";
    public string ThreeShapeDirectoryHelper
    {
        get => threeShapeDirectoryHelper!;
        set
        {
            threeShapeDirectoryHelper = value;
            RaisePropertyChanged(nameof(ThreeShapeDirectoryHelper));
        }
    }

    private string toothNumbersString = "";
    public string ToothNumbersString
    {
        get => toothNumbersString!;
        set
        {
            toothNumbersString = value;
            RaisePropertyChanged(nameof(ToothNumbersString));
        }
    }

    private string windowTitle = "Smart Order Names";
    public string WindowTitle
    {
        get => windowTitle!;
        set
        {
            windowTitle = value;
            RaisePropertyChanged(nameof(WindowTitle));
        }
    }

    private string customerName = "";
    public string CustomerName
    {
        get => customerName!;
        set
        {
            customerName = value;
            RaisePropertyChanged(nameof(CustomerName));
        }
    }

    private string lastPanNumber = "";
    public string LastPanNumber
    {
        get => lastPanNumber!;
        set
        {
            lastPanNumber = value;
            RaisePropertyChanged(nameof(LastPanNumber));
        }
    }

    private bool dontUseLastPanNumber = false;
    public bool DontUseLastPanNumber
    {
        get => dontUseLastPanNumber!;
        set
        {
            dontUseLastPanNumber = value;
            RaisePropertyChanged(nameof(DontUseLastPanNumber));
        }
    }

    private string lastPanNumberOnMainWindow = "";
    public string LastPanNumberOnMainWindow
    {
        get => lastPanNumberOnMainWindow!;
        set
        {
            lastPanNumberOnMainWindow = value;
            RaisePropertyChanged(nameof(LastPanNumberOnMainWindow));
        }
    }

    private double ageOfLastPanNumber = 0;
    public double AgeOfLastPanNumber
    {
        get => ageOfLastPanNumber!;
        set
        {
            ageOfLastPanNumber = value;
            RaisePropertyChanged(nameof(AgeOfLastPanNumber));
        }
    }
    
    private DateTime lastPanNumbersDate = DateTime.Now;
    public DateTime LastPanNumbersDate
    {
        get => lastPanNumbersDate!;
        set
        {
            lastPanNumbersDate = value;
            RaisePropertyChanged(nameof(LastPanNumbersDate));
        }
    }

    private double ageOfASConnectID = 0;
    public double AgeOfASConnectID
    {
        get => ageOfASConnectID!;
        set
        {
            ageOfASConnectID = value;
            RaisePropertyChanged(nameof(AgeOfASConnectID));
        }
    }

    private DateTime lastASConnectIDDate = DateTime.Now;
    public DateTime LastASConnectIDDate
    {
        get => lastASConnectIDDate!;
        set
        {
            lastASConnectIDDate = value;
            RaisePropertyChanged(nameof(LastASConnectIDDate));
        }
    }

    private string lastPanNumbersDateString = "";
    public string LastPanNumbersDateString
    {
        get => lastPanNumbersDateString!;
        set
        {
            lastPanNumbersDateString = value;
            RaisePropertyChanged(nameof(LastPanNumbersDateString));
        }
    }
    
    private string lastASConnectIDDateString = "";
    public string LastASConnectIDDateString
    {
        get => lastASConnectIDDateString!;
        set
        {
            lastASConnectIDDateString = value;
            RaisePropertyChanged(nameof(LastASConnectIDDateString));
        }
    }


    private bool controlsEnabled = true;
    public bool ControlsEnabled
    {
        get => controlsEnabled!;
        set
        {
            controlsEnabled = value;
            RaisePropertyChanged(nameof(ControlsEnabled));
        }
    }

    private bool orderIDIsValid = true;
    public bool OrderIDIsValid
    {
        get => orderIDIsValid!;
        set
        {
            orderIDIsValid = value;
            RaisePropertyChanged(nameof(OrderIDIsValid));
        }
    }

    public RelayCommand CloseWindowCommand { get; set; }
    public RelayCommand RefreshCommand { get; set; }
    public RelayCommand BuildNameCommand { get; set; }
    public RelayCommand ShadeButtonClickedCommand { get; set; }
    public RelayCommand RenameOrderCommand { get; set; }
    public RelayCommand AddCustomerSuggestionCommand { get; set; }
    public RelayCommand FocusOnPanNumberCommand { get; set; }
    public RelayCommand FocusOnSystemListBoxCommand { get; set; }
    public RelayCommand ValidateDexisIDCommand { get; set; }

    public RelayCommand JumpToStepPanNumberCommand { get; set; }
    public RelayCommand JumpToStepCustomerCommand { get; set; }
    public RelayCommand JumpToStepSystemCommand { get; set; }
    public RelayCommand JumpToStepAfterSystemCommand { get; set; }
    public RelayCommand JumpToStepCharacteristicsCommand { get; set; }
    public RelayCommand JumpToStepBeforeCharacteristicsCommand { get; set; }
    public RelayCommand JumpToStepShadeCommand { get; set; }
    public RelayCommand SetAsNotScrewRetainedCommand { get; set; }
    public RelayCommand SetAsScrewRetainedCommand { get; set; }

    public RelayCommand SetPanNumberFromLastUsedNumberCommand { get; set; }
    public RelayCommand SetASConnectIDCommand { get; set; }
    public RelayCommand SetASConnectIDAndJumpToStepCharacteristicsCommand { get; set; }


    public System.Timers.Timer _timer;
    public System.Timers.Timer _ageTimer;
    public System.Timers.Timer _asConnectIDAgeTimer;

    public SmartOrderNames2ViewModel()
    {
        StaticInstance = this;

        CustomerSuggestionsList = [];

        _timer = new System.Timers.Timer(10000);
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();

        _ageTimer = new System.Timers.Timer(1000);
        _ageTimer.Elapsed += AgeTimer_Elapsed;

        DontUseLastPanNumber = false;

        _asConnectIDAgeTimer = new System.Timers.Timer(1000);
        _asConnectIDAgeTimer.Elapsed += ASConnectIDAgeTimer_Elapsed;

        RefreshCommand = new RelayCommand(o => _ = Refresh());
        BuildNameCommand = new RelayCommand(o => BuildName());
        ShadeButtonClickedCommand = new RelayCommand(o => ShadeButtonClicked(o));
        RenameOrderCommand = new RelayCommand(o => RenameOrder());
        AddCustomerSuggestionCommand = new RelayCommand(o => AddCustomerSuggestion());
        AddCustomerSuggestionCommand = new RelayCommand(o => AddCustomerSuggestion());
        CloseWindowCommand = new RelayCommand(o => CloseWindow());
        FocusOnPanNumberCommand = new RelayCommand(o => FocusOnPanNumberBox());
        FocusOnSystemListBoxCommand = new RelayCommand(o => FocusOnSystemListBox());
        ValidateDexisIDCommand = new RelayCommand(o => ValidateDexisID());

        JumpToStepPanNumberCommand = new RelayCommand(o => JumpToStep("Start"));
        JumpToStepCustomerCommand = new RelayCommand(o => JumpToStep("Customer"));
        JumpToStepSystemCommand = new RelayCommand(o => JumpToStep("System"));
        JumpToStepAfterSystemCommand = new RelayCommand(o => JumpToStep("AfterSystem"));
        JumpToStepCharacteristicsCommand = new RelayCommand(o => JumpToStep("Characteristics"));
        JumpToStepBeforeCharacteristicsCommand = new RelayCommand(o => JumpToStep("BeforeCharacteristics"));
        JumpToStepShadeCommand = new RelayCommand(o => JumpToStep("Shade"));
        SetAsNotScrewRetainedCommand = new RelayCommand(o => SetAsNotScrewRetained());
        SetAsScrewRetainedCommand = new RelayCommand(o => SetAsScrewRetained());

        SetPanNumberFromLastUsedNumberCommand = new RelayCommand(o => SetPanNumberFromLastUsedNumber());
        SetASConnectIDCommand = new RelayCommand(o => SetASConnectID());
        SetASConnectIDAndJumpToStepCharacteristicsCommand = new RelayCommand(o => SetASConnectIDAndJumpToStepCharacteristics());


        ThreeShapeDirectoryHelper = GetServerFileDirectory();

        _ = bool.TryParse(ReadLocalSetting("ModuleSmartOrderNames"), out bool moduleSmartOrderNames);
        SmartOrderNamesModuleIsActive = moduleSmartOrderNames;
    }

    private void SetPanNumberFromLastUsedNumber()
    {
        PanNumber = LastPanNumber;

        JumpToStep("Customer");
    }

    private void SetASConnectID()
    {
        CarestreamID = ASConnectID;
        FocusOnCarestreamIDBox();
        JumpToStep("Characteristics");
    }

    private void SetASConnectIDAndJumpToStepCharacteristics()
    {
        CarestreamID = ASConnectID;
        FocusOnCarestreamIDBox();
        JumpToStep("Characteristics");
    }


    private void SetAsScrewRetained()
    {
        IsScrewRetained = true;
        JumpToStep("Shade");
        BuildName();
    }

    private void SetAsNotScrewRetained()
    {
        IsScrewRetained = false;
        JumpToStep("Shade");
        BuildName();
    }

    private void ValidateDexisID()
    {
        string id = CarestreamID.Trim();

        if (id.Length == 3)
        {
            CarestreamID += "-";
            CarestreamID = CarestreamID.Replace("--", "-");
            SmartOrderNames2Page.StaticInstance?.SetCursorToEndOfTextBox();
        }

        BuildName();
    }

    private void JumpToStep(string tabName)
    {
        if (string.IsNullOrEmpty(tabName)) return;

        if (tabName == "Customer" && PanNumber == "")
            PanNumber = "0000";

        if (tabName == "AfterSystem")
        {
            if (SelectedDigitalSystem.Equals("Dexis", StringComparison.CurrentCultureIgnoreCase) || SelectedDigitalSystem.Equals("Carestream", StringComparison.CurrentCultureIgnoreCase) || SelectedDigitalSystem.Equals("ASConnect", StringComparison.CurrentCultureIgnoreCase))
                tabName = "ExtraInfo";
            else
                tabName = "Characteristics";
        }

        if (tabName == "BeforeCharacteristics")
        {
            if (SelectedDigitalSystem.Equals("Dexis", StringComparison.CurrentCultureIgnoreCase) || SelectedDigitalSystem.Equals("Carestream", StringComparison.CurrentCultureIgnoreCase) || SelectedDigitalSystem.Equals("ASConnect", StringComparison.CurrentCultureIgnoreCase))
                tabName = "ExtraInfo";
            else
                tabName = "System";
        }

        SmartOrderNames2Page.StaticInstance?.JumpToTab(tabName);
        BuildName();
    }

    private void CloseWindow()
    {
        Debug.WriteLine("hit");
        MainMenuViewModel.StaticInstance.ShowSmartRenameMenuItem();
        MainViewModel.Instance.SmartOrderNamesWindow.Hide();
        WindowTitle = "Smart Order Names";
    }

    private async void SelectFirstOrder()
    {
        await Task.Delay(1000);
        if (NewOrdersByMe.Count > 0)
        {
            if (PreviouslySelectedOrder != NewOrdersByMe[0])
            {
                FirstOrderSelected = true;
                SelectedOrder = NewOrdersByMe[0];
            }
        }
    }


    private void FocusOnPanNumberBox()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SmartOrderNames2Page.StaticInstance!.smartOrderNamesTabControl.SelectedIndex = 0;
            SmartOrderNames2Page.StaticInstance!.panNumberBox.Focus();
        });
    }

    private void FocusOnSystemListBox()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SmartOrderNames2Page.StaticInstance!.listBoxDigiSystem.Focus();
        });
    }

    private void FocusOnCarestreamIDBox()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SmartOrderNames2Page.StaticInstance!.dexisIdTextBox.Focus();
        });
    }

    private void FocusOnRenameButton()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SmartOrderNames2Page.StaticInstance!.renameButton.Focus();
        });
    }

    private async void AddCustomerSuggestion()
    {
        if (SelectedOrder is null)
        {
            AddCustomerSuggestionsWindow addCustomerSuggestionWindow = new()
            {
                Owner = MainWindow.Instance
            };
            addCustomerSuggestionWindow.ShowDialog();
        }
        else
        {
            AddCustomerSuggestionsWindow addCustomerSuggestionWindow = new(SelectedOrder.Customer)
            {
                Owner = MainWindow.Instance
            };
            addCustomerSuggestionWindow.ShowDialog();
        }



        if (SelectedOrder is not null)
        {
            CustomerSuggestionsList = await CustomerHasSuggestedName(SelectedOrder.Customer!);
        }

    }

    private void ShadeButtonClicked(object obj)
    {
        SelectedShade = (string)obj;
        BuildName("shade");
        SmartOrderNames2Page.StaticInstance!.renameButton.Focus();
        JumpToStep("Review");
    }

    private async void ResetNameForm()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            PanNumber = "";
            CarestreamID = "";
            IsScrewRetained = false;
            SelectedDigitalSystem = "-- None --";
            SelectedShade = "";
            OrderNamePreview = string.Empty;
            PreviouslySelectedOrder = null;
            CustomerSuggestionsList = [];
            WindowTitle = "Smart Order Names";
        });

        JumpToStep("Start");

        await Refresh();
    }

    private async void BuildName(string obj = "")
    {
        if (SelectedOrder is null || string.IsNullOrEmpty(PanNumber))
            return;

        bool isItNightGuardCase = false;
        string finalName = "";
        string screwRetained = "";
        string patientName = $"-{SelectedOrder.Patient_LastName!}";
        string customer = SelectedOrder.Customer!;
        string shade = $"-{SelectedShade}";
        string digiSystem = $"-{SelectedDigitalSystem}";
        string carestreamDexisId = CarestreamID.Trim();

        if (SelectedShade == "NG")
            isItNightGuardCase = true;

        if (await ValidateCarestreamID(carestreamDexisId))
        {
            carestreamDexisId = "-" + carestreamDexisId;
        }
        else
        {
            if (carestreamDexisId.Length > 15)
                carestreamDexisId = "-" + carestreamDexisId;
            else
                carestreamDexisId = "";
        }

        patientName = patientName.Replace(" ", "_")
                                .Replace(",", "")
                                .Replace("'", "_")
                                .Replace("\"", "_")
                                .Replace("+", "_")
                                .Replace("\\", "_")
                                .Replace("/", "_")
                                .Replace(":", "_")
                                .Replace("*", "_")
                                .Replace("?", "_")
                                .Replace("<", "_")
                                .Replace(">", "_")
                                .Replace("&", "-")
                                .Replace("|", "_")
                                .Trim();

        if (patientName == "-" || patientName == "--")
        {
            patientName = $"-{SelectedOrder.Patient_FirstName!}";
            patientName = patientName.Replace(" ", "_")
                                .Replace(",", "")
                                .Replace("'", "_")
                                .Replace("\"", "_")
                                .Replace("+", "_")
                                .Replace("\\", "_")
                                .Replace("/", "_")
                                .Replace(":", "_")
                                .Replace("*", "_")
                                .Replace("?", "_")
                                .Replace("<", "_")
                                .Replace(">", "_")
                                .Replace("&", "-")
                                .Replace("|", "_")
                                .Trim();

            if (patientName == "-" || patientName == "--")
                patientName = "-NONAME";
        }

        List<string> customerSuggestions = await CustomerHasSuggestedName(customer);
        if (customerSuggestions.Count > 0)
        {
            if (SelectedCustomerName is null)
            {
                customer = customerSuggestions[0];
                if (customerSuggestions.Count > 1)
                {
                    if (string.IsNullOrEmpty(SelectedCustomerName))
                        SelectedCustomerName = customerSuggestions[0];

                    CustomerSuggestionsList = customerSuggestions;
                }
            }
            else
            {
                customer = SelectedCustomerName;
            }
        }


        CustomerName = CleanUpCustomerName(customer);
        customer = $"-{CustomerName}";

        ToothNumbersString = await GetToothNumbersString(SelectedOrder!.IntOrderID!);

        if (!string.IsNullOrEmpty(ToothNumbersString))
            ToothNumbersString = $"-{ToothNumbersString}";

        if (SelectedDigitalSystem.Equals("-- None --"))
            digiSystem = "";
        if (shade.Equals("-"))
            shade = "";

        // check if pan number is valid or not..!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!



        if (IsScrewRetained)
            screwRetained = "-SCR";

        if (NamingCustomerFirst)
        {
            if (obj == "shade" && isItNightGuardCase)
                finalName = $"{PanNumber}{customer}{patientName}{shade}{digiSystem}{screwRetained}";
            else
                finalName = $"{PanNumber}{customer}{patientName}{ToothNumbersString}{shade}{digiSystem}{screwRetained}";
        }

        if (NamingCustomerLast)
        {
            if (obj == "shade" && isItNightGuardCase)
            {
                if (carestreamDexisId.Length > 15)
                    finalName = $"{PanNumber}{shade}{patientName}{customer}{carestreamDexisId}{digiSystem}{screwRetained}";
                else
                    finalName = $"{PanNumber}{shade}{carestreamDexisId}{patientName}{customer}{digiSystem}{screwRetained}";
            }
            else
            {
                if (carestreamDexisId.Length > 15)
                    finalName = $"{PanNumber}{ToothNumbersString}{shade}{patientName}{customer}{carestreamDexisId}{digiSystem}{screwRetained}";
                else
                    finalName = $"{PanNumber}{ToothNumbersString}{shade}{carestreamDexisId}{patientName}{customer}{digiSystem}{screwRetained}";
            }
        }

        finalName = finalName.Replace(" ", "_")
                             .Replace("'", "")
                             .Replace("%", "")
                             .Replace("*", "")
                             .Replace(",", "")
                             .Replace(".", "")
                             .Replace("&", "")
                             .Replace("@", "")
                             .Replace("$", "")
                             .Replace("+", "")
                             .ToUpper();

        if (CheckIfTheCaseIsMarkedAsRedo(PreviouslySelectedOrder!))
            finalName += "-REDO";
        
        OrderNamePreview = finalName;

        WindowTitle = OrderNamePreview;            
    }

    private bool CheckIfTheCaseIsMarkedAsRedo(ThreeShapeOrdersModel threeShapeOrdersModel)
    {
        bool redo = false;
        if (threeShapeOrdersModel.IsItRedo)
            redo = true;

        try
        {

            if (File.Exists($@"{ThreeShapeDirectoryHelper}{threeShapeOrdersModel.IntOrderID}\{threeShapeOrdersModel.IntOrderID}.xml"))
            {
                bool StartWatching = false;

                using FileStream fs = new($@"{ThreeShapeDirectoryHelper}{threeShapeOrdersModel.IntOrderID}\{threeShapeOrdersModel.IntOrderID}.xml", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader sr = new(fs);
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine()!;

                    if (line.Contains($"<Property name=\"FieldID\" value=\"DS_aml_redo\""))
                        StartWatching = true;

                    if (StartWatching && line.Contains($"<Property name=\"Value\" value=\""))
                    {
                        if (line.Contains("redo", StringComparison.CurrentCultureIgnoreCase))
                            redo = true;
                        StartWatching = false;
                    }
                }


            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("DesDi:" + ex.Message);
        }

        string diComment = threeShapeOrdersModel.OrderComments ?? "";

        if (diComment.Contains("redo", StringComparison.CurrentCultureIgnoreCase) ||
            diComment.Contains("rescan", StringComparison.CurrentCultureIgnoreCase) ||
            diComment.Contains("re scan", StringComparison.CurrentCultureIgnoreCase) ||
            diComment.Contains("broke", StringComparison.CurrentCultureIgnoreCase) ||
            diComment.Contains("reprepped", StringComparison.CurrentCultureIgnoreCase)
            )
            redo = true;


        return redo;
    }

    private async Task<bool> ValidateCarestreamID(string carestreamDexisId)
    {
        return CSIdRegex().IsMatch(carestreamDexisId);
    }



    [GeneratedRegex(@"[A-Za-z][A-Za-z][A-Za-z]-\d\d\d\d", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CSIdRegex();

    private async Task Refresh()
    {
        NewOrdersByMe = await GetNewOrdersCreatedByMe(ShowCasesWithoutNumber);

        if (AutoSelectFirstOrder && NewOrdersByMe.Count > 0 && !FirstOrderSelected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!DontUseLastPanNumber)
                {
                    LastPanNumberOnMainWindow = MainViewModel.Instance.LastUsedPanNumber;
                    LastPanNumber = LastPanNumberOnMainWindow;
                }
                else
                    LastPanNumber = string.Empty;

                ASConnectID = MainViewModel.Instance.ASConnectOrderID;

                LastPanNumbersDate = MainViewModel.Instance.LastUsedPanNumbersDate;
                LastASConnectIDDate = MainViewModel.Instance.LastASConnectIDDate;

                if (!string.IsNullOrEmpty(ASConnectID))
                    _asConnectIDAgeTimer.Start();

                if (!string.IsNullOrEmpty(LastPanNumber))
                    _ageTimer.Start();

                MainMenuViewModel.StaticInstance.HideSmartRenameMenuItem();
                SelectFirstOrder();
                _timer.Stop();
                MainViewModel.Instance.SmartOrderNamesWindow.ShowDialog();
                _timer.Start();
            });
        }
    }

    private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        //need to fix this feature
        //hardcoded true now
        DontUseLastPanNumber = false;
        _ = bool.TryParse(ReadLocalSetting("ModuleSmartOrderNames"), out bool moduleSmartOrderNames);

        SmartOrderNamesModuleIsActive = moduleSmartOrderNames;

        if (SmartOrderNamesModuleIsActive)
        {
            NewOrdersByMe = await GetNewOrdersCreatedByMe(ShowCasesWithoutNumber);

            if (AutoSelectFirstOrder && NewOrdersByMe.Count > 0 && !FirstOrderSelected)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!DontUseLastPanNumber)
                    {
                        LastPanNumberOnMainWindow = MainViewModel.Instance.LastUsedPanNumber;
                        LastPanNumber = LastPanNumberOnMainWindow;
                    }
                    else
                        LastPanNumber = string.Empty;

                    ASConnectID = MainViewModel.Instance.ASConnectOrderID;
                    LastASConnectIDDate = MainViewModel.Instance.LastASConnectIDDate;
                    if (!string.IsNullOrEmpty(ASConnectID))
                        _asConnectIDAgeTimer.Start();


                    LastPanNumbersDate = MainViewModel.Instance.LastUsedPanNumbersDate;
                    if (!string.IsNullOrEmpty(LastPanNumber))
                        _ageTimer.Start();

                    MainMenuViewModel.StaticInstance.HideSmartRenameMenuItem();
                    SelectFirstOrder();
                    _timer.Stop();
                    try
                    {
                        MainViewModel.Instance.SmartOrderNamesWindow.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                    }
                    _timer.Start();
                });
            }
        }
    }


    private async void AgeTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        string ageOfLastPanNumberfc = "Number is 0 sec old";

        double dif = Math.Round((DateTime.Now - LastPanNumbersDate).TotalSeconds, 0);

        AgeOfLastPanNumber = dif;

        if (dif < 60)
            ageOfLastPanNumberfc = "Number is " + dif.ToString() + " sec old";
        else if (dif >= 60)
            ageOfLastPanNumberfc = "Number is " + (Math.Round(dif / 60, 0)).ToString() + " min old";
        else
        {
            ageOfLastPanNumberfc = "Number is very old now";
            _ageTimer.Stop();
        }


        Application.Current.Dispatcher.Invoke(() =>
        {
            LastPanNumbersDateString = ageOfLastPanNumberfc;
        });

    }
    
    private async void ASConnectIDAgeTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        string ageOfLastASConnectID = "Number is 0 sec old";

        double dif = Math.Round((DateTime.Now - LastASConnectIDDate).TotalSeconds, 0);

        AgeOfASConnectID = dif;

        if (dif < 60)
            ageOfLastASConnectID = "Number is " + dif.ToString() + " sec old";
        else if (dif >= 60)
            ageOfLastASConnectID = "Number is " + (Math.Round(dif / 60, 0)).ToString() + " min old";
        else
        {
            ageOfLastASConnectID = "Number is very old now";
            _asConnectIDAgeTimer.Stop();
        }


        Application.Current.Dispatcher.Invoke(() =>
        {
            LastASConnectIDDateString = ageOfLastASConnectID;
        });

    }


    private async void RenameOrder()
    {
        OriginalOrderID = PreviouslySelectedOrder!.IntOrderID!;
        if (!CheckIfOrderIDIsUnique(OrderNamePreview))
        {
            ShowMessageBox("OrderID conflict", $"It's not possible to rename the order.\nAn another order in 3Shape has the same name already.\n\nPlease ensure that the order number is unique.", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
            return;
        }


        ControlsEnabled = false;
        OrderIDIsValid = false;
        await RenamingProcess();
        ResetNameForm();
        OrderNamePreview = string.Empty;
        FirstOrderSelected = false;

        if (PanNumber != LastPanNumber)
            DontUseLastPanNumber = true;

        LastPanNumber = string.Empty;
        if (AutoSelectFirstOrder && NewOrdersByMe.Count > 0)
        {
            SelectFirstOrder();
        }

        if (NewOrdersByMe.Count < 1)
            MainViewModel.Instance.SmartOrderNamesWindow.Hide();
    }


    public async Task RenamingProcess()
    {

        //ThreeShapeOrderInspectionModel inspectedOrder = InspectThreeShapeOrder(PreviouslySelectedOrder!.IntOrderID!);
        bool error = false;
        string NewFileName = OrderNamePreview;
        string NewFolderName = NewFileName;

        await LockOrderIn3Shape(NewFileName);
        //
        // starting renaming process 
        //

        try
        {

            // renaming Order's folder to the new name
            try
            {
                Directory.Move($"{ThreeShapeDirectoryHelper}{OriginalOrderID}", $"{ThreeShapeDirectoryHelper}{NewFileName}");
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
                LogMessage = $"Couldn't rename the order's folder! (some app might still use it or 3Shape directory has a folder named already same as the order's new desired name)";
                ControlsEnabled = true;
                OrderIDIsValid = true;

                return;
            }

            // renaming the XML file to the new name
            File.Move(@$"{ThreeShapeDirectoryHelper}{NewFileName}\{OriginalOrderID}.xml", @$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}.xml");

            //
            // renaming the 3ML file if exists (designed orders only)
            //
            try
            {
                if (File.Exists(@$"{ThreeShapeDirectoryHelper}{NewFileName}\{OriginalOrderID}_3pl.3ml"))
                    File.Move(@$"{ThreeShapeDirectoryHelper}{NewFileName}\{OriginalOrderID}_3pl.3ml", @$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}_3pl.3ml");
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
            }
            //
            // END
            //



            // 
            // dealing with the XML file
            //
            string XMLFileContent = "";

            try
            {
                // opening up the XML file
                XMLFileContent = File.ReadAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}.xml");

                // replacing all the entry in the text where the original filename presented to the new name
                XMLFileContent = XMLFileContent.Replace(OriginalOrderID, NewFileName);

                // saving the XML file
                File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}.xml", XMLFileContent);



                //
                // Renaming in the database
                //

                try
                {


                    ///
                    /// renaming
                    /// 
                    /// [PrintJobItem] [OrderID]
                    /// [OrderHistory] [OrderID]
                    /// [OrderExchangeElement] [OrderID]
                    /// [ImageOverlay] [OrderID]
                    /// [CustomData] [OrderID]
                    ///

                    string connectionString = DatabaseConnection.ConnectionStrFor3Shape();

                    string queryCopyLine = @$"INSERT INTO Orders ( 
                                             [IntOrderID] 
                                            ,[ExtOrderID] 
                                            ,[ClientID] 
                                            ,[ClientOrderNo] 
                                            ,[OrderDate] 
                                            ,[OrderImportanceID] 
                                            ,[Patient_RefNo] 
                                            ,[Patient_FirstName]
                                            ,[Patient_LastName] 
                                            ,[DeliveryAddress1] 
                                            ,[DeliveryAddress2] 
                                            ,[DeliveryZip] 
                                            ,[DeliveryCity] 
                                            ,[DeliveryState] 
                                            ,[DeliveryCountryID] 
                                            ,[DeliveryType] 
                                            ,[ShipToDeliveryAddress] 
                                            ,[ClientContactPerson] 
                                            ,[LabID] 
                                            ,[LabOperator] 
                                            ,[OrderComments] 
                                            ,[CreatedFromApp] 
                                            ,[RelativePos] 
                                            ,[OperatorID] 
                                            ,[DisplayOrderID] 
                                            ,[NumOrderID] 
                                            ,[DesignModuleID] 
                                            ,[ScanModuleID] 
                                            ,[FaceScanModuleID] 
                                            ,[Items] 
                                            ,[OperatorName] 
                                            ,[Customer] 
                                            ,[ManufName] 
                                            ,[OrderRelativePositionClass] 
                                            ,[ShipToERPCustNo] 
                                            ,[ERPCustomerNo] 
                                            ,[ShipToID] 
                                            ,[ModelManufacturingID] 
                                            ,[CacheMaterialName] 
                                            ,[ScanSource] 
                                            ,[ImprovementProgramSendDate] 
                                            ,[GroupFolder] 
                                            ,[CacheColor] 
                                            ,[OriginalOrderID] 
                                            ,[ImportOrderID] 
                                            ,[CacheMaxScanDate] 
                                            ,[TraySystemType]
                                            ,[ExternalLabID] 
                                            ,[ShipToDifferentAddress] 
                                            ,[PatientGuid]) 

                                        SELECT '{NewFileName}'
                                            ,[ExtOrderID] 
                                            ,[ClientID] 
                                            ,[ClientOrderNo] 
                                            ,[OrderDate] 
                                            ,[OrderImportanceID] 
                                            ,[Patient_RefNo] 
                                            ,[Patient_FirstName] 
                                            ,[Patient_LastName] 
                                            ,[DeliveryAddress1] 
                                            ,[DeliveryAddress2] 
                                            ,[DeliveryZip] 
                                            ,[DeliveryCity] 
                                            ,[DeliveryState] 
                                            ,[DeliveryCountryID] 
                                            ,[DeliveryType] 
                                            ,[ShipToDeliveryAddress] 
                                            ,[ClientContactPerson] 
                                            ,[LabID] 
                                            ,[LabOperator] 
                                            ,[OrderComments] 
                                            ,[CreatedFromApp] 
                                            ,[RelativePos] 
                                            ,[OperatorID] 
                                            ,[DisplayOrderID] 
                                            ,[NumOrderID] 
                                            ,[DesignModuleID] 
                                            ,[ScanModuleID] 
                                            ,[FaceScanModuleID] 
                                            ,[Items] 
                                            ,[OperatorName] 
                                            ,[Customer] 
                                            ,[ManufName] 
                                            ,[OrderRelativePositionClass] 
                                            ,[ShipToERPCustNo] 
                                            ,[ERPCustomerNo] 
                                            ,[ShipToID] 
                                            ,[ModelManufacturingID] 
                                            ,[CacheMaterialName] 
                                            ,[ScanSource] 
                                            ,[ImprovementProgramSendDate] 
                                            ,[GroupFolder] 
                                            ,[CacheColor] 
                                            ,[OriginalOrderID] 
                                            ,[ImportOrderID] 
                                            ,[CacheMaxScanDate] 
                                            ,[TraySystemType] 
                                            ,[ExternalLabID] 
                                            ,[ShipToDifferentAddress] 
                                            ,[PatientGuid] FROM Orders WHERE IntOrderID = '{OriginalOrderID}'";

                    await RunCommandAsynchronouslyWithLogging(queryCopyLine, connectionString);




                    string query6 = $"UPDATE ModelJob SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query6, connectionString);

                    string query2 = $"UPDATE OrderHistory SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query2, connectionString);

                    string query5 = $"UPDATE CustomData SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query5, connectionString);

                    string query1 = $"UPDATE PrintJobItem SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query1, connectionString);

                    string query7 = $"UPDATE CommunicateOrders SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query7, connectionString);

                    string query3 = $"UPDATE OrderExchangeElement SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query3, connectionString);

                    string query4 = $"UPDATE ImageOverlay SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query4, connectionString);




                    UpdateLastModifyDateinDatabase(NewFileName);




                    string queryRemoveOriginalLine = $"DELETE FROM Orders WHERE IntOrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(queryRemoveOriginalLine, connectionString);
                }
                catch (Exception ex)
                {
                    MainViewModel.Instance.AddDebugLine(ex);
                    LogMessage = $"Error ({ex.LineNumber()}): [{ex.Message}]";
                    LogMessages.Add(LogMessage);
                    error = true;
                }

                //
                // END
                //





            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
                error = true;
                LogMessage = $"Error ({ex.LineNumber()}): [{ex.Message}]";
                LogMessages.Add(LogMessage);
                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
            }
            //
            // END
            //


        }
        catch (Exception e)
        {
            MainViewModel.Instance.AddDebugLine(e);
            error = true;
            LogMessage = $"Error ({e.LineNumber()}): [{e.Message}]";
            LogMessages.Add(LogMessage);
        }


        //
        // returning every form control to original stage
        //

        if (!error)
        {
            LogMessage = $"\nRenaming finised with no issues.";
            LogMessages.Add(LogMessage);

            if (LogMessages.Count > 0)
            {
                string message = "";
                foreach (string line in LogMessages)
                    message += line + "\n";
                try
                {
                    File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\OrderRename.log", message);
                }
                catch (Exception ex)
                {
                    MainViewModel.Instance.AddDebugLine(ex);
                }
            }

            //openOrderIdHelper = NewFileName;
            await UnLockOrderIn3Shape(NewFileName);
            ResetNameForm();
        }
        else
        {
            LogMessage = $"\nEncountered some issues during renaming..";
            LogMessages.Add(LogMessage);

            if (LogMessages.Count > 0)
            {
                string message = "";
                foreach (string line in LogMessages)
                    message += line + "\n";
                try
                {
                    File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\OrderRename.log", message);
                }
                catch (Exception ex)
                {
                    MainViewModel.Instance.AddDebugLine(ex);
                }
            }
        }


        ControlsEnabled = true;
        OrderIDIsValid = true;
        //
        // END
        //
    }




    private async Task RunCommandAsynchronouslyWithLogging(string commandText, string connectionString)
    {
        using SqlConnection connection = new(connectionString);
        try
        {
            SqlCommand command = new(commandText, connection);
            connection.Open();

            IAsyncResult result = command.BeginExecuteNonQuery();
            while (!result.IsCompleted)
            {
                Thread.Sleep(100);
            }
            LogMessage = $"Command complete. Affected [{command.EndExecuteNonQuery(result)}] rows.";
            LogMessages.Add(LogMessage);
            await Task.Delay(20);
        }
        catch (SqlException ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            LogMessage = $"Error Exception ({ex.LineNumber()}): [{ex.Message}]";
            LogMessages.Add(LogMessage);
            await Task.Delay(300);
        }
        catch (InvalidOperationException ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            LogMessage = $"Error ({ex.LineNumber()}): [{ex.Message}]";
            LogMessages.Add(LogMessage);
            await Task.Delay(300);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            LogMessage = $"Error General ({ex.LineNumber()}): [{ex.Message}]";
            LogMessages.Add(LogMessage);
            await Task.Delay(300);
        }
    }

    public SMessageBoxResult ShowMessageBox(string Title, string Message, SMessageBoxButtons Buttons,
                                              NotificationIcon MessageBoxIcon,
                                              double DismissAfterSeconds = 300,
                                              Window? Owner = null)
    {
        SMessageBox sMessageBox = new(Title, Message, Buttons, MessageBoxIcon, DismissAfterSeconds);
        if (Owner is null)
            sMessageBox.Owner = MainWindow.Instance;
        else
            sMessageBox.Owner = Owner;

        sMessageBox.ShowDialog();

        return MainViewModel.Instance.SMessageBoxxResult;
    }


}
