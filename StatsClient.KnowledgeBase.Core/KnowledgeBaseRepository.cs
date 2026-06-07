using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace StatsClient.KnowledgeBase.Core;

public sealed class KnowledgeBaseRepository
{
    public const int MaxImageBytes = 5 * 1024 * 1024;

    private static string ConnectionString => KnowledgeBaseDatabase.ConnectionStringFactory();

    public async Task<List<KnowledgeBaseCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CategoryId, Name, SortOrder
            FROM dbo.KnowledgeBaseCategory
            ORDER BY SortOrder, Name
            """;

        var list = new List<KnowledgeBaseCategory>();
        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new KnowledgeBaseCategory
            {
                CategoryId = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2)
            });
        }

        return list;
    }

    public async Task<int> EnsureCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        const string merge = """
            MERGE dbo.KnowledgeBaseCategory WITH (HOLDLOCK) AS target
            USING (SELECT @Name AS Name) AS source
            ON target.Name = source.Name
            WHEN NOT MATCHED THEN
                INSERT (Name, SortOrder) VALUES (source.Name, 0)
            OUTPUT inserted.CategoryId;
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(merge, connection);
        command.Parameters.AddWithValue("@Name", name);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is int id)
        {
            return id;
        }

        const string select = "SELECT CategoryId FROM dbo.KnowledgeBaseCategory WHERE Name = @Name";
        await using var selectCmd = new SqlCommand(select, connection);
        selectCmd.Parameters.AddWithValue("@Name", name);
        result = await selectCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    public async Task<List<KnowledgeBaseTag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TagId, TagName, UsageCount
            FROM dbo.KnowledgeBaseTag
            ORDER BY UsageCount DESC, TagName
            """;

        var list = new List<KnowledgeBaseTag>();
        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new KnowledgeBaseTag
            {
                TagId = reader.GetInt32(0),
                TagName = reader.GetString(1),
                UsageCount = reader.GetInt32(2)
            });
        }

        return list;
    }

    public async Task<List<KnowledgeBaseCardSummary>> ListCardsAsync(
        KnowledgeBaseCardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder("""
            SELECT c.CardId,
                   c.Title,
                   LEFT(c.BodyText, 240) AS BodyPreview,
                   c.CategoryId,
                   cat.Name AS CategoryName,
                   c.ModifiedUtc,
                   (
                       SELECT TOP (1) i.ThumbnailData
                       FROM dbo.KnowledgeBaseCardImage i
                       WHERE i.CardId = c.CardId
                       ORDER BY i.SortOrder, i.ImageId
                   ) AS ThumbnailData
            FROM dbo.KnowledgeBaseCard c
            LEFT JOIN dbo.KnowledgeBaseCategory cat ON cat.CategoryId = c.CategoryId
            WHERE c.IsDeleted = 0
              AND (
                    LTRIM(RTRIM(ISNULL(c.Title, N''))) <> N''
                 OR LTRIM(RTRIM(ISNULL(c.BodyText, N''))) <> N''
                 OR c.CategoryId IS NOT NULL
                 OR EXISTS (SELECT 1 FROM dbo.KnowledgeBaseCardLink l WHERE l.CardId = c.CardId)
                 OR EXISTS (SELECT 1 FROM dbo.KnowledgeBaseCardImage i WHERE i.CardId = c.CardId)
                 OR EXISTS (SELECT 1 FROM dbo.KnowledgeBaseCardTag ct WHERE ct.CardId = c.CardId)
              )
            """);

        var parameters = new List<SqlParameter>();

        if (filter.CategoryId is int categoryId)
        {
            sql.AppendLine(" AND c.CategoryId = @CategoryId");
            parameters.Add(new SqlParameter("@CategoryId", categoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            sql.AppendLine(" AND (c.Title LIKE @Search OR c.BodyText LIKE @Search)");
            parameters.Add(new SqlParameter("@Search", $"%{filter.SearchText.Trim()}%"));
        }

        if (filter.TagNames.Count > 0)
        {
            for (int i = 0; i < filter.TagNames.Count; i++)
            {
                sql.AppendLine($"""
                     AND EXISTS (
                         SELECT 1
                         FROM dbo.KnowledgeBaseCardTag ct
                         INNER JOIN dbo.KnowledgeBaseTag t ON t.TagId = ct.TagId
                         WHERE ct.CardId = c.CardId AND t.TagName = @Tag{i}
                     )
                    """);
                parameters.Add(new SqlParameter($"@Tag{i}", KnowledgeBaseTagNormalizer.Normalize(filter.TagNames[i])));
            }
        }

        sql.AppendLine(" ORDER BY c.ModifiedUtc DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY");
        parameters.Add(new SqlParameter("@Skip", filter.Skip));
        parameters.Add(new SqlParameter("@Take", filter.Take));

        var summaries = new List<KnowledgeBaseCardSummary>();
        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddRange(parameters.ToArray());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            summaries.Add(new KnowledgeBaseCardSummary
            {
                CardId = reader.GetInt32(0),
                Title = reader.GetString(1),
                BodyPreview = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                CategoryId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
                ModifiedUtc = reader.GetDateTime(5),
                ThumbnailData = reader.IsDBNull(6) ? null : (byte[])reader[6]
            });
        }

        await reader.CloseAsync().ConfigureAwait(false);

        foreach (var summary in summaries)
        {
            summary.Tags = await GetTagsForCardAsync(connection, summary.CardId, cancellationToken).ConfigureAwait(false);
        }

        return summaries;
    }

    public async Task<KnowledgeBaseCardDetail?> GetCardDetailAsync(int cardId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.CardId, c.Title, c.BodyText, c.CategoryId, cat.Name,
                   c.CreatedUtc, c.ModifiedUtc, c.CreatedByMachine, c.ModifiedByMachine
            FROM dbo.KnowledgeBaseCard c
            LEFT JOIN dbo.KnowledgeBaseCategory cat ON cat.CategoryId = c.CategoryId
            WHERE c.CardId = @CardId AND c.IsDeleted = 0
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CardId", cardId);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var detail = new KnowledgeBaseCardDetail
        {
            CardId = reader.GetInt32(0),
            Title = reader.GetString(1),
            BodyText = reader.GetString(2),
            CategoryId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedUtc = reader.GetDateTime(5),
            ModifiedUtc = reader.GetDateTime(6),
            CreatedByMachine = reader.GetString(7),
            ModifiedByMachine = reader.GetString(8)
        };

        await reader.CloseAsync().ConfigureAwait(false);
        detail.Links = await GetLinksAsync(connection, cardId, cancellationToken).ConfigureAwait(false);
        detail.Images = await GetImagesAsync(connection, cardId, includeFullData: true, cancellationToken).ConfigureAwait(false);
        detail.Tags = await GetTagsForCardAsync(connection, cardId, cancellationToken).ConfigureAwait(false);
        return detail;
    }

    public async Task<int> CreateCardAsync(string machineName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.KnowledgeBaseCard (Title, BodyText, CreatedByMachine, ModifiedByMachine)
            OUTPUT INSERTED.CardId
            VALUES (N'', N'', @Machine, @Machine)
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Machine", machineName);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    public async Task SaveCardAsync(KnowledgeBaseSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.CardId));
        }

        if (KnowledgeBaseCardContentRules.IsEmpty(request))
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            const string updateCard = """
                UPDATE dbo.KnowledgeBaseCard
                SET Title = @Title,
                    BodyText = @BodyText,
                    CategoryId = @CategoryId,
                    ModifiedUtc = SYSUTCDATETIME(),
                    ModifiedByMachine = @Machine
                WHERE CardId = @CardId AND IsDeleted = 0
                """;

            await using (var updateCmd = new SqlCommand(updateCard, connection, transaction))
            {
                updateCmd.Parameters.AddWithValue("@Title", request.Title ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@BodyText", request.BodyText ?? string.Empty);
                updateCmd.Parameters.AddWithValue("@CategoryId", (object?)request.CategoryId ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@Machine", request.MachineName);
                updateCmd.Parameters.AddWithValue("@CardId", request.CardId);
                await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await ReplaceLinksAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            await ReplaceImagesAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            await ReplaceTagsAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            await RefreshTagUsageCountsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task SoftDeleteCardAsync(int cardId, string machineName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.KnowledgeBaseCard
            SET IsDeleted = 1,
                ModifiedUtc = SYSUTCDATETIME(),
                ModifiedByMachine = @Machine
            WHERE CardId = @CardId
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CardId", cardId);
        command.Parameters.AddWithValue("@Machine", machineName);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await RefreshTagUsageCountsAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTagGloballyAsync(string tagName, CancellationToken cancellationToken = default)
    {
        tagName = KnowledgeBaseTagNormalizer.Normalize(tagName);
        const string sql = "DELETE FROM dbo.KnowledgeBaseTag WHERE TagName = @TagName";
        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TagName", tagName);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MergeAutoTagsAsync(int cardId, IReadOnlyList<string> autoTags, CancellationToken cancellationToken = default)
    {
        if (autoTags.Count == 0)
        {
            return;
        }

        var detail = await GetCardDetailAsync(cardId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return;
        }

        var merged = detail.Tags
            .Concat(autoTags.Select(KnowledgeBaseTagNormalizer.Normalize))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var request = new KnowledgeBaseSaveRequest
            {
                CardId = cardId,
                Tags = merged
            };
            await ReplaceTagsAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            await RefreshTagUsageCountsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<KnowledgeBaseCardImage?> GetImageAsync(int imageId, bool fullSize, CancellationToken cancellationToken = default)
    {
        string sql = fullSize
            ? "SELECT ImageId, CardId, FileName, ContentType, ImageData, ThumbnailData, SortOrder FROM dbo.KnowledgeBaseCardImage WHERE ImageId = @ImageId"
            : "SELECT ImageId, CardId, FileName, ContentType, ImageData, ThumbnailData, SortOrder FROM dbo.KnowledgeBaseCardImage WHERE ImageId = @ImageId";

        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ImageId", imageId);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var image = ReadImage(reader);
        if (!fullSize && image.ThumbnailData is { Length: > 0 })
        {
            image.ImageData = image.ThumbnailData;
        }

        return image;
    }

    public async Task<List<KnowledgeBaseCardImage>> GetThumbnailCandidatesAsync(
        IReadOnlyList<int>? cardIds,
        int maxImages,
        CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder("""
            SELECT TOP (@Max) i.ImageId, i.CardId, i.FileName, i.ContentType, i.ImageData, i.ThumbnailData, i.SortOrder
            FROM dbo.KnowledgeBaseCardImage i
            INNER JOIN dbo.KnowledgeBaseCard c ON c.CardId = i.CardId AND c.IsDeleted = 0
            """);

        var parameters = new List<SqlParameter> { new("@Max", maxImages) };

        if (cardIds is { Count: > 0 })
        {
            var inParts = new List<string>();
            for (int i = 0; i < cardIds.Count; i++)
            {
                inParts.Add($"@Card{i}");
                parameters.Add(new SqlParameter($"@Card{i}", cardIds[i]));
            }

            sql.Append(" WHERE i.CardId IN (").Append(string.Join(",", inParts)).Append(')');
        }

        sql.Append(" ORDER BY i.CardId, i.SortOrder, i.ImageId");

        var list = new List<KnowledgeBaseCardImage>();
        await using var connection = new SqlConnection(ConnectionString);
        await using var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddRange(parameters.ToArray());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadImage(reader));
        }

        return list;
    }

    public KnowledgeBaseCardSnapshot CreateSnapshot(KnowledgeBaseCardDetail detail)
    {
        return new KnowledgeBaseCardSnapshot
        {
            CardId = detail.CardId,
            Title = detail.Title,
            BodyText = detail.BodyText,
            CategoryId = detail.CategoryId,
            CategoryName = detail.CategoryName,
            Links = detail.Links.Select(l => new KnowledgeBaseCardLink
            {
                LinkId = l.LinkId,
                CardId = l.CardId,
                Label = l.Label,
                Url = l.Url,
                SortOrder = l.SortOrder
            }).ToList(),
            Images = detail.Images.Select(i => new KnowledgeBaseCardImageSnapshot
            {
                ImageId = i.ImageId,
                FileName = i.FileName,
                ContentType = i.ContentType,
                ThumbnailBase64 = i.ThumbnailData is { Length: > 0 } ? Convert.ToBase64String(i.ThumbnailData) : null,
                SortOrder = i.SortOrder
            }).ToList(),
            Tags = detail.Tags.ToList(),
            SnapshotUtc = DateTime.UtcNow
        };
    }

    public async Task RestoreSnapshotAsync(KnowledgeBaseCardSnapshot snapshot, string machineName, CancellationToken cancellationToken = default)
    {
        var detail = await GetCardDetailAsync(snapshot.CardId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            throw new InvalidOperationException($"Card {snapshot.CardId} was not found.");
        }

        var request = new KnowledgeBaseSaveRequest
        {
            CardId = snapshot.CardId,
            Title = snapshot.Title,
            BodyText = snapshot.BodyText,
            CategoryId = snapshot.CategoryId,
            MachineName = machineName,
            Links = snapshot.Links,
            Tags = snapshot.Tags,
            Images = detail.Images,
            IsAutoTagPass = false
        };

        await SaveCardAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<KnowledgeBaseCardLink>> GetLinksAsync(
        SqlConnection connection,
        int cardId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT LinkId, CardId, Label, Url, SortOrder
            FROM dbo.KnowledgeBaseCardLink
            WHERE CardId = @CardId
            ORDER BY SortOrder, LinkId
            """;

        var list = new List<KnowledgeBaseCardLink>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CardId", cardId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new KnowledgeBaseCardLink
            {
                LinkId = reader.GetInt32(0),
                CardId = reader.GetInt32(1),
                Label = reader.GetString(2),
                Url = reader.GetString(3),
                SortOrder = reader.GetInt32(4)
            });
        }

        return list;
    }

    private static async Task<List<KnowledgeBaseCardImage>> GetImagesAsync(
        SqlConnection connection,
        int cardId,
        bool includeFullData,
        CancellationToken cancellationToken)
    {
        string sql = includeFullData
            ? """
              SELECT ImageId, CardId, FileName, ContentType, ImageData, ThumbnailData, SortOrder
              FROM dbo.KnowledgeBaseCardImage
              WHERE CardId = @CardId
              ORDER BY SortOrder, ImageId
              """
            : """
              SELECT ImageId, CardId, FileName, ContentType, CAST(NULL AS VARBINARY(MAX)), ThumbnailData, SortOrder
              FROM dbo.KnowledgeBaseCardImage
              WHERE CardId = @CardId
              ORDER BY SortOrder, ImageId
              """;

        var list = new List<KnowledgeBaseCardImage>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CardId", cardId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadImage(reader));
        }

        return list;
    }

    private static KnowledgeBaseCardImage ReadImage(SqlDataReader reader)
    {
        return new KnowledgeBaseCardImage
        {
            ImageId = reader.GetInt32(0),
            CardId = reader.GetInt32(1),
            FileName = reader.GetString(2),
            ContentType = reader.GetString(3),
            ImageData = reader.IsDBNull(4) ? [] : (byte[])reader[4],
            ThumbnailData = reader.IsDBNull(5) ? null : (byte[])reader[5],
            SortOrder = reader.GetInt32(6)
        };
    }

    private static async Task<List<string>> GetTagsForCardAsync(
        SqlConnection connection,
        int cardId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT t.TagName
            FROM dbo.KnowledgeBaseCardTag ct
            INNER JOIN dbo.KnowledgeBaseTag t ON t.TagId = ct.TagId
            WHERE ct.CardId = @CardId
            ORDER BY t.TagName
            """;

        var tags = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CardId", cardId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    private static async Task ReplaceLinksAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        KnowledgeBaseSaveRequest request,
        CancellationToken cancellationToken)
    {
        await using (var deleteCmd = new SqlCommand("DELETE FROM dbo.KnowledgeBaseCardLink WHERE CardId = @CardId", connection, transaction))
        {
            deleteCmd.Parameters.AddWithValue("@CardId", request.CardId);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        int sort = 0;
        foreach (var link in request.Links.Where(l => !string.IsNullOrWhiteSpace(l.Url)))
        {
            const string insert = """
                INSERT INTO dbo.KnowledgeBaseCardLink (CardId, Label, Url, SortOrder)
                VALUES (@CardId, @Label, @Url, @SortOrder)
                """;

            await using var insertCmd = new SqlCommand(insert, connection, transaction);
            insertCmd.Parameters.AddWithValue("@CardId", request.CardId);
            insertCmd.Parameters.AddWithValue("@Label", link.Label ?? string.Empty);
            insertCmd.Parameters.AddWithValue("@Url", link.Url.Trim());
            insertCmd.Parameters.AddWithValue("@SortOrder", sort++);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ReplaceImagesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        KnowledgeBaseSaveRequest request,
        CancellationToken cancellationToken)
    {
        var keepIds = request.Images.Where(i => i.ImageId > 0).Select(i => i.ImageId).ToList();
        if (keepIds.Count == 0)
        {
            await using var deleteAll = new SqlCommand("DELETE FROM dbo.KnowledgeBaseCardImage WHERE CardId = @CardId", connection, transaction);
            deleteAll.Parameters.AddWithValue("@CardId", request.CardId);
            await deleteAll.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var paramNames = keepIds.Select((_, i) => $"@Keep{i}").ToList();
            var deleteSql = $"DELETE FROM dbo.KnowledgeBaseCardImage WHERE CardId = @CardId AND ImageId NOT IN ({string.Join(",", paramNames)})";
            await using var deleteCmd = new SqlCommand(deleteSql, connection, transaction);
            deleteCmd.Parameters.AddWithValue("@CardId", request.CardId);
            for (int i = 0; i < keepIds.Count; i++)
            {
                deleteCmd.Parameters.AddWithValue(paramNames[i], keepIds[i]);
            }

            await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        int sort = 0;
        foreach (var image in request.Images)
        {
            if (image.ImageData.Length == 0)
            {
                continue;
            }

            if (image.ImageData.Length > MaxImageBytes)
            {
                throw new InvalidOperationException($"Image {image.FileName} exceeds the 5 MB limit.");
            }

            if (image.ImageId > 0)
            {
                const string update = """
                    UPDATE dbo.KnowledgeBaseCardImage
                    SET FileName = @FileName,
                        ContentType = @ContentType,
                        ImageData = @ImageData,
                        ThumbnailData = @ThumbnailData,
                        SortOrder = @SortOrder
                    WHERE ImageId = @ImageId AND CardId = @CardId
                    """;

                await using var updateCmd = new SqlCommand(update, connection, transaction);
                updateCmd.Parameters.AddWithValue("@ImageId", image.ImageId);
                updateCmd.Parameters.AddWithValue("@CardId", request.CardId);
                updateCmd.Parameters.AddWithValue("@FileName", image.FileName);
                updateCmd.Parameters.AddWithValue("@ContentType", image.ContentType);
                updateCmd.Parameters.AddWithValue("@ImageData", image.ImageData);
                updateCmd.Parameters.AddWithValue("@ThumbnailData", (object?)image.ThumbnailData ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@SortOrder", sort++);
                await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                const string insert = """
                    INSERT INTO dbo.KnowledgeBaseCardImage (CardId, FileName, ContentType, ImageData, ThumbnailData, SortOrder)
                    OUTPUT INSERTED.ImageId
                    VALUES (@CardId, @FileName, @ContentType, @ImageData, @ThumbnailData, @SortOrder)
                    """;

                await using var insertCmd = new SqlCommand(insert, connection, transaction);
                insertCmd.Parameters.AddWithValue("@CardId", request.CardId);
                insertCmd.Parameters.AddWithValue("@FileName", image.FileName);
                insertCmd.Parameters.AddWithValue("@ContentType", image.ContentType);
                insertCmd.Parameters.AddWithValue("@ImageData", image.ImageData);
                insertCmd.Parameters.AddWithValue("@ThumbnailData", (object?)image.ThumbnailData ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@SortOrder", sort++);
                var newId = await insertCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                image.ImageId = Convert.ToInt32(newId);
            }
        }
    }

    private static async Task ReplaceTagsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        KnowledgeBaseSaveRequest request,
        CancellationToken cancellationToken)
    {
        await using (var deleteCmd = new SqlCommand("DELETE FROM dbo.KnowledgeBaseCardTag WHERE CardId = @CardId", connection, transaction))
        {
            deleteCmd.Parameters.AddWithValue("@CardId", request.CardId);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var tag in request.Tags.Select(KnowledgeBaseTagNormalizer.Normalize).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            const string ensureTag = """
                MERGE dbo.KnowledgeBaseTag WITH (HOLDLOCK) AS target
                USING (SELECT @TagName AS TagName) AS source
                ON target.TagName = source.TagName
                WHEN NOT MATCHED THEN INSERT (TagName) VALUES (source.TagName);
                SELECT TagId FROM dbo.KnowledgeBaseTag WHERE TagName = @TagName;
                """;

            int tagId;
            await using (var tagCmd = new SqlCommand(ensureTag, connection, transaction))
            {
                tagCmd.Parameters.AddWithValue("@TagName", tag);
                tagId = Convert.ToInt32(await tagCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            }

            const string link = """
                IF NOT EXISTS (SELECT 1 FROM dbo.KnowledgeBaseCardTag WHERE CardId = @CardId AND TagId = @TagId)
                    INSERT INTO dbo.KnowledgeBaseCardTag (CardId, TagId) VALUES (@CardId, @TagId);
                """;

            await using var linkCmd = new SqlCommand(link, connection, transaction);
            linkCmd.Parameters.AddWithValue("@CardId", request.CardId);
            linkCmd.Parameters.AddWithValue("@TagId", tagId);
            await linkCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RefreshTagUsageCountsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE t
            SET UsageCount = ISNULL(x.Cnt, 0)
            FROM dbo.KnowledgeBaseTag t
            LEFT JOIN (
                SELECT ct.TagId, COUNT(*) AS Cnt
                FROM dbo.KnowledgeBaseCardTag ct
                INNER JOIN dbo.KnowledgeBaseCard c ON c.CardId = ct.CardId AND c.IsDeleted = 0
                GROUP BY ct.TagId
            ) x ON x.TagId = t.TagId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public static class KnowledgeBaseTagNormalizer
{
    public static string Normalize(string tag) => tag.Trim().ToLowerInvariant();
}

public static class KnowledgeBaseSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Serialize(KnowledgeBaseCardSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    public static KnowledgeBaseCardSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<KnowledgeBaseCardSnapshot>(json, Options)
        ?? throw new InvalidOperationException("Invalid snapshot JSON.");
}
