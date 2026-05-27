using StatsClient.MVVM.Model;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Functions;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Builds a 3Shape order filename the same way as Order Rename → Generate Name.
/// </summary>
public static class SmartOrderNameBuilder
{
    public static async Task<string?> BuildOrderNameAsync(ThreeShapeOrdersModel order, int panNumber)
    {
        if (panNumber <= 0 || string.IsNullOrWhiteSpace(order.IntOrderID))
            return null;

        string digitalSystem = await GetDigiSystemName(order.IntOrderID);
        string patientNm = (order.Patient_LastName ?? string.Empty).Replace(" ", "_")
            .Replace(",", "")
            .Replace("'", "_")
            .Replace("\"", "_")
            .Replace("+", "_")
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("&", "-")
            .Replace("|", "_")
            .Trim();

        string customer = order.Customer ?? string.Empty;
        List<string> customerSuggestions = await CustomerHasSuggestedName(customer);
        if (customerSuggestions.Count > 0)
            customer = customerSuggestions[0];

        customer = CleanUpCustomerName(customer);
        string toothNumbersString = await GetToothNumbersString(order.IntOrderID);

        if (!string.IsNullOrEmpty(digitalSystem))
            digitalSystem = $"-{digitalSystem}";

        string builtOrderName = $"{panNumber}-{toothNumbersString}-{patientNm}-{customer}{digitalSystem}";

        string comments = order.OrderComments ?? string.Empty;
        if (comments.Contains("screw retained", StringComparison.CurrentCultureIgnoreCase) ||
            comments.Contains("screwretained", StringComparison.CurrentCultureIgnoreCase) ||
            comments.Contains("access hole", StringComparison.CurrentCultureIgnoreCase))
        {
            builtOrderName += "-SCR";
        }

        return builtOrderName.Trim().ToUpperInvariant();
    }
}
