using Microsoft.Data.SqlClient;
using System.IO;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Shared 3Shape order rename logic: safe XML order-id replacement and transactional database rename.
/// </summary>
public static class ThreeShapeOrderRenameHelper
{
    private const string ModelFilenamePropertyToken = "ModelFilename";

    /// <summary>
    /// Replaces <paramref name="originalOrderId"/> with <paramref name="newOrderId"/> line-by-line,
    /// leaving any line that defines <c>ModelFilename</c> unchanged (CAD paths keep the original folder name).
    /// </summary>
    public static string ReplaceOrderIdInXmlContent(string xmlContent, string originalOrderId, string newOrderId)
    {
        if (string.IsNullOrEmpty(xmlContent)
            || string.IsNullOrEmpty(originalOrderId)
            || string.Equals(originalOrderId, newOrderId, StringComparison.Ordinal))
        {
            return xmlContent;
        }

        string lineBreak = xmlContent.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : xmlContent.Contains('\n')
                ? "\n"
                : Environment.NewLine;

        string[] splitOn = lineBreak == "\r\n" ? ["\r\n", "\n"] : [lineBreak];
        var lines = xmlContent.Split(splitOn, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(ModelFilenamePropertyToken, StringComparison.OrdinalIgnoreCase))
                continue;

            lines[i] = lines[i].Replace(originalOrderId, newOrderId, StringComparison.Ordinal);
        }

