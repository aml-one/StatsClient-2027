using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.ViewModel.MainViewModel;

namespace StatsClient.MVVM.ViewModel;

public partial class UserPanelViewModel : ObservableObject
{
    public System.Timers.Timer _orderTimer;
    public System.Timers.Timer _startTimer;
    public System.Timers.Timer _periodicTimer;

    #region Properties

    private string designerID;
    public string DesignerID
    {
        get => designerID;
        set
        {
            designerID = value;
            RaisePropertyChanged(nameof(DesignerID));
        }
    }
    
    private string designerName;
    public string DesignerName
    {
        get => designerName;
        set
        {
            designerName = value;
            RaisePropertyChanged(nameof(DesignerName));
        }
    }

    private static UserPanelViewModel? instance;
    public static UserPanelViewModel? Instance
    {
        get => instance;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }
    
    private static SentOutCasesViewModel? _sentOutCasesViewModel;
    public static SentOutCasesViewModel sentOutCasesViewModel
    {
        get => _sentOutCasesViewModel!;
        set
        {
            _sentOutCasesViewModel = value;
            RaisePropertyChangedStatic(nameof(sentOutCasesViewModel));
        }
    }

    private static ResourceDictionary lang = [];
    public static ResourceDictionary Lang
    {
        get => lang;
        set
        {
            lang = value;
            RaisePropertyChangedStatic(nameof(Lang));
        }
    }

    private string language = "English";
    public string Language
    {
        get => language;
        set
        {
            language = value;
            RaisePropertyChanged(nameof(Language));
        }
    }

    #region units

    private double totalUnits = 0;
    public double TotalUnits
    {
        get => totalUnits;
        set
        {
            totalUnits = value;
            RaisePropertyChanged(nameof(TotalUnits));
        }
    }

    private double totalUnitsToday = 0;
    public double TotalUnitsToday
    {
        get => totalUnitsToday;
        set
        {
            totalUnitsToday = value;
            RaisePropertyChanged(nameof(TotalUnitsToday));
        }
    }

    private double totalUnitsLeftOver = 0;
    public double TotalUnitsLeftOver
    {
        get => totalUnitsLeftOver;
        set
        {
            totalUnitsLeftOver = value;
            RaisePropertyChanged(nameof(TotalUnitsLeftOver));
        }
    }



    private double totalCrowns = 0;
    public double TotalCrowns
    {
        get => totalCrowns;
        set
        {
            totalCrowns = value;
            RaisePropertyChanged(nameof(TotalCrowns));
        }
    }

    private double totalAbutments = 0;
    public double TotalAbutments
    {
        get => totalAbutments;
        set
        {
            totalAbutments = value;
            RaisePropertyChanged(nameof(TotalAbutments));
        }
    }

    private double totalOrders = 0;
    public double TotalOrders
    {
        get => totalOrders;
        set
        {
            totalOrders = value;
            RaisePropertyChanged(nameof(TotalOrders));
        }
    }

    private double totalOrdersToday = 0;
    public double TotalOrdersToday
    {
        get => totalOrdersToday;
        set
        {
            totalOrdersToday = value;
            RaisePropertyChanged(nameof(TotalOrdersToday));
        }
    }

    private double totalOrdersLeftOvers = 0;
    public double TotalOrdersLeftOvers
    {
        get => totalOrdersLeftOvers;
        set
        {
            totalOrdersLeftOvers = value;
            RaisePropertyChanged(nameof(TotalOrdersLeftOvers));
        }
    }


    private double totalUnitsFinal = 0;
    public double TotalUnitsFinal
    {
        get => totalUnitsFinal;
        set
        {
            totalUnitsFinal = value;
            RaisePropertyChanged(nameof(TotalUnitsFinal));
        }
    }

    private double totalUnitsTodayFinal = 0;
    public double TotalUnitsTodayFinal
    {
        get => totalUnitsTodayFinal;
        set
        {
            totalUnitsTodayFinal = value;
            RaisePropertyChanged(nameof(TotalUnitsTodayFinal));
        }
    }

    private double totalUnitsLeftOverFinal = 0;
    public double TotalUnitsLeftOverFinal
    {
        get => totalUnitsLeftOverFinal;
        set
        {
            totalUnitsLeftOverFinal = value;
            RaisePropertyChanged(nameof(TotalUnitsLeftOverFinal));
        }
    }



    private double totalCrownsFinal = 0;
    public double TotalCrownsFinal
    {
        get => totalCrownsFinal;
        set
        {
            totalCrownsFinal = value;
            RaisePropertyChanged(nameof(TotalCrownsFinal));
        }
    }

