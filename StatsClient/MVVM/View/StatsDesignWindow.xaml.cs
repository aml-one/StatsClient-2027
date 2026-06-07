using DCMViewer.Services;
using DCMViewer.ViewModels;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DcmViewerMainViewModel = DCMViewer.ViewModels.MainViewModel;
namespace StatsClient.MVVM.View;
public partial class StatsDesignWindow : Window
{
    private static StatsDesignWindow? _activeWindow;
    private string? _orderFolderPath;
    private WindowModeSnapshot? _windowedModeSnapshot;
    private StatsDesignViewModel ViewModel => (StatsDesignViewModel)DataContext;
    public StatsDesignWindow(ThreeShapeOrdersModel order)
    {
        InitializeComponent();
        ViewModel.Order = order;
        ViewModel.RequestClose += OnRequestClose;
        ViewModel.FullscreenChanged += ApplyFullscreenState;
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        Loaded += StatsDesignWindow_OnLoaded;
        Closing += StatsDesignWindow_OnClosing;
        PreviewKeyDown += StatsDesignWindow_PreviewKeyDown;
    }
    public static void ShowForOrder(ThreeShapeOrdersModel order, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        var orderKey = order.IntOrderID ?? string.Empty;
        if (_activeWindow is { IsLoaded: true } existing)
        {
            var sameOrder = string.Equals(existing.ViewModel.Order?.IntOrderID, orderKey, StringComparison.OrdinalIgnoreCase);
            if (sameOrder)
            {
                existing.Activate();
                if (existing.WindowState == WindowState.Minimized)
                {
                    existing.WindowState = WindowState.Normal;
                }
                return;
            }
            existing.Close();
            _activeWindow = null;
        }
        _activeWindow = new StatsDesignWindow(order)
        {
            Owner = owner
        };
        _activeWindow.Show();
    }
    private async void StatsDesignWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= StatsDesignWindow_OnLoaded;
        if (ViewModel.Order is null)
        {
            return;
        }
        _orderFolderPath = ResolveOrderFolder(ViewModel.Order);
        if (string.IsNullOrWhiteSpace(_orderFolderPath) || !Directory.Exists(_orderFolderPath))
        {
            MessageBox.Show(this, "Order folder was not found.", "Stats Design", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await LoadDesignSessionAsync();
    }
    private void ShowDesignMessage(string message, MessageBoxImage image = MessageBoxImage.Warning)
    {
        if (Dispatcher.CheckAccess())
        {
            MessageBox.Show(this, message, "Stats Design", MessageBoxButton.OK, image);
            return;
        }
        Dispatcher.Invoke(() => MessageBox.Show(this, message, "Stats Design", MessageBoxButton.OK, image));
    }
    private async Task LoadDesignSessionAsync()
    {
        DcmViewerMainViewModel? viewer = null;
        var scansLoaded = false;
        Exception? loadFailure = null;
        string? loadStatusDetail = null;
        try
        {
            var order = ViewModel.Order!;
            var orderFolderPath = _orderFolderPath!;
            var scanFiles = await Task.Run(() => DiscoverScanFiles(order, orderFolderPath))
                .ConfigureAwait(true);
            if (scanFiles.Count == 0)
            {
                ShowDesignMessage(
                    "No scan files were found for this order.\n\nCheck that the case folder contains 3Shape scan DCM files and try opening the order in Order Info first.",
                    MessageBoxImage.Information);
                return;
            }
            DcmViewerMainViewModel.UiDispatcher = null;
            DesignViewer.ConfigureDesignSession(_orderFolderPath!, ViewModel.Order!.IntOrderID ?? "design");
            await DesignViewer.EnsureEmbeddedHostReadyAsync();
            viewer = DesignViewer.ViewerViewModel;
            if (viewer is null)
            {
                ShowDesignMessage("3D viewer failed to initialize.");
                return;
            }
            await DesignViewer.LoadCaseFilesAsync(scanFiles, _orderFolderPath);
            var loadedScanCount = viewer.LoadedFiles.Count(f => f.IsVisible && !f.IsLoadFailed);
            loadStatusDetail = string.IsNullOrWhiteSpace(viewer.StatusText)
                ? null
                : viewer.StatusText.TrimEnd('.') + ".";
            SyncWorkflowFromViewer();
            DesignViewer.EnsureViewportHealth();
            scansLoaded = loadedScanCount > 0;
            if (!scansLoaded)
            {
                var detail = loadStatusDetail ?? "The scan files could not be read.";
                ShowDesignMessage($"Scans did not load for this order.\n\n{detail}");
                return;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            loadFailure = ex;
            if (viewer is not null)
            {
                scansLoaded = viewer.LoadedFiles.Any(f => !f.IsLoadFailed);
            }
        }
        if (!scansLoaded)
        {
            if (loadFailure is not null)
            {
                LogDesignLoadFailure(loadFailure);
                ShowDesignMessage(FormatDesignLoadError(loadFailure), MessageBoxImage.Error);
            }
            return;
        }
        if (viewer is null)
        {
            return;
        }
        try
        {
            await viewer.RestoreDesignArtifactsAsync();
            viewer.ApplyDesignUiDefaults();
            if (ViewModel.RestoreLastDesignOnOpen)
            {
                await viewer.RestoreLoadedDesignFilesAsync();
            }
            SyncWorkflowFromViewer();
        }
        catch (Exception ex)
        {
            viewer.SetTransientStatus($"Design restore skipped: {ex.Message}");
        }
    }
    private static void LogDesignLoadFailure(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Stats_Client",
                "stats-design-load.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {ex}\r\n");
        }
        catch
        {
            // ignore logging failures
        }
    }
    private static string FormatDesignLoadError(Exception ex)
    {
        var root = ex.GetBaseException();
        var message = root.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            message = ex.Message.Trim();
        }
        if (root is InvalidOperationException &&
            message.Contains("different thread", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Stats Design could not load the 3D viewer on the correct UI thread.\n\n" +
                "Close this window, restart Stats Client, and try DES again.\n\n" +
                $"Technical detail: {message}";
        }
        return $"Stats Design could not load scans.\n\n{message}";
    }
    private static List<DCMFileItem> DiscoverScanFiles(ThreeShapeOrdersModel order, string orderFolderPath)
    {
        var result = DCMFinder.FindForCase(order);
        return DcmViewerMainViewModel
            .FilterScanOnlyCaseFiles(result.ModelScans, orderFolderPath)
            .Where(x => !string.IsNullOrWhiteSpace(x.FilePath) && File.Exists(x.FilePath))
            .GroupBy(x => Path.GetFullPath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }
    private void SyncWorkflowFromViewer()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(SyncWorkflowFromViewer);
            return;
        }
        var viewer = DesignViewer.ViewerViewModel;
        if (viewer is null)
        {
            return;
        }
        viewer.PropertyChanged -= ViewerOnPropertyChanged;
        viewer.PropertyChanged += ViewerOnPropertyChanged;
        ViewModel.WorkflowStep = viewer.DesignWorkflowStep;
    }
    private void ViewerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ViewerOnPropertyChanged(sender, e));
            return;
        }
        if (sender is not DcmViewerMainViewModel viewer)
        {
            return;
        }
        if (string.Equals(e.PropertyName, nameof(DcmViewerMainViewModel.DesignWorkflowStep), StringComparison.Ordinal))
        {
            ViewModel.WorkflowStep = viewer.DesignWorkflowStep;
        }
    }
    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(StatsDesignViewModel.WorkflowStep), StringComparison.Ordinal) &&
            DesignViewer.ViewerViewModel is not null)
        {
            DesignViewer.Dispatcher.BeginInvoke(() =>
            {
                if (DesignViewer.ViewerViewModel is { } viewer)
                {
                    viewer.DesignWorkflowStep = ViewModel.WorkflowStep;
                }
            });
        }
    }
    private static string? ResolveOrderFolder(ThreeShapeOrdersModel order)
    {
        if (!string.IsNullOrWhiteSpace(order.OrderFolderPath) && Directory.Exists(order.OrderFolderPath))
        {
            return order.OrderFolderPath;
        }
        if (!string.IsNullOrWhiteSpace(order.XmlFilePath))
        {
            var xmlFolder = Path.GetDirectoryName(order.XmlFilePath);
            if (!string.IsNullOrWhiteSpace(xmlFolder) && Directory.Exists(xmlFolder))
            {
                return xmlFolder;
            }
        }
        var helper = StatsClient.MVVM.ViewModel.MainViewModel.Instance?.ThreeShapeDirectoryHelper;
        if (string.IsNullOrWhiteSpace(helper))
        {
            return null;
        }
        var candidate = $"{helper}{order.IntOrderID}";
        return Directory.Exists(candidate) ? candidate : null;
    }
    private void ApplyFullscreenState()
    {
        if (!IsLoaded)
        {
            Loaded += ApplyFullscreenStateOnLoaded;
            return;
        }
        Loaded -= ApplyFullscreenStateOnLoaded;
        if (new WindowInteropHelper(this).Handle == IntPtr.Zero)
        {
            SourceInitialized -= ApplyFullscreenStateOnSourceInitialized;
            SourceInitialized += ApplyFullscreenStateOnSourceInitialized;
            return;
        }
        SourceInitialized -= ApplyFullscreenStateOnSourceInitialized;
        if (ViewModel.IsFullscreen)
        {
            EnterExclusiveFullscreen();
        }
        else
        {
            ExitExclusiveFullscreen();
        }
    }
    private void ApplyFullscreenStateOnLoaded(object sender, RoutedEventArgs e) =>
        ApplyFullscreenState();
    private void ApplyFullscreenStateOnSourceInitialized(object? sender, EventArgs e) =>
        ApplyFullscreenState();
    private void EnterExclusiveFullscreen()
    {
        _windowedModeSnapshot ??= WindowModeSnapshot.Capture(this);
        var monitor = WpfMonitorBounds.GetBoundsForWindow(new WindowInteropHelper(this).Handle);
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Normal;
        Left = monitor.Left;
        Top = monitor.Top;
        Width = monitor.Width;
        Height = monitor.Height;
        OuterChromeBorder.CornerRadius = new CornerRadius(0);
        OuterChromeBorder.BorderThickness = new Thickness(0);
        InnerChromeBorder.CornerRadius = new CornerRadius(0);
        InnerChromeBorder.Margin = new Thickness(0);
        InnerChromeBorder.BorderThickness = new Thickness(0);
        RefreshViewerAfterLayoutChange();
    }
    private void ExitExclusiveFullscreen()
    {
        if (_windowedModeSnapshot is not null)
        {
            _windowedModeSnapshot.Restore(this);
            _windowedModeSnapshot = null;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
        }
        RefreshViewerAfterLayoutChange();
    }
    private void RefreshViewerAfterLayoutChange()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            UpdateLayout();
            DesignViewer.Viewport3D?.InvalidateRender();
        });
    }
    private sealed class WindowModeSnapshot
    {
        public double Left { get; init; }
        public double Top { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public WindowState WindowState { get; init; }
        public ResizeMode ResizeMode { get; init; }
        public Thickness InnerBorderMargin { get; init; }
        public Thickness InnerBorderThickness { get; init; }
        public Thickness OuterBorderThickness { get; init; }
        public CornerRadius OuterCornerRadius { get; init; }
        public CornerRadius InnerCornerRadius { get; init; }
        public static WindowModeSnapshot Capture(StatsDesignWindow window) =>
            new()
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height,
                WindowState = window.WindowState,
                ResizeMode = window.ResizeMode,
                InnerBorderMargin = window.InnerChromeBorder.Margin,
                InnerBorderThickness = window.InnerChromeBorder.BorderThickness,
                OuterBorderThickness = window.OuterChromeBorder.BorderThickness,
                OuterCornerRadius = window.OuterChromeBorder.CornerRadius,
                InnerCornerRadius = window.InnerChromeBorder.CornerRadius
            };
        public void Restore(StatsDesignWindow window)
        {
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode;
            window.WindowState = WindowState.Normal;
            window.Left = Left;
            window.Top = Top;
            window.Width = Width;
            window.Height = Height;
            window.WindowState = WindowState;
            window.OuterChromeBorder.CornerRadius = OuterCornerRadius;
            window.OuterChromeBorder.BorderThickness = OuterBorderThickness;
            window.InnerChromeBorder.CornerRadius = InnerCornerRadius;
            window.InnerChromeBorder.Margin = InnerBorderMargin;
            window.InnerChromeBorder.BorderThickness = InnerBorderThickness;
        }
    }
    private void OnRequestClose() => Close();
    private void ClearMarginButton_Click(object sender, RoutedEventArgs e)
    {
        DesignViewer.ClearDesignMargin();
    }
    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || ViewModel.IsFullscreen)
        {
            return;
        }
        try
        {
            DragMove();
        }
        catch
        {
            // DragMove throws if the left button is released before capture completes.
        }
    }
    private void StatsDesignWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_windowedModeSnapshot is not null)
        {
            _windowedModeSnapshot.Restore(this);
            _windowedModeSnapshot = null;
        }
        ViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        if (DesignViewer.ViewerViewModel is not null)
        {
            DesignViewer.ViewerViewModel.PropertyChanged -= ViewerOnPropertyChanged;
        }
        DesignViewer.ShutdownEmbeddedHost();
        _activeWindow = null;
    }
    private void StatsDesignWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel.IsFullscreen)
        {
            ViewModel.IsFullscreen = false;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (DesignViewer.TryUndoMarginFromKeyboard())
            {
                e.Handled = true;
                return;
            }
        }
        if (DesignViewer.TryUndoSculptFromKeyboard())
        {
            e.Handled = true;
        }
    }
}
