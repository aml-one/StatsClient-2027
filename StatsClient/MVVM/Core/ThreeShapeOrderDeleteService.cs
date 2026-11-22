using Microsoft.Data.SqlClient;
using System.IO;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.Core;

public sealed class ThreeShapeOrderDeleteRequest
{
    public required string OrderId { get; init; }

    public string? LiveOrderFolderPath { get; init; }

    public string? ArchiveBaseFolderPath { get; init; }

    public string? XmlFilePath { get; init; }

    public bool IsArchiveOrder { get; init; }
}

public sealed class ThreeShapeOrderDeleteResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? BackupPath { get; set; }

    public List<string> Steps { get; } = [];
}

public static class ThreeShapeOrderDeleteService
{
    private static readonly (string Label, string Sql)[] StatsDbDeleteStatements =
    [
        ("ExportHistory", "DELETE FROM dbo.ExportHistory WHERE OrderID = @orderId"),
        ("OrdersToImport", "DELETE FROM dbo.OrdersToImport WHERE OrderID = @orderId"),
        ("CheckedOutCaseImages", "DELETE FROM dbo.CheckedOutCaseImages WHERE OrderID = @orderId"),
        ("CheckedOutCases", "DELETE FROM dbo.CheckedOutCases WHERE OrderID = @orderId"),
        ("OrderDesignerRelations", "DELETE FROM dbo.OrderDesignerRelations WHERE OrderID = @orderId"),
        ("SentOutCases", "DELETE FROM dbo.SentOutCases WHERE OrderID = @orderId"),
        ("SentOutIssues", "DELETE FROM dbo.SentOutIssues WHERE OrderID = @orderId"),
        ("DigitalCasesToday", "DELETE FROM dbo.DigitalCasesToday WHERE OrderID = @orderId"),
        ("ManualImport", "DELETE FROM dbo.ManualImport WHERE OrderID = @orderId"),
        ("ModelExportQueue", "DELETE FROM dbo.ModelExportQueue WHERE OrderID = @orderId"),
        ("ModelExportHistory", "DELETE FROM dbo.ModelExportHistory WHERE OrderID = @orderId"),
        ("ModelPrintingLog", "DELETE FROM dbo.ModelPrintingLog WHERE OrderID = @orderId"),
        ("SingleUnitCases", "DELETE FROM dbo.SingleUnitCases WHERE IntOrderId = @orderId"),
        ("OrderHistory (StatsDB)", "DELETE FROM dbo.OrderHistory WHERE OrderID = @orderId"),
        ("SearchResults", "DELETE FROM dbo.SearchResults WHERE OrderID = @orderId"),
        ("LabnextManualPair", "DELETE FROM dbo.LabnextManualPair WHERE OrderID = @orderId"),
        ("DuplicatePanNrOrders", "DELETE FROM dbo.DuplicatePanNrOrders WHERE OrderID1 = @orderId OR OrderID2 = @orderId"),
    ];

    public static async Task<ThreeShapeOrderDeleteResult> DeleteOrderCompletelyAsync(
        ThreeShapeOrderDeleteRequest request,
        Action<string>? logStep = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ThreeShapeOrderDeleteResult();
        void Log(string message)
        {
            result.Steps.Add(message);
            logStep?.Invoke(message);
        }

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            result.ErrorMessage = "Order ID is required.";
            return result;
        }

        if (request.IsArchiveOrder)
        {
            result.ErrorMessage = "Archive orders cannot be deleted.";
            return result;
        }

        string orderId = request.OrderId.Trim();
        string liveFolder = ResolveLiveFolder(orderId, request);

