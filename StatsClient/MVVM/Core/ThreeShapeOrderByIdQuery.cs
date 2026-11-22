using Microsoft.Data.SqlClient;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.DatabaseOperations;

namespace StatsClient.MVVM.Core;

public static class ThreeShapeOrderByIdQuery
{
    private const string Query = @"
SELECT TOP 1
       o.IntOrderID,
       o.Patient_FirstName,
       o.Patient_LastName,
       o.Patient_RefNo,
       o.ExtOrderID,
       o.OriginalOrderID,
       o.OrderComments,
       o.Items,
       o.OperatorName,
       o.Customer,
       o.ManufName,
       o.CacheMaterialName,
       o.ScanSource,
       o.CacheMaxScanDate,
       o.TraySystemType,
       i.MaxCreateDate,
       i.MaxProcessStatusID,
       me.ProcessStatusID,
       me.AltProcessStatusID,
       me.WasSent,
       me.ModelHeight,
       me.ProcessLockID,
       oh.ModificationDate,
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
WHERE o.IntOrderID = @orderId";

    public static ThreeShapeOrdersModel? TryLoad(string? intOrderId)
    {
        if (string.IsNullOrWhiteSpace(intOrderId))
            return null;

        try
        {
            using var connection = new SqlConnection(ConnectionStrFor3Shape());
            using var command = new SqlCommand(Query, connection);
            command.Parameters.AddWithValue("@orderId", intOrderId.Trim());
            connection.Open();

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return MapReader(reader);
        }
        catch
        {
            return null;
        }
    }

    private static ThreeShapeOrdersModel MapReader(SqlDataReader reader)
    {
        string maxProcessStatusId = reader["MaxProcessStatusID"]?.ToString() ?? string.Empty;
        string scanSource = reader["ScanSource"]?.ToString() ?? string.Empty;
        string processStatusId = reader["ProcessStatusID"]?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(processStatusId))
            processStatusId = maxProcessStatusId;
        string processLockId = reader["ProcessLockID"]?.ToString() ?? string.Empty;
        string altProcessStatusId = reader["AltProcessStatusID"]?.ToString() ?? string.Empty;
        string wasSent = reader["WasSent"]?.ToString() ?? string.Empty;
        string intOrderId = reader["IntOrderID"]?.ToString() ?? string.Empty;

        string patientFirstName = reader["Patient_FirstName"]?.ToString()?.Trim() ?? string.Empty;
        string patientLastName = reader["Patient_LastName"]?.ToString()?.Trim() ?? string.Empty;
        if (patientFirstName == string.Empty)
            patientFirstName = "-";

        string panNumber = ResolvePanNumber(intOrderId, patientLastName, patientFirstName);
        string panColor = GetBackPanColorHEX(panNumber);
        if (string.IsNullOrWhiteSpace(panNumber) || panColor == ColorSchemeResourceCatalog.GetHex("WhiteBackground"))
            panColor = ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Transparent");

        string thisSite = MainViewModel.Instance?.ThisSite ?? string.Empty;
        string manufName = reader["ManufName"]?.ToString() ?? string.Empty;
        if (thisSite.Length > 0)
        {
            manufName = manufName
                .Replace(thisSite + "/", string.Empty)
                .Replace("/" + thisSite, string.Empty)
                .Replace(thisSite, string.Empty);
        }

        string cacheMaterialName = System.Net.WebUtility.HtmlDecode(reader["CacheMaterialName"]?.ToString() ?? string.Empty)
            .Replace("\"", string.Empty)
            .Trim();
        string items = RemoveChineseCharacters(reader["Items"]?.ToString() ?? string.Empty);
        string scanSourceFriendlyName = GetScanner(scanSource);
        string maxCreateDate = reader["MaxCreateDate"]?.ToString() ?? string.Empty;
        string cacheMaxScanDate = reader["CacheMaxScanDate"]?.ToString() ?? string.Empty;
        string originalOrderId = reader["OriginalOrderID"]?.ToString() ?? string.Empty;
        string modelHeight = reader["ModelHeight"]?.ToString() ?? "0";

        bool isCaseWereDesigned = modelHeight != "0" && string.IsNullOrEmpty(originalOrderId);
        bool isLocked = processLockId == "plLocked";
        bool isCheckedOut = processLockId == "plCheckedOut";

        string extOrderId = reader["ExtOrderID"]?.ToString() ?? string.Empty;
        if (int.TryParse(extOrderId, out _))
            extOrderId = string.Empty;

        return new ThreeShapeOrdersModel
        {
            IntOrderID = intOrderId,
            Patient_FirstName = patientFirstName,
            Patient_LastName = patientLastName,
            Patient_RefNo = reader["Patient_RefNo"]?.ToString(),
            ExtOrderID = extOrderId,
            OrderComments = reader["OrderComments"]?.ToString(),
            Items = items,
            OperatorName = reader["OperatorName"]?.ToString(),
            Customer = reader["Customer"]?.ToString(),
            ManufName = manufName,
            CacheMaterialName = cacheMaterialName,
            ScanSource = scanSource,
            CacheMaxScanDate = cacheMaxScanDate,
            TraySystemType = reader["TraySystemType"]?.ToString(),
            MaxCreateDate = maxCreateDate,
            MaxProcessStatusID = maxProcessStatusId,
            ProcessStatusID = processStatusId,
            AltProcessStatusID = altProcessStatusId,
            ProcessLockID = processLockId,
            WasSent = wasSent,
            ModificationDate = reader["ModificationDate"]?.ToString(),
            ImageSource = @"\Images\ListViewIcons\" + IconSelect(maxProcessStatusId, scanSource, processLockId) + ".png",
            PanColor = panColor,
            PanColorName = GetBackPanColorName(panNumber),
            CaseStatus = CaseStatusSelect(maxProcessStatusId, scanSource, processLockId),
            PanNumber = panNumber,
            ScanSourceFriendlyName = scanSourceFriendlyName,
            CacheMaxScanDateFriendly = cacheMaxScanDate,
            MaxCreateDateFriendly = maxCreateDate,
            OriginalOrderID = originalOrderId,
            IsCaseWereDesigned = isCaseWereDesigned,
            IsLocked = isLocked,
            IsCheckedOut = isCheckedOut,
        };
    }

    private static string ResolvePanNumber(string orderId, string patientLastName, string patientFirstName)
    {
        string[] orderParts = orderId.Split('-');
        if (orderParts.Length > 0 && int.TryParse(orderParts[0], out _))
            return orderParts[0];

        string[] lastNameParts = patientLastName.Split('-');
        if (lastNameParts.Length > 0 && int.TryParse(lastNameParts[0], out int panFromLast))
            return panFromLast.ToString();

        string[] firstNameParts = patientFirstName.Split('-');
        if (firstNameParts.Length > 0 && int.TryParse(firstNameParts[0], out int panFromFirst))
            return panFromFirst.ToString();

        return string.Empty;
    }
}
