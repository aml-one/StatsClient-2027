using Microsoft.Data.SqlClient;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core;

public sealed class WatchListRushScanCandidate
{
    public string IntOrderID { get; init; } = "";
    public string? OrderComments { get; init; }
    public string? MaxProcessStatusID { get; init; }
    public string? ProcessLockID { get; init; }
    public string? Patient_FirstName { get; init; }
    public string? Patient_LastName { get; init; }
    public string? Customer { get; init; }
    public string? Items { get; init; }
}

/// <summary>
/// Finds today/yesterday psScanned orders with rush keywords in <see cref="WatchListRushScanCandidate.OrderComments"/>.
/// </summary>
public static class WatchListRushScanQuery
{
    public static List<WatchListRushScanCandidate> QueryTodayAndYesterdayScanned(
        string createDateFromInclusive,
        string createDateToInclusive)
    {
        var raw = new List<WatchListRushScanCandidate>();

        const string sql = @"
SELECT o.IntOrderID,
       o.OrderComments,
       i.MaxProcessStatusID,
       me.ProcessLockID,
       o.Patient_FirstName,
       o.Patient_LastName,
       o.Customer,
       o.Items
FROM Orders o
INNER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID
LEFT JOIN ModelJob m ON m.OrderID = o.IntOrderID
LEFT JOIN ModelElement me ON me.ModelJobID = m.ModelJobID
WHERE i.MaxCreateDate > @fromDate
  AND i.MaxCreateDate < @toDate
  AND i.MaxProcessStatusID = 'psScanned'";

        try
        {
            using var connection = new SqlConnection(ConnectionStrFor3Shape());
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@fromDate", createDateFromInclusive);
            command.Parameters.AddWithValue("@toDate", createDateToInclusive);
            connection.Open();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                raw.Add(new WatchListRushScanCandidate
                {
                    IntOrderID = reader["IntOrderID"]?.ToString() ?? "",
                    OrderComments = reader["OrderComments"]?.ToString(),
                    MaxProcessStatusID = reader["MaxProcessStatusID"]?.ToString(),
                    ProcessLockID = reader["ProcessLockID"]?.ToString(),
                    Patient_FirstName = reader["Patient_FirstName"]?.ToString(),
                    Patient_LastName = reader["Patient_LastName"]?.ToString(),
                    Customer = reader["Customer"]?.ToString(),
                    Items = reader["Items"]?.ToString(),
                });
            }
        }
        catch
        {
            return [];
        }

        return raw
            .Where(c => !string.IsNullOrWhiteSpace(c.IntOrderID))
            .Where(c => OrderCommentKeywords.ContainsRushKeyword(c.OrderComments))
            .GroupBy(c => c.IntOrderID, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}
