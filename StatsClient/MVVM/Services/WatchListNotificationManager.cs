using StatsClient.MVVM.View;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace StatsClient.MVVM.Services;

public sealed class WatchListStatusChange
{
    public required string IntOrderID { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    /// <summary>Status transition line (e.g. Designed → Scanned). When set, shown muted above patient name.</summary>
    public string? StatusDetail { get; init; }
    public required string AccentColor { get; init; }
    public string? PatientName { get; init; }
    public string? PanNumber { get; init; }
}

public static class WatchListNotificationManager
{
    private static WatchListNotificationHost? _host;
    private static readonly object Gate = new();

    public static void Show(WatchListStatusChange change)
    {
        if (Application.Current is null)
        {
            Debug.WriteLine("[WatchList] Notification skipped — Application.Current is null.");
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            try
            {
                lock (Gate)
                {
                    _host ??= new WatchListNotificationHost();
                    _host.ShowChange(change);
                }

                Debug.WriteLine($"[WatchList] Notification shown: {change.Title} ({change.IntOrderID})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WatchList] Notification failed: {ex.Message}");
            }
        });
    }

    public static void Shutdown()
    {
        WatchListNotificationHost? host;
        lock (Gate)
        {
            host = _host;
            _host = null;
        }

        if (host is null)
        {
            return;
        }

        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                host.Close();
            }
            else
            {
                dispatcher.Invoke(host.Close);
            }
        }
        catch
        {
        }
    }
}
