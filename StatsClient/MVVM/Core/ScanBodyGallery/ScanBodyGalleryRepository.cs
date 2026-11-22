using Microsoft.Data.SqlClient;

using StatsClient.MVVM.Model;

using static StatsClient.MVVM.Core.DatabaseConnection;



namespace StatsClient.MVVM.Core.ScanBodyGallery;



public static class ScanBodyGalleryRepository

{

    public static bool SchemaExists()

    {

        try

        {

            using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

            using var command = new SqlCommand(

                """

                SELECT CASE

                    WHEN OBJECT_ID(N'dbo.ScanBodyGalleryEntry', N'U') IS NULL THEN 0

                    WHEN COL_LENGTH(N'dbo.ScanBodyGalleryEntry', N'IsDeleted') IS NULL THEN 0

                    WHEN OBJECT_ID(N'dbo.ScanBodyGalleryImage', N'U') IS NULL THEN 0

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



    public static async Task<List<ScanBodyGalleryEntryModel>> GetAllAsync()

    {

        return await Task.Run(() =>

        {

            var entries = new Dictionary<int, ScanBodyGalleryEntryModel>(capacity: 64);



            try

            {

                const string query = """

                    SELECT e.EntryId, e.Brand, e.SystemName, e.SizeLabel, e.OtherInfo, e.SortOrder, e.ModifiedUtc,

                           t.Name AS TagName,

                           (

                               SELECT TOP (1) i.ThumbnailData

                               FROM dbo.ScanBodyGalleryImage i

                               WHERE i.EntryId = e.EntryId

                               ORDER BY i.SortOrder, i.ImageId

                           ) AS ThumbnailData

                    FROM dbo.ScanBodyGalleryEntry e

                    LEFT JOIN dbo.ScanBodyGalleryTagMap m ON m.EntryId = e.EntryId

                    LEFT JOIN dbo.ScanBodyGalleryTag t ON t.TagId = m.TagId

                    WHERE e.IsDeleted = 0

                    ORDER BY e.SortOrder, e.Brand, e.SystemName, t.Name

                    """;



                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

                using var command = new SqlCommand(query, connection);

                connection.Open();



                using var reader = command.ExecuteReader();

                while (reader.Read())

                {

                    int entryId = reader.GetInt32(reader.GetOrdinal("EntryId"));

                    if (!entries.TryGetValue(entryId, out ScanBodyGalleryEntryModel? model))

                    {

                        model = new ScanBodyGalleryEntryModel

                        {

                            EntryId = entryId,

                            Brand = reader["Brand"]?.ToString() ?? string.Empty,

                            SystemName = reader["SystemName"]?.ToString() ?? string.Empty,

                            SizeLabel = reader["SizeLabel"]?.ToString() ?? string.Empty,

                            OtherInfo = reader["OtherInfo"]?.ToString() ?? string.Empty,

                            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),

                            ModifiedUtc = reader.GetDateTime(reader.GetOrdinal("ModifiedUtc")),

                            ThumbnailData = reader["ThumbnailData"] is DBNull or null ? null : (byte[])reader["ThumbnailData"],

                        };

                        entries[entryId] = model;

                    }



                    string? tagName = reader["TagName"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(tagName) &&

                        !model.Tags.Contains(tagName, StringComparer.OrdinalIgnoreCase))

                    {

                        model.Tags.Add(tagName);

                    }

                }

            }

            catch

            {

                return [];

            }



            return entries.Values

                .OrderBy(e => e.SortOrder)

                .ThenBy(e => e.Brand, StringComparer.OrdinalIgnoreCase)

                .ThenBy(e => e.SystemName, StringComparer.OrdinalIgnoreCase)

                .ToList();

        }).ConfigureAwait(false);

    }



    public static async Task<ScanBodyGalleryEntryDetailModel?> GetDetailAsync(int entryId)

    {

        if (entryId <= 0)

            return null;



        return await Task.Run(() =>

        {

            try

            {

                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

                connection.Open();



                ScanBodyGalleryEntryDetailModel? detail;

                using (var command = new SqlCommand("""

                    SELECT EntryId, Brand, SystemName, SizeLabel, OtherInfo, SortOrder

                    FROM dbo.ScanBodyGalleryEntry

                    WHERE EntryId = @EntryId AND IsDeleted = 0

                    """, connection))

                {

                    command.Parameters.AddWithValue("@EntryId", entryId);

                    using var reader = command.ExecuteReader();

                    if (!reader.Read())

                        return null;



                    detail = new ScanBodyGalleryEntryDetailModel

                    {

                        EntryId = reader.GetInt32(0),

                        Brand = reader.GetString(1),

                        SystemName = reader.GetString(2),

                        SizeLabel = reader.GetString(3),

                        OtherInfo = reader.GetString(4),

                        SortOrder = reader.GetInt32(5),

                    };

                }



                detail.Tags = GetTagsForEntry(connection, entryId);

                detail.Images = GetImagesForEntry(connection, entryId, includeFullData: true);

                return detail;

            }

            catch

            {

                return null;

            }

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

                    SELECT t.Name, COUNT(m.EntryId) AS UsageCount

                    FROM dbo.ScanBodyGalleryTag t

                    INNER JOIN dbo.ScanBodyGalleryTagMap m ON m.TagId = t.TagId

                    INNER JOIN dbo.ScanBodyGalleryEntry e ON e.EntryId = m.EntryId AND e.IsDeleted = 0

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



    public static async Task<List<ScanBodyGalleryImageModel>> GetThumbnailCandidatesAsync(

        IReadOnlyList<int> entryIds,

        int maxImages)

    {

        if (entryIds.Count == 0 || maxImages <= 0)

            return [];



        return await Task.Run(() =>

        {

            var list = new List<ScanBodyGalleryImageModel>();

            try

            {

                var paramNames = entryIds.Select((_, i) => $"@E{i}").ToList();

                string sql = $"""

                    SELECT TOP (@Max) i.ImageId, i.EntryId, i.FileName, i.ContentType,

                           CAST(NULL AS VARBINARY(MAX)) AS ImageData, i.ThumbnailData, i.SortOrder

                    FROM dbo.ScanBodyGalleryImage i

                    INNER JOIN dbo.ScanBodyGalleryEntry e ON e.EntryId = i.EntryId AND e.IsDeleted = 0

                    WHERE i.EntryId IN ({string.Join(",", paramNames)})

                    ORDER BY i.EntryId, i.SortOrder, i.ImageId

                    """;



                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

                using var command = new SqlCommand(sql, connection);

                command.Parameters.AddWithValue("@Max", maxImages);

                for (int i = 0; i < entryIds.Count; i++)

                    command.Parameters.AddWithValue(paramNames[i], entryIds[i]);



                connection.Open();

                using var reader = command.ExecuteReader();

                while (reader.Read())

                {

                    list.Add(ReadImage(reader));

                }

            }

            catch

            {

            }



            return list;

        }).ConfigureAwait(false);

    }



    public static async Task<List<ScanBodyGalleryImageModel>> GetImagesForEntryIdsAsync(IReadOnlyList<int> entryIds)

    {

        if (entryIds.Count == 0)

            return [];



        return await Task.Run(() =>

        {

            var list = new List<ScanBodyGalleryImageModel>();

            try

            {

                var paramNames = entryIds.Select((_, i) => $"@E{i}").ToList();

                string sql = $"""

                    SELECT ImageId, EntryId, FileName, ContentType, ImageData, ThumbnailData, SortOrder

                    FROM dbo.ScanBodyGalleryImage

                    WHERE EntryId IN ({string.Join(",", paramNames)})

                    ORDER BY EntryId, SortOrder, ImageId

                    """;



                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

                using var command = new SqlCommand(sql, connection);

                for (int i = 0; i < entryIds.Count; i++)

                    command.Parameters.AddWithValue(paramNames[i], entryIds[i]);



                connection.Open();

                using var reader = command.ExecuteReader();

                while (reader.Read())

                    list.Add(ReadImage(reader));

            }

            catch

            {

            }



            return list;

        }).ConfigureAwait(false);

    }



    public static async Task<bool> InsertAsync(ScanBodyGalleryEntryDetailModel model)

    {

        return await SaveAsync(model, isUpdate: false).ConfigureAwait(false);

    }



    public static async Task<bool> UpdateAsync(ScanBodyGalleryEntryDetailModel model)

    {

        if (model.EntryId <= 0)

            return false;



        return await SaveAsync(model, isUpdate: true).ConfigureAwait(false);

    }



    public static async Task<bool> DeleteAsync(int entryId)

    {

        if (entryId <= 0)

            return false;



        return await Task.Run(() =>

        {

            try

            {

                string machine = Environment.MachineName;

                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

                connection.Open();



                using var command = new SqlCommand("""

                    UPDATE dbo.ScanBodyGalleryEntry

                    SET IsDeleted = 1,

                        ModifiedUtc = @ModifiedUtc,

                        ModifiedByMachine = @ModifiedByMachine

                    WHERE EntryId = @EntryId AND IsDeleted = 0

                    """, connection);

                command.Parameters.AddWithValue("@EntryId", entryId);

                command.Parameters.AddWithValue("@ModifiedUtc", DateTime.UtcNow);

                command.Parameters.AddWithValue("@ModifiedByMachine", machine);

                return command.ExecuteNonQuery() > 0;

            }

            catch

            {

                return false;

            }

        }).ConfigureAwait(false);

    }



    private static async Task<bool> SaveAsync(ScanBodyGalleryEntryDetailModel model, bool isUpdate)

    {

        return await Task.Run(() =>

        {

            try

            {

                if (model.Images.Count == 0)

                    return false;



                if (string.IsNullOrWhiteSpace(model.Brand))

                    return false;



                var tags = ScanBodyGalleryTagHelper.BuildTags(

                    model.Brand,

                    model.SystemName,

                    model.SizeLabel,

                    model.OtherInfo,

                    ScanBodyGalleryTagHelper.NormalizeTags([model.ManualTagsText]));



                string machine = Environment.MachineName;

                DateTime utcNow = DateTime.UtcNow;



                using var connection = new SqlConnection(ConnectionStrToStatsDatabase());

                connection.Open();

                using var transaction = connection.BeginTransaction();



                try

                {

                    int entryId = model.EntryId;



                    if (isUpdate)

                    {

                        using var update = new SqlCommand("""

                            UPDATE dbo.ScanBodyGalleryEntry

                            SET Brand = @Brand,

                                SystemName = @SystemName,

                                SizeLabel = @SizeLabel,

                                OtherInfo = @OtherInfo,

                                SortOrder = @SortOrder,

                                ModifiedUtc = @ModifiedUtc,

                                ModifiedByMachine = @ModifiedByMachine

                            WHERE EntryId = @EntryId AND IsDeleted = 0

                            """, connection, transaction);

                        update.Parameters.AddWithValue("@EntryId", entryId);

                        update.Parameters.AddWithValue("@Brand", model.Brand.Trim());

                        update.Parameters.AddWithValue("@SystemName", model.SystemName.Trim());

                        update.Parameters.AddWithValue("@SizeLabel", model.SizeLabel.Trim());

                        update.Parameters.AddWithValue("@OtherInfo", model.OtherInfo.Trim());

                        update.Parameters.AddWithValue("@SortOrder", model.SortOrder);

                        update.Parameters.AddWithValue("@ModifiedUtc", utcNow);

                        update.Parameters.AddWithValue("@ModifiedByMachine", machine);

                        if (update.ExecuteNonQuery() == 0)

                        {

                            transaction.Rollback();

                            return false;

                        }

                    }

                    else

                    {

                        using var insert = new SqlCommand("""

                            INSERT INTO dbo.ScanBodyGalleryEntry

                                (Brand, SystemName, SizeLabel, OtherInfo, SortOrder, CreatedUtc, ModifiedUtc, CreatedByMachine, ModifiedByMachine)

                            OUTPUT INSERTED.EntryId

                            VALUES

                                (@Brand, @SystemName, @SizeLabel, @OtherInfo, @SortOrder, @CreatedUtc, @ModifiedUtc, @CreatedByMachine, @ModifiedByMachine)

                            """, connection, transaction);

                        insert.Parameters.AddWithValue("@Brand", model.Brand.Trim());

                        insert.Parameters.AddWithValue("@SystemName", model.SystemName.Trim());

                        insert.Parameters.AddWithValue("@SizeLabel", model.SizeLabel.Trim());

                        insert.Parameters.AddWithValue("@OtherInfo", model.OtherInfo.Trim());

                        insert.Parameters.AddWithValue("@SortOrder", model.SortOrder);

                        insert.Parameters.AddWithValue("@CreatedUtc", utcNow);

                        insert.Parameters.AddWithValue("@ModifiedUtc", utcNow);

                        insert.Parameters.AddWithValue("@CreatedByMachine", machine);

                        insert.Parameters.AddWithValue("@ModifiedByMachine", machine);

                        entryId = Convert.ToInt32(insert.ExecuteScalar());

                    }



                    ReplaceImages(connection, transaction, entryId, model.Images);

                    ReplaceTags(connection, transaction, entryId, tags);



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



    private static void ReplaceImages(

        SqlConnection connection,

        SqlTransaction transaction,

        int entryId,

        List<ScanBodyGalleryImageModel> images)

    {

        var keepIds = images.Where(i => i.ImageId > 0).Select(i => i.ImageId).ToList();

        if (keepIds.Count == 0)

        {

            using var deleteAll = new SqlCommand(

                "DELETE FROM dbo.ScanBodyGalleryImage WHERE EntryId = @EntryId",

                connection, transaction);

            deleteAll.Parameters.AddWithValue("@EntryId", entryId);

            deleteAll.ExecuteNonQuery();

        }

        else

        {

            var paramNames = keepIds.Select((_, i) => $"@Keep{i}").ToList();

            string deleteSql =

                $"DELETE FROM dbo.ScanBodyGalleryImage WHERE EntryId = @EntryId AND ImageId NOT IN ({string.Join(",", paramNames)})";

            using var deleteCmd = new SqlCommand(deleteSql, connection, transaction);

            deleteCmd.Parameters.AddWithValue("@EntryId", entryId);

            for (int i = 0; i < keepIds.Count; i++)

                deleteCmd.Parameters.AddWithValue(paramNames[i], keepIds[i]);

            deleteCmd.ExecuteNonQuery();

        }



        int sort = 0;

        foreach (var image in images)

        {

            if (image.ImageData.Length == 0)

                continue;



            if (image.ImageData.Length > ScanBodyGalleryImageHelper.MaxImageBytes)

                throw new InvalidOperationException($"Image {image.FileName} exceeds the 5 MB limit.");



            if (image.ImageId > 0)

            {

                using var updateCmd = new SqlCommand("""

                    UPDATE dbo.ScanBodyGalleryImage

                    SET FileName = @FileName,

                        ContentType = @ContentType,

                        ImageData = @ImageData,

                        ThumbnailData = @ThumbnailData,

                        SortOrder = @SortOrder

                    WHERE ImageId = @ImageId AND EntryId = @EntryId

                    """, connection, transaction);

                updateCmd.Parameters.AddWithValue("@ImageId", image.ImageId);

                updateCmd.Parameters.AddWithValue("@EntryId", entryId);

                updateCmd.Parameters.AddWithValue("@FileName", image.FileName);

                updateCmd.Parameters.AddWithValue("@ContentType", image.ContentType);

                updateCmd.Parameters.AddWithValue("@ImageData", image.ImageData);

                updateCmd.Parameters.AddWithValue("@ThumbnailData", (object?)image.ThumbnailData ?? DBNull.Value);

                updateCmd.Parameters.AddWithValue("@SortOrder", sort++);

                updateCmd.ExecuteNonQuery();

            }

            else

            {

                using var insertCmd = new SqlCommand("""

                    INSERT INTO dbo.ScanBodyGalleryImage (EntryId, FileName, ContentType, ImageData, ThumbnailData, SortOrder)

                    VALUES (@EntryId, @FileName, @ContentType, @ImageData, @ThumbnailData, @SortOrder)

                    """, connection, transaction);

                insertCmd.Parameters.AddWithValue("@EntryId", entryId);

                insertCmd.Parameters.AddWithValue("@FileName", image.FileName);

                insertCmd.Parameters.AddWithValue("@ContentType", image.ContentType);

                insertCmd.Parameters.AddWithValue("@ImageData", image.ImageData);

                insertCmd.Parameters.AddWithValue("@ThumbnailData", (object?)image.ThumbnailData ?? DBNull.Value);

                insertCmd.Parameters.AddWithValue("@SortOrder", sort++);

                insertCmd.ExecuteNonQuery();

            }

        }

    }



    private static void ReplaceTags(SqlConnection connection, SqlTransaction transaction, int entryId, List<string> tags)

    {

        using (var deleteCmd = new SqlCommand(

            "DELETE FROM dbo.ScanBodyGalleryTagMap WHERE EntryId = @EntryId", connection, transaction))

        {

            deleteCmd.Parameters.AddWithValue("@EntryId", entryId);

            deleteCmd.ExecuteNonQuery();

        }



        foreach (string tag in tags)

        {

            int tagId = EnsureTagId(connection, transaction, tag);

            using var map = new SqlCommand("""

                IF NOT EXISTS (SELECT 1 FROM dbo.ScanBodyGalleryTagMap WHERE EntryId = @EntryId AND TagId = @TagId)

                    INSERT INTO dbo.ScanBodyGalleryTagMap (EntryId, TagId) VALUES (@EntryId, @TagId)

                """, connection, transaction);

            map.Parameters.AddWithValue("@EntryId", entryId);

            map.Parameters.AddWithValue("@TagId", tagId);

            map.ExecuteNonQuery();

        }

    }



    private static int EnsureTagId(SqlConnection connection, SqlTransaction transaction, string tagName)

    {

        using var find = new SqlCommand("SELECT TagId FROM dbo.ScanBodyGalleryTag WHERE Name = @Name", connection, transaction);

        find.Parameters.AddWithValue("@Name", tagName);

        object? existing = find.ExecuteScalar();

        if (existing is not null and not DBNull)

            return Convert.ToInt32(existing);



        using var insert = new SqlCommand("""

            INSERT INTO dbo.ScanBodyGalleryTag (Name)

            OUTPUT INSERTED.TagId

            VALUES (@Name)

            """, connection, transaction);

        insert.Parameters.AddWithValue("@Name", tagName);

        return Convert.ToInt32(insert.ExecuteScalar());

    }



    private static List<string> GetTagsForEntry(SqlConnection connection, int entryId)

    {

        var tags = new List<string>();

        using var command = new SqlCommand("""

            SELECT t.Name

            FROM dbo.ScanBodyGalleryTagMap m

            INNER JOIN dbo.ScanBodyGalleryTag t ON t.TagId = m.TagId

            WHERE m.EntryId = @EntryId

            ORDER BY t.Name

            """, connection);

        command.Parameters.AddWithValue("@EntryId", entryId);

        using var reader = command.ExecuteReader();

        while (reader.Read())

            tags.Add(reader.GetString(0));

        return tags;

    }



    private static List<ScanBodyGalleryImageModel> GetImagesForEntry(

        SqlConnection connection,

        int entryId,

        bool includeFullData)

    {

        string sql = includeFullData

            ? """

              SELECT ImageId, EntryId, FileName, ContentType, ImageData, ThumbnailData, SortOrder

              FROM dbo.ScanBodyGalleryImage

              WHERE EntryId = @EntryId

              ORDER BY SortOrder, ImageId

              """

            : """

              SELECT ImageId, EntryId, FileName, ContentType, CAST(NULL AS VARBINARY(MAX)), ThumbnailData, SortOrder

              FROM dbo.ScanBodyGalleryImage

              WHERE EntryId = @EntryId

              ORDER BY SortOrder, ImageId

              """;



        var images = new List<ScanBodyGalleryImageModel>();

        using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@EntryId", entryId);

        using var reader = command.ExecuteReader();

        while (reader.Read())

            images.Add(ReadImage(reader));

        return images;

    }



    private static ScanBodyGalleryImageModel ReadImage(SqlDataReader reader) =>

        new()

        {

            ImageId = reader.GetInt32(0),

            EntryId = reader.GetInt32(1),

            FileName = reader.GetString(2),

            ContentType = reader.GetString(3),

            ImageData = reader.IsDBNull(4) ? [] : (byte[])reader[4],

            ThumbnailData = reader.IsDBNull(5) ? null : (byte[])reader[5],

            SortOrder = reader.GetInt32(6),

        };

}

