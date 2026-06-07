using StatsClient.MVVM.Model;
using System.IO;

namespace StatsClient.MVVM.Core;

/// <summary>
/// In-memory watch list backed by <see cref="LocalSettingsDB"/> (SQLite Settings.Config24).
/// </summary>
public static class WatchListStore
{
    private static readonly object Gate = new();
    private static List<WatchListEntry> _entries = [];

    public static event Action? Changed;

    public static IReadOnlyList<WatchListEntry> Entries
    {
        get
        {
            lock (Gate)
            {
                return _entries.ToList();
            }
        }
    }

    public static void Load()
    {
        lock (Gate)
        {
            _entries = LocalSettingsDB.LoadWatchListEntries();
            TryMigrateLegacyJsonFile();
        }
    }

    private static void TryMigrateLegacyJsonFile()
    {
        if (_entries.Count > 0)
            return;

        string legacyPath = Path.Combine(LocalSettingsDB.DataBaseFolder, "watchlist.json");
        if (!File.Exists(legacyPath))
            return;

        try
        {
            var json = File.ReadAllText(legacyPath);
            var imported = System.Text.Json.JsonSerializer.Deserialize<List<WatchListEntry>>(json);
            if (imported is null || imported.Count == 0)
                return;

            foreach (var entry in imported.Where(e => !string.IsNullOrWhiteSpace(e.IntOrderID)))
            {
                _entries.Add(entry);
                LocalSettingsDB.UpsertWatchListEntry(entry);
            }

            File.Delete(legacyPath);
        }
        catch
        {
            // keep legacy file if migration fails
        }
    }

    public static bool Contains(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return false;

        lock (Gate)
        {
            return _entries.Any(e => string.Equals(e.IntOrderID, orderId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static WatchListEntry? Find(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return null;

        lock (Gate)
        {
            return _entries.FirstOrDefault(e =>
                string.Equals(e.IntOrderID, orderId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static void AddOrUpdate(WatchListEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.IntOrderID))
            return;

        lock (Gate)
        {
            var existing = _entries.FirstOrDefault(e =>
                string.Equals(e.IntOrderID, entry.IntOrderID, StringComparison.OrdinalIgnoreCase));
            WatchListEntry persisted;
            if (existing is null)
            {
                if (entry.AddedUtc == default)
                    entry.AddedUtc = DateTime.UtcNow;
                _entries.Add(entry);
                persisted = entry;
            }
            else
            {
                existing.LastProcessStatusID = entry.LastProcessStatusID ?? existing.LastProcessStatusID;
                existing.LastProcessLockID = entry.LastProcessLockID ?? existing.LastProcessLockID;
                existing.Patient_FirstName = entry.Patient_FirstName ?? existing.Patient_FirstName;
                existing.Patient_LastName = entry.Patient_LastName ?? existing.Patient_LastName;
                existing.Customer = entry.Customer ?? existing.Customer;
                existing.Items = entry.Items ?? existing.Items;
                existing.PanNumber = entry.PanNumber ?? existing.PanNumber;
                existing.ImageSource = entry.ImageSource ?? existing.ImageSource;
                persisted = existing;
            }

            LocalSettingsDB.UpsertWatchListEntry(persisted);
        }

        Changed?.Invoke();
    }

    public static void Remove(string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return;

        lock (Gate)
        {
            _entries.RemoveAll(e =>
                string.Equals(e.IntOrderID, orderId, StringComparison.OrdinalIgnoreCase));
        }

        LocalSettingsDB.RemoveWatchListEntry(orderId);
        Changed?.Invoke();
    }

    public static int PurgeOlderThanDays(int days)
    {
        if (days <= 0)
            return 0;

        int removedDb = LocalSettingsDB.PurgeWatchListEntriesOlderThanDays(days);

        lock (Gate)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            int removedMem = _entries.RemoveAll(e => e.AddedUtc < cutoff);
            removedDb = Math.Max(removedDb, removedMem);
        }

        if (removedDb > 0)
            Changed?.Invoke();

        return removedDb;
    }
}
