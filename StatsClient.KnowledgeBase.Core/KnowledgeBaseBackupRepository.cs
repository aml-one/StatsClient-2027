using Microsoft.Data.SqlClient;

namespace StatsClient.KnowledgeBase.Core;

public sealed class KnowledgeBaseBackupRepository
{
    private static string ConnectionString => KnowledgeBaseDatabase.ConnectionStringFactory();

    public async Task<bool> TryCreateBackupIfNotExistsAsync(
        KnowledgeBaseCardSnapshot snapshot,
        string machineName,
        CancellationToken cancellationToken = default)
    {
        const string existsSql = """
            SELECT 1
            FROM dbo.KnowledgeBaseCardBackup
            WHERE CardId = @CardId AND MachineName = @Machine
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var existsCmd = new SqlCommand(existsSql, connection))
        {
            existsCmd.Parameters.AddWithValue("@CardId", snapshot.CardId);
            existsCmd.Parameters.AddWithValue("@Machine", machineName);
            var exists = await existsCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (exists is not null)
            {
                return false;
            }
        }

        const string insert = """
            INSERT INTO dbo.KnowledgeBaseCardBackup (CardId, MachineName, SnapshotJson)
            VALUES (@CardId, @Machine, @Json)
            """;

        await using var insertCmd = new SqlCommand(insert, connection);
        insertCmd.Parameters.AddWithValue("@CardId", snapshot.CardId);
        insertCmd.Parameters.AddWithValue("@Machine", machineName);
        insertCmd.Parameters.AddWithValue("@Json", KnowledgeBaseSnapshotSerializer.Serialize(snapshot));
        await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<KnowledgeBaseCardBackupInfo?> GetBackupAsync(
        int cardId,
        string machineName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT BackupId, CardId, MachineName, SnapshotJson, BackedUpUtc
            FROM dbo.KnowledgeBaseCardBackup
            WHERE CardId = @CardId AND MachineName = @Machine
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CardId", cardId);
        command.Parameters.AddWithValue("@Machine", machineName);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new KnowledgeBaseCardBackupInfo
        {
            BackupId = reader.GetInt32(0),
            CardId = reader.GetInt32(1),
            MachineName = reader.GetString(2),
            SnapshotJson = reader.GetString(3),
            BackedUpUtc = reader.GetDateTime(4)
        };
    }
}