        return string.Join(lineBreak, lines);
    }

    /// <summary>
    /// Whether the order list may offer rename for this 3Shape case (database list / context menu).
    /// </summary>
    public static bool CanRenameOrder(
        string? maxProcessStatusId,
        bool isLocked,
        bool isCheckedOut,
        bool isFilesAccessible)
    {
        if (isLocked || isCheckedOut || !isFilesAccessible)
            return false;

        return maxProcessStatusId switch
        {
            "psCreated" or "psScanned" or "psModelled" => true,
            _ => false
        };
    }

    /// <summary>
    /// Copies the Orders row, repoints related tables, then deletes the old Orders row in one transaction.
    /// Does not modify ModelFilename or other model paths in the database.
    /// </summary>
    public static async Task<ThreeShapeOrderRenameDatabaseResult> RenameOrderIdInDatabaseAsync(
        string originalOrderId,
        string newOrderId,
        string? orderCommentsOverride = null,
        Action<string>? logStep = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ThreeShapeOrderRenameDatabaseResult();
        void Log(string message)
        {
            result.Steps.Add(message);
            logStep?.Invoke(message);
        }

        if (string.IsNullOrWhiteSpace(originalOrderId) || string.IsNullOrWhiteSpace(newOrderId))
        {
            result.Success = false;
            result.ErrorMessage = "Original and new order IDs are required.";
            return result;
        }

        if (string.Equals(originalOrderId, newOrderId, StringComparison.Ordinal))
        {
            result.Success = true;
            Log("Order ID unchanged; database rename skipped.");
            return result;
        }

        string connectionString = DatabaseConnection.ConnectionStrFor3Shape();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                int sourceCount = await ScalarCountAsync(
                    connection,
                    transaction,
                    "SELECT COUNT(*) FROM Orders WHERE IntOrderID = @orderId",
                    originalOrderId,
                    cancellationToken).ConfigureAwait(false);

                if (sourceCount < 1)
                {
                    throw new InvalidOperationException(
                        $"Source order '{originalOrderId}' was not found in Orders.");
                }

                int destCount = await ScalarCountAsync(
                    connection,
                    transaction,
                    "SELECT COUNT(*) FROM Orders WHERE IntOrderID = @orderId",
                    newOrderId,
                    cancellationToken).ConfigureAwait(false);

                if (destCount > 0)
                {
                    throw new InvalidOperationException(
                        $"Destination order '{newOrderId}' already exists in Orders. Resolve duplicates before renaming.");
                }

                int inserted = await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    BuildInsertOrderCopySql(),
                    newOrderId,
                    originalOrderId,
                    cancellationToken).ConfigureAwait(false);
                Log($"INSERT Orders (copy): affected [{inserted}] row(s).");

                if (inserted < 1)
                {
                    throw new InvalidOperationException(
                        $"Could not copy Orders row from '{originalOrderId}' to '{newOrderId}'.");
                }

                string[] updateStatements =
                [
                    "UPDATE ModelJob SET OrderID = @newId WHERE OrderID = @oldId",
                    "UPDATE OrderHistory SET OrderID = @newId WHERE OrderID = @oldId",
                    "UPDATE CustomData SET OrderID = @newId WHERE OrderID = @oldId",
                    "UPDATE PrintJobItem SET OrderID = @newId WHERE OrderID = @oldId",
                    "UPDATE CommunicateOrders SET OrderID = @newId WHERE OrderID = @oldId",
                    "UPDATE OrderExchangeElement SET OrderID = @newId WHERE OrderID = @oldId",
                    "UPDATE ImageOverlay SET OrderID = @newId WHERE OrderID = @oldId",
                ];

                foreach (string sql in updateStatements)
                {
                    int affected = await ExecuteNonQueryAsync(
                        connection,
                        transaction,
                        sql,
                        newOrderId,
                        originalOrderId,
                        cancellationToken).ConfigureAwait(false);
                    Log($"Command complete. Affected [{affected}] rows.");
                }

                int newRowCount = await ScalarCountAsync(
                    connection,
                    transaction,
                    "SELECT COUNT(*) FROM Orders WHERE IntOrderID = @orderId",
                    newOrderId,
                    cancellationToken).ConfigureAwait(false);

                if (newRowCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one Orders row for '{newOrderId}', found {newRowCount}.");
                }

                int deleted = await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    "DELETE FROM Orders WHERE IntOrderID = @oldId",
                    newOrderId,
                    originalOrderId,
                    cancellationToken).ConfigureAwait(false);
                Log($"DELETE old Orders row: affected [{deleted}] row(s).");

                if (deleted < 1)
                {
                    throw new InvalidOperationException(
                        $"Could not delete Orders row for '{originalOrderId}'.");
                }

                int leftover = await ScalarCountAsync(
                    connection,
                    transaction,
                    "SELECT COUNT(*) FROM Orders WHERE IntOrderID = @orderId",
                    originalOrderId,
                    cancellationToken).ConfigureAwait(false);

                if (leftover > 0)
                {
                    throw new InvalidOperationException(
                        $"Old Orders row '{originalOrderId}' is still present after delete.");
                }

                if (!string.IsNullOrEmpty(orderCommentsOverride))
                {
                    int commentRows = await ExecuteNonQueryAsync(
                        connection,
                        transaction,
                        "UPDATE Orders SET OrderComments = @comments WHERE IntOrderID = @newId",
                        newOrderId,
                        originalOrderId,
                        cancellationToken,
                        orderCommentsOverride).ConfigureAwait(false);
                    Log($"UPDATE OrderComments: affected [{commentRows}] row(s).");
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                result.Success = true;
                Log("Database rename committed.");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Log($"Database rename failed: {ex.Message}");
        }

        return result;
    }

    private static async Task<int> ScalarCountAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        string orderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(scalar);
    }

    private static async Task<int> ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        string newOrderId,
        string originalOrderId,
        CancellationToken cancellationToken,
        string? commentsOverride = null)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@newId", newOrderId);
        command.Parameters.AddWithValue("@oldId", originalOrderId);
        if (commentsOverride != null)
            command.Parameters.AddWithValue("@comments", commentsOverride);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildInsertOrderCopySql() =>
        @"INSERT INTO Orders ( 
             [IntOrderID] 
            ,[ExtOrderID] 
            ,[ClientID] 
            ,[ClientOrderNo] 
            ,[OrderDate] 
            ,[OrderImportanceID] 
            ,[Patient_RefNo] 
            ,[Patient_FirstName]
            ,[Patient_LastName] 
            ,[DeliveryAddress1] 
            ,[DeliveryAddress2] 
            ,[DeliveryZip] 
            ,[DeliveryCity] 
            ,[DeliveryState] 
            ,[DeliveryCountryID] 
            ,[DeliveryType] 
            ,[ShipToDeliveryAddress] 
            ,[ClientContactPerson] 
            ,[LabID] 
            ,[LabOperator] 
            ,[OrderComments] 
            ,[CreatedFromApp] 
            ,[RelativePos] 
            ,[OperatorID] 
            ,[DisplayOrderID] 
            ,[NumOrderID] 
            ,[DesignModuleID] 
            ,[ScanModuleID] 
            ,[FaceScanModuleID] 
            ,[Items] 
            ,[OperatorName] 
            ,[Customer] 
            ,[ManufName] 
            ,[OrderRelativePositionClass] 
            ,[ShipToERPCustNo] 
            ,[ERPCustomerNo] 
            ,[ShipToID] 
            ,[ModelManufacturingID] 
            ,[CacheMaterialName] 
            ,[ScanSource] 
            ,[ImprovementProgramSendDate] 
            ,[GroupFolder] 
            ,[CacheColor] 
            ,[OriginalOrderID] 
            ,[ImportOrderID] 
            ,[CacheMaxScanDate] 
            ,[TraySystemType]
            ,[ExternalLabID] 
            ,[ShipToDifferentAddress] 
            ,[PatientGuid]) 
        SELECT @newId
            ,[ExtOrderID] 
            ,[ClientID] 
            ,[ClientOrderNo] 
            ,[OrderDate] 
            ,[OrderImportanceID] 
            ,[Patient_RefNo] 
            ,[Patient_FirstName] 
            ,[Patient_LastName] 
            ,[DeliveryAddress1] 
            ,[DeliveryAddress2] 
            ,[DeliveryZip] 
            ,[DeliveryCity] 
            ,[DeliveryState] 
            ,[DeliveryCountryID] 
            ,[DeliveryType] 
            ,[ShipToDeliveryAddress] 
            ,[ClientContactPerson] 
            ,[LabID] 
            ,[LabOperator] 
            ,[OrderComments] 
            ,[CreatedFromApp] 
            ,[RelativePos] 
            ,[OperatorID] 
            ,[DisplayOrderID] 
            ,[NumOrderID] 
            ,[DesignModuleID] 
            ,[ScanModuleID] 
            ,[FaceScanModuleID] 
            ,[Items] 
            ,[OperatorName] 
            ,[Customer] 
            ,[ManufName] 
            ,[OrderRelativePositionClass] 
            ,[ShipToERPCustNo] 
            ,[ERPCustomerNo] 
            ,[ShipToID] 
            ,[ModelManufacturingID] 
            ,[CacheMaterialName] 
            ,[ScanSource] 
            ,[ImprovementProgramSendDate] 
            ,[GroupFolder] 
            ,[CacheColor] 
            ,[OriginalOrderID] 
            ,[ImportOrderID] 
            ,[CacheMaxScanDate] 
            ,[TraySystemType] 
            ,[ExternalLabID] 
            ,[ShipToDifferentAddress] 
            ,[PatientGuid] 
        FROM Orders WHERE IntOrderID = @oldId";

    /// <summary>
    /// Renames order folder, XML, optional 3ML, and database rows (no comment/info side files).
    /// </summary>
    public static async Task<ThreeShapeOrderRenameResult> RenameImportedOrderAsync(
        string originalOrderId,
        string newOrderId,
        string threeShapeDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = new ThreeShapeOrderRenameResult();

        if (string.IsNullOrWhiteSpace(originalOrderId) || string.IsNullOrWhiteSpace(newOrderId))
        {
            result.ErrorMessage = "Original and new order IDs are required.";
            return result;
        }

        if (string.Equals(originalOrderId, newOrderId, StringComparison.Ordinal))
        {
            result.Success = true;
            return result;
        }

        string sourceFolder = Path.Combine(threeShapeDirectory, originalOrderId);
        string targetFolder = Path.Combine(threeShapeDirectory, newOrderId);

        try
        {
            if (!Directory.Exists(sourceFolder))
            {
                result.ErrorMessage = $"Order folder not found: {sourceFolder}";
                return result;
            }

            if (Directory.Exists(targetFolder))
            {
                result.ErrorMessage = $"Target folder already exists: {targetFolder}";
                return result;
            }

            Directory.Move(sourceFolder, targetFolder);

            string sourceXml = Path.Combine(targetFolder, $"{originalOrderId}.xml");
            string targetXml = Path.Combine(targetFolder, $"{newOrderId}.xml");
            if (File.Exists(sourceXml))
                File.Move(sourceXml, targetXml);

            string source3ml = Path.Combine(targetFolder, $"{originalOrderId}_3pl.3ml");
            string target3ml = Path.Combine(targetFolder, $"{newOrderId}_3pl.3ml");
            if (File.Exists(source3ml))
                File.Move(source3ml, target3ml);

            if (File.Exists(targetXml))
            {
                string xmlContent = await File.ReadAllTextAsync(targetXml, cancellationToken).ConfigureAwait(false);
                xmlContent = ReplaceOrderIdInXmlContent(xmlContent, originalOrderId, newOrderId);
                await File.WriteAllTextAsync(targetXml, xmlContent, cancellationToken).ConfigureAwait(false);
            }

            ThreeShapeOrderRenameDatabaseResult dbResult = await RenameOrderIdInDatabaseAsync(
                originalOrderId,
                newOrderId,
                orderCommentsOverride: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!dbResult.Success)
            {
                result.ErrorMessage = dbResult.ErrorMessage ?? "Database rename failed.";
                return result;
            }

            DatabaseOperations.UpdateLastModifyDateinDatabase(newOrderId);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}

public sealed class ThreeShapeOrderRenameResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ThreeShapeOrderRenameDatabaseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Steps { get; } = [];
}
