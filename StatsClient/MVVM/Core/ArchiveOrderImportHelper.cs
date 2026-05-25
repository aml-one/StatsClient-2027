using Microsoft.Data.SqlClient;
using System.IO;
using System.IO.Compression;

namespace StatsClient.MVVM.Core;

public static class ArchiveOrderImportHelper
{
    public sealed class QueueImportResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string OrderId { get; init; } = string.Empty;
        public string ZipPath { get; init; } = string.Empty;
    }

    public static bool DestinationOrderExists(string orderId, string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(destinationRoot))
            return false;

        return Directory.Exists(Path.Combine(destinationRoot, orderId.Trim()));
    }

    public static string GetClientImportPath()
    {
        return DatabaseConnection.ReadStatsSetting("ClientImportPath").Trim();
    }

    public static bool EnsureClientImportPathAvailable(string clientImportPath, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(clientImportPath))
        {
            message = "ClientImportPath setting is empty.";
            return false;
        }

        if (!Directory.Exists(clientImportPath))
        {
            message = "ClientImportPath folder is not accessible.";
            return false;
        }

        return true;
    }

    public static string? BuildArchiveOrderZip(string orderId, string sourceFolder, string clientImportPath, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(sourceFolder))
        {
            message = "Order ID or source folder is empty.";
            return null;
        }

        if (!Directory.Exists(sourceFolder))
        {
            message = "Archive source folder does not exist.";
            return null;
        }

        if (!EnsureClientImportPathAvailable(clientImportPath, out message))
        {
            return null;
        }

        try
        {
            string zipName = $"{orderId}-{DateTime.Now:yyyyMMddHHmmss}.zip";
            string zipPath = Path.Combine(clientImportPath, zipName);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            string tempWrapRoot = Path.Combine(Path.GetTempPath(), "StatsClientArchiveWrap", Guid.NewGuid().ToString("N"));
            string wrappedOrderFolder = Path.Combine(tempWrapRoot, orderId);
            Directory.CreateDirectory(wrappedOrderFolder);

            CopyDirectory(sourceFolder, wrappedOrderFolder);
            ZipFile.CreateFromDirectory(tempWrapRoot, zipPath, CompressionLevel.Optimal, false);

            try
            {
                Directory.Delete(tempWrapRoot, true);
            }
            catch
            {
            }

            return zipPath;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return null;
        }
    }

    public static string? CopyZipToClientImportPath(string sourceZipPath, string orderId, string clientImportPath, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceZipPath) || !File.Exists(sourceZipPath))
        {
            message = "Source ZIP file is missing.";
            return null;
        }

        if (!EnsureClientImportPathAvailable(clientImportPath, out message))
        {
            return null;
        }

        try
        {
            string targetZipName = $"{orderId}-{DateTime.Now:yyyyMMddHHmmss}.zip";
            string targetZipPath = Path.Combine(clientImportPath, targetZipName);
            File.Copy(sourceZipPath, targetZipPath, true);
            return targetZipPath;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return null;
        }
    }

    public static QueueImportResult QueueImportRequest(string orderId, string zipPath, string requestingComputer)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return new QueueImportResult { Success = false, Message = "Order ID is missing." };
        }

        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return new QueueImportResult
            {
                Success = false,
                OrderId = orderId,
                Message = "ZIP file is missing or inaccessible."
            };
        }

        try
        {
            string connectionString = DatabaseConnection.ConnectionStrToStatsDatabase();
            using SqlConnection connection = new(connectionString);
            connection.Open();

            string query = @"
DELETE FROM dbo.OrdersToImport WHERE OrderID = @orderId;
INSERT INTO dbo.OrdersToImport (OrderID, Path, RequestingComputer)
VALUES (@orderId, @zipPath, @requestingComputer);";

            using SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@orderId", orderId);
            command.Parameters.AddWithValue("@zipPath", zipPath);
            command.Parameters.AddWithValue("@requestingComputer", string.IsNullOrWhiteSpace(requestingComputer) ? Environment.MachineName : requestingComputer);
            command.ExecuteNonQuery();

            return new QueueImportResult
            {
                Success = true,
                OrderId = orderId,
                ZipPath = zipPath,
                Message = "Order queued for import."
            };
        }
        catch (Exception ex)
        {
            return new QueueImportResult
            {
                Success = false,
                OrderId = orderId,
                ZipPath = zipPath,
                Message = ex.Message
            };
        }
    }

    public static bool TryReadOrderIdFromZip(string zipPath, out string orderId)
    {
        orderId = string.Empty;

        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            return false;

        string fileNameOrderId = Path.GetFileNameWithoutExtension(zipPath);

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            ZipArchiveEntry? rootXml = archive.Entries
                .FirstOrDefault(x => x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                                     x.FullName.Count(c => c == '/' || c == '\\') <= 1);

            if (rootXml is not null)
            {
                orderId = Path.GetFileNameWithoutExtension(rootXml.Name);
                if (!string.IsNullOrWhiteSpace(orderId))
                    return true;
            }

            orderId = fileNameOrderId;
            return !string.IsNullOrWhiteSpace(orderId);
        }
        catch
        {
            orderId = fileNameOrderId;
            return !string.IsNullOrWhiteSpace(orderId);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            string destSubDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
