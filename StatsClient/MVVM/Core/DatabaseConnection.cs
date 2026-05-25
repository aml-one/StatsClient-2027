using System;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Threading;

namespace StatsClient.MVVM.Core;

public class DatabaseConnection
{
    public static string SQLMonitordbName = "SQLMonitor";
    public static string ManagementXdbName = "ManagementXDB";
    public static string StatsdbName = "StatsDB";

    public static string StatsdbPasswd = "";//"Stats_Server_Pass-7!";
    public static string StatsdbUserName = "";//"StatsServer";

    public static string StatsdbInstance = "";//"STATSSERVER";
    public static string StatsServerAddress = ""; // LocalSettingsDB.ReadLocalSetting("StatsServerAddress");

    public static void SetCredentials()
    {
        try
        {
            string[] credParts = BaseConfigReader.ReadBaseSettings().Split('|');
            
            StatsdbUserName = credParts[0];
            StatsdbPasswd = credParts[1];
            StatsdbInstance = credParts[2];
            StatsServerAddress = credParts[3];
            StatsdbName = credParts[4];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
    }

    #region >> Generating connection strings
    public static string ConnectionStrToStatsDatabase()
    {
        string connectionString = "user id=" + StatsdbUserName + ";" +
                            "password=" + StatsdbPasswd + ";server=" + StatsServerAddress + "\\" + StatsdbInstance + ";" +
                            "Trusted_Connection=no;" +
                            "Encrypt=false;" +
                            "database=" + StatsdbName + "; " +
                            "connection timeout=10";

        //MessageBox.Show(connectionString);

        return connectionString;
    }

    public static string ConnectionStrToSQLLoadDatabase()
    {
        string connectionString = "user id=" + StatsdbUserName + ";" +
                            "password=" + StatsdbPasswd + ";server=" + StatsServerAddress + "\\" + StatsdbInstance + ";" +
                            "Trusted_Connection=no;" +
                            "Encrypt=false;" +
                            "database=" + SQLMonitordbName + "; " +
                            "connection timeout=10";

        return connectionString;
    }

    public static string ConnectionStrToManagementXDatabase()
    {
        string connectionString = "user id=" + StatsdbUserName + ";" +
                            "password=" + StatsdbPasswd + ";server=" + StatsServerAddress + "\\" + StatsdbInstance + ";" +
                            "Trusted_Connection=no;" +
                            "Encrypt=false;" +
                            "database=" + ManagementXdbName + "; " +
                            "connection timeout=10";

        return connectionString;
    }

    public static string ConnectionStrFor3Shape()
    {
        //sa 3SDMdbmspw
        string dbPasswd = "3SDentalManager";
        string dbUserName = "DentalManager";
        string dbDatabase = "DentalManager";
        string dbInstance = "";

        if (GetServerDatabasePasswd() != "")
            dbPasswd = GetServerDatabasePasswd();

        if (GetServerDatabaseUser() != "")
            dbUserName = GetServerDatabaseUser();

        if (GetServerDatabaseName() != "")
            dbDatabase = GetServerDatabaseName();

        if (GetServerDatabaseInstance() != "")
            dbInstance = "\\" + GetServerDatabaseInstance();

        string serverAddress = GetServerAddress();

        string connectionString = "user id=" + dbUserName + ";" +
                                  "password=" + dbPasswd + ";server=" + serverAddress + dbInstance + ";" +
                                  "Trusted_Connection=no;" +
                                  "Encrypt=false;" +
                                  "database=" + dbDatabase + "; " +
                                  "connection timeout=10";

        return connectionString;
    }

    #endregion

    #region >> Getting 3Shape Server info
    public static string GetServerDatabasePasswd()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) dbPasswd FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
        return "";
    }

    public static string GetServerDatabaseUser()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) dbUser FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
        return "";
    }

    public static string GetServerDatabaseName()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) dbName FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new (connectionString);
            SqlCommand command = new (query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
        return "";
    }

    public static string GetServerSiteName()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) SiteName FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
        return "";
    }

    public static string GetServerFileDirectory()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) FileDirectory FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
        return "";
    }

    public static string GetServerDatabaseInstance()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) DatabaseInstance FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
        }
        return "";
    }

    public static string GetServerAddress()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) DataBaseAddress FROM dbo.ThreeShapeServer";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
            return ex.Message;
        }
        return "";
    }

    //public static string GetServerName(int ServerId)
    //{
    //    try
    //    {
    //        string connectionString = ConnectionStrToStatsDatabase();
    //        string query = @"SELECT TOP(1) FriendlyName FROM dbo.ThreeShapeServer";

    //        using SqlConnection connection = new(connectionString);
    //        SqlCommand command = new(query, connection);
    //        connection.Open();

    //        using SqlDataReader reader = command.ExecuteReader();
    //        while (reader.Read())
    //        {
    //            var value = reader[0].ToString();
    //            if (value != null)
    //                return value;
    //        }
    //    }
    //    catch
    //    {
    //    }
    //    return "";
    //}
    #endregion

    public static String WriteStatsSetting(String sName, String sValue)
    {
        try
        {
            String connectionString = ConnectionStrToStatsDatabase();

            if (sValue == "True" || sValue == "False")
                sValue = sValue.ToLower();

            String query = @"merge dbo.Settings with(HOLDLOCK) as target
                                 using (values ('" + sName + @"', '" + sValue + @"'))
                                     as source (sName, sValue)
                                     on target.sName = '" + sName + @"'
                                 when matched then
                                     update
                                     set sName = source.sName,
                                         sValue = source.sValue
                                 when not matched then
                                     insert (sName, sValue)
                                     values (source.sName, source.sValue);
                               ";

            RunSQLCommandAsynchronously(query, connectionString);

            return "all good";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
            return ex.Message;
        }
    }

    public static String ReadStatsSetting(String sName)
    {
        try
        {
            String connectionString = ConnectionStrToStatsDatabase();
            String query = @"SELECT sValue FROM dbo.Settings WHERE sName = '" + sName + @"'";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var value = reader["sValue"].ToString();
                        if (value != null)
                            return value;
                    }
                }
            }
        }
        catch (Exception ex)
        { 
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
            return "";
        }
        return "";
    }

    public static string RunSQLCommandAsynchronously(string commandText, string connectionString)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            try
            {
                SqlCommand command = new SqlCommand(commandText, connection);
                connection.Open();

                IAsyncResult result = command.BeginExecuteNonQuery();
                while (!result.IsCompleted)
                {
                    // Wait for 1/10 second, so the counter
                    // does not consume all available resources 
                    // on the main thread.
                    Thread.Sleep(100);
                }
                return command.EndExecuteNonQuery(result).ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
                return ex.Message;
            }

        }
    }

    public static String RunSQLCommandWithExpectedResult(string commandText, string connectionString)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(commandText, connection);
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var value = reader[0].ToString();
                        if (value != null)
                            return value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
            return "";
        }
        return "";
    }

    public static String RunSQLCommandWithRowCountResult(string commandText, string connectionString)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            try
            {
                SqlCommand command = new SqlCommand(commandText, connection);
                connection.Open();

                IAsyncResult result = command.BeginExecuteNonQuery();
                while (!result.IsCompleted)
                {
                    // Wait for 1/10 second, so the counter
                    // does not consume all available resources 
                    // on the main thread.
                    Thread.Sleep(100);
                }
                String resultText = command.EndExecuteNonQuery(result).ToString();
                return resultText.Substring(resultText.Length - 1, 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{ex.LineNumber()}] {ex.Message}");
                return ex.Message.Substring(ex.Message.Length - 1, 1);
            }

        }
    }
}
