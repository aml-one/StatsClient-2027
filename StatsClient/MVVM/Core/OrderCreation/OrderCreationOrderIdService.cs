using Microsoft.Data.SqlClient;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.DatabaseOperations;

namespace StatsClient.MVVM.Core.OrderCreation;

internal static class OrderCreationOrderIdService
{
    public static void EnsureIntOrderIdIsAvailable(string intOrderId)
    {
        if (string.IsNullOrWhiteSpace(intOrderId))
        {
            throw new InvalidOperationException("Final order ID is required.");
        }

        if (!CheckIfOrderIDIsUnique(intOrderId))
        {
            throw new InvalidOperationException(
                $"Order ID '{intOrderId}' already exists in 3Shape.");
        }
    }

    public static string AllocateUniqueNumOrderId()
    {
        long maxExisting = QueryMaxNumOrderId();
        long candidateBase = maxExisting > 0
            ? maxExisting + 1000
            : long.Parse(OrderCreationIdFactory.NewNumericOrderIdCandidate());

        for (int i = 0; i < 10000; i++)
        {
            string candidate = (candidateBase + i).ToString();
            if (!NumOrderIdExists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique NumOrderID.");
    }

    private static long QueryMaxNumOrderId()
    {
        const string query = """
            SELECT MAX(TRY_CAST(NumOrderID AS BIGINT))
            FROM dbo.Orders
            WHERE NumOrderID IS NOT NULL AND LTRIM(RTRIM(NumOrderID)) <> ''
            """;

        using SqlConnection connection = new(ConnectionStrFor3Shape());
        using SqlCommand command = new(query, connection);
        connection.Open();
        object? result = command.ExecuteScalar();
        if (result is null or DBNull)
        {
            return 0;
        }

        return Convert.ToInt64(result);
    }

    private static bool NumOrderIdExists(string numOrderId)
    {
        const string query = "SELECT TOP (1) 1 FROM dbo.Orders WHERE NumOrderID = @numOrderId";

        using SqlConnection connection = new(ConnectionStrFor3Shape());
        using SqlCommand command = new(query, connection);
        command.Parameters.AddWithValue("@numOrderId", numOrderId);
        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();
        return reader.Read();
    }
}
