using StatsClient.MVVM.Model;
using static StatsClient.MVVM.Core.DatabaseOperations;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private static void ApplyCheckedOutDesignerNames(
        List<ThreeShapeOrdersModel> orders,
        IReadOnlyDictionary<string, string> designerFriendlyNameByOrderId)
    {
        if (orders.Count == 0 || designerFriendlyNameByOrderId.Count == 0)
            return;

        foreach (var order in orders)
        {
            if (!IsCheckedOutForDesignerLookup(order))
                continue;

            if (!string.IsNullOrWhiteSpace(order.ExtOrderID))
                continue;

            if (string.IsNullOrWhiteSpace(order.IntOrderID))
                continue;

            if (!designerFriendlyNameByOrderId.TryGetValue(order.IntOrderID, out string? friendlyName)
                || string.IsNullOrWhiteSpace(friendlyName))
            {
                continue;
            }

            order.ExtOrderID = friendlyName;
            order.DesignerName = friendlyName;
            order.ShowCheckedOutDesignerFromStats = true;
        }
    }

    /// <summary>
    /// Checked-out in 3Shape is <c>plCheckedOut</c> on ModelElement (not a process status id).
    /// </summary>
    private static bool IsCheckedOutForDesignerLookup(ThreeShapeOrdersModel order) =>
        order.IsCheckedOut
        || string.Equals(order.ProcessLockID, "plCheckedOut", StringComparison.OrdinalIgnoreCase);
}
