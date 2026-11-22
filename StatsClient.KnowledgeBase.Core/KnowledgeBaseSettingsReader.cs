using Microsoft.Data.SqlClient;

namespace StatsClient.KnowledgeBase.Core;

public static class KnowledgeBaseSettingsReader
{
    public static async Task<string?> ReadAsync(string settingName, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT sValue FROM dbo.Settings WHERE sName = @Name";

        await using var connection = new SqlConnection(KnowledgeBaseDatabase.ConnectionStringFactory());
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", settingName);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? null : Convert.ToString(result);
    }
}
