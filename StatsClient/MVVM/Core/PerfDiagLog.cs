using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Runtime performance / freeze diagnostics written to
/// %ProgramData%\Stats_Client\perf.log (append, auto-flush).
/// Use alongside startup.log when investigating UI stalls on remote machines.
/// </summary>
public static class PerfDiagLog
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static string? _logPath;
    private static Timer? _uiMonitorTimer;
    private static long _uiBeat;
    private static long _lastUiPingMs;
    private static long _peakUiPingMs;
    private static DateTime _lastMirrorUtc = DateTime.MinValue;

    public const int StallThresholdMs = 400;
    public const int FreezeThresholdMs = 2000;
    public const int SlowOpThresholdMs = 500;

    /// <summary>When perf.log exceeds this size it is rotated to perf.log.old.</summary>
    private const long MaxLogBytes = 2 * 1024 * 1024;

    private static readonly TimeSpan TrimCheckInterval = TimeSpan.FromMinutes(15);
    private static DateTime _lastTrimCheckUtc = DateTime.MinValue;
    private static long _writesSinceTrimCheck;

    public static string LogPath =>
        _logPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Stats_Client",
            "perf.log");

    public static void BeginSession()
    {
        lock (Sync)
        {
            try
            {
                var folder = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(folder);
                MaybeRotateLog(force: true);

                _writer?.Dispose();
                _writer = new StreamWriter(LogPath, append: true, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                var version = typeof(PerfDiagLog).Assembly.GetName().Version;
                _writer.WriteLine();
                _writer.WriteLine(new string('=', 72));
                _writer.WriteLine("=== StatsClient perf session ===");
                _writer.WriteLine($"Started : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                _writer.WriteLine($"Machine : {Environment.MachineName}");
                _writer.WriteLine($"User    : {Environment.UserDomainName}\\{Environment.UserName}");
                _writer.WriteLine($"PID     : {Environment.ProcessId}");
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

    public static void StartUiMonitor(int intervalSeconds = 5)
    {
        if (_uiMonitorTimer is not null)
            return;

        Write("UI", $"Monitor started (interval={intervalSeconds}s, stall>={StallThresholdMs}ms, freeze>={FreezeThresholdMs}ms)");

        _uiMonitorTimer = new Timer(
            _ => ProbeUiThread(intervalSeconds),
            null,
            TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromSeconds(intervalSeconds));
    }

    public static void Write(string category, string message, bool mirrorToDebugTab = false)
    {
        lock (Sync)
        {
            try
            {
                MaybeRotateLog(force: false);

                string line =
                    $"[{DateTime.Now:HH:mm:ss.fff}] [{category,-10}] [T{Environment.CurrentManagedThreadId,3}] {message}";

                _writer?.WriteLine(line);

                if (_writer is null)
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ignore.
            }
        }

        if (mirrorToDebugTab)
            MirrorToDebugTab(category, message);
    }

    public static void WriteSlow(string category, string operation, long elapsedMs, string? detail = null)
    {
        string msg = detail is null
            ? $"SLOW {operation} took {elapsedMs}ms"
            : $"SLOW {operation} took {elapsedMs}ms — {detail}";

        bool severe = elapsedMs >= FreezeThresholdMs;
        Write(category, msg, mirrorToDebugTab: severe);

        if (elapsedMs >= StallThresholdMs && !severe)
            MirrorToDebugTab(category, msg);
    }

    public static void Track(string category, string operation, Action work, string? detail = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            work();
        }
        catch (Exception ex)
        {
            sw.Stop();
            Write(category, $"FAIL {operation} after {sw.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}", mirrorToDebugTab: true);
            throw;
        }

        sw.Stop();
        if (sw.ElapsedMilliseconds >= SlowOpThresholdMs)
            WriteSlow(category, operation, sw.ElapsedMilliseconds, detail);
        else
            Write(category, $"{operation} {sw.ElapsedMilliseconds}ms" + (detail is null ? "" : $" ({detail})"));
    }

    public static T Track<T>(string category, string operation, Func<T> work, string? detail = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return work();
        }
        catch (Exception ex)
        {
            sw.Stop();
            Write(category, $"FAIL {operation} after {sw.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}", mirrorToDebugTab: true);
            throw;
        }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds >= SlowOpThresholdMs)
                WriteSlow(category, operation, sw.ElapsedMilliseconds, detail);
            else
                Write(category, $"{operation} {sw.ElapsedMilliseconds}ms" + (detail is null ? "" : $" ({detail})"));
        }
    }

    public static async Task TrackAsync(string category, string operation, Func<Task> work, string? detail = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Write(category, $"FAIL {operation} after {sw.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}", mirrorToDebugTab: true);
            throw;
        }

        sw.Stop();
        if (sw.ElapsedMilliseconds >= SlowOpThresholdMs)
            WriteSlow(category, operation, sw.ElapsedMilliseconds, detail);
        else
            Write(category, $"{operation} {sw.ElapsedMilliseconds}ms" + (detail is null ? "" : $" ({detail})"));
    }

    public static async Task<T> TrackAsync<T>(string category, string operation, Func<Task<T>> work, string? detail = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Write(category, $"FAIL {operation} after {sw.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}", mirrorToDebugTab: true);
            throw;
        }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds >= SlowOpThresholdMs)
                WriteSlow(category, operation, sw.ElapsedMilliseconds, detail);
            else
                Write(category, $"{operation} {sw.ElapsedMilliseconds}ms" + (detail is null ? "" : $" ({detail})"));
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

    private static void ProbeUiThread(int intervalSeconds)
    {
        _uiBeat++;
        var app = Application.Current;
        if (app is null)
            return;

        long pingStarted = Environment.TickCount64;
        var gate = new ManualResetEventSlim(false);
        Exception? invokeError = null;

        try
        {
            app.Dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        _lastUiPingMs = Environment.TickCount64 - pingStarted;
                        if (_lastUiPingMs > _peakUiPingMs)
                            _peakUiPingMs = _lastUiPingMs;
                    }
                    finally
                    {
                        gate.Set();
                    }
                },
                DispatcherPriority.Send);
        }
        catch (Exception ex)
        {
            invokeError = ex;
            gate.Set();
        }

        bool responded = gate.Wait(TimeSpan.FromSeconds(8));
        long workingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

        if (invokeError is not null)
        {
            Write("UI", $"FREEZE monitor BeginInvoke failed — {invokeError.GetType().Name}: {invokeError.Message}", mirrorToDebugTab: true);
            return;
        }

        if (!responded)
        {
            Write(
                "UI",
                $"FREEZE dispatcher did not respond within 8s (beat #{_uiBeat}, peakPing={_peakUiPingMs}ms, mem={workingSetMb}MB)",
                mirrorToDebugTab: true);
            return;
        }

        if (_lastUiPingMs >= FreezeThresholdMs)
        {
            Write(
                "UI",
                $"FREEZE uiPingMs={_lastUiPingMs} peak={_peakUiPingMs}ms mem={workingSetMb}MB beat=#{_uiBeat}",
                mirrorToDebugTab: true);
        }
        else if (_lastUiPingMs >= StallThresholdMs)
        {
            Write(
                "UI",
                $"STALL uiPingMs={_lastUiPingMs} peak={_peakUiPingMs}ms mem={workingSetMb}MB beat=#{_uiBeat}",
                mirrorToDebugTab: true);
        }
        else if (_uiBeat % 12 == 0)
        {
            Write("UI", $"OK beat=#{_uiBeat} uiPingMs={_lastUiPingMs} peak={_peakUiPingMs}ms mem={workingSetMb}MB interval={intervalSeconds}s");
        }
    }

    private static void MaybeRotateLog(bool force)
    {
        _writesSinceTrimCheck++;
        if (!force)
        {
            bool dueForCheck = DateTime.UtcNow - _lastTrimCheckUtc >= TrimCheckInterval
                               || _writesSinceTrimCheck >= 250;
            if (!dueForCheck)
                return;
        }

        _lastTrimCheckUtc = DateTime.UtcNow;
        _writesSinceTrimCheck = 0;

        try
        {
            if (!File.Exists(LogPath))
                return;

            long length = new FileInfo(LogPath).Length;
            if (!force && length <= MaxLogBytes)
                return;

            if (force && length <= MaxLogBytes)
                return;

            RotateLogFileLocked(length);
        }
        catch
        {
            // Ignore.
        }
    }

    private static void RotateLogFileLocked(long previousLengthBytes)
    {
        _writer?.Dispose();
        _writer = null;

        string archive = LogPath + ".old";
        try
        {
            if (File.Exists(archive))
                File.Delete(archive);

            if (File.Exists(LogPath))
                File.Move(LogPath, archive);
        }
        catch
        {
            return;
        }

        _writer = new StreamWriter(LogPath, append: false, Encoding.UTF8)
        {
            AutoFlush = true
        };

        double previousKb = previousLengthBytes / 1024.0;
        _writer.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [PerfDiag  ] Log rotated — previous {previousKb:0} KB archived to perf.log.old");
        _writer.WriteLine(new string('-', 72));
    }

    public static void EndSession()
    {
        lock (Sync)
        {
            try
            {
                _uiMonitorTimer?.Dispose();
                _uiMonitorTimer = null;
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
            catch
            {
                // Ignore.
            }
        }
    }

    private static void MirrorToDebugTab(string category, string message)
    {
        if ((DateTime.UtcNow - _lastMirrorUtc).TotalSeconds < 2)
            return;

        _lastMirrorUtc = DateTime.UtcNow;

        try
        {
            var vm = ViewModel.MainViewModel.Instance;
            if (vm is null)
                return;

            Application.Current?.Dispatcher.BeginInvoke(() =>
                vm.AddPerfDiagMirrorLine(category, message), DispatcherPriority.Background);
        }
        catch
        {
            // Ignore.
        }
    }
}
