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
        SyncfusionLicenseRegistrar.Register();
        DataContext = MainViewModel.Instance;
        InitializeComponent();
        LabnextWebViewTransparency.ConfigureHost(webviewLabnext);
        DataObject.AddPastingHandler(tbPmAddNewNumber, TbPmAddNewNumber_Pasting);

        webviewLabnext.Source = new Uri(url);
    }

    public MainWindow()
    {
        SyncfusionLicenseRegistrar.Register();
        StartupLog.WritePhase("MainWindow", "Constructor begin");
        Instance = this;
        DataContext = MainViewModel.Instance;
        StartupLog.WriteDetail("MainWindow", $"DataContext set to Instance hash={MainViewModel.Instance.GetHashCode()}");
        InitializeComponent();
        LabnextWebViewTransparency.ConfigureHost(webviewLabnext);
        StartupLog.WritePhase("MainWindow", "InitializeComponent complete");
        DataObject.AddPastingHandler(tbPmAddNewNumber, TbPmAddNewNumber_Pasting);
        MainViewModel.Instance._MainWindow = this;
        SplashViewModel.Instance.mainWindow = this;
        Loaded += MainWindow_StartupLoaded;

        pbArchivesProgressBar.Value = 0;
        Hide3ShapeOrderProgressBar();

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
            MainViewModel.Instance.SelectedGroupByItem = groupProp;
        }

        string filterUsed = ReadLocalSetting("FilterUsed");
        if (!string.IsNullOrEmpty(filterUsed))
        {
            MainViewModel.Instance.ScheduleStartupFilterRestore(filterUsed);
        }

        tbSearch.PreviewKeyDown += new KeyEventHandler(HandleEsc);

        InitializePrescriptionMakerImageInput();
    }

    private void IteroZipCelebration_DismissRequested(object sender, EventArgs e)
        => MainViewModel.Instance.HideZipArchiveIconCommand.Execute(null);

    public void ShowIteroZipCelebration()
    {
        iteroZipCelebration.EntranceCompleted -= IteroZipCelebration_EntranceCompleted;
        iteroZipCelebration.EntranceCompleted += IteroZipCelebration_EntranceCompleted;
        iteroZipCelebration.PlayEntrance();
    }

    private void IteroZipCelebration_EntranceCompleted(object? sender, EventArgs e)
    {
        iteroZipCelebration.EntranceCompleted -= IteroZipCelebration_EntranceCompleted;
        MainViewModel.Instance.NotifyIteroZipCelebrationEntranceCompleted();
    }

    public void HideIteroZipCelebrationOverlay() => iteroZipCelebration.PlayExit();

    public void Hide3ShapeOrderProgressBar()
    {
        pb3ShapeProgressBar.Value = 0;
        pb3ShapeProgressBar.Visibility = Visibility.Collapsed;
    }

    public void Increment3ShapeOrderProgressBar()
    {
        pb3ShapeProgressBar.Visibility = Visibility.Visible;
        pb3ShapeProgressBar.Value += 1;
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


    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        MainViewModel.Instance?.PrepareApplicationShutdown();
        _ = ResetPingDifferenceInDatabaseOnClose();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        MainViewModel.Instance?.PrepareApplicationShutdown();

        var app = Application.Current;
        if (app is not null && !app.Dispatcher.HasShutdownStarted)
        {
            try
            {
                app.Shutdown();
            }
            catch
            {
            }
        }

        // WPF Shutdown() can return while WebView2, hidden windows, or SharpDX hosts keep the process alive.
        Dispatcher.BeginInvoke(static () => Environment.Exit(0), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    public void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsWithinAppLogoMenu(source))
        {
            return;
        }

        if (e.ClickCount >= 2)
            BtnMaximize_Click(sender, e);

        if (e.ChangedButton == MouseButton.Left)
            try
            {
                this.DragMove();
            }
            catch { }
    }

    private void MainMenuDismissOverlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.MainMenuOpen = Visibility.Collapsed;
        }

        e.Handled = true;
    }

    private bool IsWithinAppLogoMenu(DependencyObject source)
    {
        while (source != null)
        {
            if (source == btnAppLogoMenu)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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

    private void BtnCloseApplication_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
        if (e.Source != mainTabControl)
            return;

        MainViewModel.Instance.UpdateTabChromeForSelection();

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

        if (paymentsTab.IsSelected)
            _ = MainViewModel.Instance.EnsurePaymentTabDataLoadedAsync();
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

    private void FlatGridViewColumnHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not GridViewColumnHeader header || header.Column is null)
            return;

        var width = header.Column switch
        {
            var c when ReferenceEquals(c, headerIcon) => 117d,
            var c when ReferenceEquals(c, headerButtons) => 170d,
            var c when ReferenceEquals(c, headerShade) => 44d,
            var c when ReferenceEquals(c, archeaderIcon) => 30d,
            var c when ReferenceEquals(c, archeaderCaseID) => 460d,
            var c when ReferenceEquals(c, archeaderButtons) => 100d,
            var c when ReferenceEquals(c, archeaderDesigner) => 140d,
            var c when ReferenceEquals(c, archeaderYear) => 80d,
            var c when ReferenceEquals(c, archeaderCustomer) => 272d,
            var c when ReferenceEquals(c, archeaderDates) => 260d,
            _ => (double?)null
        };

        if (width is null)
            return;

        e.Handled = true;
        header.Column.Width = width.Value;
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

    private void MainWindow_StartupLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_StartupLoaded;
        StartupLog.WritePhase("MainWindow", "Loaded event — visual tree ready");
        MainViewModel.Instance.LogStartupVmSnapshot("MainWindow Loaded");
        PerfDiagLog.Write("Startup", "MainWindow Loaded — visual tree ready");
        StartupLog.Flush();
        PerfDiagLog.Flush();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.SmartOrderNamesWindow.Owner = this;
    }

    private async void WebviewLabnext_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess || webviewLabnext.CoreWebView2 is not { } coreWebView2)
            return;

        LabnextWebViewTransparency.ConfigureHost(webviewLabnext);
        await LabnextWebViewTransparency.RegisterAsync(coreWebView2);
        UpdateLabnextChromeForColorScheme();
        if (LabnextWebViewTransparency.IsHostStyleEnabled)
            MainViewModel.Instance.BeginLabnextHostStyleApplication();

        coreWebView2.Settings.IsPasswordAutosaveEnabled = true;
        coreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        coreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        coreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
        coreWebView2.Settings.IsScriptEnabled = true;
        coreWebView2.Settings.IsWebMessageEnabled = true;
        coreWebView2.Settings.IsZoomControlEnabled = false;
        coreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
        coreWebView2.NavigationStarting -= WebviewLabnext_NavigationStarting;
        coreWebView2.NavigationStarting += WebviewLabnext_NavigationStarting;
        webviewLabnext.NavigationCompleted -= WebviewLabnext_NavigationCompleted;
        webviewLabnext.NavigationCompleted += WebviewLabnext_NavigationCompleted;
        MainViewModel.Instance.TryEnsureLabnextWebViewEventHandlers();
    }

    private void WebviewLabnext_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (LabnextWebViewTransparency.IsHostStyleEnabled)
            MainViewModel.Instance.BeginLabnextHostStyleApplication();
    }

    private async void WebviewLabnext_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            if (e.IsSuccess)
            {
                bool applied = await LabnextWebViewTransparency.ApplyForCurrentSchemeAsync(webviewLabnext, webviewLabnext.CoreWebView2);
                if (applied && LabnextWebViewTransparency.IsHostStyleEnabled)
                    await Task.Delay(80);
            }

            if (FolderSubscriptionTabPanel.Children.Contains(LabnextView))
                webviewLabnext.ZoomFactor = 0.75;
        }
        catch
        {
        }
        finally
        {
            MainViewModel.Instance.EndLabnextHostStyleApplication();
        }
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        CoreWebView2Deferral deferral = e.GetDeferral();
        deferral.Complete();
    }

    public void UpdateLabnextChromeForColorScheme()
    {
        Brush panelBackground = LabnextWebViewTransparency.GetHostPanelBackgroundBrush();
        LabnextTabPanel.Background = panelBackground;
        LabnextView.Background = panelBackground;
        LabnextWebViewTransparency.ConfigureHost(webviewLabnext);
    }

    public async Task ApplyLabnextHostStyleForCurrentSchemeAsync()
    {
        UpdateLabnextChromeForColorScheme();
        if (webviewLabnext.CoreWebView2 is not { } coreWebView2)
            return;

        MainViewModel.Instance.BeginLabnextHostStyleApplication();
        try
        {
            bool applied = await LabnextWebViewTransparency.ApplyForCurrentSchemeAsync(webviewLabnext, coreWebView2);
            if (applied && LabnextWebViewTransparency.IsHostStyleEnabled)
                await Task.Delay(80);
        }
        finally
        {
            MainViewModel.Instance.EndLabnextHostStyleApplication();
        }
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
        if (LabnextWebViewTransparency.IsHostStyleEnabled)
            MainViewModel.Instance.BeginLabnextHostStyleApplication();

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
            MainViewModel.Instance.GoHomeOnLabnextWebViewCommand.Execute(null);
            await Task.Delay(1200);

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

    private const int PmAddNewNumberMax = 99999;

    private static bool IsAllowedPmAddNewNumberText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        foreach (char c in text)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        if (text[0] == '0')
        {
            return false;
        }

        if (text.Length > 5)
        {
            return false;
        }

        return !int.TryParse(text, out int value) || value <= PmAddNewNumberMax;
    }

    private static string BuildProposedTextBoxValue(TextBox textBox, string insertion)
    {
        string current = textBox.Text;
        int start = textBox.SelectionStart;
        int length = textBox.SelectionLength;
        if (start < 0)
        {
            start = 0;
        }

        if (start > current.Length)
        {
            start = current.Length;
        }

        if (length < 0 || start + length > current.Length)
        {
            length = 0;
        }

        return current.Remove(start, length).Insert(start, insertion);
    }

    private void TbPmAddNewNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        string proposed = BuildProposedTextBoxValue(textBox, e.Text);
        e.Handled = !IsAllowedPmAddNewNumberText(proposed);
    }

    private void TbPmAddNewNumber_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(typeof(string)))
        {
            return;
        }

        string pasteText = (string)e.DataObject.GetData(typeof(string))!;
        string proposed = BuildProposedTextBoxValue(textBox, pasteText);
        if (!IsAllowedPmAddNewNumberText(proposed))
        {
            e.CancelCommand();
        }
    }

    private void PrescriptionPreviewOverlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && IsVisualDescendantOf(source, prescriptionEditPanToolbar))
        {
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.HidePrescriptionPreviewPanelCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void EditPanPositionButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainViewModel vm)
        {
            vm.EditPrescriptionPanPositionCommand.Execute(null);
        }
    }

    private void EditRushPositionButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainViewModel vm)
        {
            vm.EditPrescriptionRushPositionCommand.Execute(null);
        }
    }

    private void EditInfoLabelPositionButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainViewModel vm)
        {
            vm.EditPrescriptionInfoLabelPositionCommand.Execute(null);
        }
    }

    private void PrescriptionEventsLoadingOverlay_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MainViewModel vm)
        {
            vm.DismissPrescriptionProcessingOverlayCommand.Execute(null);
        }
    }

    private static bool IsVisualDescendantOf(DependencyObject? child, DependencyObject? ancestor)
    {
        while (child is not null)
        {
            if (child == ancestor)
            {
                return true;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return false;
    }
}
