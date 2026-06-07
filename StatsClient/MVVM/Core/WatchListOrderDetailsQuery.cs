using Microsoft.Data.SqlClient;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core;

public sealed class WatchListOrderDetailsRow
{
    public string IntOrderID { get; init; } = "";
    public string? Patient_FirstName { get; init; }
    public string? Patient_LastName { get; init; }
    public string? Customer { get; init; }
    public string? Items { get; init; }
    public string? ExtOrderID { get; init; }
    public string? MaxProcessStatusID { get; init; }
    public string? ProcessLockID { get; init; }
    public string? ModificationDate { get; init; }
    public string? MaxCreateDate { get; init; }
    public string? ScanSource { get; init; }
    public string? UserID { get; init; }
    public string? Shade { get; init; }
}

public static class WatchListOrderDetailsQuery
{
    private const int ChunkSize = 200;

    public static Dictionary<string, WatchListOrderDetailsRow> QueryByOrderIds(IReadOnlyList<string> orderIds)
    {
        var map = new Dictionary<string, WatchListOrderDetailsRow>(StringComparer.OrdinalIgnoreCase);
        if (orderIds.Count == 0)
            return map;

        var distinct = orderIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int offset = 0; offset < distinct.Count; offset += ChunkSize)
        {
            var chunk = distinct.Skip(offset).Take(ChunkSize).ToList();
            foreach (var row in QueryChunk(chunk))
            {
                if (!string.IsNullOrWhiteSpace(row.IntOrderID))
                    map[row.IntOrderID] = row;
            }
        }

        return map;
    }

    private static List<WatchListOrderDetailsRow> QueryChunk(List<string> orderIds)
    {
        if (orderIds.Count == 0)
            return [];

        string inClause = string.Join(",", orderIds.Select((_, i) => $"@id{i}"));
        const string query = @"
SELECT o.IntOrderID,
       o.Patient_FirstName,
       o.Patient_LastName,
       o.Customer,
       o.Items,
       o.ExtOrderID,
       o.ScanSource,
       i.MaxProcessStatusID,
       i.MaxCreateDate,
       i.ModificationDate,
       me.ProcessLockID,
       oh.UserID
FROM Orders o
INNER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
LEFT JOIN ModelJob m ON m.OrderID = o.IntOrderID
LEFT JOIN ModelElement me ON me.ModelJobID = m.ModelJobID
LEFT JOIN (
    SELECT OrderID, MAX(UserID) AS UserID
    FROM OrderHistory
    GROUP BY OrderID
) oh ON oh.OrderID = o.IntOrderID
WHERE o.IntOrderID IN ({0})";

        var list = new List<WatchListOrderDetailsRow>();

        try
        {
            using var connection = new SqlConnection(ConnectionStrFor3Shape());
            using var command = new SqlCommand(string.Format(query, inClause), connection);
            for (int i = 0; i < orderIds.Count; i++)
                command.Parameters.AddWithValue($"@id{i}", orderIds[i]);

            connection.Open();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new WatchListOrderDetailsRow
                {
                    IntOrderID = reader["IntOrderID"]?.ToString() ?? "",
                    Patient_FirstName = reader["Patient_FirstName"]?.ToString(),
                    Patient_LastName = reader["Patient_LastName"]?.ToString(),
                    Customer = reader["Customer"]?.ToString(),
                    Items = reader["Items"]?.ToString(),
                    ExtOrderID = reader["ExtOrderID"]?.ToString(),
                    ScanSource = reader["ScanSource"]?.ToString(),
                    MaxProcessStatusID = reader["MaxProcessStatusID"]?.ToString(),
                    MaxCreateDate = reader["MaxCreateDate"]?.ToString(),
                    ModificationDate = reader["ModificationDate"]?.ToString(),
                    ProcessLockID = reader["ProcessLockID"]?.ToString(),
                    UserID = reader["UserID"]?.ToString(),
                });
            }
        }
        catch
        {
            return [];
        }

        return list
            .Where(r => !string.IsNullOrWhiteSpace(r.IntOrderID))
            .GroupBy(r => r.IntOrderID, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}
