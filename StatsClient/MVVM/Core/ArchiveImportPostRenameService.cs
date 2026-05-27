using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using static StatsClient.MVVM.Core.DatabaseOperations;

namespace StatsClient.MVVM.Core;

public static class ArchiveImportPostRenameService
{
    private static readonly TimeSpan WatchTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public static void StartWatchAfterImport(string importedOrderId, int newPanNumber, ThreeShapeOrdersModel archiveSnapshot)
    {
        if (string.IsNullOrWhiteSpace(importedOrderId) || newPanNumber <= 0)
            return;

        _ = Task.Run(() => WatchAndRenameAsync(importedOrderId, newPanNumber, archiveSnapshot));
    }

    private static async Task WatchAndRenameAsync(
        string importedOrderId,
        int newPanNumber,
        ThreeShapeOrdersModel archiveSnapshot)
    {
        DateTime deadline = DateTime.UtcNow.Add(WatchTimeout);

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                if (OrderExistsIn3Shape(importedOrderId))
                {
                    await TryRenameToPanAsync(importedOrderId, newPanNumber, archiveSnapshot).ConfigureAwait(false);
                    return;
                }

                await Task.Delay(PollInterval).ConfigureAwait(false);
            }

            Notify(
                "Import rename",
                $"Order {importedOrderId} was queued but did not appear in 3Shape within one minute. Pan rename was not applied.",
                MainViewModel.NotificationIcon.Warning);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            Notify(
                "Import rename",
                $"Error while waiting to rename {importedOrderId}: {ex.Message}",
                MainViewModel.NotificationIcon.Error);
        }
    }

    private static async Task TryRenameToPanAsync(
        string importedOrderId,
        int newPanNumber,
        ThreeShapeOrdersModel archiveSnapshot)
    {
        string? targetOrderId = await ResolveTargetOrderIdAsync(importedOrderId, newPanNumber, archiveSnapshot)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(targetOrderId))
        {
            Notify(
                "Import rename",
                $"Could not build a new order name for {importedOrderId}.",
                MainViewModel.NotificationIcon.Warning);
            return;
        }

        if (!CheckIfOrderIDIsUnique(targetOrderId))
        {
            Notify(
                "Import rename",
                $"Cannot rename to {targetOrderId} — that order ID already exists in 3Shape.",
                MainViewModel.NotificationIcon.Warning);
            return;
        }

        string threeShapeDirectory = GetServerFileDirectory();
        ThreeShapeOrderRenameResult renameResult = await ThreeShapeOrderRenameHelper
            .RenameImportedOrderAsync(importedOrderId, targetOrderId, threeShapeDirectory)
            .ConfigureAwait(false);

        if (renameResult.Success)
        {
            Notify(
                "Import rename",
                $"Order imported and renamed to {targetOrderId}.",
                MainViewModel.NotificationIcon.Success);
        }
        else
        {
            Notify(
                "Import rename",
                renameResult.ErrorMessage ?? $"Rename from {importedOrderId} to {targetOrderId} failed.",
                MainViewModel.NotificationIcon.Warning);
        }
    }

    private static async Task<string?> ResolveTargetOrderIdAsync(
        string importedOrderId,
        int newPanNumber,
        ThreeShapeOrdersModel archiveSnapshot)
    {
        if (ArchiveImportNameHelper.HasLeadingPanPrefix(importedOrderId))
        {
            return ArchiveImportNameHelper.ReplaceLeadingPan(importedOrderId, newPanNumber);
        }

        var namingModel = new ThreeShapeOrdersModel
        {
            IntOrderID = importedOrderId,
            Patient_FirstName = archiveSnapshot.Patient_FirstName,
            Patient_LastName = archiveSnapshot.Patient_LastName,
            Customer = archiveSnapshot.Customer,
            OrderComments = archiveSnapshot.OrderComments,
            Items = archiveSnapshot.Items,
        };

        return await SmartOrderNameBuilder.BuildOrderNameAsync(namingModel, newPanNumber).ConfigureAwait(false);
    }

    public static bool OrderExistsIn3Shape(string intOrderId)
    {
        if (string.IsNullOrWhiteSpace(intOrderId))
            return false;

        try
        {
            string connectionString = DatabaseConnection.ConnectionStrFor3Shape();
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            using var command = new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT COUNT(*) FROM Orders WHERE IntOrderID = @orderId",
                connection);
            command.Parameters.AddWithValue("@orderId", intOrderId);
            connection.Open();
            object? scalar = command.ExecuteScalar();
            return Convert.ToInt32(scalar) > 0;
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            return false;
        }
    }

    private static void Notify(string title, string message, MainViewModel.NotificationIcon icon)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            MainViewModel.Instance.ShowNotificationMessage(title, message, icon);
        });
    }
}
