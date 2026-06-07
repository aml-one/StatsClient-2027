using StatsClient.MVVM.Model;
using StatsClient.MVVM.Core;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Animation;

namespace StatsClient.MVVM.Core
{
    /// <summary>
    /// Local SQLite settings store (%ProgramData%\Stats_Client\Settings.Config24).
    /// <para><b>Persistence rule:</b> always persist client settings, preferences, and user-specific
    /// lists through this class — use <see cref="WriteLocalSetting"/> / <see cref="ReadLocalSetting"/>
    /// for key/value flags, or add a dedicated table + region here (see WatchList, IgnoredOrders).
    /// Do not write ad-hoc JSON/XML files under AppData for settings.</para>
    /// </summary>
    public class LocalSettingsDB
    {
        const string DataBaseFileName = "Settings.Config24";
        public static string DataBaseFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\Stats_Client\\";
        static string DataBasePath = DataBaseFolder + DataBaseFileName;

        private static readonly object DatabaseSync = new();

        private static string ConnectionString =>
            $"Data Source={DataBasePath};Version=3;Busy Timeout=5000;Journal Mode=WAL;";

        private sealed class SqliteSession : IDisposable
        {
            public SQLiteConnection Connection { get; }

            public SqliteSession()
            {
                Monitor.Enter(DatabaseSync);
                Connection = new SQLiteConnection(ConnectionString);
                Connection.Open();
            }

            public void Dispose()
            {
                Connection.Dispose();
                Monitor.Exit(DatabaseSync);
            }
        }

        private static SqliteSession OpenConnection() => new();

        #region Creating Local Config File
        public static string CreatingLocalConfigFiles()
        {
            Directory.CreateDirectory(DataBaseFolder);

            if (!File.Exists(DataBasePath))
                SQLiteConnection.CreateFile(DataBasePath);
                

            try
            {
                using var session = OpenConnection();
                string sql = @"CREATE TABLE IF NOT EXISTS main.Settings (
                                 Name   TEXT PRIMARY KEY, 
                                Value   TEXT
                               ) WITHOUT ROWID;

                               CREATE TABLE IF NOT EXISTS main.IgnoredOrders (
                              OrderID   TEXT PRIMARY KEY, 
                                 Date   TEXT
                               ) WITHOUT ROWID;
                
                               CREATE TABLE IF NOT EXISTS main.PMEvents (
                             EventStr   TEXT PRIMARY KEY, 
                                Color   TEXT,
                                 Date   TEXT,
                              OrderBy   TEXT
                               ) WITHOUT ROWID;

                               CREATE TABLE IF NOT EXISTS main.SearchHistory (
                         SearchedText   TEXT PRIMARY KEY, 
                                 Date   TEXT,
                              OrderBy   TEXT
                               ) WITHOUT ROWID;

                               CREATE TABLE IF NOT EXISTS main.WatchListEntries (
                              IntOrderID   TEXT PRIMARY KEY,
                                AddedUtc   TEXT,
                       LastProcessStatusID   TEXT,
                         LastProcessLockID   TEXT,
                       Patient_FirstName   TEXT,
                        Patient_LastName   TEXT,
                               Customer   TEXT,
                                  Items   TEXT,
                              PanNumber   TEXT,
                            ImageSource   TEXT
                               ) WITHOUT ROWID;";

                SQLiteCommand command = new (sql, session.Connection);
                command.ExecuteNonQuery();
            }
            catch (Exception ex) 
            {
                Debug.WriteLine(ex.Message);
            }

            return "all good";
        }
        #endregion

