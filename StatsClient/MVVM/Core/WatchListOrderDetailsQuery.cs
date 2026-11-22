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
    public string? ElementProcessStatusID { get; init; }
    public string? ProcessLockID { get; init; }
    public string? ModelHeight { get; init; }
    public string? OriginalOrderID { get; init; }
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
       o.OriginalOrderID,
       oh.ModificationDate,
       me.ProcessStatusID,
       me.ProcessLockID,
       me.ModelHeight,
       oh.UserID
FROM Orders o
INNER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
LEFT JOIN ModelJob m ON m.OrderID = o.IntOrderID
LEFT JOIN ModelElement me ON me.ModelJobID = m.ModelJobID
OUTER APPLY (
    SELECT TOP 1 h.UserID, h.ModificationDate
    FROM OrderHistoryView h
    WHERE h.OrderID = o.IntOrderID
    ORDER BY h.ModificationDate DESC
) oh
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
                    OriginalOrderID = reader["OriginalOrderID"]?.ToString(),
                    ModificationDate = reader["ModificationDate"]?.ToString(),
                    ElementProcessStatusID = reader["ProcessStatusID"]?.ToString(),
                    ProcessLockID = reader["ProcessLockID"]?.ToString(),
                    ModelHeight = reader["ModelHeight"]?.ToString(),
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
            .Select(MergeOrderDetailsRows)
            .ToList();
    }

    private static WatchListOrderDetailsRow MergeOrderDetailsRows(IGrouping<string, WatchListOrderDetailsRow> group)
    {
        var rows = group.ToList();
        var first = rows[0];

        double maxModelHeight = rows
            .Select(r => ParseModelHeight(r.ModelHeight))
            .DefaultIfEmpty(0d)
            .Max();

        string status = ResolveEffectiveProcessStatus(first.MaxProcessStatusID, rows.Select(r => r.ElementProcessStatusID));
        string processLock = NormalizeProcessLock(PickDisplayProcessLock(rows.Select(r => r.ProcessLockID)));

        return new WatchListOrderDetailsRow
        {
            IntOrderID = first.IntOrderID,
            Patient_FirstName = first.Patient_FirstName,
            Patient_LastName = first.Patient_LastName,
            Customer = first.Customer,
            Items = first.Items,
            ExtOrderID = first.ExtOrderID,
            ScanSource = first.ScanSource,
            MaxProcessStatusID = status,
            MaxCreateDate = first.MaxCreateDate,
            OriginalOrderID = first.OriginalOrderID,
            ModificationDate = first.ModificationDate,
            ProcessLockID = processLock,
            ModelHeight = maxModelHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
            UserID = first.UserID,
            Shade = first.Shade,
        };
    }

    internal static double ParseModelHeight(string? modelHeight) =>
        double.TryParse(modelHeight, out double height) ? height : 0d;

    /// <summary>
    /// Case is designed when any ModelElement is psModelled, even if others are still psScanned.
    /// </summary>
    internal static bool ResolveIsDesigned(IReadOnlyList<WatchListOrderDetailsRow> rows) =>
        rows.Any(r => string.Equals(r.ElementProcessStatusID, "psModelled", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Case process status from elements: any psModelled wins; otherwise fall back to OrdersInfo.
    /// </summary>
    internal static string ResolveEffectiveProcessStatus(string? ordersInfoStatus, IEnumerable<string?> elementStatuses)
    {
        var elements = elementStatuses
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();

        if (elements.Exists(s => string.Equals(s, "psModelled", StringComparison.OrdinalIgnoreCase)))
            return "psModelled";

        if (elements.Exists(s => string.Equals(s, "psSent", StringComparison.OrdinalIgnoreCase)))
            return "psSent";

        if (elements.Exists(s => string.Equals(s, "psScanned", StringComparison.OrdinalIgnoreCase)))
            return "psScanned";

        return string.IsNullOrWhiteSpace(ordersInfoStatus) ? "psCreated" : ordersInfoStatus;
    }

    /// <summary>
    /// Case is checked out when any ModelElement is plCheckedOut.
    /// </summary>
    internal static string? PickDisplayProcessLock(IEnumerable<string?> locks)
    {
        var distinct = locks
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
            return null;

        if (distinct.Exists(l => string.Equals(l, "plCheckedOut", StringComparison.OrdinalIgnoreCase)))
            return "plCheckedOut";

        if (distinct.Exists(l => string.Equals(l, "plLocked", StringComparison.OrdinalIgnoreCase)))
            return "plLocked";

        if (distinct.Exists(l => string.Equals(l, "plSent", StringComparison.OrdinalIgnoreCase)))
            return "plSent";

        return distinct[0];
    }

    internal static string NormalizeProcessLock(string? processLock) =>
        string.IsNullOrWhiteSpace(processLock) ? "plReady" : processLock;
}