    private double totalAbutmentsFinal = 0;
    public double TotalAbutmentsFinal
    {
        get => totalAbutmentsFinal;
        set
        {
            totalAbutmentsFinal = value;
            RaisePropertyChanged(nameof(TotalAbutmentsFinal));
        }
    }

    private double totalOrdersFinal = 0;
    public double TotalOrdersFinal
    {
        get => totalOrdersFinal;
        set
        {
            totalOrdersFinal = value;
            RaisePropertyChanged(nameof(TotalOrdersFinal));
        }
    }

    private double totalOrdersTodayFinal = 0;
    public double TotalOrdersTodayFinal
    {
        get => totalOrdersTodayFinal;
        set
        {
            totalOrdersTodayFinal = value;
            RaisePropertyChanged(nameof(TotalOrdersTodayFinal));
        }
    }

    private double totalOrdersLeftOversFinal = 0;
    public double TotalOrdersLeftOversFinal
    {
        get => totalOrdersLeftOversFinal;
        set
        {
            totalOrdersLeftOversFinal = value;
            RaisePropertyChanged(nameof(TotalOrdersLeftOversFinal));
        }
    }
    private Visibility totalUnitsTodaySameAsAllTimeTotal = Visibility.Visible;
    public Visibility TotalUnitsTodaySameAsAllTimeTotal
    {
        get => totalUnitsTodaySameAsAllTimeTotal;
        set
        {
            totalUnitsTodaySameAsAllTimeTotal = value;
            RaisePropertyChanged(nameof(TotalUnitsTodaySameAsAllTimeTotal));
        }
    }

    private Visibility totalOrdersTodaySameAsAllTimeTotal = Visibility.Visible;
    public Visibility TotalOrdersTodaySameAsAllTimeTotal
    {
        get => totalOrdersTodaySameAsAllTimeTotal;
        set
        {
            totalOrdersTodaySameAsAllTimeTotal = value;
            RaisePropertyChanged(nameof(TotalOrdersTodaySameAsAllTimeTotal));
        }
    }


    #endregion units
        
    private List<CheckedOutCasesModel> sentOutCasesModel = [];
    public List<CheckedOutCasesModel> SentOutCasesModel
    {
        get => sentOutCasesModel;
        set
        {
            sentOutCasesModel = value;
            RaisePropertyChanged(nameof(SentOutCasesModel));
        }
    }

    private List<CheckedOutCasesModel> sentOutCasesModelFinal = [];
    public List<CheckedOutCasesModel> SentOutCasesModelFinal
    {
        get => sentOutCasesModelFinal;
        set
        {
            sentOutCasesModelFinal = value;
            RaisePropertyChanged(nameof(SentOutCasesModelFinal));
        }
    }

    private string search;
    public string Search
    {
        get => search;
        set
        {
            search = value;
            RaisePropertyChanged(nameof(Search));
            if (!string.IsNullOrEmpty(value))
                Filter();
            else
                SentOutCasesModelFinal = SentOutCasesModel;
        }
    }


    private bool gettingOrderInfosNow = false;
    public bool GettingOrderInfosNow
    {
        get => gettingOrderInfosNow;
        set
        {
            gettingOrderInfosNow = value;
            RaisePropertyChanged(nameof(GettingOrderInfosNow));
        }
    }
    
    private bool firstQuery = true;
    public bool FirstQuery
    {
        get => firstQuery;
        set
        {
            firstQuery = value;
            RaisePropertyChanged(nameof(FirstQuery));
        }
    }


    #endregion Properties


    public RelayCommand FilterCommand { get; set; }
    public RelayCommand ClearFilterCommand { get; set; }
    


    public UserPanelViewModel()
    {
        Instance = this;
        sentOutCasesViewModel = SentOutCasesViewModel.StaticInstance!;
        
        _orderTimer = new System.Timers.Timer(10000);
        _orderTimer.Elapsed += OrderTimer_Elapsed;
        _orderTimer.Start(); 

        _startTimer = new System.Timers.Timer(1000);
        _startTimer.Elapsed += StartTimer_Elapsed;
        _startTimer.Start();
        
        
        FilterCommand = new RelayCommand(o => Filter());
        ClearFilterCommand = new RelayCommand(o => ClearFilter());
    }


    private async void StartTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        DesignerName = sentOutCasesViewModel.DesignersModel.FirstOrDefault(x => x.DesignerID == DesignerID)?.FriendlyName!;
     
