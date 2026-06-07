using System.IO;
using System.Text;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Fresh startup trace written to %ProgramData%\Stats_Client\startup.log on each launch.
/// Uses an auto-flushing writer so remote machines still get lines if the process hangs.
/// </summary>
public static class StartupLog
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static string? _logPath;

    public static string LogPath =>
        _logPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Stats_Client",
            "startup.log");

    public static void BeginSession()
    {
        lock (Sync)
        {
            try
            {
                var folder = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(folder);

                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }

                _writer?.Dispose();
                _writer = new StreamWriter(LogPath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                var version = typeof(StartupLog).Assembly.GetName().Version;
                _writer.WriteLine("=== StatsClient startup log ===");
                _writer.WriteLine($"Started : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                _writer.WriteLine($"Machine : {Environment.MachineName}");
                _writer.WriteLine($"User    : {Environment.UserDomainName}\\{Environment.UserName}");
                _writer.WriteLine($"OS      : {Environment.OSVersion}");
                _writer.WriteLine($".NET    : {Environment.Version}");
                _writer.WriteLine($"Version : {version}");
                _writer.WriteLine($"LogPath : {LogPath}");
                _writer.WriteLine(new string('-', 72));
            }
            catch
            {
                // Never crash the app over logging.
            }
        }
    }

    public static void WriteStep(string message) => WriteLine("STEP", message);

    public static void WriteDetail(string area, string message) =>
        WriteLine("DETAIL", $"[{area}] {message}");

    public static void WritePhase(string phase, string message) =>
        WriteLine("PHASE", $"{phase} | {message}");

    public static void WriteError(string message, Exception? ex = null)
    {
        WriteLine("ERROR", message);
        if (ex is null)
        {
            return;
        }

        WriteLine("ERROR", $"  {ex.GetType().Name}: {ex.Message}");
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            foreach (string line in ex.StackTrace.Split(Environment.NewLine))
            {
                WriteLine("ERROR", $"  {line}");
            }
        }

        if (ex.InnerException is not null)
        {
            WriteLine("ERROR", $"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
    }

    public static void Flush()
    {
        lock (Sync)
        {
            try
            {
                _writer?.Flush();
            }
            catch
            {
                // Ignore.
            }
        }
    }

    private static void WriteLine(string level, string message)
    {
        lock (Sync)
        {
            try
            {
                string line =
                    $"[{DateTime.Now:HH:mm:ss.fff}] [{level,-5}] [T{Environment.CurrentManagedThreadId,3}] {message}";

                _writer?.WriteLine(line);

                if (_writer is null)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Ignore log write failures.
            }
        }
    }
}
