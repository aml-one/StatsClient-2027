using Microsoft.Data.SqlClient;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core;

public readonly record struct WatchListOrderStatus(
    string IntOrderID,
    string? ProcessStatusID,
    string? ProcessLockID);

public static class WatchListStatusQuery
{
    private const int ChunkSize = 400;

    public static List<WatchListOrderStatus> QueryStatuses(IReadOnlyList<string> orderIds)
    {
        var results = new List<WatchListOrderStatus>();
        if (orderIds.Count == 0)
            return results;

        var distinct = orderIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int offset = 0; offset < distinct.Count; offset += ChunkSize)
        {
            var chunk = distinct.Skip(offset).Take(ChunkSize).ToList();
            results.AddRange(QueryChunk(chunk));
        }

        return results;
    }

    private static List<WatchListOrderStatus> QueryChunk(List<string> orderIds)
    {
        if (orderIds.Count == 0)
            return [];

        string inClause = string.Join(",", orderIds.Select((_, i) => $"@id{i}"));
        string query = $@"
SELECT o.IntOrderID,
       i.MaxProcessStatusID,
       me.ProcessLockID
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
                    reader["ProcessLockID"]?.ToString()));
            }
        }
        catch
        {
            return [];
        }

        return raw
            .Where(r => !string.IsNullOrWhiteSpace(r.IntOrderID))
            .GroupBy(r => r.IntOrderID, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                string? processLock = g.Select(x => x.ProcessLockID).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                return first with { ProcessLockID = processLock ?? first.ProcessLockID };
            })
            .ToList();
    }
}
