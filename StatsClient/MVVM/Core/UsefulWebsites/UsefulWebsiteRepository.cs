using Microsoft.Data.SqlClient;
using StatsClient.MVVM.Model;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core.UsefulWebsites;

public static class UsefulWebsiteRepository
{
    public static bool SchemaExists()
    {
        try
        {
            using var connection = new SqlConnection(ConnectionStrToStatsDatabase());
            using var command = new SqlCommand(
                """
                SELECT CASE
                    WHEN OBJECT_ID(N'dbo.UsefulWebsite', N'U') IS NULL THEN 0
                    WHEN COL_LENGTH(N'dbo.UsefulWebsite', N'IsDeleted') IS NULL THEN 0
                    ELSE 1
                END
                """,
                connection);
            connection.Open();
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<List<UsefulWebsiteModel>> GetAllAsync()
    {
        return await Task.Run(() =>
        {
            var websites = new Dictionary<int, UsefulWebsiteModel>(capacity: 64);

            try
            {
                const string query = """
                    SELECT w.WebsiteId, w.Title, w.Url, w.SortOrder, w.ModifiedUtc,
                           t.Name AS TagName
                    FROM dbo.UsefulWebsite w
                    LEFT JOIN dbo.UsefulWebsiteTagMap m ON m.WebsiteId = w.WebsiteId
                    LEFT JOIN dbo.UsefulWebsiteTag t ON t.TagId = m.TagId
                    WHERE w.IsDeleted = 0
                    ORDER BY w.SortOrder, w.Title, t.Name
                    """;

                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());
                using var command = new SqlCommand(query, connection);
                connection.Open();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int websiteId = reader.GetInt32(reader.GetOrdinal("WebsiteId"));
                    if (!websites.TryGetValue(websiteId, out UsefulWebsiteModel? model))
                    {
                        model = new UsefulWebsiteModel
                        {
                            WebsiteId = websiteId,
                            Title = reader["Title"]?.ToString() ?? string.Empty,
                            Url = reader["Url"]?.ToString() ?? string.Empty,
                            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
                            ModifiedUtc = reader.GetDateTime(reader.GetOrdinal("ModifiedUtc")),
                        };
                        websites[websiteId] = model;
                    }

                    string? tagName = reader["TagName"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(tagName) && !model.Tags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
                        model.Tags.Add(tagName);
                }
            }
            catch
            {
                return [];
            }

            return websites.Values
                .OrderBy(w => w.SortOrder)
                .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }).ConfigureAwait(false);
    }

    public static async Task<List<(string TagName, int UsageCount)>> GetTagCountsAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<(string, int)>();
            try
            {
                const string query = """
                    SELECT t.Name, COUNT(m.WebsiteId) AS UsageCount
                    FROM dbo.UsefulWebsiteTag t
                    INNER JOIN dbo.UsefulWebsiteTagMap m ON m.TagId = t.TagId
                    INNER JOIN dbo.UsefulWebsite w ON w.WebsiteId = m.WebsiteId AND w.IsDeleted = 0
                    GROUP BY t.Name
                    ORDER BY t.Name
                    """;

                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());
                using var command = new SqlCommand(query, connection);
                connection.Open();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    list.Add((reader["Name"]?.ToString() ?? string.Empty, Convert.ToInt32(reader["UsageCount"])));
                }
            }
            catch
            {
            }

            return list;
        }).ConfigureAwait(false);
    }

    public static async Task<bool> InsertAsync(UsefulWebsiteModel model, IEnumerable<string> tags)
    {
        return await SaveAsync(model, tags, isUpdate: false).ConfigureAwait(false);
    }

    public static async Task<bool> UpdateAsync(UsefulWebsiteModel model, IEnumerable<string> tags)
    {
        if (model.WebsiteId <= 0)
            return false;

        return await SaveAsync(model, tags, isUpdate: true).ConfigureAwait(false);
    }

    public static async Task<bool> DeleteAsync(int websiteId)
    {
        if (websiteId <= 0)
            return false;

        return await SoftDeleteAsync(websiteId).ConfigureAwait(false);
    }

    public static async Task<bool> SoftDeleteAsync(int websiteId)
    {
        if (websiteId <= 0)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                string machine = Environment.MachineName;
                DateTime utcNow = DateTime.UtcNow;

                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());
                connection.Open();

                using var command = new SqlCommand("""
                    UPDATE dbo.UsefulWebsite
                    SET IsDeleted = 1,
                        ModifiedUtc = @ModifiedUtc,
                        ModifiedByMachine = @ModifiedByMachine
                    WHERE WebsiteId = @WebsiteId
                      AND IsDeleted = 0
                    """, connection);
                command.Parameters.AddWithValue("@WebsiteId", websiteId);
                command.Parameters.AddWithValue("@ModifiedUtc", utcNow);
                command.Parameters.AddWithValue("@ModifiedByMachine", machine);
                return command.ExecuteNonQuery() > 0;
            }
            catch
            {
                return false;
            }
        }).ConfigureAwait(false);
    }

    private static async Task<bool> SaveAsync(UsefulWebsiteModel model, IEnumerable<string> tags, bool isUpdate)
    {
        return await Task.Run(() =>
        {
            try
            {
                var normalizedTags = NormalizeTags(tags);
                string machine = Environment.MachineName;
                DateTime utcNow = DateTime.UtcNow;

                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    int websiteId = model.WebsiteId;

                    if (isUpdate)
                    {
                        using var update = new SqlCommand("""
                            UPDATE dbo.UsefulWebsite
                            SET Title = @Title,
                                Url = @Url,
                                SortOrder = @SortOrder,
                                ModifiedUtc = @ModifiedUtc,
                                ModifiedByMachine = @ModifiedByMachine
                            WHERE WebsiteId = @WebsiteId
                              AND IsDeleted = 0
                            """, connection, transaction);
                        update.Parameters.AddWithValue("@WebsiteId", websiteId);
                        update.Parameters.AddWithValue("@Title", model.Title.Trim());
                        update.Parameters.AddWithValue("@Url", model.Url.Trim());
                        update.Parameters.AddWithValue("@SortOrder", model.SortOrder);
                        update.Parameters.AddWithValue("@ModifiedUtc", utcNow);
                        update.Parameters.AddWithValue("@ModifiedByMachine", machine);
                        if (update.ExecuteNonQuery() == 0)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        using var clearTags = new SqlCommand(
                            "DELETE FROM dbo.UsefulWebsiteTagMap WHERE WebsiteId = @WebsiteId",
                            connection, transaction);
                        clearTags.Parameters.AddWithValue("@WebsiteId", websiteId);
                        clearTags.ExecuteNonQuery();
                    }
                    else
                    {
                        using var insert = new SqlCommand("""
                            INSERT INTO dbo.UsefulWebsite
                                (Title, Url, SortOrder, CreatedUtc, ModifiedUtc, CreatedByMachine, ModifiedByMachine)
                            OUTPUT INSERTED.WebsiteId
                            VALUES
                                (@Title, @Url, @SortOrder, @CreatedUtc, @ModifiedUtc, @CreatedByMachine, @ModifiedByMachine)
                            """, connection, transaction);
                        insert.Parameters.AddWithValue("@Title", model.Title.Trim());
                        insert.Parameters.AddWithValue("@Url", model.Url.Trim());
                        insert.Parameters.AddWithValue("@SortOrder", model.SortOrder);
                        insert.Parameters.AddWithValue("@CreatedUtc", utcNow);
                        insert.Parameters.AddWithValue("@ModifiedUtc", utcNow);
                        insert.Parameters.AddWithValue("@CreatedByMachine", machine);
                        insert.Parameters.AddWithValue("@ModifiedByMachine", machine);
                        websiteId = Convert.ToInt32(insert.ExecuteScalar());
                    }

                    foreach (string tag in normalizedTags)
                    {
                        int tagId = EnsureTagId(connection, transaction, tag);
                        using var map = new SqlCommand("""
                            IF NOT EXISTS (SELECT 1 FROM dbo.UsefulWebsiteTagMap WHERE WebsiteId = @WebsiteId AND TagId = @TagId)
                                INSERT INTO dbo.UsefulWebsiteTagMap (WebsiteId, TagId) VALUES (@WebsiteId, @TagId)
                            """, connection, transaction);
                        map.Parameters.AddWithValue("@WebsiteId", websiteId);
                        map.Parameters.AddWithValue("@TagId", tagId);
                        map.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch
            {
                return false;
            }
        }).ConfigureAwait(false);
    }

    private static int EnsureTagId(SqlConnection connection, SqlTransaction transaction, string tagName)
    {
        using var find = new SqlCommand("SELECT TagId FROM dbo.UsefulWebsiteTag WHERE Name = @Name", connection, transaction);
        find.Parameters.AddWithValue("@Name", tagName);
        object? existing = find.ExecuteScalar();
        if (existing is not null and not DBNull)
            return Convert.ToInt32(existing);

        using var insert = new SqlCommand("""
            INSERT INTO dbo.UsefulWebsiteTag (Name)
            OUTPUT INSERTED.TagId
            VALUES (@Name)
            """, connection, transaction);
        insert.Parameters.AddWithValue("@Name", tagName);
        return Convert.ToInt32(insert.ExecuteScalar());
    }

    public static List<string> NormalizeTags(IEnumerable<string> tags)
    {
        return tags
            .SelectMany(t => t.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string NormalizeUrl(string url)
    {
        string trimmed = url.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        return trimmed;
    }
}
