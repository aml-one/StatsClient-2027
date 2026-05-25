using IWshRuntimeLibrary;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using File = System.IO.File;
using Path = System.IO.Path;

namespace StatsClientUpdater;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public static readonly string LocalConfigFolderHelper = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Stats_Client\";
    public System.Timers.Timer _timer;
    public int AppStartTryCount = 0;
    private static readonly string ProgramFiles = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
    private string appPath = @$"{Path.Combine(ProgramFiles, "AmL", "StatsClient")}\";

    private ResourceDictionary lang = [];
    public ResourceDictionary Lang
    {
        get => lang;
        set
        {
            lang = value;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
    }

    private double progressValue;
    public double ProgressValue
    {
        get => progressValue;
        set
        {
            progressValue = value;
            RaisePropertyChanged(nameof(ProgressValue));
            if (value == 100)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tbStatus.Text = "Download complete..";
                });
                GoToUpzip();
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        SetLanguageDictionary();

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += Timer_Elapsed;
    }

    public void SetLanguageDictionary(string language = "")
    {
        if (language.Equals(""))
        {
            Lang.Source = Thread.CurrentThread.CurrentCulture.ToString() switch
            {
                "en-US" => new Uri("\\Lang\\StringResources_English.xaml", UriKind.Relative),
                "zh-Hans" => new Uri("\\Lang\\StringResources_Chinese.xaml", UriKind.Relative),
                "zh-Hant" => new Uri("\\Lang\\StringResources_Chinese.xaml", UriKind.Relative),
                "zh-CHT" => new Uri("\\Lang\\StringResources_Chinese.xaml", UriKind.Relative),
                "zh-CN" => new Uri("\\Lang\\StringResources_Chinese.xaml", UriKind.Relative),
                "zh-CHS" => new Uri("\\Lang\\StringResources_Chinese.xaml", UriKind.Relative),
                "zh-HK" => new Uri("\\Lang\\StringResources_Chinese.xaml", UriKind.Relative),
                _ => new Uri("\\Lang\\StringResources_English.xaml", UriKind.Relative),
            };
        }
        else
        {
            try
            {
                Lang.Source = new Uri("\\Lang\\StringResources_" + language + ".xaml", UriKind.Relative);
            }
            catch (IOException)
            {
                Lang.Source = new Uri("\\Lang\\StringResources_English.xaml", UriKind.Relative);
            }
        }

        this.Resources.MergedDictionaries.Add(lang);
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        Debug.WriteLine(appPath);
        _timer.Stop();
        StopMainApp();
    }

    private async void StopMainApp()
    {
        var Processes = Process.GetProcesses()
                           .Where(pr => pr.ProcessName == "StatsClient");
        foreach (var process in Processes)
        {
            process.Kill();
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            closeButton.Visibility = Visibility.Hidden;
            tbStatus.Text = "Please wait..";
        });


        Task.Run(DownloadUpdate).Wait(new TimeSpan(0, 0, 2));
    }

    private async void DownloadUpdate()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Cursor = Cursors.Wait;
        });
        Thread.Sleep(1000);

        if (!Directory.Exists(appPath))
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tbStatus.Text = "Creating directory..";
                });
                Directory.CreateDirectory(appPath);
            }
            catch (Exception)
            {
                appPath = Environment.SpecialFolder.Desktop.ToString();
            }
        }

        try
        {
            Thread.Sleep(500);
            if (File.Exists($@"{LocalConfigFolderHelper}StatsClient_old.exe"))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tbStatus.Text = "Cleaning environment..";
                });
                File.Delete($@"{LocalConfigFolderHelper}StatsClient_old.exe");
            }
            Thread.Sleep(500);
            if (File.Exists($@"{appPath}\StatsClient.exe"))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    tbStatus.Text = "Creating backup..";
                });
                File.Move($@"{appPath}\StatsClient.exe", $@"{LocalConfigFolderHelper}StatsClient_old.exe");
            }
            Thread.Sleep(500);
            if (File.Exists($@"{LocalConfigFolderHelper}StatsClient.zip"))
            {
                File.Delete($@"{LocalConfigFolderHelper}StatsClient.zip");
            }
            Thread.Sleep(1000);

            Application.Current.Dispatcher.Invoke(() =>
            {
                tbStatus.Text = "Downloading update..";
            });
            var handler = new HttpClientHandler() { AllowAutoRedirect = false };
            var ph = new ProgressMessageHandler(handler);

            ph.HttpReceiveProgress += (_, args) =>
            {
                ProgressValue = args.ProgressPercentage;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    progressBar.Value = args.ProgressPercentage;
                    tbStatus.Text = $"Downloading update - {args.ProgressPercentage}%";
                });

                Debug.WriteLine($"download progress: {((double)args.BytesTransferred / args.TotalBytes) * 100}%");
            };

            using var client = new HttpClient(ph);
            using var s = await client.GetStreamAsync("https://raw.githubusercontent.com/aml-one/StatsClient-2027/master/StatsClient/Executable/StatsClient.zip");
            using var fs = new FileStream($@"{LocalConfigFolderHelper}StatsClient.zip", FileMode.OpenOrCreate);
            await s.CopyToAsync(fs);            
        }
        catch (Exception exx)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Cursor = Cursors.Arrow;
                closeButton.Visibility = Visibility.Visible;
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, exx.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
            if (File.Exists($@"{LocalConfigFolderHelper}StatsClient_old.exe"))
                File.Move($@"{LocalConfigFolderHelper}StatsClient_old.exe", $@"{appPath}StatsClient.exe");
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            tbStatus.Text = "Finalizing update..";
        });

        Thread.Sleep(3000);
    }

    private async Task GoToUpzip()
    {
        await Task.Delay(3000);

        await Application.Current.Dispatcher.Invoke(async () =>
        {
            int i = 0;
            unzip:
            i++;
            tbStatus.Text = "Unpacking files..";
            try
            {

                await Task.Run(() =>  ZipFile.ExtractToDirectory($@"{LocalConfigFolderHelper}StatsClient.zip", appPath, true));
            }
            catch (Exception ex)
            {
                tbStatus.Text = "An error occured: " + ex.Message;
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                if (File.Exists($@"{LocalConfigFolderHelper}StatsClient_old.exe"))
                    File.Copy($@"{LocalConfigFolderHelper}StatsClient_old.exe", $@"{appPath}StatsClient.exe");

                await Task.Delay(1000);
                if (i < 10)
                    goto unzip;
            }
        });




        Application.Current.Dispatcher.Invoke(() =>
        {
            tbStatus.Text = "Creating shortcut..";
            Cursor = Cursors.Arrow;
        });
        CreateShortcut(appPath);
    }

    private async void DownloadComplete()
    {
        await Task.Delay(5000);
        Application.Current.Dispatcher.Invoke(() =>
        {
            tbStatus.Text = "Starting application";
        });
        Thread.Sleep(500);

        StartCaseApp();
    }

    private void CreateShortcut(string appFolder)
    {
        object shDesktop = (object)"Desktop";
        WshShell shell = new();
        string oldShortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @"\Stats Client 2025.lnk";
        string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @"\Stats 2027.lnk";
        IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
        shortcut.Description = "Stats 2027";
        shortcut.Hotkey = "Ctrl+Shift+S";
        shortcut.TargetPath = @$"{appFolder}StatsClient.exe";
        shortcut.WorkingDirectory = appFolder;
        shortcut.Save();

        if (File.Exists(oldShortcutAddress))
            File.Delete(oldShortcutAddress);

        DownloadComplete();
    }

    private void StartCaseApp()
    {
        AppStartTryCount++;
        Thread.Sleep(1000);
        try
        {
            var p = new Process();

            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = $"/c \"{appPath}StatsClient.exe\"";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            Debug.WriteLine($"{appPath}StatsClient.exe");

            Thread.Sleep(2000);
            CloseThisApp();
        }
        catch (Exception)
        {
            MessageBox.Show((string)Lang["couldNotStart"], (string)Lang["error"], MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!CheckIfAppIsRunning() && AppStartTryCount < 4)
            StartCaseApp();
    }

    private static bool CheckIfAppIsRunning()
    {
        var Processes = Process.GetProcesses()
                           .Where(pr => pr.ProcessName == "StatsClient");
        foreach (var process in Processes)
        {
            if (process.Id > 0)
                return true;
        }

        return false;
    }


    private static void CloseThisApp()
    {
        Thread.Sleep(1000);
        Environment.Exit(0);
    }

    private void Label_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show((string)Lang["closeMessage"], (string)Lang["caseCheckerUpdater"], MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            if (File.Exists($@"{LocalConfigFolderHelper}StatsClient_old.exe"))
                Task.Run(() => File.Move($@"{LocalConfigFolderHelper}StatsClient_old.exe", $@"{appPath}StatsClient.exe")).Wait();
            Close();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
    }
}