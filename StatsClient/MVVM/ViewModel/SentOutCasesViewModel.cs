using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.ViewModel.MainViewModel;

namespace StatsClient.MVVM.ViewModel;

public partial class SentOutCasesViewModel : ObservableObject
{
    #region Properties
    private bool panelsAddedalready = false;
    public bool PanelsAddedalready
    {
        get => panelsAddedalready;
        set
        {
            panelsAddedalready = value;
            RaisePropertyChanged(nameof(PanelsAddedalready));
        }
    }

    private SentOutCasesViewModel? instance;
    public SentOutCasesViewModel? Instance
    {
        get => instance;
        set
        {
            instance = value;
            RaisePropertyChanged(nameof(Instance));
        }
    }

    private static SentOutCasesViewModel? staticInstance;
    public static SentOutCasesViewModel? StaticInstance
    {
        get => staticInstance;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    private int panelCount = 1;
    public int PanelCount
    {
        get => panelCount;
        set
        {
            panelCount = value;
            RaisePropertyChanged(nameof(PanelCount));
        }
    }

    private List<DesignerModel>? designersModel;
    public List<DesignerModel> DesignersModel
    {
        get => designersModel!;
        set
        {
            designersModel = value;
            RaisePropertyChanged(nameof(DesignersModel));
        }
    }


    private Dictionary<string, double>? designerPagesTotalUnits = [];
    public Dictionary<string, double> DesignerPagesTotalUnits
    {
        get => designerPagesTotalUnits!;
        set
        {
            designerPagesTotalUnits = value;
            RaisePropertyChanged(nameof(DesignerPagesTotalUnits));
        }
    }

    private Dictionary<string, double>? designerPagesTotalOrders = [];
    public Dictionary<string, double> DesignerPagesTotalOrders
    {
        get => designerPagesTotalOrders!;
        set
        {
            designerPagesTotalOrders = value;
            RaisePropertyChanged(nameof(DesignerPagesTotalOrders));
        }
    }

    private bool startCheckingForUnitNumbersToo = false;
    public bool StartCheckingForUnitNumbersToo
    {
        get => startCheckingForUnitNumbersToo!;
        set
        {
            startCheckingForUnitNumbersToo = value;
            RaisePropertyChanged(nameof(StartCheckingForUnitNumbersToo));
        }
    }


    private StatsDBSettingsModel? serverInfoModel;
    public StatsDBSettingsModel ServerInfoModel
    {
        get => serverInfoModel!;
        set
        {
            serverInfoModel = value;
            RaisePropertyChanged(nameof(ServerInfoModel));
        }
    }



    private string lastDBUpdate = "";
    public string LastDBUpdate
    {
        get => lastDBUpdate;
        set
        {
            lastDBUpdate = value;
            RaisePropertyChanged(nameof(LastDBUpdate));
        }
    }

    private string lastDBUpdateLocalTime = "Fetching data..";
    public string LastDBUpdateLocalTime
    {
        get => lastDBUpdateLocalTime;
        set
        {
            lastDBUpdateLocalTime = value;
            RaisePropertyChanged(nameof(LastDBUpdateLocalTime));
        }
    }

    private double updateTimeOpacity = 1;
    public double UpdateTimeOpacity
    {
        get => updateTimeOpacity;
        set
        {
            updateTimeOpacity = value;
            RaisePropertyChanged(nameof(UpdateTimeOpacity));
        }
    }

    private string statusColor = "LightGreen";
    public string StatusColor
    {
        get => statusColor;
        set
        {
            statusColor = value;
            RaisePropertyChanged(nameof(StatusColor));
        }
    }

    private string updateTimeColor = "LightGreen";
    public string UpdateTimeColor
    {
        get => updateTimeColor;
        set
        {
            updateTimeColor = value;
            RaisePropertyChanged(nameof(UpdateTimeColor));
        }
    }


    private ObservableCollection<CheckedOutCasesModel> sentOutCasesModel = [];
    public ObservableCollection<CheckedOutCasesModel> SentOutCasesModel
    {
        get => sentOutCasesModel;
        set
        {
            sentOutCasesModel = value;
            RaisePropertyChanged(nameof(SentOutCasesModel));
        }
    }

    private ObservableCollection<SentOutDesignerAccordionItem> designerPanels = [];
    public ObservableCollection<SentOutDesignerAccordionItem> DesignerPanels
    {
        get => designerPanels;
        set
        {
            designerPanels = value;
            RaisePropertyChanged(nameof(DesignerPanels));
        }
    }

    private double expandedPanelHeight = 400;
    public double ExpandedPanelHeight
    {
        get => expandedPanelHeight;
        set
        {
            if (Math.Abs(expandedPanelHeight - value) < 0.5)
                return;
            expandedPanelHeight = value;
            RaisePropertyChanged(nameof(ExpandedPanelHeight));
        }
    }

    public RelayCommand ToggleDesignerPanelCommand { get; }

    #endregion Properties

    public System.Timers.Timer _timer;

    public SentOutCasesViewModel()
    {
        Instance = this;
        StaticInstance = this;
        MainViewModel.Instance.SentOutCasesViewModel = this;
        ToggleDesignerPanelCommand = new RelayCommand(ToggleDesignerPanel);

        _timer = new System.Timers.Timer(10000);
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();

        if (Application.Current is not null)
        {
            Application.Current.Exit += (_, _) => StopTimer();
        }

        _ = GetServerInfo();
    }

    public void RecalculateExpandedPanelHeight(double hostHeight)
    {
        if (hostHeight <= 0)
            return;

        int collapsedCount = DesignerPanels.Count(p => p.IsVisibleInAccordion && !p.IsExpanded);
        const double stripHeight = 36;
        const double spacing = 2;
        double reserved = collapsedCount * (stripHeight + spacing) + 8;
        ExpandedPanelHeight = Math.Max(220, hostHeight - reserved);
    }

    private void ToggleDesignerPanel(object? parameter)
    {
        if (parameter is not string designerId || string.IsNullOrWhiteSpace(designerId))
            return;

        SentOutDesignerAccordionItem? item = DesignerPanels.FirstOrDefault(p =>
            string.Equals(p.DesignerID, designerId, StringComparison.OrdinalIgnoreCase));

        if (item is null || !item.IsVisibleInAccordion)
            return;

        if (item.IsExpanded)
            return;

        foreach (SentOutDesignerAccordionItem panel in DesignerPanels)
            panel.IsExpanded = string.Equals(panel.DesignerID, designerId, StringComparison.OrdinalIgnoreCase);

        SentOutCasesPage.Instance?.RecalculateLayout();
    }

    private void StopTimer()
    {
        try
        {
            _timer.Stop();
            _timer.Dispose();
        }
        catch
        {
        }
    }

    private static bool TryInvokeOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return false;
        }

