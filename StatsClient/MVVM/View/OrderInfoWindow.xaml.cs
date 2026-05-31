using DCMViewer.Services;
using DcmViewerViewModel = DCMViewer.ViewModels.MainViewModel;
using StatsClient.MVVM.View;
using StatsClient.MVVM.Core;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.ViewModel.MainViewModel;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using static StatsClient.MVVM.Core.MessageBoxes;
using DataFormats = System.Windows.DataFormats;
using Ookii.Dialogs.Wpf;
using TaskDialog = Ookii.Dialogs.Wpf.TaskDialog;
using TaskDialogIcon = Ookii.Dialogs.Wpf.TaskDialogIcon;
using TaskDialogButton = Ookii.Dialogs.Wpf.TaskDialogButton;
using ListView = System.Windows.Controls.ListView;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventHandler = System.Windows.Input.KeyEventHandler;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Task = System.Threading.Tasks.Task;

namespace StatsClient.MVVM.View;

public partial class OrderInfoWindow : Window, INotifyPropertyChanged
{
    private OrderInfoWindow? instance;
    private List<DCMFileItem>? _currentCaseFiles;
    private DcmViewerViewModel? _hookedViewerViewModel;
    private OrderScanPickerWindow? _scanPickerWindow;

    private const double ScanPickerGapAboveButton = 6;
    public OrderInfoWindow Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChanged(nameof(Instance));
        }
    }
    
    private static OrderInfoWindow? staticInstance;
    public static OrderInfoWindow StaticInstance
    {
        get => staticInstance!;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    public OrderInfoWindow(ThreeShapeOrdersModel ThreeShapeObject)
    {
        Instance = this;
        StaticInstance = this;
        InitializeComponent();
        OrderInfoViewModel.Instance._InfoWindow = this;
        OrderInfoViewModel.Instance.ThreeShapeObject = ThreeShapeObject;
        OrderInfoViewModel.Instance.IsLoading = true;

        Loaded += OrderInfoWindow_Loaded;
        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
        OrderInfoViewModel.Instance.PropertyChanged += OrderInfoViewModelOnPropertyChanged;
        dcmViewer.Loaded += (_, _) => EnsureViewerLoadingHook();

        LocationChanged += (_, _) => PositionScanPickerWindow();
        SizeChanged += (_, _) => PositionScanPickerWindow();
        Activated += (_, _) => KeepScanPickerAboveOwner();
    }

    private async void OrderInfoWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OrderInfoWindow_Loaded;

        if (OrderInfoViewModel.Instance.ThreeShapeObject is null)
        {
            return;
        }

        await RefreshOrderAsync();
    }

    public async Task RefreshOrderAsync()
    {
        if (OrderInfoViewModel.Instance.ThreeShapeObject is null)
        {
            return;
        }

        OrderInfoViewModel.Instance.IsLoading = true;

        try
        {
            await dcmViewer.EnsureEmbeddedHostReadyAsync();
            dcmViewer.ViewerViewModel?.BeginCancellableBusy("Loading order...");

            await Dispatcher.Yield(DispatcherPriority.Background);
            dcmViewer.ViewerViewModel?.BusyCancellationToken.ThrowIfCancellationRequested();

            await OrderInfoViewModel.Instance.UpdateForm();
            dcmViewer.ViewerViewModel?.BusyCancellationToken.ThrowIfCancellationRequested();

            await ReloadCaseFilesForCurrentOrderAsync();
        }
        catch (OperationCanceledException)
        {
            // CancelBusyWork already updated status and cleared the overlay.
        }
        finally
        {
            OrderInfoViewModel.Instance.IsLoading = false;
            if (dcmViewer.ViewerViewModel?.IsBusy == true)
            {
                dcmViewer.ViewerViewModel.CompleteBusyWork();
            }
        }
    }

    private void OrderInfoViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(OrderInfoViewModel.IsLoading), StringComparison.Ordinal))
        {
            UpdateOverviewLoadingOverlay();
        }
    }

    private void EnsureViewerLoadingHook()
    {
        var viewModel = dcmViewer.ViewerViewModel;
        if (viewModel is null || ReferenceEquals(viewModel, _hookedViewerViewModel))
        {
            return;
        }

        if (_hookedViewerViewModel is not null)
        {
            _hookedViewerViewModel.PropertyChanged -= ViewerViewModelOnPropertyChanged;
        }

        _hookedViewerViewModel = viewModel;
        viewModel.PropertyChanged += ViewerViewModelOnPropertyChanged;
        UpdateOverviewLoadingOverlay();
        UpdateOrderInfoLeftPanelVisibility();
        UpdateLastTouchedByPanelVisibility();
    }

    private void ViewerViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "IsBusy" or "LoadProgress" or "StatusText")
        {
            Dispatcher.BeginInvoke(UpdateOverviewLoadingOverlay, DispatcherPriority.DataBind);
        }

        if (string.Equals(e.PropertyName, "IsSectionMode", StringComparison.Ordinal))
        {
            Dispatcher.BeginInvoke(UpdateOrderInfoLeftPanelVisibility, DispatcherPriority.DataBind);
        }

        if (string.Equals(e.PropertyName, "IsSculptMode", StringComparison.Ordinal))
        {
            Dispatcher.BeginInvoke(UpdateLastTouchedByPanelVisibility, DispatcherPriority.DataBind);
        }
    }

    internal void UpdateLastTouchedByPanelVisibility()
    {
        if (borderLastTouchedByPanel is null)
        {
            return;
        }

        if (dcmViewer.ViewerViewModel?.IsSculptMode == true)
        {
            borderLastTouchedByPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var hasItems = OrderInfoViewModel.Instance.LastTouchedByList?.Count > 0;
        borderLastTouchedByPanel.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateOrderInfoLeftPanelVisibility()
    {
        if (OrderInfoLeftPanel is null)
        {
            return;
        }

        var hideForSection = dcmViewer.ViewerViewModel?.IsSectionMode == true;
        OrderInfoLeftPanel.Visibility = hideForSection ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateOverviewLoadingOverlay()
    {
        if (OverviewLoadingOverlay is not null)
        {
            OverviewLoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (dcmViewer.TryUndoSculptFromKeyboard())
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Escape)
            Close();
    }

    public static event PropertyChangedEventHandler? PropertyChangedStatic;
    public event PropertyChangedEventHandler? PropertyChanged;

    public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
    {
        PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }
    public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
    {
        PropertyChanged?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }



    private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is System.Windows.Controls.TabControl)
        {
            if (tabPageImages.IsSelected)
            {
                OrderInfoViewModel.Instance.ReloadImages(false);
            }
            else if (tabPageOverview.IsSelected)
            {
                if (_currentCaseFiles is { Count: > 0 })
                {
                    await dcmViewer.ReloadCaseFilesAsync(_currentCaseFiles, GetCurrentOrderFolder());
                }
            }
            else
            {
                dcmViewer.UnloadViewer();
            }
         }
     }


    private void Window_Closing(object sender, CancelEventArgs e)
    {
        CloseScanPickerWindow();
        OrderInfoViewModel.Instance.WindowClosing();
    }


    private void Thumbnails_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            OrderInfoViewModel.Instance.SaveImagesInto3ShapeOrder(files);
        }
    }

    public void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            try
            {
                this.DragMove();
            }
            catch { }
    }

    private void AddScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (OrderInfoViewModel.Instance.ThreeShapeObject?.IntOrderID is not string orderId || string.IsNullOrWhiteSpace(orderId))
        {
            return;
        }

        var candidates = BuildScanPickerItems(orderId);
        if (candidates.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "No DCM/STL/PLY files found in Scans or CAD folders.", "Add scan/model", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_scanPickerWindow is { IsVisible: true })
        {
            _scanPickerWindow.RefreshItems(candidates);
            PositionScanPickerWindow();
            Dispatcher.BeginInvoke(PositionScanPickerWindow, DispatcherPriority.Loaded);
            _scanPickerWindow.Activate();
            return;
        }

        CloseScanPickerWindow();

        _scanPickerWindow = new OrderScanPickerWindow(candidates, ToggleScanPickerItemAsync)
        {
            Owner = this,
            ShowInTaskbar = false,
            ShowActivated = false
        };
        _scanPickerWindow.Closed += ScanPickerWindow_Closed;

        _scanPickerWindow.Show();
        _scanPickerWindow.UpdateLayout();
        PositionScanPickerWindow();
        KeepScanPickerAboveOwner();
        Dispatcher.BeginInvoke(PositionScanPickerWindow, DispatcherPriority.Loaded);
    }

    private void ScanPickerWindow_Closed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _scanPickerWindow))
        {
            _scanPickerWindow.Closed -= ScanPickerWindow_Closed;
            _scanPickerWindow = null;
        }
    }

    private void PositionScanPickerWindow()
    {
        if (_scanPickerWindow is null)
        {
            return;
        }

        UpdateLayout();

        if (addScanButton is { IsVisible: true, ActualWidth: > 0, ActualHeight: > 0 })
        {
            var buttonOrigin = addScanButton.PointToScreen(new Point(0, 0));
            var pickerWidth = _scanPickerWindow.ActualWidth > 0 ? _scanPickerWindow.ActualWidth : _scanPickerWindow.Width;
            var pickerHeight = _scanPickerWindow.ActualHeight > 0 ? _scanPickerWindow.ActualHeight : _scanPickerWindow.Height;

            _scanPickerWindow.Left = buttonOrigin.X + (addScanButton.ActualWidth - pickerWidth) / 2;
            _scanPickerWindow.Top = buttonOrigin.Y - pickerHeight - ScanPickerGapAboveButton;

            var workArea = SystemParameters.WorkArea;
            if (_scanPickerWindow.Left + pickerWidth > workArea.Right)
            {
                _scanPickerWindow.Left = workArea.Right - pickerWidth;
            }

            if (_scanPickerWindow.Left < workArea.Left)
            {
                _scanPickerWindow.Left = workArea.Left;
            }

            if (_scanPickerWindow.Top < workArea.Top)
            {
                _scanPickerWindow.Top = buttonOrigin.Y + addScanButton.ActualHeight + ScanPickerGapAboveButton;
            }

            return;
        }

        _scanPickerWindow.Left = Left + 70;
        _scanPickerWindow.Top = Top + ActualHeight - _scanPickerWindow.Height - ScanPickerGapAboveButton;
    }

    private void KeepScanPickerAboveOwner()
    {
        if (_scanPickerWindow is not { IsVisible: true })
        {
            return;
        }

        // Re-assert owned-window z-order so the picker stays above Order Info like a docked panel.
        _scanPickerWindow.Topmost = true;
        _scanPickerWindow.Topmost = false;
    }

    private void CloseScanPickerWindow()
    {
        if (_scanPickerWindow is null)
        {
            return;
        }

        _scanPickerWindow.Closed -= ScanPickerWindow_Closed;
        _scanPickerWindow.Close();
        _scanPickerWindow = null;
    }

    private async Task<bool> ToggleScanPickerItemAsync(OrderScanPickerItem item)
    {
        if (!File.Exists(item.FullPath))
        {
            return false;
        }

        _currentCaseFiles ??= [];

        var fullPath = Path.GetFullPath(item.FullPath);
        var existing = _currentCaseFiles.FirstOrDefault(x =>
            string.Equals(Path.GetFullPath(x.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            _currentCaseFiles.Remove(existing);
            await dcmViewer.RemoveCaseFileAsync(fullPath);
            return true;
        }

        var caseFileItem = CreateCaseFileItem(item);
        _currentCaseFiles.Add(caseFileItem);
        await dcmViewer.AddCaseFileAsync(caseFileItem);
        return true;
    }

    private async void RefreshScansButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentCaseFiles is not { Count: > 0 })
        {
            return;
        }

        await dcmViewer.ReloadCaseFilesAsync(_currentCaseFiles, GetCurrentOrderFolder());
    }

    /// <summary>
    /// Unloads the embedded DCM viewer so the 3Shape order folder is not locked during rename.
    /// </summary>
    public async Task ReleaseOrderFileLocksForRenameAsync()
    {
        CloseScanPickerWindow();
        _currentCaseFiles = null;
        dcmViewer.UnloadViewer();
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    public static async Task ReleaseLocksIfViewingOrderAsync(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId) || staticInstance is null || !staticInstance.IsLoaded)
            return;

        string? currentId = OrderInfoViewModel.Instance.ThreeShapeObject?.IntOrderID;
        if (string.IsNullOrWhiteSpace(currentId) ||
            !string.Equals(currentId, orderId, StringComparison.OrdinalIgnoreCase))
            return;

        await staticInstance.ReleaseOrderFileLocksForRenameAsync();
    }

    public async Task ReloadCaseFilesForCurrentOrderAsync()
    {
        if (OrderInfoViewModel.Instance.ThreeShapeObject is null)
            return;

        dcmViewer.ViewerViewModel?.BusyCancellationToken.ThrowIfCancellationRequested();

        DCMFinderResult result = await Task.Run(
            () => DCMFinder.FindForCase(OrderInfoViewModel.Instance.ThreeShapeObject),
            dcmViewer.ViewerViewModel?.BusyCancellationToken ?? CancellationToken.None);

        dcmViewer.ViewerViewModel?.BusyCancellationToken.ThrowIfCancellationRequested();

        if (result.AllFiles.Count == 0)
        {
            _currentCaseFiles = [];
            dcmViewer.UnloadViewer();
            return;
        }

        _currentCaseFiles = result.AllFiles
            .GroupBy(x => Path.GetFullPath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        AddDefaultPreparationScans(_currentCaseFiles);
        await dcmViewer.LoadCaseFilesAsync(_currentCaseFiles, GetCurrentOrderFolder());
        EnsureViewerLoadingHook();
    }

    private static DCMFileItem CreateCaseFileItem(OrderScanPickerItem item)
    {
        bool isCad = item.Group.Equals("CAD", StringComparison.OrdinalIgnoreCase) || item.FullPath.Contains($"{Path.DirectorySeparatorChar}CAD{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        return new DCMFileItem
        {
            FilePath = item.FullPath,
            RelativePath = item.FullPath,
            DisplayName = Path.GetFileNameWithoutExtension(item.FullPath),
            MaterialName = PrepScanMaterialRules.IsPreopScan(item.FullPath)
                ? PrepScanMaterialRules.TextureName
                : isCad ? "Zirconia" : "Model",
            GroupName = isCad ? "Restoration" : item.Group,
            SourceKind = isCad ? DCMFileSourceKind.DesignedElement : DCMFileSourceKind.ModelScan,
            IsDesigned = isCad
        };
    }

    private void AddDefaultPreparationScans(List<DCMFileItem> caseFiles)
    {
        if (OrderInfoViewModel.Instance.ThreeShapeObject?.IntOrderID is not string orderId || string.IsNullOrWhiteSpace(orderId))
        {
            return;
        }

        string orderFolder = ResolveOrderFolder(orderId);
        string scansFolder = Path.Combine(orderFolder, "Scans");
        string cadFolder = Path.Combine(orderFolder, "CAD");

        var knownPaths = caseFiles
            .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
            .Select(x => Path.GetFullPath(x.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in new[] { scansFolder, cadFolder })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                         .Where(path => path.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)
                                     || path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)
                                     || path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase)))
            {
                var fullPath = Path.GetFullPath(filePath);
                if (knownPaths.Contains(fullPath))
                {
                    continue;
                }

                if (!PrepScanMaterialRules.IsPreopScan(fullPath))
                {
                    continue;
                }

                string group;
                bool fromCadFolder = fullPath.Contains($"{Path.DirectorySeparatorChar}CAD{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
                if (fromCadFolder)
                {
                    group = "CAD";
                }
                else
                {
                    string relative = Path.GetRelativePath(scansFolder, fullPath);
                    string firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault() ?? "Scans";
                    group = firstSegment.Equals("Upper", StringComparison.OrdinalIgnoreCase)
                        ? "Upper"
                        : firstSegment.Equals("Lower", StringComparison.OrdinalIgnoreCase)
                            ? "Lower"
                            : firstSegment.Equals("Misc", StringComparison.OrdinalIgnoreCase)
                                ? "Misc"
                                : "Scans";
                }

                var pickerItem = new OrderScanPickerItem
                {
                    Group = group,
                    DisplayName = Path.GetFileName(fullPath),
                    FullPath = fullPath,
                    IsLoaded = true
                };

                caseFiles.Add(CreateCaseFileItem(pickerItem));
                knownPaths.Add(fullPath);
            }
        }
    }

    private string? GetCurrentOrderFolder()
    {
        var orderId = OrderInfoViewModel.Instance.ThreeShapeObject?.IntOrderID;
        return string.IsNullOrWhiteSpace(orderId) ? null : ResolveOrderFolder(orderId);
    }

    private static string ResolveOrderFolder(string orderId)
    {
        var obj = OrderInfoViewModel.Instance.ThreeShapeObject;
        if (obj is not null)
        {
            if (!string.IsNullOrWhiteSpace(obj.OrderFolderPath) && Directory.Exists(obj.OrderFolderPath))
                return obj.OrderFolderPath;

            if (!string.IsNullOrWhiteSpace(obj.XmlFilePath))
            {
                string? xmlDir = Path.GetDirectoryName(obj.XmlFilePath);
                if (!string.IsNullOrWhiteSpace(xmlDir) && Directory.Exists(xmlDir))
                    return xmlDir;
            }
        }
        return Path.Combine(DatabaseOperations.GetServerFileDirectory(), orderId);
    }

    private static List<OrderScanPickerItem> BuildScanPickerItems(string orderId)
    {
        string orderFolder = ResolveOrderFolder(orderId);
        string scansFolder = Path.Combine(orderFolder, "Scans");
        string cadFolder = Path.Combine(orderFolder, "CAD");

        var loadedPaths = (StaticInstance._currentCaseFiles ?? [])
            .Select(x => Path.GetFullPath(x.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new List<OrderScanPickerItem>();
        AddCandidatesFromFolder(items, scansFolder, loadedPaths, fromCadFolder: false);
        AddCandidatesFromFolder(items, cadFolder, loadedPaths, fromCadFolder: true);
        return items;
    }

    private static void AddCandidatesFromFolder(List<OrderScanPickerItem> target, string rootFolder, HashSet<string> loadedPaths, bool fromCadFolder)
    {
        if (!Directory.Exists(rootFolder))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)
                                 || path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)
                                 || path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase)))
        {
            var fullPath = Path.GetFullPath(filePath);
            string group;
            if (fromCadFolder)
            {
                group = "CAD";
            }
            else
            {
                string relative = Path.GetRelativePath(rootFolder, fullPath);
                string firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault() ?? "Scans";
                group = firstSegment.Equals("Upper", StringComparison.OrdinalIgnoreCase)
                    ? "Upper"
                    : firstSegment.Equals("Lower", StringComparison.OrdinalIgnoreCase)
                        ? "Lower"
                        : firstSegment.Equals("Misc", StringComparison.OrdinalIgnoreCase)
                            ? "Misc"
                            : "Scans";
            }

            target.Add(new OrderScanPickerItem
            {
                Group = group,
                DisplayName = Path.GetFileName(fullPath),
                FullPath = fullPath,
                IsLoaded = loadedPaths.Contains(fullPath)
            });
        }
    }

    private async void IdentifyEncodeCap_Click(object sender, RoutedEventArgs e)
    {
        if (!MainViewModel.Instance.CbSettingModuleEncodeIdentifier)
        {
            MainViewModel.Instance.ShowMessageBox(
                "Encode Identifier",
                "Enable the Encode Identifier module under Settings → Modules.",
                SMessageBoxButtons.Close,
                NotificationIcon.Warning,
                120,
                this);
            return;
        }

        if (string.IsNullOrWhiteSpace(ReadStatsSetting("Nvidia_API_KEY")))
        {
            MainViewModel.Instance.ShowMessageBox(
                "Encode Identifier",
                "Set Nvidia_API_KEY in the Stats database Settings table, then configure the vision endpoint under Settings → Encode Identifier.",
                SMessageBoxButtons.Close,
                NotificationIcon.Warning,
                160,
                this);
            return;
        }

        btnIdentifyEncodeCap.IsEnabled = false;
        try
        {
            await dcmViewer.StartEncodeIdentifyAsync(ShowEncodeIdentifyResult);
        }
        finally
        {
            btnIdentifyEncodeCap.IsEnabled = true;
        }
    }

    private void ShowEncodeIdentifyResult(EncodeCapIdentifyResult? result)
    {
        if (result is null)
        {
            return;
        }

        if (!result.Success)
        {
            string errorBody = result.ErrorMessage;
            if (!string.IsNullOrWhiteSpace(result.MeasurementSummary))
            {
                errorBody += $"\n\nMeasurement: {result.MeasurementSummary}";
            }

            if (!string.IsNullOrWhiteSpace(result.VisionDebugLogFilePath))
            {
                errorBody += $"\n\nVision debug log:\n{result.VisionDebugLogFilePath}\n\n{TruncateForDialog(result.VisionDebugLog, 1200)}";
            }
            else if (!string.IsNullOrWhiteSpace(result.VisionDebugLog))
            {
                errorBody += $"\n\nVision debug:\n{TruncateForDialog(result.VisionDebugLog, 1500)}";
            }

            var report = $"Encode Identifier failed\n\n{errorBody}";
            var wnd = new EncodeIdentifyReportWindow(report)
            {
                Owner = this
            };
            wnd.Show();
            return;
        }

        string body =
            $"Suggested 3Shape library entry:\n\n{result.ThreeShapeSuggestion}\n\n" +
            $"Profile: {result.Profile}\n" +
            $"Family: {result.Family}\n" +
            $"Center grooves: {result.CenterGrooves}\n" +
            $"Measured Ø: {result.MeasuredDiameterMm:F2} mm\n" +
            $"Platform (resolved): {result.PlatformMm:F2} mm\n" +
            $"Confidence: {result.Confidence:P0}";

        if (!string.IsNullOrWhiteSpace(result.MeasurementSummary))
        {
            body += $"\n\nMeasurement: {result.MeasurementSummary}";
        }

        if (!string.IsNullOrWhiteSpace(result.Notes))
        {
            body += $"\n\nNotes: {result.Notes}";
        }

        MainViewModel.Instance.ShowMessageBox(
            "Encode Identifier",
            body,
            SMessageBoxButtons.Close,
            NotificationIcon.Info,
            220,
            this);
    }

    private static string TruncateForDialog(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars] + "…";
    }
}
