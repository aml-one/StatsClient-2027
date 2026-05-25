using Microsoft.Data.SqlClient;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.ViewModel.MainViewModel;

namespace StatsClient.MVVM.ViewModel;

public class SplashViewModel : ObservableObject
{
    public MainWindow? mainWindow;
    public bool isEverythingOkay = true;
    private readonly DispatcherTimer timerCheckServerConnectionFirstTime = new ();
    
    private static SplashViewModel? instance;
    public static SplashViewModel Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }

    private string loadingText = "";
    public string LoadingText
    {
        get => loadingText;
        set
        {
            loadingText = value;
            RaisePropertyChanged(nameof(LoadingText));
        }
    }

    private double loadingProgress = 0;
    public double LoadingProgress
    {
        get => loadingProgress;
        set
        {
            loadingProgress = value;
            RaisePropertyChanged(nameof(LoadingProgress));
        }
    }

    private DispatcherTimer? progressAnimationTimer;
    private double progressAnimationStart;
    private double progressAnimationTarget;
    private DateTime progressAnimationStartTime;
    private int progressAnimationDuration;
    private TaskCompletionSource<bool>? progressAnimationTcs;

    private Task AnimateProgress(double targetValue, int durationMs = 500)
    {
        progressAnimationTcs = new TaskCompletionSource<bool>();

        progressAnimationStart = LoadingProgress;
        progressAnimationTarget = targetValue;
        progressAnimationDuration = durationMs;
        progressAnimationStartTime = DateTime.Now;

        if (progressAnimationTimer == null)
        {
            progressAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            progressAnimationTimer.Tick += ProgressAnimationTimer_Tick;
        }

        if (!progressAnimationTimer.IsEnabled)
        {
            progressAnimationTimer.Start();
        }

        return progressAnimationTcs.Task;
    }

    private void ProgressAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.Now - progressAnimationStartTime).TotalMilliseconds;
        var progress = Math.Min(elapsed / progressAnimationDuration, 1.0);

        // Ease out cubic for smoother ending
        var eased = 1 - Math.Pow(1 - progress, 3);

        LoadingProgress = progressAnimationStart + ((progressAnimationTarget - progressAnimationStart) * eased);

        if (progress >= 1.0)
        {
            LoadingProgress = progressAnimationTarget;
            progressAnimationTimer?.Stop();
            progressAnimationTcs?.TrySetResult(true);
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

    private bool finishedWithServerConnectionCheck;
    public bool FinishedWithServerConnectionCheck
    {
        get => finishedWithServerConnectionCheck;
        set
        {
            finishedWithServerConnectionCheck = value;
            RaisePropertyChanged(nameof(FinishedWithServerConnectionCheck));
        }
    }

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
    
    private ImageSource backgroundPicture;
    public ImageSource BackgroundPicture
    {
        get => backgroundPicture;
        set
        {
            backgroundPicture = value;
            RaisePropertyChanged(nameof(BackgroundPicture));
        }
    }
    
    private Visibility windowPosResetText = Visibility.Hidden;
    public Visibility WindowPosResetText
    {
        get => windowPosResetText;
        set
        {
            windowPosResetText = value;
            RaisePropertyChanged(nameof(WindowPosResetText));
        }
    }

    
    public RelayCommand ResetWindowPositionCommand { get; set; }


    public SplashViewModel()
    {
        Instance = this;

        _ = SetAppVersion();

        timerCheckServerConnectionFirstTime.Tick += TimerCheckServerConnectionFirstTime_Tick;
        timerCheckServerConnectionFirstTime.Interval = new TimeSpan(0, 0, 1);

        _ = bool.TryParse(ReadLocalSetting("GlassyEffect"), out bool GlassyEffect);
        CbSettingGlassyEffect = GlassyEffect;

        ResetWindowPositionCommand = new RelayCommand(o => ResetWindowPosition());

        int day = 31;
        day = DateTime.Now.Day;
        BackgroundPicture = new BitmapImage(new Uri($"/Images/Splash/{day}.jpg", UriKind.Relative));
    }

    private void ResetWindowPosition()
    {
        WriteLocalSetting("WindowTop", "10");
        WriteLocalSetting("WindowLeft", "10");

        WriteLocalSetting("WindowWidth", "1120");
        WriteLocalSetting("WindowHeight", "550");

        WindowPosResetText = Visibility.Visible;
    }
    
    private async Task CheckSavedWindowPosition()
    {
        _ = double.TryParse(ReadLocalSetting("WindowTop"), out double WindowTop);
        _ = double.TryParse(ReadLocalSetting("WindowLeft"), out double WindowLeft);

        _ = double.TryParse(ReadLocalSetting("WindowWidth"), out double WindowWidth);
        _ = double.TryParse(ReadLocalSetting("WindowHeight"), out double WindowHeight);

        double screenWidth = SystemParameters.WorkArea.Width;
        double screenHeight = SystemParameters.WorkArea.Height;

        if (WindowTop < 0 ||
            WindowTop > screenHeight ||
            WindowLeft < 0 ||
            WindowLeft > screenWidth)
        {
            WriteLocalSetting("WindowTop", "5");
            WriteLocalSetting("WindowLeft", "5");
            WindowPosResetText = Visibility.Visible;
        }
         
        if (WindowWidth < 1120 || WindowWidth > screenWidth)
            WriteLocalSetting("WindowWidth", "1120");
        
        if (WindowHeight < 550 ||WindowHeight > screenHeight)
            WriteLocalSetting("WindowHeight", "550");

        await Task.Delay(10);
    }



    private async Task SetAppVersion()
    {
        SoftwareVersion = await GetAppVersion();
    }

    private void TimerCheckServerConnectionFirstTime_Tick(object? sender, EventArgs e)
    {
        timerCheckServerConnectionFirstTime.Stop();
        CheckStatsServerConnection();
    }

    internal async void StartLoading()
    {
        await AnimateProgress(20); // Step 1: Starting
        await Task.Run(DatabaseConnection.SetCredentials); // getting credentials for SQL server from BaseSettings.Config file

        await AnimateProgress(50); // Step 2: Credentials loaded
        LoadingText = "Checking local configs..";
        await Task.Run(CreatingLocalConfigFiles); // first try.. initialize database

        await AnimateProgress(80); // Step 3: Local configs checked
        timerCheckServerConnectionFirstTime.Start();
    }

    private async void CheckStatsServerConnection()
    {
        isEverythingOkay = true;
        await Task.Run(CreatingLocalConfigFiles); // double tap.. to make sure database initialized correctly
        LoadingText = "Checking server connection..";
        try
        {
            using (var connection = new SqlConnection(DatabaseConnection.ConnectionStrToStatsDatabase()))
            {
                var query = "select 1";
                var command = new SqlCommand(query, connection);
                connection.Open();
                command.ExecuteScalar();
            }
            FinishedWithServerConnectionCheck = true;
            await AnimateProgress(173, 800); // Step 4: Server connection established (100%)
            LoadingText = "Successfully connected to server!";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
            MainViewModel.Instance?.AddDebugLine(ex);
            if (ex.Message.Contains("Login failed for user"))
            {
                ShowMessageBox("Error", $"{ex.Message}\n\nApplication will shutdown!", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, SplashWindow.Instance);
                SplashWindow.Instance.Close();
                return;
            }
            else
            {
                try
                {
                    DatabaseConnection.StatsdbInstance = "";
                    using (var connection = new SqlConnection(DatabaseConnection.ConnectionStrToStatsDatabase()))
                    {
                        var query = "select 1";
                        var command = new SqlCommand(query, connection);
                        connection.Open();
                        command.ExecuteScalar();
                    }
                    FinishedWithServerConnectionCheck = true;
                    await AnimateProgress(173, 800); // Step 4: Server connection established (100%)
                    LoadingText = "Successfully connected to server!";
                }
                catch (Exception exx)
                {
                    MainViewModel.Instance?.AddDebugLine(exx);
                    Debug.WriteLine($"[{exx.LineNumber()}] {exx.Message}");
                    isEverythingOkay = false;
                    LoadingText = "Couldn't connect to server..";
                    LoadingText = exx.Message;
                }
            }
        }

        FinishedWithServerConnectionCheck = isEverythingOkay;
        AfterServerConnectionChecked();
    }

    private async void AfterServerConnectionChecked()
    {
        if (!isEverythingOkay)
        {
            SMessageBoxResult dg = ShowMessageBox("Error", $"Could not connect to DataBase server!\nServer might be offline or not accessible.", SMessageBoxButtons.TryAgainClose, NotificationIcon.Warning, 15, SplashWindow.Instance);
            if (dg == SMessageBoxResult.TryAgain)
            {
                isEverythingOkay = true;
                CheckStatsServerConnection();
            }
            else if (dg == SMessageBoxResult.Close)
            {
                SplashWindow.Instance.Close();
            }
        }
        else
        {
            await CheckSavedWindowPosition();

            mainWindow = new();
            MainViewModel.StartInitialTasks();
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

        if (MainViewModel.Instance is not null)
            return MainViewModel.Instance.SMessageBoxxResult;
        else
            return SMessageBoxxResult;
    }
}