        try
        {
            dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (Application.Current?.Dispatcher is null || Application.Current.Dispatcher.HasShutdownStarted || Application.Current.Dispatcher.HasShutdownFinished)
        {
            StopTimer();
            return;
        }

        if (ServerInfoModel is not null)
        {
            if (LastDBUpdate != ServerInfoModel.LastDBUpdate && !ServerInfoModel.ServerIsWritingDatabase)
            {
                LastDBUpdateLocalTime = DateTime.Now.ToString("MMM d - h:mm:ss tt");
                UpdateTimeColor = "LightGreen";
                UpdateTimeOpacity = 1;
                LastDBUpdate = ServerInfoModel.LastDBUpdate!;
            }

            await GetServerInfo();

            if (!PanelsAddedalready)
            {
                DesignersModel = await GetDesignersModel();
                PanelCount = DesignersModel.Count;
                PanelsAddedalready = true;
                BuildDesignerPanels();
            }
            else
            {
                await GetTotalUnitsForEachDesigner();
                SyncAccordionSummaries();
            }
        }
    }

    private void BuildDesignerPanels()
    {
        _ = TryInvokeOnUi(() =>
        {
            DesignerPanels.Clear();

            foreach (DesignerModel designer in DesignersModel)
            {
                string designerId = designer.DesignerID ?? "";
                if (string.IsNullOrWhiteSpace(designerId))
                    continue;

                DesignerPagesTotalUnits.TryAdd(designerId, 0);
                DesignerPagesTotalOrders.TryAdd(designerId, 0);

                var item = new SentOutDesignerAccordionItem
                {
                    DesignerID = designerId,
                    DesignerName = designer.FriendlyName ?? designerId,
                    TotalUnits = DesignerPagesTotalUnits.TryGetValue(designerId, out double units) ? units : 0,
                    TotalOrders = DesignerPagesTotalOrders.TryGetValue(designerId, out double orders) ? orders : 0,
                };

                item.Panel = new UserPanel(designerId);
                DesignerPanels.Add(item);
            }

            SentOutDesignerAccordionItem? firstWithWork = DesignerPanels.FirstOrDefault(p => p.HasWork)
                ?? DesignerPanels.FirstOrDefault();

            if (firstWithWork is not null)
                firstWithWork.IsExpanded = true;

            SentOutCasesPage.Instance?.RecalculateLayout();
        });
    }

    private void SyncAccordionSummaries()
    {
        _ = TryInvokeOnUi(() =>
        {
            foreach (SentOutDesignerAccordionItem item in DesignerPanels)
            {
                if (DesignerPagesTotalUnits.TryGetValue(item.DesignerID, out double units))
                    item.TotalUnits = units;

                if (DesignerPagesTotalOrders.TryGetValue(item.DesignerID, out double orders))
                    item.TotalOrders = orders;

                if (!item.HasWork && item.IsExpanded)
                {
                    item.IsExpanded = false;
                    SentOutDesignerAccordionItem? fallback = DesignerPanels.FirstOrDefault(p => p.HasWork);
                    if (fallback is not null)
                        fallback.IsExpanded = true;
                }
            }

            if (DesignerPanels.All(p => !p.IsExpanded))
            {
                SentOutDesignerAccordionItem? fallback = DesignerPanels.FirstOrDefault(p => p.HasWork)
                    ?? DesignerPanels.FirstOrDefault();
                if (fallback is not null)
                    fallback.IsExpanded = true;
            }

            SentOutCasesPage.Instance?.RecalculateLayout();
        });
    }

    private async Task GetTotalUnitsForEachDesigner()
    {
        if (DesignerPagesTotalUnits.Count < 0)
            return;

        List<DesignerUnitsModel> designerUnitsModels = await GetDesignerUnitsModel();
        if (designerUnitsModels.Count > 0)
        {
            foreach (DesignerUnitsModel item in designerUnitsModels)
            {
                DesignerPagesTotalUnits[item.DesignerID!] = item.TotalUnits;
                DesignerPagesTotalOrders[item.DesignerID!] = item.TotalOrders;
            }
        }
    }


    private async Task GetServerInfo()
    {
        try
        {
            ServerInfoModel = await Task.Run(GetStatsDBSettingsModel);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
        }
    }
}
