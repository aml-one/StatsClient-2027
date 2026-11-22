using Microsoft.Data.SqlClient;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core;

public readonly record struct WatchListOrderStatus(
    string IntOrderID,
    string? ProcessStatusID,
    string? ProcessLockID,
    string? ModelHeight = null,
    string? OriginalOrderID = null,
    string? ElementProcessStatusID = null,
    string? OrderComments = null);

public static class WatchListStatusQuery
{
    private const int ChunkSize = 400;

    public static List<WatchListOrderStatus> QueryStatuses(IReadOnlyList<string> orderIds)
        => QueryStatusesWithResult(orderIds).Statuses;

    public static (List<WatchListOrderStatus> Statuses, bool Succeeded) QueryStatusesWithResult(IReadOnlyList<string> orderIds)
    {
        var results = new List<WatchListOrderStatus>();
        if (orderIds.Count == 0)
            return (results, true);

        var distinct = orderIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool succeeded = true;
        for (int offset = 0; offset < distinct.Count; offset += ChunkSize)
        {
            var chunk = distinct.Skip(offset).Take(ChunkSize).ToList();
            var (chunkResults, chunkSucceeded) = QueryChunk(chunk);
            if (!chunkSucceeded)
                succeeded = false;
            results.AddRange(chunkResults);
        }

        return (results, succeeded);
    }

    private static (List<WatchListOrderStatus> Results, bool Succeeded) QueryChunk(List<string> orderIds)
    {
        if (orderIds.Count == 0)
            return ([], true);

        string inClause = string.Join(",", orderIds.Select((_, i) => $"@id{i}"));
        string query = $@"
SELECT o.IntOrderID,
       i.MaxProcessStatusID,
       me.ProcessStatusID,
       me.ProcessLockID,
       me.ModelHeight,
       o.OriginalOrderID,
       o.OrderComments
FROM Orders o
INNER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
LEFT JOIN ModelJob m ON m.OrderID = o.IntOrderID
LEFT JOIN ModelElement me ON me.ModelJobID = m.ModelJobID
WHERE o.IntOrderID IN ({inClause})";

        var raw = new List<WatchListOrderStatus>();

        try
        {
            using var connection = new SqlConnection(ConnectionStrFor3Shape());
            using var command = new SqlCommand(query, connection);
            for (int i = 0; i < orderIds.Count; i++)
                command.Parameters.AddWithValue($"@id{i}", orderIds[i]);

            connection.Open();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                raw.Add(new WatchListOrderStatus(
                    reader["IntOrderID"]?.ToString() ?? "",
                    reader["MaxProcessStatusID"]?.ToString(),
                    reader["ProcessLockID"]?.ToString(),
                    reader["ModelHeight"]?.ToString(),
                    reader["OriginalOrderID"]?.ToString(),
                    reader["ProcessStatusID"]?.ToString(),
                    reader["OrderComments"]?.ToString()));
            }
        }
        catch
        {
            return ([], false);
        }

        return (raw
            .Where(r => !string.IsNullOrWhiteSpace(r.IntOrderID))
            .GroupBy(r => r.IntOrderID, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var rows = g.ToList();
                var first = rows[0];

                string status = WatchListOrderDetailsQuery.ResolveEffectiveProcessStatus(
                    first.ProcessStatusID,
                    rows.Select(r => r.ElementProcessStatusID));
                string processLock = WatchListOrderDetailsQuery.NormalizeProcessLock(
                    WatchListOrderDetailsQuery.PickDisplayProcessLock(rows.Select(r => r.ProcessLockID)));

                return first with
                {
                    ProcessStatusID = status,
                    ProcessLockID = processLock,
                };
            })
            .ToList(), true);
    }
}
