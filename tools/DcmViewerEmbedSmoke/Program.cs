using System.IO;
using System.Windows;
using System.Windows.Threading;
using DCMViewer.Controls;
using StatsClient.MVVM.Core;

namespace DcmViewerEmbedSmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "dcm-embed-smoke.log");
        File.WriteAllText(logPath, $"[{DateTime.Now:O}] Starting smoke test\r\n");

        void Log(string message)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}\r\n");
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log($"Unhandled: {args.ExceptionObject}");
        };

        try
        {
            var app = new Application();
            app.DispatcherUnhandledException += (_, args) =>
            {
                Log($"Dispatcher: {args.Exception}");
                args.Handled = true;
            };
            var window = new Window
            {
                Title = "DCM embed smoke",
                Width = 1200,
                Height = 800,
                Background = System.Windows.Media.Brushes.LightGray
            };

            var viewer = new DcmViewerCanvasComponent
            {
                UseFullAppShell = true,
                IsBackgroundTransparent = true
            };

            window.Content = viewer;
            window.Loaded += async (_, _) =>
            {
                try
                {
                    Log("Loaded - embedding viewer");
                    var docs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Docs"));
                    var scan = Path.Combine(docs, "scan.dcm");
                    var teeth = Path.Combine(docs, "teeth.dcm");
                    var files = new List<DCMFileItem>();
                    if (File.Exists(scan))
                    {
                        files.Add(new DCMFileItem { FilePath = scan, GroupName = "LOWER" });
                    }

                    if (File.Exists(teeth))
                    {
                        files.Add(new DCMFileItem { FilePath = teeth, GroupName = "RESTORATION" });
                    }

                    Log($"Loading {files.Count} file(s)");
                    await viewer.LoadCaseFilesAsync(files);
                    Log("Load complete");

                    _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                    {
                        window.Close();
                    }, DispatcherPriority.ApplicationIdle);
                }
                catch (Exception ex)
                {
                    Log($"Load failed: {ex}");
                    window.Close();
                }
            };

            window.Closed += (_, _) =>
            {
                Log("Window closed");
                app.Shutdown();
            };

            window.Show();
            Log("Show complete - running");
            app.Run();
            Log("App exit OK");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"Fatal: {ex}");
            return 1;
        }
    }
}
