using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.Core.Enums;
using static System.Windows.Forms.LinkLabel;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;

namespace StatsClient.MVVM.View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public WebView2 webview => serverLogPanel.WebView;

    public TextBox tbSearchAccInfos => accountInfosPanel.SearchBox;

    private DateTime _lastLabnextAutoLoginAttemptUtc = DateTime.MinValue;
    private bool _isLabnextAutoLoginInProgress;

    public static event PropertyChangedEventHandler? PropertyChangedStatic;
    public event PropertyChangedEventHandler? PropertyChanged;

    public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
    {
        PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }

    private static MainWindow? instance;
    public static MainWindow Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }
    
    public MainWindow(string url)
    {
        InitializeComponent();

        webviewLabnext.Source = new Uri(url);
    }

    public MainWindow()
    {
        //Register Syncfusion license
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JGaF5cXGpCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH1ceXRcQ2heVkZ+XkpWYEs=");
        Instance = this;
        InitializeComponent();
        DataContext = MainViewModel.Instance;

        MainViewModel.Instance._MainWindow = this;
        pb3ShapeProgressBar.Value = 0;
        pbArchivesProgressBar.Value = 0;

        _ = int.TryParse(ReadLocalSetting("WindowWidth"), out int wWidth);
        _ = int.TryParse(ReadLocalSetting("WindowHeight"), out int wHeight);
        _ = int.TryParse(ReadLocalSetting("WindowTop"), out int wTop);
        _ = int.TryParse(ReadLocalSetting("WindowLeft"), out int wLeft);

        Width = wWidth;
        Height = wHeight;
        Top = wTop;
        Left = wLeft;

        string groupProp = ReadLocalSetting("GroupBy");
        if (groupProp != null)
        {
            GroupBy.SelectedItem = groupProp;
            MainViewModel.Instance.GroupList();
        }

        string filterUsed = ReadLocalSetting("FilterUsed");
        if (!string.IsNullOrEmpty(filterUsed)) 
        {
            MainViewModel.Instance.Search(filterUsed, true);
            MainViewModel.Instance.ShowNotificationMessage("Startup", "Last view was restored!");
        }

        tbSearch.PreviewKeyDown += new KeyEventHandler(HandleEsc);
        //tbFlyingSearch.PreviewKeyDown += new KeyEventHandler(HandleEscFs);

        zipArchiveIcon.Width = 0;

        InitializePrescriptionMakerImageInput();
    }

    private void InitializePrescriptionMakerImageInput()
    {
        prescriptionMakerTabGrid.AllowDrop = true;
        prescriptionMakerTabGrid.DragOver += PrescriptionMakerTabGrid_DragOver;
        prescriptionMakerTabGrid.Drop += PrescriptionMakerTabGrid_Drop;
        PreviewKeyDown += PrescriptionMakerImagePaste_PreviewKeyDown;
    }

    private bool IsPrescriptionMakerTabSelected()
        => mainTabControl.SelectedItem == prescriptionMakerTab;

    private void PrescriptionMakerTabGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!IsPrescriptionMakerTabSelected())
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = PrescriptionMakerImageHelper.GetFirstImagePathFromDrop(e.Data) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void PrescriptionMakerTabGrid_Drop(object sender, DragEventArgs e)
    {
        if (!IsPrescriptionMakerTabSelected())
        {
            return;
        }

        string? imagePath = PrescriptionMakerImageHelper.GetFirstImagePathFromDrop(e.Data);
        if (imagePath is null)
        {
            return;
        }

        e.Handled = true;
        if (!PrescriptionMakerImageHelper.TryLoadPngBytesFromFile(imagePath, out byte[] pngBytes))
        {
            MainViewModel.Instance.ShowMessageBox(
                "Image",
                "Could not load the dropped image file.",
                SMessageBoxButtons.Close,
                MainViewModel.NotificationIcon.Warning,
                12,
                this);
            return;
        }

        MainViewModel.Instance.HandlePrescriptionMakerImageReceived(pngBytes);
    }

    private void PrescriptionMakerImagePaste_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsPrescriptionMakerTabSelected())
        {
            return;
        }

        bool isPaste =
            (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) ||
            (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.Shift);

        if (!isPaste)
        {
            return;
        }

        if (!PrescriptionMakerImageHelper.TryGetClipboardImagePng(out byte[] pngBytes))
        {
            return;
        }

        e.Handled = true;
        MainViewModel.Instance.HandlePrescriptionMakerImageReceived(pngBytes);
    }


    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        await ResetPingDifferenceInDatabaseOnClose();
        Application.Current.Shutdown();
    }

    public void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
            BtnMaximize_Click(sender, e);

        if (e.ChangedButton == MouseButton.Left)
            try
            {
                this.DragMove();
            }
            catch { }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
        MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;

        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            this.BorderThickness = new Thickness(0);
            btnMaximize.Content = "▣";
        }
        else if (WindowState == WindowState.Normal)
        {
            WindowState = WindowState.Maximized;
            this.BorderThickness = new Thickness(6, 6, 6, 6);
            btnMaximize.Content = "⧉";
        }

    }

    private async void BtnCloseApplication_Click(object sender, RoutedEventArgs e)
    {
        await ResetPingDifferenceInDatabaseOnClose();
        Application.Current.Shutdown();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
        MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;

        if (WindowState == WindowState.Maximized)
        {
            this.BorderThickness = new Thickness(6, 6, 6, 6);
            btnMaximize.Content = "⧉";
        }
        else if (WindowState == WindowState.Normal)
        {
            this.BorderThickness = new Thickness(0);
            btnMaximize.Content = "▣";
        }

        if (WindowState != WindowState.Minimized)
        {
            WriteLocalSetting("WindowWidth", Width.ToString());
            WriteLocalSetting("WindowHeight", Height.ToString());
        }

       
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ThreeShapeTab.IsSelected)
        {
            MainViewModel.Instance.ThreeShapeObject = null;
            MainViewModel.Instance.Is3ShapeTabSelected = false;
        }
        else
        {
            MainViewModel.Instance.Is3ShapeTabSelected = true;
        }

        if (infoTab is not null)
            if (!infoTab.IsSelected)
                aboutTab.IsSelected = true;
    }

    //private void BtnFilter_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    //{
    //    if (e.ChangedButton == MouseButton.Left)
    //    {
    //        Button? button = sender as Button;
    //        ContextMenu contextMenu = button!.ContextMenu;
    //        contextMenu.PlacementTarget = button;
    //        contextMenu.Placement = PlacementMode.Top;
    //        contextMenu.IsOpen = true;
    //        e.Handled = true;
    //    }
    //}

    private void GridViewColumnHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing icon column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 117;
    }
    
    private void GridViewForButtonsColumnHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing button column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 170;
    }
    
    private void GridViewForShadeColumnHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing shade column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 44;
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        WriteLocalSetting("WindowTop", Top.ToString());
        WriteLocalSetting("WindowLeft", Left.ToString());
    }

    
    private void ListView3ShapeOrders_MouseDown(object sender, MouseButtonEventArgs e)
    {
        tbSearch.Focus();
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            tbSearch.Clear();
    }
  
    private void HandleEscFs(object sender, KeyEventArgs e)
    {
        //if (e.Key == Key.Escape)
        //    tbFlyingSearch.Clear();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        settingsTab.IsSelected = true;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.SmartOrderNamesWindow.Owner = this;
    }

    private void WebviewLabnext_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        webviewLabnext.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
        webviewLabnext.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        webviewLabnext.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        webviewLabnext.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
        webviewLabnext.CoreWebView2.Settings.IsScriptEnabled = true;
        webviewLabnext.CoreWebView2.Settings.IsWebMessageEnabled = true;
        webviewLabnext.CoreWebView2.Settings.IsZoomControlEnabled = false;
        webviewLabnext.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
        webviewLabnext.NavigationCompleted -= WebviewLabnext_NavigationCompleted;
        webviewLabnext.NavigationCompleted += WebviewLabnext_NavigationCompleted;
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        //CoreWebView2 cwv2 = (CoreWebView2)sender;

        CoreWebView2Deferral deferral = e.GetDeferral();

        //LabnextChildWindow childWindow = new(e.Uri)
        //{
        //    Title = "Child Window"
        //};
        //childWindow.Show();

        //e.Handled = true;
        deferral.Complete();
    }

    private void WebviewLabnext_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            if (FolderSubscriptionTabPanel.Children.Contains(LabnextView))
            {
                webviewLabnext.ZoomFactor = 0.75;
            }
        }
        catch
        {
        }
    }

    private void ArcGridViewColumnHeaderIcon_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 30;
    }

    private void ArcGridViewColumnHeaderCaseId_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 460;
    }

    private void ArcGridViewColumnHeaderAction_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 100;
    }

    private void ArcGridViewColumnHeaderDesigner_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 140;
    }

    private void ArcGridViewColumnHeaderFromYear_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 80;
    }

    private void ArcGridViewColumnHeaderCustomer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 272;
    }

    private void ArcGridViewColumnHeaderDates_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // preventing column from resize
        e.Handled = true;
        ((GridViewColumnHeader)sender).Column.Width = 260;
    }

    private void ListViewFolderSubscription_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        AlignFolderSubscriptionGridViewHeader(listView);
        listView.SizeChanged += (_, _) => AlignFolderSubscriptionGridViewHeader(listView);
    }

    private static void AlignFolderSubscriptionGridViewHeader(ListView listView)
    {
        var headerPresenter = FindVisualChild<GridViewHeaderRowPresenter>(listView);
        if (headerPresenter == null)
            return;

        var scrollbarWidth = 0.0;
        foreach (var scrollViewer in FindVisualChildren<ScrollViewer>(listView))
        {
            if (scrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
                continue;

            if (scrollViewer.Template?.FindName("PART_VerticalScrollBar", scrollViewer) is ScrollBar bar && bar.ActualWidth > 0)
                scrollbarWidth = Math.Max(scrollbarWidth, bar.ActualWidth);
            else
                scrollbarWidth = Math.Max(scrollbarWidth, SystemParameters.VerticalScrollBarWidth);
        }

        headerPresenter.Margin = new Thickness(0, 0, scrollbarWidth, 0);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;

            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }

    
    private async void WebviewLabnext_ContentLoading(object sender, CoreWebView2ContentLoadingEventArgs e)
    {
        if (webviewLabnext.Source is not null)
        {
            MainViewModel.Instance.LabNextWebViewStatusText = webviewLabnext.Source.ToString().Replace($"https://{MainViewModel.Instance.LabnextLabID}.labnext.net/lab", "");
        }

        if (MainViewModel.Instance.LabNextWebViewStatusText.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            await TryAutoLoginToLabnextAsync();
        }
    }

    private async Task TryAutoLoginToLabnextAsync()
    {
        if (_isLabnextAutoLoginInProgress ||
            !MainViewModel.Instance.CbSettingModuleLabnext ||
            !MainViewModel.Instance.CbSettingKeepUserLoggedInLabnext ||
            webviewLabnext.CoreWebView2 is null)
        {
            return;
        }

        if ((DateTime.UtcNow - _lastLabnextAutoLoginAttemptUtc) < TimeSpan.FromSeconds(10))
        {
            return;
        }

        string userName = DatabaseConnection.ReadStatsSetting("LabnextEmail");
        string password = DatabaseConnection.ReadStatsSetting("LabnextPassword");
        string emailSelector = DatabaseConnection.ReadStatsSetting("LabnextEmailSelector");
        string passwordSelector = DatabaseConnection.ReadStatsSetting("LabnextPasswordSelector");

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(emailSelector) ||
            string.IsNullOrWhiteSpace(passwordSelector))
        {
            return;
        }

        _isLabnextAutoLoginInProgress = true;
        _lastLabnextAutoLoginAttemptUtc = DateTime.UtcNow;

        try
        {
            if (!string.IsNullOrWhiteSpace(MainViewModel.Instance.LabnextUrl))
            {
                webviewLabnext.Source = new Uri(MainViewModel.Instance.LabnextUrl);
                await Task.Delay(1200);
            }
            else
            {
                await Task.Delay(700);
            }

            string userJs = EscapeForSingleQuotedJs(userName);
            string passJs = EscapeForSingleQuotedJs(password);
            string emailSelectorJs = EscapeForSingleQuotedJs(emailSelector);
            string passwordSelectorJs = EscapeForSingleQuotedJs(passwordSelector);

            string script = $$"""
(() => {
const setInputValue = (element, value) => {
  if (!element) return;
  const descriptor = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
  if (descriptor && descriptor.set) {
    descriptor.set.call(element, value);
  } else {
    element.value = value;
  }
  element.dispatchEvent(new Event('input', { bubbles: true }));
  element.dispatchEvent(new Event('change', { bubbles: true }));
};

const userField = document.querySelector('{{emailSelectorJs}}');
const passwordField = document.querySelector('{{passwordSelectorJs}}');

if (!userField || !passwordField) {
  return 'form-not-found';
}

setInputValue(userField, '{{userJs}}');
setInputValue(passwordField, '{{passJs}}');

const submitButton = document.querySelector('button[type="submit"], input[type="submit"]');
if (submitButton) {
  submitButton.click();
  return 'submitted-click';
}

const form = passwordField.form || userField.form;
if (form) {
  if (typeof form.requestSubmit === 'function') {
    form.requestSubmit();
  } else {
    form.submit();
  }
  return 'submitted-form';
}

return 'submit-not-found';
})();
""";

            await webviewLabnext.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
        }
        finally
        {
            _isLabnextAutoLoginInProgress = false;
        }
    }

    private static string EscapeForSingleQuotedJs(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "")
            .Replace("\n", "");
    }

    private void Border_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            MainViewModel.Instance.FileWasDroppedToWindow(files);
        }
    }

    private void ListView3ShapeOrders_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length != 1 || !files[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return;

        string zipPath = files[0];

        if (!ArchiveOrderImportHelper.TryReadOrderIdFromZip(zipPath, out string orderId) || string.IsNullOrWhiteSpace(orderId))
        {
            MainViewModel.Instance.ShowMessageBox("Import failed", "Could not identify OrderID from ZIP file.", SMessageBoxButtons.Ok, MainViewModel.NotificationIcon.Error, 12, this);
            return;
        }

        string clientImportPath = ArchiveOrderImportHelper.GetClientImportPath();
        if (!ArchiveOrderImportHelper.EnsureClientImportPathAvailable(clientImportPath, out string pathError))
        {
            MainViewModel.Instance.ShowMessageBox("Import failed", $"ClientImportPath is unavailable: {pathError}", SMessageBoxButtons.Ok, MainViewModel.NotificationIcon.Error, 12, this);
            return;
        }

        string destinationRoot = DatabaseOperations.GetServerFileDirectory();
        if (ArchiveOrderImportHelper.DestinationOrderExists(orderId, destinationRoot))
        {
            var confirm = MainViewModel.Instance.ShowMessageBox("Overwrite existing order?",
                $"Order {orderId} already exists in 3Shape. Are you sure you want to overwrite it?",
                SMessageBoxButtons.YesNo,
                MainViewModel.NotificationIcon.Warning,
                20,
                this);

            if (confirm != SMessageBoxResult.Yes)
                return;
        }

        string? stagedZipPath = ArchiveOrderImportHelper.CopyZipToClientImportPath(zipPath, orderId, clientImportPath, out string stageError);
        if (string.IsNullOrWhiteSpace(stagedZipPath))
        {
            MainViewModel.Instance.ShowMessageBox("Import failed", $"Could not copy ZIP to ClientImportPath: {stageError}", SMessageBoxButtons.Ok, MainViewModel.NotificationIcon.Error, 15, this);
            return;
        }

        var queueResult = ArchiveOrderImportHelper.QueueImportRequest(orderId, stagedZipPath, Environment.MachineName);
        if (!queueResult.Success)
        {
            MainViewModel.Instance.ShowMessageBox("Import failed", $"Could not queue order {orderId}: {queueResult.Message}", SMessageBoxButtons.Ok, MainViewModel.NotificationIcon.Error, 15, this);
            return;
        }

        MainViewModel.Instance.ShowNotificationMessage("Order Import", $"Order {orderId} queued for import service.", MainViewModel.NotificationIcon.Success);
    }

    private async void PaymentsTab_Loaded(object sender, RoutedEventArgs e)
    {
        // Load designers for payment order lists when PAYMENTS tab is first loaded
        if (MainViewModel.Instance.PaymentOrderListDesigners.Count == 0)
        {
            await MainViewModel.Instance.LoadPaymentDesignersAsync();
        }
    }
}
