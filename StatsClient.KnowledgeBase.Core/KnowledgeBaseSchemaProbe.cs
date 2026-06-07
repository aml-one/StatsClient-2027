using Microsoft.Data.SqlClient;

namespace StatsClient.KnowledgeBase.Core;

public static class KnowledgeBaseSchemaProbe
{
    public static async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(KnowledgeBaseDatabase.ConnectionStringFactory());
            await using var command = new SqlCommand(
                "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KnowledgeBaseCard'",
                connection);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is not null;
        }
        catch
        {
            return false;
        }
    }
}