        try
        {
            string backupRoot = CreateBackupRoot(orderId);
            result.BackupPath = backupRoot;

            if (Directory.Exists(liveFolder))
            {
                string backupLive = Path.Combine(backupRoot, "Live");
                Directory.CreateDirectory(backupLive);
                if (!CopyDirectory(liveFolder, backupLive))
                {
                    throw new InvalidOperationException($"Could not back up live order folder:\n{liveFolder}");
                }

                Log($"Backed up live folder to {backupLive}");
            }
            else
            {
                Log("Live order folder not found; skipped live backup.");
            }

            Log("Archive folders and archive database rows are never deleted.");

            bool existsIn3Shape = await OrderExistsIn3ShapeAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (existsIn3Shape)
            {
                bool deletedFrom3Shape = await DeleteFrom3ShapeDatabaseAsync(orderId, Log, cancellationToken)
                    .ConfigureAwait(false);
                if (!deletedFrom3Shape)
                {
                    throw new InvalidOperationException("Failed to delete order from the 3Shape database.");
                }
            }
            else
            {
                Log("Order not found in 3Shape database; skipped 3Shape delete.");
            }

            await DeleteFromStatsDatabaseAsync(orderId, Log, cancellationToken).ConfigureAwait(false);

            RemoveWatchListEntry(orderId);
            Log("Removed local watch-list entry (if any).");

            if (Directory.Exists(liveFolder))
            {
                Directory.Delete(liveFolder, recursive: true);
                Log($"Deleted live folder: {liveFolder}");
            }

            result.Success = true;
            Log("Order deletion completed.");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Log($"Deletion failed: {ex.Message}");
        }

