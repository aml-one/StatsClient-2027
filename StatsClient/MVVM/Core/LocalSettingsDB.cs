using StatsClient.MVVM.Model;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Animation;

namespace StatsClient.MVVM.Core
{
    public class LocalSettingsDB
    {
        const string DataBaseFileName = "Settings.Config24";
        public static string DataBaseFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\Stats_Client\\";
        static string DataBasePath = DataBaseFolder + DataBaseFileName;

        #region Creating Local Config File
        public static string CreatingLocalConfigFiles()
        {
            Directory.CreateDirectory(DataBaseFolder);

            if (!File.Exists(DataBasePath))
                SQLiteConnection.CreateFile(DataBasePath);
                

            try
            {
                using SQLiteConnection m_dbConnection = new ("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();
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
                               ) WITHOUT ROWID;";

                SQLiteCommand command = new (sql, m_dbConnection);
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
                using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                string sql = @$"INSERT OR REPLACE INTO main.SearchHistory (SearchedText, Date, OrderBy) VALUES ('{searchedText}', '{DateTime.Now:yyyy-MM-dd}', '{DateTime.Now:yyyyMMddHHmmss}');";

                SQLiteCommand command = new(sql, m_dbConnection);
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
                using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                string sql = @$"DELETE FROM main.SearchHistory WHERE Date < '{DateTime.Now.AddDays(-1):yyyy-MM-dd}';";

                SQLiteCommand command = new(sql, m_dbConnection);
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
                    using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                    m_dbConnection.Open();
                    string sql = @$"SELECT * FROM main.SearchHistory WHERE Date = '{DateTime.Now:yyyy-MM-dd}' ORDER BY OrderBy DESC";
                    SQLiteCommand command = new(sql, m_dbConnection);
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


        public static string AddEventToEventListLocalDB(string eventStr, string eventColor = "Black")
        {
            try
            {
                using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                string sql = @$"INSERT INTO main.PMEvents (EventStr, Color, Date, OrderBy) VALUES ('{eventStr}', '{eventColor}', '{DateTime.Now:yyyy-MM-dd}', '{DateTime.Now:yyyyMMddHHmmss}');";

                SQLiteCommand command = new(sql, m_dbConnection);
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
                using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                string sql = @$"DELETE FROM main.PMEvents WHERE Date < '{DateTime.Now:yyyy-MM-dd}';";

                SQLiteCommand command = new(sql, m_dbConnection);
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
                    using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                    m_dbConnection.Open();
                    string sql = @$"SELECT * FROM main.PMEvents WHERE Date = '{DateTime.Now:yyyy-MM-dd}' ORDER BY OrderBy DESC";
                    SQLiteCommand command = new(sql, m_dbConnection);
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
                using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                string sql = @$"INSERT INTO main.IgnoredOrders (OrderID, Date) VALUES ('{orderID}', '{DateTime.Now:yyyy-MM-dd}');";

                SQLiteCommand command = new(sql, m_dbConnection);
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
                using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                string sql = @$"DELETE FROM main.IgnoredOrders WHERE Date < '{DateTime.Now:yyyy-MM-dd}';";

                SQLiteCommand command = new(sql, m_dbConnection);
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
                    using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                    m_dbConnection.Open();
                    string sql = @$"SELECT * FROM main.IgnoredOrders WHERE Date = '{DateTime.Now:yyyy-MM-dd}'";
                    SQLiteCommand command = new(sql, m_dbConnection);
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


        #region Write Local Settings with SQLite
        public static string WriteLocalSetting(string KeyName, string Value)
        {
            try
            {
                using SQLiteConnection m_dbConnection = new ("Data Source=" + DataBasePath + ";Version=3;");
                m_dbConnection.Open();

                if (Value == "True" || Value == "False")
                    Value = Value.ToLower();

                string sql = @"INSERT OR REPLACE INTO main.Settings (Name, Value) VALUES ( '" + KeyName + @"', '" + Value + @"' );";

                SQLiteCommand command = new (sql, m_dbConnection);
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
                    using SQLiteConnection m_dbConnection = new("Data Source=" + DataBasePath + ";Version=3;");
                    m_dbConnection.Open();
                    string sql = @"SELECT Value FROM main.Settings WHERE Name = '" + KeyName + @"'";
                    SQLiteCommand command = new(sql, m_dbConnection);
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