        await GetTheOrderInfos();
        _startTimer.Stop();
    }

    private async void OrderTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!sentOutCasesViewModel.ServerInfoModel.ServerIsWritingDatabase && string.IsNullOrEmpty(Search) && !GettingOrderInfosNow)
        {   
            await GetTheOrderInfos();
        }
    }



    private void Filter()
    {
        if (!string.IsNullOrEmpty(Search))
            SentOutCasesModelFinal = SentOutCasesModel.Where(x =>
                x.OrderID!.Contains(Search, StringComparison.CurrentCultureIgnoreCase) ||
                x.Items!.Contains(Search, StringComparison.CurrentCultureIgnoreCase) ||
                x.CommentIn3Shape!.Contains(Search, StringComparison.CurrentCultureIgnoreCase)
            ).ToList();
    }

    private void ClearFilter()
    {
        Search = "";
    }

    private async Task GetTheOrderInfos()
    {
        GettingOrderInfosNow = true;
        List<CheckedOutCasesModel> modelList = await GetCheckedOutCasesFromStatsDatabase(DesignerID);
        
        if (!SentOutCasesModel.All(modelList.Contains) || SentOutCasesModel.Count == 0 || FirstQuery)
        {
            FirstQuery = false;
            List<CheckedOutCasesModel> sortedModelList = [];

            try
            {
                await Task.Run(() =>
                {
                    TotalAbutments = 0;
                    TotalCrowns = 0;
                    TotalOrders = 0;
                    TotalOrdersToday = 0;
                    TotalUnits = 0;
                    TotalUnitsToday = 0;
                    TotalOrdersLeftOvers = 0;
                    TotalUnitsLeftOver = 0;

                    foreach (var model in modelList)
                    {
                        model.OriginalSentOn = model.SentOn;

                        if (model.TotalUnits!.Length == 1)
                            model.TotalUnitsWithPrefixZero = "0" + model.TotalUnits;
                        else
                            model.TotalUnitsWithPrefixZero = model.TotalUnits;

                        if (model.Crowns == "0")
                            model.Crowns = "";
                        if (model.Abutments == "0")
                            model.Abutments = "";
                        if (model.Models == "0")
                            model.Models = "";
                        else
                            model.Models = "🗸";

                        if (model.OriginalSentOn == DateTime.Now.ToString("MM-dd-yyyy"))
                            model.SentOn = $"zToday";
                        if (model.OriginalSentOn == DateTime.Now.AddDays(-1).ToString("MM-dd-yyyy"))
                            model.SentOn = $"9Yesterday";

                        if (model.SentOn != $"zToday" || model.SentOn != $"9Yesterday")
                        {
                            if (DateTime.TryParse(model.SentOn, out DateTime sentOn))
                            {
                                string dayName = sentOn.ToString("dddd");

                                dayName = dayName switch
                                {
                                    "Monday" => $"2Monday",
                                    "Tuesday" => $"3Tuesday",
                                    "Wednesday" => $"4Wednesday",
                                    "Thursday" => $"5Thursday",
                                    "Friday" => $"6Friday",
                                    "Saturday" => $"7Saturday",
                                    "Sunday" => $"8Sunday",
                                    _ => dayName,
                                };
                                model.SentOn = dayName;
                            }
                        }

                        model.OriginalSentOnForChangedSentOn = model.SentOn;

                        model.IconImage = GetIcon(model.ScanSource!, model.CommentIcon!);

                        model.Items = model.Items!.Replace("Unsectioned model, Antagonist model", "Model")
                                                  .Replace("Unsectioned model", "Model")
                                                  .Replace("Antagonist model", "Model");

                        model.CommentIn3Shape = model.CommentIn3Shape!.Trim()
                                                                     .Replace("!", "")
                                                                     .Replace("Thanks", "")
                                                                     .Replace("Thank you", "")
                                                                     .Replace("Thank You", "")
                                                                     .Replace("[Converted From FDI]", "")
                                                                     .Trim();

                        model.CommentIn3Shape = LineBreaksRegEx().Replace(model.CommentIn3Shape, string.Empty);

                        if (model.CommentIn3Shape!.Contains(" redo", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("redo ", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("re do", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Equals("redo", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("remake ", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains(" remake", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Equals("remake", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("return to lab", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("returned to lab", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("open margin", StringComparison.CurrentCultureIgnoreCase))
                        {
                            model.CommentColor = "Maroon";
                            model.Redo = "1";
                        }

                        if (model.CommentIn3Shape!.Contains("screw retained", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("access hole", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("screwmented", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("screwret", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("srewret", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("screw access", StringComparison.CurrentCultureIgnoreCase) ||
                            model.OrderID!.EndsWith("-SCR", StringComparison.CurrentCultureIgnoreCase) ||
                            model.OrderID!.EndsWith("-SRC", StringComparison.CurrentCultureIgnoreCase) ||
                            model.OrderID!.EndsWith("-ACH", StringComparison.CurrentCultureIgnoreCase))
                        {
                            model.ScrewRetained = true;
                            model.CommentColor = "#b90ffa";
                        }

                        if (model.CommentIn3Shape!.Contains(" rush", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("rush ", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Equals("rush", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("expedite ", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains(" expedite", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Equals("expedite", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains("asap ", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Contains(" asap", StringComparison.CurrentCultureIgnoreCase) ||
                            model.CommentIn3Shape!.Equals("asap", StringComparison.CurrentCultureIgnoreCase) ||
                            model.OrderID!.EndsWith("-ASAP", StringComparison.CurrentCultureIgnoreCase) ||
                            model.OrderID!.EndsWith("-RUSH", StringComparison.CurrentCultureIgnoreCase))
                        {
                            model.Rush = "1";
                            model.CommentColor = "Crimson";
                            model.SentOn = $"0RUSH";
                        }

                        // clearing comment, if it's a standard iTero comment
                        if (model.CommentIn3Shape.Contains("Exported from iTero system"))
                            model.CommentIn3Shape = "";
                        else if (!string.IsNullOrEmpty(model.CommentIn3Shape))
                        {
                            string comment = model.CommentIn3Shape;
                            string commentCleaned = "";
                            foreach (var line in comment.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!line.StartsWith("This case is a copy of") || !line.StartsWith("Renamed file of:"))
                                    commentCleaned += line + Environment.NewLine;
                            }
                            if (string.IsNullOrEmpty(commentCleaned))
                                commentCleaned = model.CommentIn3Shape;

                            model.CommentIn3Shape = char.ToUpper(commentCleaned[0]) + commentCleaned.Substring(1);
                        }



                        if (model.CommentIcon == "7")
                        {
                            model.Crowns = "";
                            model.Abutments = "";
                            model.Models = "";
                            model.TotalUnits = "0";
                            model.TotalUnitsWithPrefixZero = "00";
                            model.SentOn = $"0Deasign ready";
                        }

                        bool noScanFile = false;
                        if (model.Comment is not null)
                        {
                            if (model.Comment.StartsWith("This case is NOT in"))
                            {
                                model.Crowns = "";
                                model.Abutments = "";
                                model.Models = "";
                                model.TotalUnits = "0";
                                model.TotalUnitsWithPrefixZero = "00";
                                model.CommentColor = "Gray";
                                model.SentOn = "No scan file";
                                noScanFile = true;
                            }
                        }

                        if (model.CommentIcon == "8")
                        {
                            model.CommentColor = "Blue";
                            model.SentOn = $"1Needs to change";
                        }

                        #region Counting units

                        if (!string.IsNullOrEmpty(model.Crowns))
                        {
                            _ = int.TryParse(model.Crowns, out int crowns);
                        
                            TotalCrowns += crowns;
                            TotalUnits += crowns;

                            if (model.OriginalSentOn!.Equals(DateTime.Now.ToString("MM-dd-yyyy")) ||
                                (model.OriginalSentOn!.Equals(DateTime.Now.AddDays(-1).ToString("MM-dd-yyyy")) &&
                                    DateTime.Now.Hour < 5))
                                TotalUnitsToday += crowns;                        
                        }

                        if (!string.IsNullOrEmpty(model.Abutments))
                        {
                            _ = int.TryParse(model.Abutments, out int abutments);
                        
                            TotalAbutments += abutments;
                            TotalUnits += abutments;

                            if (model.OriginalSentOn!.Equals(DateTime.Now.ToString("MM-dd-yyyy")) ||
                                (model.OriginalSentOn!.Equals(DateTime.Now.AddDays(-1).ToString("MM-dd-yyyy")) &&
                                    DateTime.Now.Hour < 5))
                                TotalUnitsToday += abutments;
                        }

                    
                        if (model.OriginalSentOn!.Equals(DateTime.Now.ToString("MM-dd-yyyy")) ||
                                (model.OriginalSentOn!.Equals(DateTime.Now.AddDays(-1).ToString("MM-dd-yyyy")) &&
                                    DateTime.Now.Hour < 5))
                        {
                            if (!noScanFile)
                                TotalOrdersToday++;
                        }

                        if (!noScanFile)
                            TotalOrders++;
                    

                        if (TotalOrders == TotalOrdersToday || TotalOrdersToday == 0)
                            TotalOrdersTodaySameAsAllTimeTotal = Visibility.Hidden;
                        else
                            TotalOrdersTodaySameAsAllTimeTotal = Visibility.Visible;

                        if (TotalUnits == TotalUnitsToday || TotalUnitsToday == 0)
                            TotalUnitsTodaySameAsAllTimeTotal = Visibility.Hidden;
                        else
                            TotalUnitsTodaySameAsAllTimeTotal = Visibility.Visible;

                        #endregion Counting units

                        if (model.Rush == "1")
                        {
                            model.CommentColor = "Crimson";
                            model.SentOn = $"0RUSH";
                        }
                    }

                });

                TotalOrdersLeftOvers = TotalOrders - TotalOrdersToday;
            

                TotalUnitsLeftOver = TotalUnits - TotalUnitsToday;

                await Task.Run(StartPresentingUnitNumbers);

                sortedModelList = [.. modelList.OrderBy(x => x.SentOn).ThenByDescending(x => x.Rush).ThenBy(x => x.CommentIcon).ThenByDescending(x => x.TotalUnitsWithPrefixZero)];
            
                SentOutCasesModel = sortedModelList;
                if (string.IsNullOrEmpty(Search))
                    SentOutCasesModelFinal = sortedModelList;
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
                ShowErrorMessage(ex);
            }
        }

        GettingOrderInfosNow = false;
    }

    private void ShowErrorMessage(Exception ex)
    {
        ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
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

    private static string GetIcon(string ScanSource, string commentIcon)
    {
        if (commentIcon == "7") return "/Images/SentOutCases/crown.png";

        return ScanSource switch
        {
            "ss3ShapeDesktopScanner" => "/Images/SentOutCases/i10.png",
            "ss3SE4" => "/Images/SentOutCases/i33.png",
            "ss3SE3" => "/Images/SentOutCases/i33.png",
            "ss3SE2" => "/Images/SentOutCases/i33.png",
            "ss3SD2000" => "/Images/SentOutCases/i20.png",
            "ss3SD1000" => "/Images/SentOutCases/i20.png",
            "ss3SD900" => "/Images/SentOutCases/i20.png",
            "ss3SD810" => "/Images/SentOutCases/i20.png",
            "ss3SD800" => "/Images/SentOutCases/i20.png",
            "ss3SD700" => "/Images/SentOutCases/i20.png",
            _ => "/Images/SentOutCases/trios_new.png",
        };
    }

    
    private async void StartPresentingUnitNumbers()
    {
        if (TotalUnitsFinal != TotalUnits)
            await CountUp_TotalUnits(TotalUnits);
        if (TotalCrownsFinal != TotalCrowns)
            await CountUp_TotalCrowns(TotalCrowns);
        if (TotalAbutmentsFinal != TotalAbutments)
            await CountUp_TotalAbutments(TotalAbutments);
        if (TotalOrdersFinal != TotalOrders)
            await CountUp_TotalOrders(TotalOrders);
        if (TotalUnitsLeftOverFinal != TotalUnitsLeftOver)
            await CountUp_TotalUnitsLeftOver(TotalUnitsLeftOver);
        if (TotalOrdersLeftOversFinal != TotalOrdersLeftOvers)
            await CountUp_TotalOrdersLeftOvers(TotalOrdersLeftOvers);
        if (TotalUnitsTodayFinal != TotalUnitsToday)
            await CountUp_TotalUnitsToday(TotalUnitsToday);
        if (TotalOrdersTodayFinal != TotalOrdersToday)
            await CountUp_TotalOrdersToday(TotalOrdersToday);
    }

    #region CoutUp functions
    private async Task CountUp_TotalUnits(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalUnitsFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalCrowns(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalCrownsFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalAbutments(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalAbutmentsFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalOrders(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalOrdersFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalUnitsLeftOver(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalUnitsLeftOverFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalOrdersLeftOvers(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalOrdersLeftOversFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalUnitsToday(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalUnitsTodayFinal = i;
            await Task.Delay(10);
        }
    }

    private async Task CountUp_TotalOrdersToday(double Max)
    {
        for (int i = 0; i <= Max; i++)
        {
            TotalOrdersTodayFinal = i;
            await Task.Delay(10);
        }
    }


    #endregion CoutUp functions

    [GeneratedRegex(@"^\s+$[\r\n]*", RegexOptions.Multiline)]
    private static partial Regex LineBreaksRegEx();
}