        #region Search history
        public static string AddStringToSearchHistoryLocalDB(string searchedText)
        {
            try
            {
                using var session = OpenConnection();

                string sql = @$"INSERT OR REPLACE INTO main.SearchHistory (SearchedText, Date, OrderBy) VALUES ('{searchedText}', '{DateTime.Now:yyyy-MM-dd}', '{DateTime.Now:yyyyMMddHHmmss}');";

                SQLiteCommand command = new(sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch
            {
                return "error";
            }
        }

        public static string DeleteOldSearchHistoryFromLocalDB()
        {
            try
            {
                using var session = OpenConnection();

                string sql = @$"DELETE FROM main.SearchHistory WHERE Date < '{DateTime.Now.AddDays(-1):yyyy-MM-dd}';";

                SQLiteCommand command = new(sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch
            {
                return "error";
            }
        }

        public static async Task<List<string>> GetBackAllSearchHistoryFromLocalDB()
        {
            List<string> list = [];

            if (File.Exists(DataBasePath))
            {
                try
                {
                    using var session = OpenConnection();
                    string sql = @$"SELECT * FROM main.SearchHistory WHERE Date = '{DateTime.Now:yyyy-MM-dd}' ORDER BY OrderBy DESC";
                    SQLiteCommand command = new(sql, session.Connection);
                    using SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(reader["SearchedText"].ToString()!);
                    }
                }
                catch
                {
                }
            }

            await Task.Delay(10);

            return list;
        }
        #endregion Search history




        #region event Table


        public static string AddEventToEventListLocalDB(string eventStr, string? eventColor = null)
        {
            eventColor ??= ColorSchemeResourceCatalog.GetNamedColorString("NamedColorString_Black");
            try
            {
                using var session = OpenConnection();

                string sql = @$"INSERT INTO main.PMEvents (EventStr, Color, Date, OrderBy) VALUES ('{eventStr}', '{eventColor}', '{DateTime.Now:yyyy-MM-dd}', '{DateTime.Now:yyyyMMddHHmmss}');";

                SQLiteCommand command = new(sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch
            {
                return "error";
            }
        }

        public static string DeleteOldPMEventsFromLocalDB()
        {
            try
            {
                using var session = OpenConnection();

                string sql = @$"DELETE FROM main.PMEvents WHERE Date < '{DateTime.Now:yyyy-MM-dd}';";

                SQLiteCommand command = new(sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch
            {
                return "error";
            }
        }

        public static async Task<List<PMEventModel>> GetBackAllEventFromLocalDB()
        {
            List<PMEventModel> list = [];

            if (File.Exists(DataBasePath))
            {
                try
                {
                    using var session = OpenConnection();
                    string sql = @$"SELECT * FROM main.PMEvents WHERE Date = '{DateTime.Now:yyyy-MM-dd}' ORDER BY OrderBy DESC";
                    SQLiteCommand command = new(sql, session.Connection);
                    using SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string time = reader["OrderBy"].ToString()!.Replace(DateTime.Now.ToString("yyyyMMdd"), "")[..4];

                        if (time.StartsWith('0'))
                            time = time[1..];

                        list.Add(new PMEventModel
                        {
                            Color = reader["Color"].ToString(),
                            EventStr = reader["EventStr"].ToString(),
                            TimeStr = time,
                        });
                    }
                }
                catch
                {
                }
            }

            await Task.Delay(10);

            return list;
        }
        #endregion event Table

        #region IgnoredOrdersList Table
        public static string AddOrderToIgnoredListLocalDB(string orderID)
        {
            try
            {
                using var session = OpenConnection();

                string sql = @$"INSERT INTO main.IgnoredOrders (OrderID, Date) VALUES ('{orderID}', '{DateTime.Now:yyyy-MM-dd}');";

                SQLiteCommand command = new(sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch
            {
                return "error";
            }
        }
        
        public static string DeleteOldOrderToIgnoredListLocalDB()
        {
            try
            {
                using var session = OpenConnection();

                string sql = @$"DELETE FROM main.IgnoredOrders WHERE Date < '{DateTime.Now:yyyy-MM-dd}';";

                SQLiteCommand command = new(sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch
            {
                return "error";
            }
        }

        public static async Task<List<InconsistencyModel>> GetBackAllOrderToBeIgnoredFromLocalDB()
        {
            List<InconsistencyModel> list = [];

            if (File.Exists(DataBasePath))
            {
                try
                {
                    using var session = OpenConnection();
                    string sql = @$"SELECT * FROM main.IgnoredOrders WHERE Date = '{DateTime.Now:yyyy-MM-dd}'";
                    SQLiteCommand command = new(sql, session.Connection);
                    using SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new InconsistencyModel
                        {
                            OrderID = reader["OrderID"].ToString()!,
                            Ignored = true,
                        });
                    }
                }
                catch
                {
                }
            }

            await Task.Delay(10);

            return list;
        }
        #endregion IgnoredOrdersList Table

        #region WatchList Table
        public static List<WatchListEntry> LoadWatchListEntries()
        {
            var list = new List<WatchListEntry>();
            if (!File.Exists(DataBasePath))
                return list;

            try
            {
                using var session = OpenConnection();
                string sql = @"SELECT IntOrderID, AddedUtc, LastProcessStatusID, LastProcessLockID,
                                      Patient_FirstName, Patient_LastName, Customer, Items, PanNumber, ImageSource
                               FROM main.WatchListEntries";
                using var command = new SQLiteCommand(sql, session.Connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string addedRaw = reader["AddedUtc"]?.ToString() ?? "";
                    _ = DateTime.TryParse(addedRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime addedUtc);

                    list.Add(new WatchListEntry
                    {
                        IntOrderID = reader["IntOrderID"]?.ToString() ?? "",
                        AddedUtc = addedUtc,
                        LastProcessStatusID = reader["LastProcessStatusID"]?.ToString(),
                        LastProcessLockID = reader["LastProcessLockID"]?.ToString(),
                        Patient_FirstName = reader["Patient_FirstName"]?.ToString(),
                        Patient_LastName = reader["Patient_LastName"]?.ToString(),
                        Customer = reader["Customer"]?.ToString(),
                        Items = reader["Items"]?.ToString(),
                        PanNumber = reader["PanNumber"]?.ToString(),
                        ImageSource = reader["ImageSource"]?.ToString(),
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return list.Where(e => !string.IsNullOrWhiteSpace(e.IntOrderID)).ToList();
        }

        public static void UpsertWatchListEntry(WatchListEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.IntOrderID))
                return;

            Directory.CreateDirectory(DataBaseFolder);
            CreatingLocalConfigFiles();

            if (entry.AddedUtc == default)
                entry.AddedUtc = DateTime.UtcNow;

            try
            {
                using var session = OpenConnection();
                string sql = @"INSERT OR REPLACE INTO main.WatchListEntries
                    (IntOrderID, AddedUtc, LastProcessStatusID, LastProcessLockID,
                     Patient_FirstName, Patient_LastName, Customer, Items, PanNumber, ImageSource)
                    VALUES (@id, @added, @status, @lock, @fn, @ln, @cust, @items, @pan, @img)";
                using var command = new SQLiteCommand(sql, session.Connection);
                command.Parameters.AddWithValue("@id", entry.IntOrderID);
                command.Parameters.AddWithValue("@added", entry.AddedUtc.ToString("o"));
                command.Parameters.AddWithValue("@status", entry.LastProcessStatusID ?? "");
                command.Parameters.AddWithValue("@lock", entry.LastProcessLockID ?? "");
                command.Parameters.AddWithValue("@fn", entry.Patient_FirstName ?? "");
                command.Parameters.AddWithValue("@ln", entry.Patient_LastName ?? "");
                command.Parameters.AddWithValue("@cust", entry.Customer ?? "");
                command.Parameters.AddWithValue("@items", entry.Items ?? "");
                command.Parameters.AddWithValue("@pan", entry.PanNumber ?? "");
                command.Parameters.AddWithValue("@img", entry.ImageSource ?? "");
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static void RemoveWatchListEntry(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId) || !File.Exists(DataBasePath))
                return;

            try
            {
                using var session = OpenConnection();
                using var command = new SQLiteCommand(
                    "DELETE FROM main.WatchListEntries WHERE IntOrderID = @id", session.Connection);
                command.Parameters.AddWithValue("@id", orderId);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static int PurgeWatchListEntriesOlderThanDays(int days)
        {
            if (days <= 0 || !File.Exists(DataBasePath))
                return 0;

            string cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            try
            {
                using var session = OpenConnection();
                using var command = new SQLiteCommand(
                    "DELETE FROM main.WatchListEntries WHERE AddedUtc < @cutoff", session.Connection);
                command.Parameters.AddWithValue("@cutoff", cutoff);
                return command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return 0;
            }
        }

        #endregion WatchList Table


        #region Write Local Settings with SQLite
        public static string WriteLocalSetting(string KeyName, string Value)
        {
            try
            {
                using var session = OpenConnection();

                if (Value == "True" || Value == "False")
                    Value = Value.ToLower();

                string sql = @"INSERT OR REPLACE INTO main.Settings (Name, Value) VALUES ( '" + KeyName + @"', '" + Value + @"' );";

                SQLiteCommand command = new (sql, session.Connection);
                command.ExecuteNonQuery();
                return "all good";
            }
            catch (Exception ex) 
            {
                Debug.WriteLine(ex.Message);
                return "error";
            }
        }
        #endregion

        #region Read Local Settings with SQLite

        public static string ReadLocalSetting(String KeyName)
        {
            if (File.Exists(DataBasePath))
            {
                try
                {
                    using var session = OpenConnection();
                    string sql = @"SELECT Value FROM main.Settings WHERE Name = '" + KeyName + @"'";
                    SQLiteCommand command = new(sql, session.Connection);
                    using SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                        return (String)reader.GetValue(0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            return "";
        }
        #endregion
    }
}