        return result;
    }

    private static string CreateBackupRoot(string orderId)
    {
        string safeOrderId = string.Join("_", orderId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "StatsSystems",
            "OrderDeleteBackups",
            $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{safeOrderId}");

        Directory.CreateDirectory(root);
        return root;
    }

    private static string ResolveLiveFolder(string orderId, ThreeShapeOrderDeleteRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.LiveOrderFolderPath) && Directory.Exists(request.LiveOrderFolderPath))
        {
            return request.LiveOrderFolderPath;
        }

        string serverRoot = DatabaseOperations.GetServerFileDirectory();
        if (string.IsNullOrWhiteSpace(serverRoot))
        {
            return request.LiveOrderFolderPath ?? string.Empty;
        }

        if (!serverRoot.EndsWith('\\') && !serverRoot.EndsWith('/'))
        {
            serverRoot += Path.DirectorySeparatorChar;
        }

        return Path.Combine(serverRoot, orderId);
    }

    private static async Task<bool> OrderExistsIn3ShapeAsync(string orderId, CancellationToken cancellationToken)
    {
        string connectionString = DatabaseConnection.ConnectionStrFor3Shape();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Orders WHERE IntOrderID = @orderId", connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(scalar) > 0;
    }

    private static async Task<bool> DeleteFrom3ShapeDatabaseAsync(
        string orderId,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        string connectionString = DatabaseConnection.ConnectionStrFor3Shape();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string? modelJobId = await ReadSingleStringAsync(
                connection,
                transaction,
                "SELECT ModelJobID FROM dbo.ModelJob WHERE OrderID = @orderId",
                orderId,
                cancellationToken).ConfigureAwait(false);

            List<string> modelElementIds = [];
            List<string> linkIds = [];
            List<string> toothElementIds = [];

            if (!string.IsNullOrWhiteSpace(modelJobId))
            {
                modelElementIds = await ReadStringListAsync(
                    connection,
                    transaction,
                    "SELECT ModelElementID FROM dbo.ModelElement WHERE ModelJobID = @modelJobId",
                    ("@modelJobId", modelJobId),
                    cancellationToken).ConfigureAwait(false);

                foreach (string modelElementId in modelElementIds)
                {
                    List<string> ids = await ReadStringListAsync(
                        connection,
                        transaction,
                        "SELECT LinkID FROM dbo.Link WHERE ModelElementID = @modelElementId",
                        ("@modelElementId", modelElementId),
                        cancellationToken).ConfigureAwait(false);

                    foreach (string linkId in ids)
                    {
                        if (!linkIds.Contains(linkId, StringComparer.Ordinal))
                        {
                            linkIds.Add(linkId);
                        }
                    }
                }

                foreach (string linkId in linkIds)
                {
                    List<string> ids = await ReadStringListAsync(
                        connection,
                        transaction,
                        "SELECT ToothElementID FROM dbo.LinkToothElement WHERE LinkID = @linkId",
                        ("@linkId", linkId),
                        cancellationToken).ConfigureAwait(false);

                    foreach (string toothElementId in ids)
                    {
                        if (!toothElementIds.Contains(toothElementId, StringComparer.Ordinal))
                        {
                            toothElementIds.Add(toothElementId);
                        }
                    }
                }

                foreach (string modelElementId in modelElementIds)
                {
                    await ExecuteDeleteAsync(
                        connection,
                        transaction,
                        "DELETE FROM dbo.Scan WHERE ModelElementID = @id",
                        ("@id", modelElementId),
                        log,
                        $"Scan ({modelElementId})",
                        cancellationToken).ConfigureAwait(false);

                    await ExecuteDeleteAsync(
                        connection,
                        transaction,
                        "DELETE FROM dbo.InboxData WHERE ModelElementID = @id",
                        ("@id", modelElementId),
                        log,
                        $"InboxData ({modelElementId})",
                        cancellationToken).ConfigureAwait(false);

                    await ExecuteDeleteAsync(
                        connection,
                        transaction,
                        "DELETE FROM dbo.TrackAndTrace WHERE ModelElementID = @id",
                        ("@id", modelElementId),
                        log,
                        $"TrackAndTrace ({modelElementId})",
                        cancellationToken).ConfigureAwait(false);
                }
            }

            await ExecuteDeleteAsync(
                connection,
                transaction,
                "DELETE FROM dbo.OrderExchangeAttachment WHERE OrderExchangeElementID IN (SELECT OrderExchangeElementID FROM dbo.OrderExchangeElement WHERE OrderID = @orderId)",
                ("@orderId", orderId),
                log,
                "OrderExchangeAttachment",
                cancellationToken).ConfigureAwait(false);

            foreach (string linkId in linkIds)
            {
                await ExecuteDeleteAsync(
                    connection,
                    transaction,
                    "DELETE FROM dbo.LinkToothElement WHERE LinkID = @linkId",
                    ("@linkId", linkId),
                    log,
                    $"LinkToothElement ({linkId})",
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (string toothElementId in toothElementIds)
            {
                await ExecuteDeleteAsync(
                    connection,
                    transaction,
                    "DELETE FROM dbo.ToothElement WHERE ToothElementID = @toothElementId",
                    ("@toothElementId", toothElementId),
                    log,
                    $"ToothElement ({toothElementId})",
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (string linkId in linkIds)
            {
                await ExecuteDeleteAsync(
                    connection,
                    transaction,
                    "DELETE FROM dbo.Link WHERE LinkID = @linkId",
                    ("@linkId", linkId),
                    log,
                    $"Link ({linkId})",
                    cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(modelJobId))
            {
                await ExecuteDeleteAsync(
                    connection,
                    transaction,
                    "DELETE FROM dbo.ModelElement WHERE ModelJobID = @modelJobId",
                    ("@modelJobId", modelJobId),
                    log,
                    "ModelElement",
                    cancellationToken).ConfigureAwait(false);
            }

            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.ModelJob WHERE OrderID = @orderId", ("@orderId", orderId), log, "ModelJob", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.OrderHistory WHERE OrderID = @orderId", ("@orderId", orderId), log, "OrderHistory", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.CustomData WHERE OrderID = @orderId", ("@orderId", orderId), log, "CustomData", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.PrintJobItem WHERE OrderID = @orderId", ("@orderId", orderId), log, "PrintJobItem", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.CommunicateOrders WHERE OrderID = @orderId", ("@orderId", orderId), log, "CommunicateOrders", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.OrderExchangeElement WHERE OrderID = @orderId", ("@orderId", orderId), log, "OrderExchangeElement", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.ImageOverlay WHERE OrderID = @orderId", ("@orderId", orderId), log, "ImageOverlay", cancellationToken).ConfigureAwait(false);
            await ExecuteDeleteAsync(connection, transaction, "DELETE FROM dbo.Orders WHERE IntOrderID = @orderId", ("@orderId", orderId), log, "Orders", cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            log("3Shape database deletion committed.");
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task DeleteFromStatsDatabaseAsync(
        string orderId,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach ((string label, string sql) in StatsDbDeleteStatements)
        {
            try
            {
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@orderId", orderId);
                int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                log($"StatsDB {label}: deleted {affected} row(s).");
            }
            catch (SqlException ex) when (ex.Number is 208 or 3701)
            {
                log($"StatsDB {label}: skipped (table not present).");
            }
        }
    }

    private static async Task ExecuteDeleteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        (string Name, string Value) parameter,
        Action<string> log,
        string label,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        log($"3Shape {label}: deleted {affected} row(s).");
    }

    private static async Task<string?> ReadSingleStringAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        string orderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value?.ToString();
    }

    private static async Task<List<string>> ReadStringListAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        (string Name, string Value) parameter,
        CancellationToken cancellationToken)
    {
        var list = new List<string>();
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string? value = reader[0]?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                list.Add(value);
            }
        }

        return list;
    }
}
