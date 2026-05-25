using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using KeyEventHandler = System.Windows.Input.KeyEventHandler;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace StatsClient.MVVM.View;

public partial class OrderInfoWindow : Window, INotifyPropertyChanged
{
    private OrderInfoWindow? instance;
    private List<DCMFileItem>? _currentCaseFiles;
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
        OrderInfoViewModel.Instance.UpdateForm();

        Loaded += OrderInfoWindow_Loaded;
        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
    }

    private async void OrderInfoWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OrderInfoWindow_Loaded;

        if (OrderInfoViewModel.Instance.ThreeShapeObject is null)
        {
            return;
        }

        DCMFinderResult result = await System.Threading.Tasks.Task.Run(() => DCMFinder.FindForCase(OrderInfoViewModel.Instance.ThreeShapeObject));
        if (result.AllFiles.Count == 0)
        {
            return;
        }

        _currentCaseFiles = result.AllFiles
            .GroupBy(x => Path.GetFullPath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        AddDefaultPreparationScans(_currentCaseFiles);
        await dcmViewer.LoadCaseFilesAsync(_currentCaseFiles);
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
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
                    await dcmViewer.ReloadCaseFilesAsync(_currentCaseFiles);
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

    private async void AddScanButton_Click(object sender, RoutedEventArgs e)
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

        var picker = new OrderScanPickerWindow(candidates, ToggleScanPickerItemAsync)
        {
            Owner = this
        };

        const double pickerOffsetLeft = 70;
        const double pickerOffsetBottomAnchor = 520;
        const double pickerOffsetTopAdjustment = 43;

        picker.Left = this.Left + pickerOffsetLeft;
        picker.Top = this.Top + (this.ActualHeight - pickerOffsetBottomAnchor) + pickerOffsetTopAdjustment;

        picker.ShowDialog();
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

        await dcmViewer.ReloadCaseFilesAsync(_currentCaseFiles);
    }

    private static DCMFileItem CreateCaseFileItem(OrderScanPickerItem item)
    {
        bool isCad = item.Group.Equals("CAD", StringComparison.OrdinalIgnoreCase) || item.FullPath.Contains($"{Path.DirectorySeparatorChar}CAD{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        return new DCMFileItem
        {
            FilePath = item.FullPath,
            RelativePath = item.FullPath,
            DisplayName = Path.GetFileNameWithoutExtension(item.FullPath),
            MaterialName = isCad ? "Zirconia" : "Model",
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

        var prepPatterns = new[] { "PrePreparationScan", "GenericDoublePrepScan" };
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

                var fileName = Path.GetFileNameWithoutExtension(fullPath);
                if (!prepPatterns.Any(pattern => fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
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
}
