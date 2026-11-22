using System.Windows.Threading;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private bool labnextWebViewIsApplyingHostStyle;
    private bool _labnextWebViewSuspended;
    private Uri? _labnextSuspendedRestoreUri;
    private int _labnextPanLookupAttempts;
    private const int LabnextPanLookupMaxAttempts = 15;

    private readonly DispatcherTimer _labnextTabIdleTimer = new();
    private static readonly TimeSpan LabnextTabIdleReturnHome = TimeSpan.FromMinutes(5);

    /// <summary>True while dark-mode host CSS is loading (hides webview, shows loader).</summary>
    public bool LabnextWebViewIsApplyingHostStyle
    {
        get => labnextWebViewIsApplyingHostStyle;
        private set
        {
            if (labnextWebViewIsApplyingHostStyle == value)
                return;

            labnextWebViewIsApplyingHostStyle = value;
            RaisePropertyChanged(nameof(LabnextWebViewIsApplyingHostStyle));
            NotifyLabnextWebViewLoaderVisibility();
        }
    }

    public bool LabnextWebViewShowLoader =>
        LabnextWebViewIsApplyingHostStyle || LabnextWebviewIsLookingUpPanNumber;

    public bool LabnextWebViewContentVisible => !LabnextWebViewShowLoader;

    public bool CanOpenMainMenu => !IsLabnextOrFolderSubscriptionTabActive();

    public bool IsLabnextWebViewTabActive()
    {
        if (!IsMainWindowReady || _MainWindow.mainTabControl is null)
            return false;

        object? selected = _MainWindow.mainTabControl.SelectedItem;
        return selected == _MainWindow.tabLabnext
            || selected == _MainWindow.folderSubscriptionTab
            || selected == _MainWindow.paymentsTab;
    }

    public bool IsLabnextOrFolderSubscriptionTabActive() => IsLabnextWebViewTabActive();

    private void InitializeLabnextTabIdleWatch()
    {
        _labnextTabIdleTimer.Tick += LabnextTabIdleTimer_Tick;
        _labnextTabIdleTimer.Interval = TimeSpan.FromSeconds(30);
        _labnextTabIdleTimer.Start();
    }

    private bool IsLabnextOrFolderSubscriptionIdleWatchTabActive()
    {
        if (!IsMainWindowReady || _MainWindow.mainTabControl is null)
            return false;

        object? selected = _MainWindow.mainTabControl.SelectedItem;
        return selected == _MainWindow.tabLabnext || selected == _MainWindow.folderSubscriptionTab;
    }

    private void LabnextTabIdleTimer_Tick(object? sender, EventArgs e)
    {
        if (!AppIsFullyLoaded || !IsMainWindowReady)
            return;

        if (!IsLabnextOrFolderSubscriptionIdleWatchTabActive())
            return;

        if (UserIdleHelper.GetIdleTime() < LabnextTabIdleReturnHome)
            return;

        string tabName = ReferenceEquals(_MainWindow.mainTabControl.SelectedItem, _MainWindow.tabLabnext)
            ? "Labnext"
            : "Folder Subscription";

        PerfDiagLog.Write("Network", $"Idle {LabnextTabIdleReturnHome.TotalMinutes:0} min on {tabName} — switching to Home");
        SwitchToHomeTab();
    }

    internal void UpdateLabnextWebViewPowerState()
    {
        if (!CbSettingModuleLabnext || !IsMainWindowReady)
            return;

        if (!_MainWindow.webviewLabnext.IsInitialized)
            return;

        bool shouldBeActive = IsLabnextWebViewTabActive();

        if (shouldBeActive)
        {
            if (_labnextWebViewSuspended && _labnextSuspendedRestoreUri is not null)
            {
                _MainWindow.webviewLabnext.Source = _labnextSuspendedRestoreUri;
                _labnextSuspendedRestoreUri = null;
                _labnextWebViewSuspended = false;
                LabnextLoadingHiderTimer.Start();
                PerfDiagLog.Write("Network", "Labnext WebView restored");
            }
        }
        else if (!_labnextWebViewSuspended)
        {
            Uri? current = _MainWindow.webviewLabnext.Source;
            if (current is null || string.Equals(current.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase))
                return;

            StopLabnextPanLookupTimer();
            LabnextKeepAliveTimer.Stop();
            LabnextLoadingHiderTimer.Stop();
            LabnextCanReload = false;

            _labnextSuspendedRestoreUri = current;
            _MainWindow.webviewLabnext.Source = new Uri("about:blank");
            _labnextWebViewSuspended = true;
            PerfDiagLog.Write("Network", "Labnext WebView parked (about:blank)");
        }
    }

    private void StopLabnextPanLookupTimer()
    {
        _labnextPanLookupAttempts = 0;
        LabnextPanLookupTimer.Stop();
    }

    private void BeginLabnextPanLookup()
    {
        _labnextPanLookupAttempts = 0;
        LabnextPanLookupTimer.Start();
    }

    private void RegisterLabnextPanLookupAttempt()
    {
        _labnextPanLookupAttempts++;
        if (_labnextPanLookupAttempts >= LabnextPanLookupMaxAttempts)
        {
            PerfDiagLog.Write("Network", $"Labnext pan lookup gave up after {LabnextPanLookupMaxAttempts} attempts", mirrorToDebugTab: true);
            StopLabnextPanLookupTimer();
        }
    }

    public void BeginLabnextHostStyleApplication()
    {
        if (!Core.LabnextWebViewTransparency.IsHostStyleEnabled)
            return;

        LabnextWebViewIsApplyingHostStyle = true;
    }

    public void EndLabnextHostStyleApplication()
    {
        LabnextWebViewIsApplyingHostStyle = false;
    }

    private void NotifyLabnextWebViewLoaderVisibility()
    {
        RaisePropertyChanged(nameof(LabnextWebViewShowLoader));
        RaisePropertyChanged(nameof(LabnextWebViewContentVisible));
    }
}
