using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.ViewModel;
using Syncfusion.Windows.Shared;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.Functions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace StatsClient.MVVM.Core;

public partial class DatabaseOperations
{

    public static string GetLastRebuiltDateForArchives()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP (1) LastUdated FROM dbo.Archives ORDER BY LastUdated DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                _ = DateTime.TryParse(value, out DateTime dt);
                return dt.ToString("M/d/yyyy");
            }
        }
        catch
        {
            return "";
        }
        return "";
    }

    public static int GetTotalOrdersForArchives()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT COUNT(OrderID) FROM dbo.Archives";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                _ = int.TryParse(value, out int result);
                return result;
            }
        }
        catch
        {
            return 0;
        }
        return 0;
    }

    public static string GetOldestOrdersDateForArchives()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP (1) CreateDate FROM dbo.Archives ORDER BY CreateDate ASC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                string dt = UnixTimeStampToDateTime(value, false);
                string[] dateParts = dt.Split(" ");
                return dateParts[0];
            }
        }
        catch
        {
            return "";
        }
        return "";
    }

    public static string GetOrdersBetweenDatesForArchives()
    {
        string OldestOrder = "", LatestOrder = "";
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP (1) CreateDate FROM dbo.Archives ORDER BY CreateDate ASC";

            using (SqlConnection connection = new(connectionString))
            {
                SqlCommand command = new(query, connection);
                connection.Open();

                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var value = reader[0].ToString();
                    string dt = UnixTimeStampToDateTime(value, true);
                    string[] dateParts = dt.Split("-");
                    OldestOrder = dateParts[0];
                }
            }

            string query2 = @"SELECT TOP (1) CreateDate FROM dbo.Archives ORDER BY CreateDate DESC";

            using (SqlConnection connection = new(connectionString))
            {
                SqlCommand command = new(query2, connection);
                connection.Open();

                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var value = reader[0].ToString();
                    string dt = UnixTimeStampToDateTime(value, true);
                    string[] dateParts = dt.Split("-");
                    LatestOrder = dateParts[0];
                }
            }
        }
        catch
        {
            return "";
        }
        return OldestOrder + " - " + LatestOrder;
    }


    public static async Task<List<InconsistencyModel>> GetPrescriptionInconsistencys()
    {
        List<InconsistencyModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT OrderID, Prescriptions.PanNumber 
                              FROM dbo.DigitalCasesToday DigiCases 
                              FULL OUTER JOIN PrescriptionNumbers Prescriptions ON Prescriptions.PanNumber = DigiCases.PanNumber
                              ORDER BY PanNumber ASC, OrderID ASC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? orderID = reader["OrderID"].ToString();
                string? panNumber = reader["PanNumber"].ToString();

                orderID ??= "";
                panNumber ??= "";

                if (orderID == "" || panNumber == "")
                    list.Add(new InconsistencyModel
                    {
                        OrderID = orderID,
                        PanNumber = panNumber,
                    });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
            return list;
        }

        return list;
    }

    public static async Task<List<InconsistencyModel>> GetPrescriptionWithNoInconsistencys()
    {
        List<InconsistencyModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT OrderID, Prescriptions.PanNumber 
                              FROM dbo.DigitalCasesToday DigiCases 
                              FULL OUTER JOIN PrescriptionNumbers Prescriptions ON Prescriptions.PanNumber = DigiCases.PanNumber
                              ORDER BY PanNumber ASC, OrderID ASC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? orderID = reader["OrderID"].ToString();
                string? panNumber = reader["PanNumber"].ToString();

                orderID ??= "";
                panNumber ??= "";

                if (orderID != "" && panNumber != "")
                    list.Add(new InconsistencyModel
                    {
                        OrderID = orderID,
                        PanNumber = panNumber,
                    });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
            return list;
        }

        return list;
    }

    public static async Task<List<AvailablePanCountModel>> GetBackAllAvailablePanNumberListCount(double NumberFontSize, double TitleFontSize, double NamesFontSize)
    {
        List<AvailablePanCountModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT Count([Owner]), [Owner] FROM dbo.PMPanNumbers GROUP BY [Owner]";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!list!.Where(x => x.ComputerName == reader["Owner"].ToString()).Any())
                {
                    if (int.TryParse(reader[0].ToString(), out int count))
                        list.Add(new AvailablePanCountModel
                        {
                            Count = count,
                            ComputerName = reader["Owner"].ToString(),
                            FriendlyName = ReadComputerName(reader["Owner"].ToString()!),
                            NumberFontSize = NumberFontSize,
                            TitleFontSize = TitleFontSize,
                            NamesFontSize = NamesFontSize,
                        });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
            return list;
        }

        return list;
    }

    public static string GetAgeByDate(string date)
    {
        if (DateTime.TryParse(date, out DateTime lastUpdate))
        {
            var diffInSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
            TimeSpan time = TimeSpan.FromSeconds(diffInSeconds);

            double displayTime = Math.Round(time.TotalMinutes);

            if (displayTime == 0)
                return "Just now";
            else if (displayTime == 1)
                return $"{displayTime} minute ago";
            else if (displayTime < 60)
                return $"{displayTime} minutes ago";
            else if (displayTime > 119)
                return $"{Math.Round(displayTime / 60)}+ hours ago";
            else if (displayTime >= 70)
                return $"{Math.Round(displayTime / 60)}+ hour ago";
            else if (displayTime >= 60)
                return $"{Math.Round(displayTime / 60)} hour ago";
        }
        return "";
    }

    public static double GetAgeByDateInSeconds(string date)
    {
        if (DateTime.TryParse(date, out DateTime lastUpdate))
        {
            var diffInSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
            TimeSpan time = TimeSpan.FromSeconds(diffInSeconds);

            double displayTime = Math.Round(time.TotalSeconds);

            return displayTime;
        }
        return 100;
    }

    public static async Task RemoveTestEntrysFromImportHistory()
    {
        string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
        string queryAddLastModify = @$"DELETE FROM dbo.ImportHistory WHERE OrderID LIKE '00000-%'";
        RunSQLCommandAsynchronously(queryAddLastModify, connectionString);
    }

    public static async Task AddTestEntryToImportHistory()
    {
        string orderID = "00000-" + DateTime.Now.ToString("ss") + "-B1-JOHNDOE-DOCTOR-SYSTEM-SCR";

        string DateTimeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string ImportTime = DateTime.Now.ToString("h:mm tt");
        string OrderBy = DateTime.Now.ToString("yyyyMMddHHmmss");


        string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
        string queryAddLastModify = @$"INSERT INTO dbo.ImportHistory (OrderID, DesignerID, FriendlyName, ImportPath, DateTime, ImportTime, Event, OrderBy) 
                                                   VALUES ('{orderID}', 'dsg', 'RandomDesigner', '', '{DateTimeStr}', '{ImportTime}', 'got designed by', '{OrderBy}')";
        RunSQLCommandAsynchronously(queryAddLastModify, connectionString);
    }

    public static async Task<List<ImportHistoryModel>> GetBackImportHistory(double multiplier = 1)
    {
        List<ImportHistoryModel> importHistory = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT * FROM dbo.ImportHistory WHERE OrderBy > '{DateTime.Now:yyyyMMdd}000001' ORDER BY OrderBy DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!importHistory!.Where(x =>
                    x.OrderID == reader["OrderID"].ToString() &&
                    x.ImportTime == reader["ImportTime"].ToString()
                    ).Any())
                {
                    string age = GetAgeByDate(reader["DateTime"].ToString()!);
                    double ageInSeconds = GetAgeByDateInSeconds(reader["DateTime"].ToString()!);
                    importHistory.Add(new ImportHistoryModel
                    {
                        OrderID = reader["OrderID"].ToString(),
                        DesignerID = reader["DesignerID"].ToString(),
                        FriendlyName = reader["FriendlyName"].ToString(),
                        ImportPath = reader["ImportPath"].ToString(),
                        DateTime = reader["DateTime"].ToString(),
                        ImportTime = reader["ImportTime"].ToString(),
                        Event = reader["Event"].ToString(),
                        OrderBy = reader["OrderBy"].ToString(),
                        Age = age,
                        AgeInSeconds = ageInSeconds,
                        Multiplier = multiplier,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
            return importHistory;
        }

        return importHistory;
    }

    public static async Task<List<ExportHistoryModel>> GetBackExportHistory()
    {
        List<ExportHistoryModel> exportHistory = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT * FROM dbo.ExportHistory WHERE OrderBy > '{DateTime.Now:yyyyMMdd}000001' ORDER BY OrderBy DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!exportHistory!.Where(x =>
                    x.OrderID == reader["OrderID"].ToString() &&
                    x.ExportTime == reader["ExportTime"].ToString()
                    ).Any())
                {
                    exportHistory.Add(new ExportHistoryModel
                    {
                        OrderID = reader["OrderID"].ToString(),
                        DesignerID = reader["DesignerID"].ToString(),
                        FriendlyName = reader["FriendlyName"].ToString(),
                        ExportPath = reader["ExportPath"].ToString(),
                        DateTime = reader["DateTime"].ToString(),
                        ExportTime = reader["ExportTime"].ToString(),
                        Event = reader["Event"].ToString(),
                        OrderBy = reader["OrderBy"].ToString(),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
            return exportHistory;
        }

        return exportHistory;
    }


    public static async Task<List<DesignerModel>> GetDesignersListAtStartAsync()
    {
        List<DesignerModel> designers = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT * FROM dbo.Designers";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!designers!.Where(x =>
                    x.DesignerID == reader["DesignerID"].ToString() ||
                    x.FriendlyName == reader["FriendlyName"].ToString()
                    ).Any())
                {
                    designers.Add(new DesignerModel
                    {
                        DesignerID = reader["DesignerID"].ToString(),
                        FriendlyName = reader["FriendlyName"].ToString(),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
            return designers;
        }

        return designers;
    }


    public static async Task<ObservableCollection<HealthReportModel>>? GetHealthReportsAsync()
    {
        ObservableCollection<HealthReportModel> modelList = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT * FROM dbo.ServiceHealth";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();
            /*
              TaskName
              ServiceName
              LastReport
              OneBeforeLastReport
              ExpectedDifference
              ExpectedDifferenceNightTime
              NightHoursStart
              NightHoursEnd
              CurrentTime
              NoNightTime
            */
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string currentTime = reader["CurrentTime"].ToString()!;
                string serviceStatus = "Healthy";
                string foreColor = "LightGreen";

                _ = int.TryParse(reader["ExpectedDifference"].ToString(), out int expectedDifference);
                _ = int.TryParse(reader["ExpectedDifferenceNightTime"].ToString(), out int expectedDifferenceNightTime);
                _ = int.TryParse(reader["NightHoursStart"].ToString(), out int nightHoursStart);
                _ = int.TryParse(reader["NightHoursEnd"].ToString(), out int nightHoursEnd);
                bool notRunningNightTime = false;

                if (reader["NoNightTime"].ToString() == "1")
                    notRunningNightTime = true;

                string lastReport = reader["LastReport"].ToString()!;
                string oneBeforeLastReport = reader["OneBeforeLastReport"].ToString()!;
                double calculatedDifference = -1;
                double lastReportVsTimeNowDifference = -1;
                double TwentyPercent = expectedDifference * 0.2;
                double FourtyPercent = expectedDifference * 0.4;

                int hours = DateTime.Now.Hour;
                if (!notRunningNightTime && (hours >= nightHoursStart || hours <= nightHoursEnd))
                    expectedDifference = expectedDifferenceNightTime;

                DateTime TimeNow = DateTime.Now;

                if (DateTime.TryParse(currentTime, out DateTime dtCurrentTime))
                {
                    if (DateTime.TryParse(lastReport, out DateTime dtLastReport))
                    {
                        if (DateTime.TryParse(oneBeforeLastReport, out DateTime dtOneBeforeLastReport))
                        {
                            calculatedDifference = (dtLastReport - dtOneBeforeLastReport).TotalSeconds;
                            lastReportVsTimeNowDifference = (TimeNow - dtLastReport).TotalSeconds;

                            // supposed to run night time
                            if (!notRunningNightTime)
                            {
                                if (hours >= nightHoursStart || hours <= nightHoursEnd)
                                {
                                    if (calculatedDifference < expectedDifference + 300)
                                    {
                                        if (Math.Round(lastReportVsTimeNowDifference) < expectedDifference + 60)
                                        {
                                            serviceStatus = "Healthy";
                                            goto CheckColor;
                                        }
                                    }
                                }
                            }
                            // supossed to sleep night time
                            else
                            {
                                if (hours >= nightHoursStart || hours <= nightHoursEnd)
                                {
                                    if (calculatedDifference < expectedDifference + 300)
                                    {
                                        if (Math.Round(lastReportVsTimeNowDifference) < expectedDifference + 12 * 60 * 60)
                                        {
                                            serviceStatus = "Sleeping";
                                            goto CheckColor;
                                        }
                                    }
                                }
                            }


                            if ((TimeNow - dtCurrentTime).TotalSeconds > 75 || lastReportVsTimeNowDifference > expectedDifference + 90)
                            {
                                serviceStatus = "Dead / Stopped";
                                goto CheckColor;
                            }

                            if (calculatedDifference > expectedDifference + 50 || lastReportVsTimeNowDifference > expectedDifference + 70)
                            {
                                serviceStatus = "Struggling";
                                goto CheckColor;
                            }

                            if (calculatedDifference > expectedDifference + 25 || lastReportVsTimeNowDifference > expectedDifference + 40)
                            {
                                serviceStatus = "Late to report";
                                goto CheckColor;
                            }
                        }
                        else
                        {
                            serviceStatus = "Dead / Stopped";
                        }
                    }
                    else
                    {
                        serviceStatus = "Dead / Stopped";
                    }
                }
                else
                {
                    serviceStatus = "Dead / Stopped";
                }

            CheckColor:

                switch (serviceStatus)
                {
                    case "Healthy":
                        foreColor = "LightGreen";
                        break;

                    case "Sleeping":
                        foreColor = "LightBlue";
                        break;

                    case "Late to report":
                        foreColor = "Yellow";
                        break;

                    case "Struggling":
                        foreColor = "#f5bd5d";
                        break;

                    case "Dead / Stopped":
                        foreColor = "#ff7a95";
                        break;
                }

                modelList.Add(new HealthReportModel
                {
                    TaskName = reader["TaskName"].ToString(),
                    ServiceName = reader["ServiceName"].ToString(),
                    LastReport = lastReport,
                    OneBeforeLastReport = oneBeforeLastReport,
                    ExpectedDifference = expectedDifference,
                    ExpectedDifferenceNightTime = expectedDifferenceNightTime,
                    NightHoursStart = nightHoursStart,
                    NightHoursEnd = nightHoursEnd,
                    CurrentTime = currentTime,
                    NoNightTime = notRunningNightTime,
                    ServiceStatus = serviceStatus,
                    ForeColor = foreColor,
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[" + ex.LineNumber() + "] (DataBaseOperations)" + ex.Message);
        }

        return modelList;
    }

    public static async Task<TaskModel> GetPendingTaskFromDatabase()
    {
        string lastCommandID = await GetLastCommandId();

        TaskModel task = new();
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT TOP 1 * FROM dbo.ClientTasks ORDER BY Id DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader["Task"].ToString();
                if (value is not null)
                {
                    if (reader["Id"].ToString() == lastCommandID)
                        return task;
                    else
                    {
                        _ = DateTime.TryParse(reader["Time"].ToString(), out DateTime dTime);

                        task.Id = reader["Id"].ToString();
                        task.Task = value;
                        task.ComputerName = reader["ComputerName"].ToString();
                        task.Time = dTime;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{ex.LineNumber()} - {ex.Message}");
        }
        return task;
    }

    private static async Task<string> GetLastCommandId()
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT LastCommandId FROM dbo.ClientStatus WHERE ComputerName = '{Environment.MachineName}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value is not null)
                    return value;
            }
        }
        catch (Exception)
        {
        }
        return "-1";
    }

    public static async Task WriteDownLastCommandId(string id)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"UPDATE dbo.ClientStatus SET LastCommandId = '{id}' WHERE ComputerName = '{Environment.MachineName}'";

            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception)
        {
        }
    }

    public static async Task ResetPingDifferenceInDatabaseOnClose()
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"UPDATE dbo.ClientStatus SET PingDifference = '0',
                                                    PingDifferenceBefore = '-999'
                              WHERE ComputerName = '{Environment.MachineName}'";

            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception)
        {
        }
    }


    public static async Task AddOrUpdateLabnextManualPair(string OrderID, string LabnextID)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            string query = @$"merge dbo.LabnextManualPair with(HOLDLOCK) as target
                                 using (values ('{OrderID}', '{LabnextID}', '{Environment.MachineName}', '{DateTime.Now:yyyy-MM-dd HH:mm:ss}'))
                                     as source (OrderID, LabnextID, ComputerName, DateTime)
                                     on target.LabnextID = '{LabnextID}'
                                 when matched then
                                     update
                                     set OrderID = source.OrderID,
                                       LabnextID = source.LabnextID,
                                    ComputerName = source.ComputerName,
                                        DateTime = source.DateTime
                                 when not matched then
                                     insert (OrderID, LabnextID, ComputerName, DateTime)
                                     values (source.OrderID, source.LabnextID, source.ComputerName, source.DateTime);
                                 ";

            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception)
        {
        }
    }

    public static async Task<bool> GetOrderIDAssignedToPaymentIssue(int labnextID, string intOrderID)
    {
        LabnextIssueModel model = new();

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT * FROM dbo.LabnextIssues WHERE LabnextID = '{labnextID}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader["PanNumber"].ToString(), out int panNumber);
                _ = int.TryParse(reader["UnitCount"].ToString(), out int unitCount);
                _ = double.TryParse(reader["Price"].ToString(), out double price);

                model.CreationDate = reader["CreationDate"].ToString();
                model.InvoiceDate = reader["InvoiceDate"].ToString();
                model.PanNumber = panNumber;
                model.Status = reader["Status"].ToString();
                model.Patient_FirstName = reader["Patient_FirstName"].ToString();
                model.Patient_LastName = reader["Patient_LastName"].ToString();
                model.UnitCount = unitCount;
                model.Items = reader["Items"].ToString();
                model.TeethNumbers = reader["TeethNumbers"].ToString();
                model.Price = price;
                model.Issue = reader["Issue"].ToString();
                model.InvoiceDateRange = reader["InvoiceDateRange"].ToString();
                model.DesignerName = reader["DesignerName"].ToString();
                model.DesignerID = reader["DesignerID"].ToString();
                model.LabnextID = labnextID;
                model.Customer = reader["Customer"].ToString();

            }
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, $"[{ex.LineNumber()}] {ex.Message}", "DatabaseOperations");
            return false;
        }


        string designerID = model.DesignerID!;
        try
        {
            string PaymentID = $"{designerID}{intOrderID}";
            string touchedBy = Environment.MachineName;

            
            bool isItRedo = false;
            if (model.UnitCount == 0)
                isItRedo = true;

            string dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string tTime = DateTime.Now.ToString("h:mm tt");

            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"merge dbo.PaymentHistory with(HOLDLOCK) as target
                                 using (values ('{PaymentID}', '{intOrderID}', '{designerID}', '{model.DesignerName}', '', '{dateTime}', '{tTime}', '{isItRedo}', '0', '0', '0', '0', '{model.PanNumber}', '{model.Patient_FirstName}', '{model.Patient_LastName}',
                                                '{labnextID}', '{model.CreationDate}', '{model.InvoiceDate}', '{model.Status}', '{model.UnitCount}', '', '{model.TeethNumbers}', '{model.Price}', '1', '', '{model.InvoiceDateRange}', '{touchedBy}', '0', '{model.Customer}'))
                                     as source (PaymentID, OrderID, DesignerID, FriendlyName, ImportPath, DateTime, ImportTime, IsitRedo, Crowns, Gingiva, Abutments, TotalUnits, LxPanNumber, LxPatient_FirstName, LxPatient_LastName,
                                                LxLabnextID, LxCreationDate, LxInvoiceDate, LxStatus, LxUnitCount, LxItems, LxTeethNumbers, LxPrice, LxPaid, LxIssue, LxInvoiceDateRange, ProcessedBy, IsAutoProcess, Customer)
                                     on target.PaymentID = '{PaymentID}'
                                 when matched then
                                     update
                                     set LxLabnextID = source.LxLabnextID,
                                      LxCreationDate = source.LxCreationDate,
                                       LxInvoiceDate = source.LxInvoiceDate,
                                            LxStatus = source.LxStatus,
                                         LxUnitCount = source.LxUnitCount,
                                             LxItems = source.LxItems,
                                      LxTeethNumbers = source.LxTeethNumbers,
                                             LxPrice = source.LxPrice,
                                              LxPaid = source.LxPaid,
                                             LxIssue = source.LxIssue,
                                  LxInvoiceDateRange = source.LxInvoiceDateRange,
                                          DesignerID = source.DesignerID,
                                        FriendlyName = source.FriendlyName,
                                         ProcessedBy = source.ProcessedBy,
                                       IsAutoProcess = source.IsAutoProcess,
                                            Customer = source.Customer
                                  
                                   when not matched then
                                     insert (PaymentID, OrderID, DesignerID, FriendlyName, ImportPath, DateTime, ImportTime, IsitRedo, Crowns, Gingiva, Abutments, TotalUnits, LxPanNumber, LxPatient_FirstName, LxPatient_LastName,
                                             LxLabnextID, LxCreationDate, LxInvoiceDate, LxStatus, LxUnitCount, LxItems, LxTeethNumbers, LxPrice, LxPaid, LxIssue, LxInvoiceDateRange, ProcessedBy, IsAutoProcess, Customer)
                                     values (source.PaymentID, source.OrderID, source.DesignerID, source.FriendlyName, source.ImportPath, source.DateTime, source.ImportTime, source.IsitRedo, source.Crowns, source.Gingiva, source.Abutments, source.TotalUnits, source.LxPanNumber, source.LxPatient_FirstName, source.LxPatient_LastName,
                                             source.LxLabnextID, source.LxCreationDate, source.LxInvoiceDate, source.LxStatus, source.LxUnitCount, source.LxItems, source.LxTeethNumbers, source.LxPrice, source.LxPaid, source.LxIssue, source.LxInvoiceDateRange, source.ProcessedBy, source.IsAutoProcess, source.Customer);
           
                                 ";

            RunSQLCommandAsynchronously(query, connectionString);


            await AddPaymentHistoryEventToDatabase(PaymentID, intOrderID, designerID, model.DesignerName, labnextID.ToString(), model.InvoiceDate, model.PanNumber.ToString(), model.Patient_FirstName, model.Patient_LastName, model.InvoiceDateRange, touchedBy);


        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, $"[{ex.LineNumber()}] {ex.Message}", "DatabaseOperations");
            Debug.WriteLine($"({ex.LineNumber}) {ex.Message}");
            return false;
        }

        return true;
    }

    private static async Task AddPaymentHistoryEventToDatabase(string paymentID, string threeShapeOrderId, string designerID, string designerName, string labnextId, string invoiceDate, string panNumber, string patient_Firstname, string patient_Lastname, string? invoiceDateRange, string touchedBy)
    {
        try
        {
            string touchedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"merge dbo.PaymentHistoryEvents with(HOLDLOCK) as target
                                 using (values ('{paymentID}', '{threeShapeOrderId}', '{designerID}', '{designerName}', '{touchedTime}', '{touchedBy}', '{labnextId}', '{invoiceDate}', '{panNumber}', '{patient_Firstname}', '{patient_Lastname}', '{invoiceDateRange}'))
                                     as source (PaymentID, OrderID, DesignerID, FriendlyName, TouchTime, TouchedBy, LabnextID, InvoiceDate, PanNumber, Patient_FirstName, Patient_LastName, InvoiceDateRange)
                                     on target.PaymentID = '{paymentID}'
                                   when not matched then
                                     insert (PaymentID, OrderID, DesignerID, FriendlyName, TouchTime, TouchedBy, LabnextID, InvoiceDate, PanNumber, Patient_FirstName, Patient_LastName, InvoiceDateRange)
                                     values (source.PaymentID, source.OrderID, source.DesignerID, source.FriendlyName, source.TouchTime, source.TouchedBy, source.LabnextID, source.InvoiceDate, source.PanNumber, source.Patient_FirstName, source.Patient_LastName, source.InvoiceDateRange);
                             ";

            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, $"[{ex.LineNumber()}] {ex.Message}", "DatabaseOperations");
            Debug.WriteLine($"({ex.LineNumber}) {ex.Message}");
        }
    }

    public static async Task RemovePaymentIssueFromPaymentIssuesTable(string labnextId)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"DELETE FROM dbo.LabnextIssues WHERE LabnextID = '{labnextId}'";
            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, $"[{ex.LineNumber()}] {ex.Message}", "DatabaseOperations");
            Debug.WriteLine($"({ex.LineNumber}) {ex.Message}");
        }
    }

    public static async Task<int> GetPaymentIssueCountFromDB()
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT COUNT(Id) FROM dbo.LabnextIssues";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                return (int)reader[0];
            }
        }
        catch (Exception)
        {
        }
        return 0;
    }

    public static async Task<List<DesignerPaymentSummary>> GetDesignerPaymentSummaryFromDB()
    {
        List<DesignerPaymentSummary> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT DesignerName, COUNT(Id) FROM dbo.LabnextIssues GROUP BY DesignerName";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                DesignerPaymentSummary model = new()
                {
                    DesignerName = reader["DesignerName"].ToString(),
                    PaymentIssues = (int)reader[1],
                };
                list.Add(model);
            }
        }
        catch (Exception)
        {
        }
        return list;
    }

    public static async Task<List<string>> GetDoublePaidOrderIDsFromDB()
    {
        // get all the orders where friendly name in payment history is different than import history and is not a redo
        List<string> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT ph.OrderID OrderID, ih.IsitRedo redo
                                FROM dbo.PaymentHistory ph 
                                INNER JOIN ImportHistory ih ON ih.OrderID = ph.OrderID
                              WHERE ph.FriendlyName NOT LIKE ih.FriendlyName
                              ORDER BY ih.DateTime DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? redo = reader["redo"].ToString();
                string? orderID = reader["OrderID"].ToString();

                if (string.IsNullOrEmpty(redo))
                    redo = "false";

                if (redo.Equals("false", StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrEmpty(orderID) && !list.Contains(orderID))
                    list.Add(orderID);

                //MainViewModel.Instance.AddDebugLine(null, $"******* Conflicted order: {orderID}", "DBO");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            MainViewModel.Instance.AddDebugLine(ex, ex.Message, "DBO");
        }

        //MainViewModel.Instance.AddDebugLine(null, $"Total conflicted orders: {list.Count}", "DBO");

        return list;
    }
    public static async Task<List<DoublePaidOrdersModel>> GetDoublePaidOrdersListFromDB()
    {
        List<string> orderIDs = await GetDoublePaidOrderIDsFromDB();


        List<DoublePaidOrdersModel> doublePaidOrders = [];


        foreach (string orderID in orderIDs)
        {
            string? DesignerName = "";
            string? DesignDate = "";
            string? DesignTime = "";

            string? GotPaid = "";

            string? SecondDesignerName = "";
            string? SecondDesignDate = "";
            string? SecondDesignTime = "";

            try
            {
                string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
                string query = $@"SELECT ih.OrderID OrderID, ih.FriendlyName DesignerName, ih.DateTime DateTime, ih.ImportTime ImportTime, ih.PanNumber PanNumber, ih.Patient_Lastname Patient_Lastname, ih.Patient_Firstname Patient_Firstname, ph.FriendlyName PaidDesigner, ph.LxLabnextID, ph.LxInvoiceDate, ph.LxInvoiceDateRange FROM dbo.ImportHistory ih
                                  FULL JOIN PaymentHistory ph ON  ih.OrderID = ph.OrderID
                                  WHERE ih.OrderID = '{orderID}' ORDER BY ih.Id DESC";

                using SqlConnection connection = new(connectionString);
                SqlCommand command = new(query, connection);
                connection.Open();

                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.IsNullOrEmpty(DesignerName))
                        DesignerName = reader["DesignerName"].ToString();

                    if (string.IsNullOrEmpty(DesignDate))
                        DesignDate = reader["DateTime"].ToString();

                    if (string.IsNullOrEmpty(DesignTime))
                        DesignTime = reader["ImportTime"].ToString();



                    if (!string.IsNullOrEmpty(DesignerName) && DesignerName != reader["FriendlyName"].ToString())
                        SecondDesignerName = reader["FriendlyName"].ToString();

                    if (!string.IsNullOrEmpty(DesignDate) && DesignDate != reader["DateTime"].ToString())
                        SecondDesignDate = reader["DateTime"].ToString();

                    if (!string.IsNullOrEmpty(DesignTime) && DesignTime != reader["ImportTime"].ToString())
                        SecondDesignTime = reader["ImportTime"].ToString();

                    _ = int.TryParse(reader["PanNumber"].ToString(), out int panNumber);
                    _ = int.TryParse(reader["LxLabnextID"].ToString(), out int labnextID);

                    if (!string.IsNullOrEmpty(DesignerName) &&
                        !string.IsNullOrEmpty(DesignDate) &&
                        !string.IsNullOrEmpty(DesignTime) &&
                        !string.IsNullOrEmpty(SecondDesignerName) &&
                        !string.IsNullOrEmpty(SecondDesignDate) &&
                        !string.IsNullOrEmpty(SecondDesignTime))
                    {
                        doublePaidOrders.Add(new DoublePaidOrdersModel
                        {
                            DesignerName = DesignerName,
                            DesignDate = DesignDate,
                            DesignTime = DesignTime,
                            SecondDesignerName = SecondDesignerName,
                            SecondDesignDate = SecondDesignDate,
                            SecondDesignTime = SecondDesignTime,
                            OrderID = orderID,
                            PanNumber = panNumber,
                            Patient_Firstname = reader["Patient_Firstname"].ToString(),
                            Patient_Lastname = reader["Patient_Lastname"].ToString(),
                            GotPaid = reader["PaidDesigner"].ToString(),
                            LxInvoiceDate = reader["LxInvoiceDate"].ToString(),
                            LxInvoiceDateRange = reader["LxInvoiceDateRange"].ToString(),
                            LxLabnextID = labnextID,
                        });
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        return doublePaidOrders;
    }

    public static async Task<List<PaidToWrongPersonOrdersModel>> GetPaidToWrongPersonsOrdersListFromDB()
    {
        List<string> orderIDs = await GetDoublePaidOrderIDsFromDB();


        List<PaidToWrongPersonOrdersModel> list = [];

        int i = 0;
        int v = 0;
        foreach (string orderID in orderIDs)
        {

            try
            {
                string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
                string query = $@"SELECT TOP 1 ih.OrderID OrderID, ih.FriendlyName DesignerName, ih.DateTime DesignDateTime, ih.ImportTime ImportTime, ih.PanNumber PanNumber, ih.Patient_Lastname Patient_Lastname, ih.Patient_Firstname Patient_Firstname, ph.FriendlyName PaidDesigner, ph.LxLabnextID, ph.LxInvoiceDate, ph.LxInvoiceDateRange 
                                  FROM dbo.PaymentHistory ph 
                                  INNER JOIN ImportHistory ih ON ih.OrderID = ph.OrderID
                                  WHERE ih.OrderID = '{orderID}' ORDER BY ih.Id DESC";

                using SqlConnection connection = new(connectionString);
                SqlCommand command = new(query, connection);
                connection.Open();

                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    _ = int.TryParse(reader["PanNumber"].ToString(), out int panNumber);
                    _ = int.TryParse(reader["LxLabnextID"].ToString(), out int labnextID);


                    if (!list.Any(x => x.OrderID == orderID))
                    {
                        list.Add(new PaidToWrongPersonOrdersModel
                        {
                            DesignerName = reader["DesignerName"].ToString(),
                            DesignDate = reader["DesignDateTime"].ToString(),
                            DesignTime = reader["ImportTime"].ToString(),
                            OrderID = orderID,
                            PanNumber = panNumber,
                            Patient_Firstname = reader["Patient_Firstname"].ToString(),
                            Patient_Lastname = reader["Patient_Lastname"].ToString(),
                            GotPaid = reader["PaidDesigner"].ToString(),
                            LxInvoiceDate = reader["LxInvoiceDate"].ToString(),
                            LxInvoiceDateRange = reader["LxInvoiceDateRange"].ToString(),
                            LxLabnextID = labnextID,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex, ex.Message, "DBO");
            }

            await Task.Delay(50);
        }


        await CalculateStatisticsOfWrongfulPayments(list);

        return [.. list.OrderBy(x => x.DesignerName)];
    }

    private static async Task CalculateStatisticsOfWrongfulPayments(List<PaidToWrongPersonOrdersModel> list)
    {
        List<WrongfulPaymentsModel> wlist = [];

        foreach (var item in list)
        {
            if (wlist.Any(x => x.PaidDesigner == item.GotPaid))
            {
                wlist.FirstOrDefault(x => x.PaidDesigner == item.GotPaid)!.PaidDesigner = item.GotPaid;
                wlist.FirstOrDefault(x => x.PaidDesigner == item.GotPaid)!.DidNotGetPaidDesigner = item.DesignerName;
                wlist.FirstOrDefault(x => x.PaidDesigner == item.GotPaid)!.PaidCases++;
            }
            else
            {
                wlist.Add(new WrongfulPaymentsModel
                {
                    PaidDesigner = item.GotPaid,
                    DidNotGetPaidDesigner = item.DesignerName,
                    PaidCases = 1,
                });
            }
        }

        foreach (var item in list)
        {
            wlist.FirstOrDefault(x => x.PaidDesigner == item.GotPaid)!.PaidUnits += await GetBackUnitsOfPaidOrder(item.OrderID, item.LxLabnextID, item.GotPaid);
        }

        MainViewModel.Instance.WrongfullyPaidCasesList = wlist;
    }

    private static async Task<int> GetBackUnitsOfPaidOrder(string? orderID, int? lxLabnextID, string? gotPaid)
    {
        int units = 0;
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT TOP 1 * FROM dbo.PaymentHistory 
                              WHERE OrderID = '{orderID}' AND LxLabnextID = '{lxLabnextID}' AND FriendlyName = '{gotPaid}'
                              ORDER BY Id DESC";


            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader["LxUnitCount"].ToString(), out units);
            }
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, ex.Message, "DBO");
        }

        return units;
    }


    public static async Task<List<LabnextIssueModel>> GetAllCasesWithIssues(string designerName)
    {
        List<LabnextIssueModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT * FROM dbo.LabnextIssues WHERE DesignerName = '{designerName}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader["PanNumber"].ToString(), out int panNumber);
                _ = int.TryParse(reader["UnitCount"].ToString(), out int unitCount);
                _ = int.TryParse(reader["LabnextID"].ToString(), out int labnextID);
                _ = double.TryParse(reader["Price"].ToString(), out double price);

                list.Add(new LabnextIssueModel
                {
                    CreationDate = reader["CreationDate"].ToString(),
                    InvoiceDate = reader["InvoiceDate"].ToString(),
                    PanNumber = panNumber,
                    Status = reader["Status"].ToString(),
                    Patient_FirstName = reader["Patient_FirstName"].ToString(),
                    Patient_LastName = reader["Patient_LastName"].ToString(),
                    UnitCount = unitCount,
                    Items = reader["Items"].ToString(),
                    TeethNumbers = reader["TeethNumbers"].ToString(),
                    Price = price,
                    Issue = reader["Issue"].ToString(),
                    InvoiceDateRange = reader["InvoiceDateRange"].ToString(),
                    DesignerName = reader["DesignerName"].ToString(),
                    DesignerID = reader["DesignerID"].ToString(),
                    LabnextID = labnextID,
                    Customer = reader["Customer"].ToString(),
                });
            }
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, ex.Message, "DBO");
        }
        return list;
    }

    //public static async Task<List<ThreeShapeOrdersModel>> GetPossibleOrderMatchesForLabnextIssueCaseFromArchives(LabnextIssueModel selectedPaymentIssueForDesigner)
    //{

    //}

    //public static async Task<List<ThreeShapeOrdersModel>> GetPossibleOrderMatchesForLabnextIssueCaseFrom3Shape(LabnextIssueModel selectedPaymentIssueForDesigner)
    //{

    //}

    public static async Task ReportClientLoginToDatabase(bool InitialReport = false)
    {
        try
        {
            string ComputerName = Environment.MachineName;
            string Ping = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string AppVersion = await GetAppVersion();
            string LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            string memoryUsage = Math.Round(await GetMemoryUsage() / (1024 * 1024)).ToString();

            string lastPingDifference = await GetLastReportTimeFromClientApp();
            string serverPing = ReadStatsSetting("ServerPing");

            string query = "";
            if (InitialReport)
                query = @$"merge dbo.ClientStatus with(HOLDLOCK) as target
                                 using (values ('{ComputerName}', '{Ping}', '0', '0', '{AppVersion}', '{LastLogin}', '-1', '{memoryUsage}'))
                                     as source (ComputerName, Ping, PingDifference, PingDifferenceBefore, AppVersion, LastLogin, LastCommandId, MemoryUsage)
                                     on target.ComputerName = '{ComputerName}'
                                 when matched then
                                     update
                                     set ComputerName = source.ComputerName,
                                                 Ping = source.Ping,
                                       PingDifference = source.PingDifference,
                                 PingDifferenceBefore = source.PingDifferenceBefore,
                                           AppVersion = source.AppVersion,
                                            LastLogin = source.LastLogin,
                                          MemoryUsage = source.MemoryUsage
                                 when not matched then
                                     insert (ComputerName, Ping, PingDifference, PingDifferenceBefore, AppVersion, LastLogin, LastCommandId, MemoryUsage)
                                     values (source.ComputerName, source.Ping, source.PingDifference, source.PingDifferenceBefore, source.AppVersion, source.LastLogin, source.LastCommandId, source.MemoryUsage);
                                 ";
            else
                query = @$"merge dbo.ClientStatus with(HOLDLOCK) as target
                                 using (values ('{ComputerName}', '{Ping}', '{lastPingDifference}', '{memoryUsage}'))
                                     as source (ComputerName, Ping, PingDifferenceBefore, MemoryUsage)
                                     on target.ComputerName = '{ComputerName}'
                                 when matched then
                                     update
                                     set ComputerName = source.ComputerName,
                                                 Ping = source.Ping,
                                 PingDifferenceBefore = source.PingDifferenceBefore,
                                          MemoryUsage = source.MemoryUsage;
                                 ";

            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, ex.Message, "DBO");
        }
    }

    public static async Task<double> GetMemoryUsage()
    {
        try
        {
            var memory = 0.0;
            using Process proc = Process.GetCurrentProcess();
            memory = proc.PrivateMemorySize64;

            return memory;
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, null, "DBO");
            return 0;
        }
    }

    public static async Task<double> GetTotalMemoryInGiB()
    {
        try
        {
            var mem = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
            return mem / (1024 * 1024 * 1024);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, null, "DBO");
            return 0;
        }
    }

    public static async Task<double> GetTotalMemoryInMiB()
    {
        try
        {
            var mem = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
            return mem / (1024 * 1024);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, null, "DBO");
            return 0;
        }
    }

    private static async Task<string> GetLastReportTimeFromClientApp()
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = $@"SELECT PingDifference FROM dbo.ClientStatus WHERE ComputerName = '{Environment.MachineName}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value is not null)
                    return value;
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("A network-related or instance-specific error", StringComparison.CurrentCultureIgnoreCase))
                MainViewModel.Instance.ThreeShapeServerIsDown = true;
            else
                MainViewModel.Instance.AddDebugLine(ex, null, "DBO");
        }
        return "";
    }

    public static async Task<List<string>> GetCustomerSuggestionsReplacementList(string customerName)
    {
        List<string> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query;

            query = @$"SELECT NewName FROM dbo.CustomerSuggestion WHERE CustomerName = '{customerName}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["NewName"].ToString()!);
            }
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex, null, "DBO");
        }

        return list;
    }

    public static async Task<List<string>> GetCustomerSuggestionsCustomerNamesList()
    {
        List<string> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query;

            query = @$"SELECT CustomerName FROM dbo.CustomerSuggestion GROUP by CustomerName";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["CustomerName"].ToString()!);
            }
        }
        catch (Exception)
        {

        }

        return list;
    }

    public static async Task<List<CommentRulesModel>> GetCommentRulesList()
    {
        List<CommentRulesModel> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query;

            query = @$"SELECT * FROM dbo.CommentRules ORDER BY Customer ASC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new CommentRulesModel
                {
                    Customer = reader["Customer"].ToString()!,
                    RuleName = reader["RuleName"].ToString(),
                    ItemsContains = reader["ItemsContains"].ToString()!,
                    ExtraText = reader["ExtraText"].ToString()!,
                });
            }
        }
        catch (Exception)
        {

        }

        return list;
    }

    public static async Task<List<DesignerUnitsModel>> GetDesignerUnitsModel()
    {
        List<DesignerUnitsModel> list = [];


        List<DesignerModel> designers = await GetDesignersModel();

        foreach (var designer in designers)
        {
            string designerID = designer.DesignerID!;
            double totalUnits = 0;

            try
            {
                string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
                string query;

                query = @$"SELECT * FROM dbo.CheckedOutCases WHERE Designer = '{designerID}'";

                using SqlConnection connection = new(connectionString);
                SqlCommand command = new(query, connection);
                connection.Open();

                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    double crowns = 0;
                    double abutments = 0;

                    if (reader["Crowns"].ToString() is not null)
                        _ = double.TryParse(reader["Crowns"].ToString(), out crowns);
                    if (reader["Abutments"].ToString() is not null)
                        _ = double.TryParse(reader["Abutments"].ToString(), out abutments);

                    totalUnits += crowns + abutments;
                }
            }
            catch (Exception ex)
            {

            }

            list.Add(new DesignerUnitsModel()
            {
                DesignerID = designerID,
                TotalUnits = totalUnits,
            });

        }


        return list;
    }

    public static async Task<List<string>> GetAccountInfoCategories()
    {
        List<string> list = [];
        list.Add("All");

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"SELECT Category FROM dbo.AccountInfos GROUP BY Category";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader[0].ToString()!);
            }
        }
        catch (Exception)
        {
        }

        return list;
    }

    public static async Task<List<AccountInfoModel>> GetAccountInfoList(Dictionary<string, string> bgBorderColors)
    {
        List<AccountInfoModel> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"SELECT * FROM dbo.AccountInfos ORDER BY Category ASC, SubCategory ASC, FriendlyName ASC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[] data = Convert.FromBase64String(reader["Credentials"].ToString()!);
                string json = Encoding.UTF8.GetString(data);

                AccountCredential credentials = JsonConvert.DeserializeObject<AccountCredential>(json)!;

                string subCategory = reader["SubCategory"].ToString()!;

                string bgBorderColor = bgBorderColors.FirstOrDefault(x => x.Value == subCategory).Key;
                if (string.IsNullOrEmpty(bgBorderColor))
                {
                    string colorKey = bgBorderColors.FirstOrDefault(x => x.Value == "").Key;
                    bgBorderColor = colorKey;
                    bgBorderColors[colorKey] = subCategory;
                }

                AccountInfoModel model = new()
                {
                    FriendlyName = reader["FriendlyName"].ToString(),
                    Category = reader["Category"].ToString(),
                    SubCategory = subCategory,
                    Website = reader["Website"].ToString(),
                    Credentials = credentials,
                    ApplicationName = reader["ApplicationName"].ToString(),
                    ApplicationPath = reader["ApplicationPath"].ToString(),
                    Color = bgBorderColor
                };

                list.Add(model);
                MainViewModel.Instance.BgBorderColors = bgBorderColors;
            }
        }
        catch (Exception)
        {
        }

        return list;
    }


    public static async Task<List<CheckedOutCasesModel>> GetCheckedOutCasesFromStatsDatabase(string designer)
    {
        List<CheckedOutCasesModel> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query;

            if (designer.Equals("both", StringComparison.CurrentCultureIgnoreCase))
                query = @$"SELECT * FROM dbo.CheckedOutCases";
            else
                query = @$"SELECT * FROM dbo.CheckedOutCases WHERE Designer = '{designer}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                CheckedOutCasesModel model = new();
                model.CaseID = reader["CaseID"].ToString();
                model.OrderID = reader["OrderID"].ToString();
                model.Crowns = reader["Crowns"].ToString();
                model.Abutments = reader["Abutments"].ToString();
                model.Models = reader["Models"].ToString();

                int crowns = 0;
                int abutments = 0;
                if (model.Crowns is not null)
                    _ = int.TryParse(model.Crowns, out crowns);
                if (model.Abutments is not null)
                    _ = int.TryParse(model.Abutments, out abutments);

                model.TotalUnits = (crowns + abutments).ToString();
                model.Comment = reader["Comment"].ToString();
                model.SentOn = reader["SentOn"].ToString();
                model.Items = reader["Items"].ToString();
                model.Manufacturer = reader["Manufacturer"].ToString();
                model.Rush = reader["Rush"].ToString();
                model.Designer = reader["Designer"].ToString();
                model.Directory = reader["Directory"].ToString();
                model.MaxProcessStatusID = reader["MaxProcessStatusID"].ToString();
                model.ProcessLockID = reader["ProcessLockID"].ToString();
                model.ScanSource = reader["ScanSource"].ToString();
                model.CommentIcon = reader["CommentIcon"].ToString();
                model.CommentColor = reader["CommentColor"].ToString();
                model.CommentIn3Shape = reader["CommentIn3Shape"].ToString();
                model.EncodeCase = reader["EncodeCase"].ToString();

                if (model.Directory != null)
                    model.Directory = model.Directory.Replace(@"\", "|");

                list.Add(model);
            }
        }
        catch (Exception)
        {
        }

        return list;
    }

    public static async Task<List<DesignerModel>> GetDesignersModel()
    {
        List<DesignerModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT * FROM dbo.Designers";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DesignerModel
                {
                    DesignerID = reader["DesignerID"].ToString(),
                    FriendlyName = reader["FriendlyName"].ToString(),
                });
            }

        }
        catch (Exception)
        {
        }

        return list;
    }

    public static async Task AddOrUpdatePanNumber(string Color, string Number, string FriendlyName)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            string query = @$"merge dbo.PanNumbers with(HOLDLOCK) as target
                                 using (values ('{Number}', '{Color}', '{FriendlyName}'))
                                     as source (PanNumber, Color, FriendlyName)
                                     on target.PanNumber = '{Number}'
                                 when matched then
                                     update
                                     set Color = source.Color,
                                         PanNumber = source.PanNumber,
                                         FriendlyName = source.FriendlyName
                                 when not matched then
                                     insert (PanNumber, Color, FriendlyName)
                                     values (source.PanNumber, source.Color, source.FriendlyName);
                                 ";

            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception)
        {
        }
    }

    public static async Task<List<PanColorModel>> GetAvailablePanColorsAsync()
    {
        List<PanColorModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT Color, FriendlyName FROM dbo.PanColors";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var color = RgbToBrushConverter(reader["Color"].ToString()!);
                bool isItDarkColor = CheckIfItsDarkColor(reader["Color"].ToString()!);
                list.Add(new PanColorModel
                {
                    Color = color,
                    RgbColor = reader["Color"].ToString(),
                    FriendlyName = reader["FriendlyName"].ToString(),
                    IsItDarkColor = isItDarkColor
                });
            }

        }
        catch (Exception)
        {
        }
        return list;
    }


    public static async Task<bool> DeleteCustomer(string customerName)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"DELETE FROM dbo.CustomerSuggestion WHERE CustomerName = '{customerName}'";

            RunSQLCommandAsynchronously(query, connectionString);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public static async Task<bool> DeleteCustomerSuggestion(string customerName, string customerSuggestion)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"DELETE FROM dbo.CustomerSuggestion WHERE CustomerName = '{customerName}' AND NewName = '{customerSuggestion}'";

            RunSQLCommandAsynchronously(query, connectionString);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<bool> AddNewCustomerSuggestion(string originalCustomer, string suggestedCustomer)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            string query = @$"SELECT NewName FROM dbo.CustomerSuggestion WHERE CustomerName = '{originalCustomer}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null && value == suggestedCustomer)
                    return false;
            }


            string queryStr = $@"INSERT INTO dbo.CustomerSuggestion (CustomerName, NewName)
                              VALUES ('{originalCustomer}','{suggestedCustomer}')";

            if (!string.IsNullOrEmpty(RunSQLCommandWithExpectedResult(queryStr, connectionString)))
                return true;
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    public static async Task<List<string>> CustomerHasSuggestedName(string customer)
    {
        List<string> list = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"SELECT NewName FROM dbo.CustomerSuggestion WHERE CustomerName = '{customer}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader[0].ToString();
                if (value != null)
                    list.Add(value);
            }
        }
        catch (Exception)
        {
        }

        return list;
    }


    public static async Task<bool> DeleteCommentRule(CommentRulesModel rule)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string queryStr = $@"DELETE FROM dbo.CommentRules WHERE RuleName = '{rule.RuleName}' AND Customer = '{rule.Customer}' AND ItemsContains = '{rule.ItemsContains}' AND ExtraText = '{rule.ExtraText}';";

            if (!string.IsNullOrEmpty(RunSQLCommandWithExpectedResult(queryStr, connectionString)))
                return true;
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    public static async Task<bool> AddNewCommentRule(string ruleName, string customer, string comment, string item)
    {
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string queryStr = $@"INSERT INTO dbo.CommentRules (RuleName, Customer, ItemsContains, ExtraText)
                              VALUES ('{ruleName}','{customer}','{item}','{comment}')";

            if (!string.IsNullOrEmpty(RunSQLCommandWithExpectedResult(queryStr, connectionString)))
                return true;
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }

    public static async Task<bool> PanNumberIsValid(int panNumber)
    {
        try
        {
            string connectionstring = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @$"SELECT PanNumber FROM dbo.PanNumbers WHERE PanNumber = '{panNumber}';";

            if (!string.IsNullOrEmpty(RunSQLCommandWithExpectedResult(query, connectionstring)))
                return true;
        }
        catch (Exception)
        {
        }

        return false;
    }

    public static async Task<string> GetDigiSystemName(string intOrderID)
    {
        string digiSystem = "";

        try
        {
            string connectionString = await Task.Run(ConnectionStrFor3Shape);
            string queryString = @$"SELECT ScanSource FROM Orders 
                                    WHERE IntOrderID = '{intOrderID}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string scanSource = reader[0].ToString()!;
                digiSystem = ConvertScanSourceToDigiSystemName(scanSource);
            }
        }
        catch (Exception)
        {

        }

        return digiSystem;
    }


    public static async Task<string> GetToothNumbersString(string intOrderID)
    {
        string toothNumberString = "";
        try
        {
            List<int> ToothNumbers = [];

            string connectionString = await Task.Run(ConnectionStrFor3Shape);
            string queryString = @$"SELECT 
                                      IntOrderID, 
                                      Patient_FirstName, 
                                      Patient_LastName, 
                                      OrderComments, 
                                      o.Items, 
                                      OperatorName, 
                                      Customer, 
                                      o.ManufName, 
                                      o.CacheMaterialName, 
                                      ScanSource, 
                                      CacheMaxScanDate, 
                                      TraySystemType, 
                                      MaxCreateDate, 
                                      MaxProcessStatusID, 
                                      ProcessStatusID, 
                                      AltProcessStatusID, 
                                      ModelHeight, 
                                      ProcessLockID, 
                                      m.ModelJobID,  
                                      te.ToothNumber 

                                    FROM Orders o 
                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                                    FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                                    FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                                    FULL OUTER JOIN ToothElement te ON me.ModelElementID = te.ModelElementID 

                                    WHERE o.IntOrderID = '{intOrderID}' AND ToothNumber <> ''";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["ToothNumber"].ToString() != "")
                {
                    if (int.TryParse(reader["ToothNumber"].ToString(), out int toothNr))
                        ToothNumbers.Add(toothNr);
                }
            }

            int ToothNrLow = ToothNumbers.Min();
            int ToothNrHigh = ToothNumbers.Max();

            if (ToothNrHigh == ToothNrLow)
                toothNumberString = ToothNrHigh.ToString();
            else
                toothNumberString = ToothNrLow.ToString() + "-" + ToothNrHigh.ToString();
        }
        catch (Exception)
        {
        }

        return toothNumberString;
    }

    public static void UpdateLastModifyDateinDatabase(string intOrderID)
    {
        string connectionString = ConnectionStrFor3Shape();
        bool isInDatabase = false;
        string LastModifyDateAndTimeHelper = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ".518";
        // Checking if this is the same computer which has edited the order the las time

        try
        {
            string queryString = $"SELECT * FROM OrderHistory WHERE OrderID = '{intOrderID}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["UserID"].ToString() == Environment.MachineName)
                    isInDatabase = true;
            }

        }
        catch (Exception)
        {
        }

        // if this is the same computer then updating the LastModify entry in database
        if (isInDatabase)
        {
            // If this is the computer which edited the Order the last time then UPDATE the entry in database
            try
            {
                string queryAddLastModify = $@"UPDATE OrderHistory SET ModificationDate = '{LastModifyDateAndTimeHelper}'
                                               WHERE OrderID = '{intOrderID}' AND UserID = '{Environment.MachineName}'";
                RunSQLCommandAsynchronously(queryAddLastModify, connectionString);
            }
            catch (Exception)
            {
            }
        }
        else
        {
            // If this isn't the computer which edited the Order the last time then INSERT new entry into database
            try
            {
                string queryAddLastModify = @$"INSERT INTO OrderHistory (OrderID, UserID, ModificationDate) 
                                               VALUES ('{intOrderID}', '{Environment.MachineName}', '{LastModifyDateAndTimeHelper}')";
                RunSQLCommandAsynchronously(queryAddLastModify, connectionString);
            }
            catch (Exception)
            {
            }
        }
    }

    public static async Task LockOrderIn3Shape(string intOrderID)
    {
        try
        {
            string connectionString = ConnectionStrFor3Shape();
            string queryUpdateProcessStatus = @$"UPDATE ModelElement SET ProcessLockID = 'plLocked'
                                                    FROM Orders o 
                                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                                                    FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                                                    FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                                                    FULL OUTER JOIN ToothElement te ON me.ModelElementID = te.ModelElementID 
                                                  WHERE  (o.IntOrderID = '{intOrderID}')";

            await Task.Run(() => RunSQLCommandAsynchronously(queryUpdateProcessStatus, connectionString));
        }
        catch (Exception)
        {

        }
    }

    public static async Task UnLockOrderIn3Shape(string intOrderID)
    {
        try
        {
            string connectionString = ConnectionStrFor3Shape();
            string queryUpdateProcessStatus = @$"UPDATE ModelElement SET ProcessLockID = 'plReady'
                                                    FROM Orders o 
                                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                                                    FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                                                    FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                                                    FULL OUTER JOIN ToothElement te ON me.ModelElementID = te.ModelElementID 
                                                  WHERE  (o.IntOrderID = '{intOrderID}')";

            await Task.Run(() => RunSQLCommandAsynchronously(queryUpdateProcessStatus, connectionString));
        }
        catch (Exception)
        {

        }
    }

    public static async Task CheckOutOrderIn3Shape(string intOrderID)
    {
        try
        {
            string connectionString = ConnectionStrFor3Shape();
            string queryUpdateProcessStatus = @$"UPDATE ModelElement SET ProcessLockID = 'plCheckedOut'
                                                    FROM Orders o 
                                                    FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                                                    FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                                                    FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                                                    FULL OUTER JOIN ToothElement te ON me.ModelElementID = te.ModelElementID 
                                                  WHERE  (o.IntOrderID = '{intOrderID}')";

            await Task.Run(() => RunSQLCommandAsynchronously(queryUpdateProcessStatus, connectionString));
        }
        catch (Exception)
        {

        }
    }


    public static async Task<ObservableCollection<ThreeShapeOrdersModel>> GetNewOrdersCreatedByMe(bool showCasesWithoutNumber = true)
    {
        ObservableCollection<ThreeShapeOrdersModel> list = [];
        try
        {
            string queryString = @$"SELECT 


                              IntOrderID, 
                              Patient_FirstName, 
                              Patient_LastName, 
                              Patient_RefNo, 
                              o.ExtOrderID, 
                              o.OriginalOrderID, 
                              OrderComments, 
                              o.Items, 
                              OperatorName, 
                              Customer, 
                              o.ManufName, 
                              o.CacheMaterialName, 
                              ScanSource, 
                              CacheMaxScanDate, 
                              TraySystemType, 
                              MaxCreateDate, 
                              MaxProcessStatusID, 
                              ProcessStatusID, 
                              AltProcessStatusID, 
                              ProcessLockID,  
                              WasSent, 
                              ModificationDate, 
                              UserID 


                FROM Orders o 
                            FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                            FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                            FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                            FULL OUTER JOIN OrderHistory oh ON oh.OrderID = o.IntOrderID 

                            WHERE MaxProcessStatusID = 'psCreated' AND UserID = '{Environment.MachineName}'

                
                    GROUP BY 
                              IntOrderID, 
                              Patient_FirstName, 
                              Patient_LastName, 
                              Patient_RefNo, 
                              o.ExtOrderID, 
                              o.OriginalOrderID, 
                              OrderComments, 
                              o.Items, 
                              OperatorName, 
                              Customer, 
                              o.ManufName, 
                              o.CacheMaterialName, 
                              ScanSource, 
                              CacheMaxScanDate, 
                              TraySystemType, 
                              MaxCreateDate, 
                              MaxProcessStatusID, 
                              ProcessStatusID, 
                              AltProcessStatusID, 
                              ProcessLockID,  
                              WasSent, 
                              ModificationDate, 
                              UserID 
                

            


            ORDER BY IntOrderID ASC, oh.ModificationDate DESC";
            string connectionString = ConnectionStrFor3Shape();

            try
            {
                using SqlConnection connection = new(connectionString);
                SqlCommand command = new(queryString, connection);
                connection.Open();
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string panNumber;
                    string AlternateColoring = "";

                    string MaxProcessStatusID = reader["MaxProcessStatusID"].ToString()!;
                    string ScanSource = reader["ScanSource"].ToString()!;
                    string ProcessStatusID = "";
                    string ProcessLockID = "";
                    string AltProcessStatusID = "";
                    string WasSent = "";

                    ProcessLockID = reader["ProcessLockID"].ToString()!;
                    ProcessStatusID = reader["ProcessStatusID"].ToString()!;
                    AltProcessStatusID = reader["AltProcessStatusID"].ToString()!;
                    WasSent = reader["WasSent"].ToString()!;





                    #region >> Determining the pan number

                    string orderIDHelpr = reader["IntOrderID"].ToString()!;
                    List<string> orderIDDarabolt = [];
                    orderIDDarabolt = [.. orderIDHelpr.Split('-')];

                    bool foundPanNumber = int.TryParse(orderIDDarabolt[0].ToString(), out int panNr);

                    if (foundPanNumber)
                    {
                        panNumber = panNr.ToString();
                    }
                    else
                    {
                        // checking if we can find any pan number in the patient name section
                        string orderIDHelprFromPtName = reader["Patient_LastName"].ToString()!;
                        List<string> orderIDHelprFromPtNameDarabolt = [];
                        orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                        bool foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out int panNr2);
                        if (foundPanNumber2)
                        {
                            panNumber = panNr2.ToString();
                        }
                        else
                        {
                            orderIDHelprFromPtName = reader["Patient_FirstName"].ToString()!;
                            orderIDHelprFromPtNameDarabolt = [];
                            orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                            panNr2 = 0;
                            foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out panNr2);

                            if (foundPanNumber2)
                            {
                                panNumber = panNr2.ToString();
                            }
                            else
                                panNumber = "";
                        }

                    }
                    #endregion

                    bool isAbutmentCase = false;







                    string CaseStatus = CaseStatusSelect(MaxProcessStatusID, ScanSource, ProcessLockID);
                    string ImageSource = @"\Images\ListViewIcons\" + IconSelect(MaxProcessStatusID, ScanSource, ProcessLockID) + ".png";
                    string PanColorName = GetBackPanColorName(panNumber);
                    string PanColor = GetBackPanColorHEX(panNumber);

                    if (panNumber == "" || PanColor == "#FFFFFF")
                        PanColor = "Transparent";

                    string Patient_FirstName = reader["Patient_FirstName"].ToString()!.Trim();
                    string Patient_LastName = reader["Patient_LastName"].ToString()!.Trim();
                    if (Patient_FirstName == "")
                        Patient_FirstName = "-";

                    string manufName = "";
                    if (MainViewModel.Instance.ThisSite.Length > 0)
                    {
                        manufName = reader["ManufName"].ToString()!
                                            .Replace(MainViewModel.Instance.ThisSite + "/", "")
                                            .Replace("/" + MainViewModel.Instance.ThisSite, "")
                                            .Replace(MainViewModel.Instance.ThisSite, "");
                    }
                    else
                    {
                        manufName = reader["ManufName"].ToString()!;
                    }


                    _ = DateTime.TryParse(reader["ModificationDate"].ToString(), out DateTime LastModificationForSortingDateTime);
                    string LastModificationForSorting = "";
                    if (reader["ModificationDate"].ToString() != "")
                        LastModificationForSorting = LastModificationForSortingDateTime.ToString("yyyy-MM-dd-HHmmss");

                    _ = DateTime.TryParse(reader["MaxCreateDate"].ToString(), out DateTime CreateDateForSortingDateTime);
                    string CreateDateForSorting = "";
                    CreateDateForSorting = CreateDateForSortingDateTime.ToString("yyyy-MM-dd-HHmmss");

                    string ScanSourceFriendlyName = GetScanner(ScanSource);




                    string CacheMaxScanDate = reader["CacheMaxScanDate"].ToString()!;
                    string CacheMaxScanDateFriendly = CacheMaxScanDate;
                    if (IsItToday(CacheMaxScanDate))
                    {
                        _ = DateTime.TryParse(CacheMaxScanDate, out DateTime CacheMaxScanDateDT);
                        CacheMaxScanDateFriendly = CacheMaxScanDateDT.ToString("h:mm tt");
                    }
                    else if (IsItThisYear(CacheMaxScanDate))
                    {
                        _ = DateTime.TryParse(CacheMaxScanDate, out DateTime CacheMaxScanDateDT);
                        CacheMaxScanDateFriendly = CacheMaxScanDateDT.ToString("MM/dd - h:mm tt");
                    }

                    if (CacheMaxScanDateFriendly.StartsWith("000"))
                        CacheMaxScanDateFriendly = CacheMaxScanDate;




                    string MaxCreateDate = reader["MaxCreateDate"].ToString()!;
                    string MaxCreateDateFriendly = MaxCreateDate;
                    if (IsItToday(MaxCreateDate))
                    {
                        _ = DateTime.TryParse(MaxCreateDate, out DateTime MaxCreateDateDT);
                        MaxCreateDateFriendly = MaxCreateDateDT.ToString("h:mm tt");
                    }
                    else if (IsItThisYear(MaxCreateDate))
                    {
                        _ = DateTime.TryParse(MaxCreateDate, out DateTime MaxCreateDateDT);
                        MaxCreateDateFriendly = MaxCreateDateDT.ToString("MM/dd - h:mm tt");
                    }







                    string ModificationDate = reader["ModificationDate"].ToString()!;

                    string CacheMaterialName = System.Net.WebUtility.HtmlDecode(reader["CacheMaterialName"].ToString()!).Replace("\"", "").Trim();

                    string LastModifiedComputerName = ReadComputerName(reader["UserID"].ToString()!);

                    string Items = RemoveChineseCharacters(reader["Items"].ToString()!);

                    string[] CaseStatusByManufacturerParts = manufName.Split('/');
                    string CaseStatusByManufacturer = CaseStatusByManufacturerParts[0];
                    if (CaseStatusByManufacturer == "")
                    {
                        if (Items.Contains("Abutment") && CacheMaterialName.Contains("Ti"))
                            CaseStatusByManufacturer = "Abutments (3rd Party)";
                        else
                            CaseStatusByManufacturer = "Miscellaneous";
                    }

                    CaseStatus = CaseStatusByManufacturer;


                    if (Items.Contains("Abutment") &&
                                        !IsItEncodeUnit(reader["ManufName"].ToString()!, reader["CacheMaterialName"].ToString()!) &&
                                                MaxProcessStatusID == "psModelled" && ProcessLockID != "plLocked")
                        isAbutmentCase = true;



                    // alternate coloring
                    if (PanColor == "#FFFFFF")
                        AlternateColoring = "nopancolor";

                    if (CacheMaterialName.Contains("NO MATERIAL"))
                    {
                        AlternateColoring = "encode";
                        isAbutmentCase = false;
                    }



                    string ExtOrderID = reader["ExtOrderID"].ToString()!;
                    var isNumeric = int.TryParse(ExtOrderID, out _);
                    if (isNumeric)
                        ExtOrderID = "";

                    if ((string.IsNullOrEmpty(panNumber) && showCasesWithoutNumber) || !showCasesWithoutNumber)
                        list.Add(new ThreeShapeOrdersModel
                        {
                            IntOrderID = reader["IntOrderID"].ToString(),
                            Patient_FirstName = Patient_FirstName,
                            Patient_LastName = Patient_LastName,
                            Patient_RefNo = reader["Patient_RefNo"].ToString(),
                            ExtOrderID = ExtOrderID,
                            OrderComments = reader["OrderComments"].ToString(),
                            Items = Items,
                            OperatorName = reader["OperatorName"].ToString(),
                            Customer = reader["Customer"].ToString(),
                            ManufName = manufName,
                            CacheMaterialName = CacheMaterialName,
                            ScanSource = ScanSource,
                            CacheMaxScanDate = CacheMaxScanDate,
                            TraySystemType = reader["TraySystemType"].ToString(),
                            MaxCreateDate = MaxCreateDate,
                            MaxProcessStatusID = MaxProcessStatusID,
                            ProcessStatusID = ProcessStatusID,
                            AltProcessStatusID = AltProcessStatusID,
                            ProcessLockID = ProcessLockID,
                            WasSent = WasSent,
                            ModificationDate = ModificationDate,
                            ImageSource = ImageSource,
                            ListViewGroup = "",
                            PanColor = PanColor,
                            PanColorName = PanColorName,
                            CaseStatus = CaseStatus,
                            PanNumber = panNumber,
                            LastModificationForSorting = LastModificationForSorting,
                            LastModifiedComputerName = LastModifiedComputerName,
                            CreateDateForSorting = CreateDateForSorting,
                            ScanSourceFriendlyName = ScanSourceFriendlyName,
                            CacheMaxScanDateFriendly = CacheMaxScanDateFriendly,
                            MaxCreateDateFriendly = MaxCreateDateFriendly,
                            CaseStatusByManufacturer = CaseStatusByManufacturer,
                            AlternateColoring = AlternateColoring,
                            OriginalOrderID = reader["OriginalOrderID"].ToString()
                        });
                }
            }
            catch (Exception)
            {
            }
        }
        catch (Exception ex)
        {
        }
        return list;
    }


    public static int GetDigiCasesIn3ShapeTodayCount()
    {
        string sFilter = $@"WHERE (i.MaxProcessStatusID LIKE 'psScanned') AND 
                                (
                                    (o.ScanSource LIKE 'ssImportThirdPartySTL')  OR 
                                    (o.ScanSource LIKE 'ssImportPLY')  OR 
                                    (o.ScanSource LIKE 'ssImport')  OR
                                    (o.ScanSource LIKE 'ssItero')  OR 
                                    (o.ScanSource LIKE 'ssTRIOS')  OR 
                                    (o.ScanSource LIKE 'ssImport3ShapeSTL')
                                ) AND i.MaxCreateDate > '{MainViewModel.Instance.DtYesterday} 17:00:00.001'
                                
                            ";

        string query = $@"select count(*) 
                            from 
                            ( 
                            select count(IntOrderID) tot
                            FROM Orders o 
                            FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                            FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                            FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                            {sFilter} 

                            group by IntOrderID 
                            )  src;";

        string connectionString = ConnectionStrFor3Shape();
        int result = 0;
        try
        {
            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader[0].ToString()!, out result);
            }

        }
        catch (Exception)
        {
        }

        return result;
    }

    public static int GetCurrentDigiPrescriptionCount()
    {
        _ = int.TryParse(ReadStatsSetting("CurrentDigiPrescriptionCount"), out int result);
        return result;
    }

    public static string GetStatsServerStatus()
    {
        return ReadStatsSetting("StatsServerStatus");
    }

    public static string GetBackFolderSubscriptionCountedEntries()
    {
        return ReadStatsSetting("fs_CountedEntries");
    }

    public static string GetLastDatabaseUpdate()
    {
        if (DateTime.TryParse(ReadStatsSetting("FolderWatcherLastUpdate"), out DateTime lastUpdate))
        {
            var diffInSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
            TimeSpan time = TimeSpan.FromSeconds(diffInSeconds);

            string displayTime = Math.Round(time.TotalMinutes).ToString();

            if (displayTime == "0")
                return "Few seconds ago";
            else if (displayTime == "1")
                return $"{displayTime} minute ago";
            else
                return $"{displayTime} minutes ago";
        }
        return "Not long ago";
    }

    private static string LastDCASUpdateTime = "";
    public static string GetLastDCASUpdate()
    {
        int seconds = DateTime.Now.Second;
        if (string.IsNullOrEmpty(LastDCASUpdateTime))
            LastDCASUpdateTime = ReadStatsSetting("dcas_LastCheckForEmails");

        if (seconds % 15 == 0 || seconds < 5)
            LastDCASUpdateTime = ReadStatsSetting("dcas_LastCheckForEmails");

        if (DateTime.TryParse(LastDCASUpdateTime, out DateTime lastUpdate))
        {
            var diffInSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
            TimeSpan time = TimeSpan.FromSeconds(diffInSeconds);

            string displayTimeInMinutes = Math.Floor(time.TotalMinutes).ToString();
            string displayTimeInSeconds = Math.Round(time.TotalSeconds).ToString();

            if (displayTimeInMinutes == "0")
            {
                return $"{displayTimeInSeconds} seconds ago";
            }
            else if (displayTimeInMinutes == "1")
                return $"{displayTimeInMinutes} minute ago";
            else
                return $"{displayTimeInMinutes} minutes ago";
        }
        return "Not long ago";
    }

    public static bool CheckIfServerIsWritingDatabase()
    {
        _ = bool.TryParse(ReadStatsSetting("ServerIsWritingDatabase"), out bool result);
        return result;
    }


    public static bool CheckIfOrderIDIsUnique(string OrderID)
    {
        string queryString = $"SELECT IntOrderID FROM Orders WHERE IntOrderID = '{OrderID}'";
        string connectionString = ConnectionStrFor3Shape();

        try
        {
            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["IntOrderID"].ToString()!.Length > 0)
                {
                    return false;
                }
            }
        }
        catch (Exception)
        {
        }

        return true;
    }



    public static int Counting_result(string querystring)
    {
        string connectionstring = ConnectionStrFor3Shape();
        int result = 0;
        MainViewModel.Instance.ThreeShapeServerIsDown = false;
        try
        {
            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(querystring, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader[0].ToString()!, out result);
            }

        }
        catch (Exception Ex)
        {
            if (Ex.Message.Contains("A network-related or instance-specific error", StringComparison.CurrentCultureIgnoreCase)
                || Ex.Message.Contains("handshake", StringComparison.CurrentCultureIgnoreCase))
                MainViewModel.Instance.ThreeShapeServerIsDown = true;
            else
            {
                if (!Ex.Message.Contains("Incorrect syntax near ')'"))
                    MainViewModel.Instance.AddDebugLine(Ex, Ex.Message, "DBO");
                //MessageBox.Show(Ex.Message, "Error #312", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        return result;
    }

    public static int GetBackTotalEntryLinesCount(int srchLimit, string filter)
    {
        string querystring = $@"SELECT  TOP ({srchLimit}) COUNT(IntOrderID) 
                            FROM Orders o 
                            FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                            FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                            FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                            FULL OUTER JOIN OrderHistory oh ON oh.OrderID = o.IntOrderID 
                            {filter}
                          GROUP BY  IntOrderID";
        int result = 0;
        string connectionstring = ConnectionStrFor3Shape();
        MainViewModel.Instance.ThreeShapeServerIsDown = false;
        try
        {
            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(querystring, connection);
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader[0].ToString()!, out int rslt);
                result += rslt;
            }

        }
        catch (Exception Ex)
        {
            if (Ex.Message.Contains("A network-related or instance-specific error", StringComparison.CurrentCultureIgnoreCase)
                || Ex.Message.Contains("handshake", StringComparison.CurrentCultureIgnoreCase))
                MainViewModel.Instance.ThreeShapeServerIsDown = true;
            else
            {
                if (!Ex.Message.Contains("Incorrect syntax near ')'"))
                    MessageBox.Show(Ex.Message, "Error #312", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        return result;
    }


    public static string GetSentOutIssuesCount()
    {
        try
        {
            string connectionstring = ConnectionStrToStatsDatabase();
            string query = @"SELECT count(*) FROM dbo.SentOutIssues;";

            return RunSQLCommandWithExpectedResult(query, connectionstring);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static async Task<List<OrderIssueModel>> GetAllSentOutIssues()
    {
        List<OrderIssueModel> list = [];
        try
        {
            string connectionstring = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT * FROM dbo.SentOutIssues";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new OrderIssueModel()
                {
                    Level = reader["Level"].ToString(),
                    OrderID = reader["OrderID"].ToString(),
                    IssueDescription = reader["SkipReason"].ToString()!.Replace("&apos;", "'"),
                    Color = reader["ForeColor"].ToString(),
                });
            }
        }
        catch
        {

        }
        return list;
    }

    public static async Task<List<DuplicatePanNumberOrdersModel>> GetAllPanNrDuplicates()
    {
        List<DuplicatePanNumberOrdersModel> list = [];
        try
        {
            string connectionstring = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT * FROM dbo.DuplicatePanNrOrders";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DuplicatePanNumberOrdersModel()
                {
                    OrderID_1 = reader["OrderID1"].ToString(),
                    Created_1 = reader["Created1"].ToString(),
                    Customer_1 = reader["Customer1"].ToString(),
                    Patient_FirstName_1 = reader["Patient_FirstName1"].ToString(),
                    Patient_LastName_1 = reader["Patient_LastName1"].ToString(),
                    OrderID_2 = reader["OrderID2"].ToString(),
                    Created_2 = reader["Created2"].ToString(),
                    Customer_2 = reader["Customer2"].ToString(),
                    Patient_FirstName_2 = reader["Patient_FirstName2"].ToString(),
                    Patient_LastName_2 = reader["Patient_LastName2"].ToString(),
                    PanNr = reader["PanNumber"].ToString()
                });
            }
        }
        catch
        {

        }
        return list;
    }

    public static int GetBackTodayCasesCount()
    {
        string FilterToday = "WHERE(i.MaxCreateDate > '" + MainViewModel.Instance.DtToday + MainViewModel.Instance.RestDayStart + "' AND i.MaxCreateDate < '" + MainViewModel.Instance.DtToday + MainViewModel.Instance.RestDayEnd + "') ";

        string sql = "select count(*) " +
                        "from " +
                        "( " +
                        "select count(IntOrderID) tot " +
                        "FROM Orders o " +
                        "FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID " +
                        FilterToday +
                        "  group by IntOrderID " +
                        ")  src;";

        int countedEntries = Counting_result(sql);

        return countedEntries;
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
        catch
        {

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
        catch
        {

        }
        return "";
    }

    public static string GetServerAddress()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT TOP(1) ServerAddress FROM dbo.ThreeShapeServer";

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
        catch
        {

        }
        return "";
    }

    public static ThreeShapeOrderInspectionModel InspectThreeShapeOrder(string selectedOrderID)
    {
        string queryString = @$"SELECT 
                                  IntOrderID, 
                                  Patient_FirstName, 
                                  Patient_LastName, 
                                  OrderComments, 
                                  o.Items, 
                                  OperatorName, 
                                  Customer, 
                                  o.ManufName, 
                                  o.CacheMaterialName, 
                                  ScanSource, 
                                  CacheMaxScanDate, 
                                  TraySystemType, 
                                  MaxCreateDate, 
                                  MaxProcessStatusID, 
                                  ProcessStatusID, 
                                  AltProcessStatusID, 
                                  ModelHeight, 
                                  ProcessLockID,  
                                  OriginalOrderID,
                                  m.ModelJobID,  
                                  te.ToothNumber 

                                FROM Orders o 
                                FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                                FULL OUTER JOIN ModelJob m ON m.OrderID = o.IntOrderID 
                                FULL OUTER JOIN ModelElement me ON me.ModelJobID = m.ModelJobID 
                                FULL OUTER JOIN ToothElement te ON me.ModelElementID = te.ModelElementID 

                                WHERE o.IntOrderID = '{selectedOrderID}' AND ToothNumber <> ''";



        string connectionString = ConnectionStrFor3Shape();

        ThreeShapeOrderInspectionModel threeShapeInspectModel = new();

        try
        {
            using (SqlConnection connection = new(connectionString))
            {
                SqlCommand command = new(queryString, connection);
                connection.Open();
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {

                    if (reader["ModelHeight"].ToString() != "0" && string.IsNullOrEmpty(reader["OriginalOrderID"].ToString()))
                        threeShapeInspectModel.IsCaseWereDesigned = true;
                    else
                        threeShapeInspectModel.IsCaseWereDesigned = false;

                    // determining the pan number

                    string orderIDHelpr = reader["IntOrderID"].ToString()!;
                    List<string> orderIDDarabolt = [];
                    orderIDDarabolt = [.. orderIDHelpr.Split('-')];

                    bool foundPanNumber = int.TryParse(orderIDDarabolt[0].ToString(), out int panNr);

                    if (foundPanNumber)
                    {
                        threeShapeInspectModel.PanNumber = panNr;
                    }
                    else
                    {
                        // checking if we can find any pan number in the patient name section
                        string orderIDHelprFromPtName = reader["Patient_LastName"].ToString()!;
                        List<string> orderIDHelprFromPtNameDarabolt = [];
                        orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                        bool foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out int panNr2);
                        if (foundPanNumber2)
                        {
                            threeShapeInspectModel.PanNumber = panNr2;
                        }
                        else
                        {
                            orderIDHelprFromPtName = reader["Patient_FirstName"].ToString()!;
                            orderIDHelprFromPtNameDarabolt = [];
                            orderIDHelprFromPtNameDarabolt = [.. orderIDHelprFromPtName.Split('-')];
                            panNr2 = 0;
                            foundPanNumber2 = int.TryParse(orderIDHelprFromPtNameDarabolt[0].ToString(), out panNr2);

                            if (foundPanNumber2)
                                threeShapeInspectModel.PanNumber = panNr2;
                            else
                                threeShapeInspectModel.PanNumber = 0;
                        }

                    }
                    // END


                    // checking if case is locked
                    if (reader["ProcessLockID"].ToString() == "plLocked")
                        threeShapeInspectModel.IsLocked = true;

                    // checking if case is checked out
                    if (reader["ProcessLockID"].ToString() == "plCheckedOut")
                        threeShapeInspectModel.IsCheckedOut = true;


                    threeShapeInspectModel.CaseStatus = reader["MaxProcessStatusID"].ToString();
                    threeShapeInspectModel.OriginalLockStatusID = reader["ProcessLockID"].ToString(); // helper for locking the case during renaming
                    threeShapeInspectModel.ModelJobIDForLock = reader["ModelJobID"].ToString(); // helper for locking the case during renaming

                }
            }


            return threeShapeInspectModel;

        }
        catch (Exception)
        {
            return new ThreeShapeOrderInspectionModel();
        }
    }

    public static Color GetBackPanColor(string panNumbr)
    {
        string color;
        if (panNumbr == "")
            return Color.FromArgb(255, 255, 255);
        else
        {
            try
            {
                _ = int.TryParse(panNumbr, out int pnNr);
                color = GetPanColorByNumber(pnNr);
                string[] rgb = color.Split('-');

                _ = int.TryParse(rgb[0], out int red);
                _ = int.TryParse(rgb[1], out int green);
                _ = int.TryParse(rgb[2], out int blue);

                if (red == 0 && green == 0 && blue == 0)
                    return Color.FromArgb(255, 255, 255);

                return Color.FromArgb(red, green, blue);
            }
            catch
            {
                return Color.White;
            }

        }
    }

    public static string GetBackPanColorHEX(string panNumbr)
    {
        string color;
        if (panNumbr == "")
            return "#FFFFFF";
        else
        {
            try
            {
                _ = int.TryParse(panNumbr, out int pnNr);
                color = GetPanColorByNumber(pnNr);
                string[] rgb = color.Split('-');
                _ = int.TryParse(rgb[0], out int red);
                _ = int.TryParse(rgb[1], out int green);
                _ = int.TryParse(rgb[2], out int blue);

                if (red == 0 && green == 0 && blue == 0)
                    return "#FFFFFF";

                Color PanColor = Color.FromArgb(red, green, blue);
                string hex = "#" + PanColor.R.ToString("X2") + PanColor.G.ToString("X2") + PanColor.B.ToString("X2");
                return hex;
            }
            catch
            {
                return "#FFFFFF";
            }

        }
    }



    public static string GetBackPanColorName(string panNumbr)
    {
        string color;
        if (panNumbr == "")
            return "";
        else
        {
            try
            {
                _ = int.TryParse(panNumbr, out int pnNr);
                color = GetPanColorNameByNumber(pnNr);
                return color;
            }
            catch (Exception)
            {
                return "";
            }

        }
    }

    public static List<string> GetAllPanNumbers()
    {
        List<string> list = [];
        try
        {
            string connectionstring = ConnectionStrToStatsDatabase();
            string query = @"SELECT * FROM dbo.PanNumbers";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["Color"].ToString() + "|" + reader["PanNumber"].ToString());
            }

        }
        catch (Exception)
        {
        }
        return list;
    }

    public static List<string> GetAllPanColor()
    {
        List<string> list = [];
        try
        {
            string connectionstring = ConnectionStrToStatsDatabase();
            string query = @"SELECT Color, FriendlyName FROM dbo.PanColors";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader["Color"].ToString() + "|" + reader["FriendlyName"].ToString());
            }

        }
        catch (Exception)
        {
        }
        return list;
    }

    public static string ReadPanNumberColor(int PanNumber)
    {
        try
        {
            string connectionstring = ConnectionStrToStatsDatabase();
            string query = @"SELECT Color, FriendlyName FROM dbo.PanNumbers WHERE PanNumber = '" + PanNumber.ToString() + @"'";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader["Color"].ToString() + "|" + reader["FriendlyName"].ToString();
                if (value != null)
                    return value;
            }
        }
        catch
        {
            return "";
        }
        return "";
    }

    public static string GetPanColorByNumber(int PanNumber)
    {
        try
        {
            string connectionstring = ConnectionStrToStatsDatabase();
            string query = @"SELECT Color FROM dbo.PanNumbers WHERE PanNumber = '" + PanNumber.ToString() + @"'";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader["Color"].ToString();
                if (value != null)
                    return value;
            }
        }
        catch
        {
            return "0-0-0";
        }
        return "0-0-0";
    }

    public static string GetPanColorNameByNumber(int PanNumber)
    {
        try
        {
            string connectionstring = ConnectionStrToStatsDatabase();
            string query = @"SELECT FriendlyName FROM dbo.PanNumbers WHERE PanNumber = '" + PanNumber.ToString() + @"'";

            using SqlConnection connection = new(connectionstring);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader["FriendlyName"].ToString();
                if (value != null)
                    return value;
            }
        }
        catch
        {
            return "";
        }
        return "";
    }


    public static int GetOpenedForDesignCasesCount(int ServerID)
    {
        try
        {
            string connectionstring = ConnectionStrFor3Shape();
            string query = @"SELECT count(*) FROM 
                             (
                                SELECT 
                                    IntOrderID, 
                                    MaxProcessStatusID
                                FROM Orders o 
                                FULL OUTER JOIN OrdersInfo i ON i.OrderID = o.IntOrderID 
                                WHERE i.MaxProcessStatusID LIKE 'psModelling' 
                                GROUP BY IntOrderID, MaxProcessStatusID
                             ) src;";

            _ = int.TryParse(RunSQLCommandWithExpectedResult(query, connectionstring), out int count);

            return count;
        }
        catch
        {
            return 0;
        }
    }

    public static string ReadComputerName(string ComputerName)
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT FriendlyName FROM dbo.ComputerNames WHERE ComputerName = '" + ComputerName + @"'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = reader["FriendlyName"].ToString();
                if (value != null)
                    return value;
            }
        }
        catch
        {
            return ComputerName;
        }
        return ComputerName;
    }


    public static Color ColorSelect(string processStatusID)
    {
        return processStatusID switch
        {
            "psCreated" => Color.FromArgb(192, 255, 255),
            "psScanned" => Color.FromArgb(96, 197, 241),
            "psScanning" => Color.FromArgb(148, 216, 246),
            "psModelled" => Color.FromArgb(97, 226, 167),
            "psModelling" => Color.FromArgb(255, 178, 125),
            "psClosed" => Color.FromArgb(248, 161, 164),
            "psAccepted" => Color.FromArgb(255, 242, 0),
            "psRejected" => Color.FromArgb(136, 0, 21),
            "psSent" => Color.FromArgb(166, 166, 166),
            _ => Color.FromArgb(255, 255, 255),
        };
    }

    public static string GetScanner(string scanSource)
    {
        switch (scanSource)
        {
            case "ssUnknown": MainViewModel.Instance.DigiCase = false; return "";
            case "ssImportThirdPartySTL": MainViewModel.Instance.DigiCase = true; return "3rd party STL";
            case "ssImportPLY": MainViewModel.Instance.DigiCase = true; return "PLY import";
            case "ssImport": MainViewModel.Instance.DigiCase = true; return "STL import";
            case "ssItero": MainViewModel.Instance.DigiCase = true; return "iTero";
            case "ssTRIOS": MainViewModel.Instance.DigiCase = true; return "Trios";
            case "ssImport3ShapeSTL": MainViewModel.Instance.DigiCase = false; return "3Shape STL";
            case "ssImportDCM": MainViewModel.Instance.DigiCase = false; return "3Shape DCM";
            case "ss3SD2000": MainViewModel.Instance.DigiCase = false; return "D2000";
            case "ss3ShapeDesktopScanner": MainViewModel.Instance.DigiCase = false; return "3Shape Desktop";
            case "ss3SE4": MainViewModel.Instance.DigiCase = false; return "E4";
            case "ss3SE3": MainViewModel.Instance.DigiCase = false; return "E3";
            case "ss3SE2": MainViewModel.Instance.DigiCase = false; return "E2";
            case "ss3SD1000": MainViewModel.Instance.DigiCase = false; return "D1000";
            case "ss3SD900": MainViewModel.Instance.DigiCase = false; return "D900";
            case "ss3SD810": MainViewModel.Instance.DigiCase = false; return "D810";
            case "ss3SD800": MainViewModel.Instance.DigiCase = false; return "D800";
            case "ss3SD700": MainViewModel.Instance.DigiCase = false; return "D700";
            case "ss3SD640": MainViewModel.Instance.DigiCase = false; return "D640";
            case "ss3SD500": MainViewModel.Instance.DigiCase = false; return "D500";

            default: return scanSource;
        }
    }

    public static string ConvertScanSourceToDigiSystemName(string scanSource)
    {
        return scanSource switch
        {
            "ssItero" => "ITERO",
            "ssTRIOS" => "TRIOS",
            _ => "",
        };
    }

    public static bool IsDigitalCase(string scanSource)
    {
        return scanSource switch
        {
            "ssUnknown" => false,
            "ssImportThirdPartySTL" => true,
            "ssImportPLY" => true,
            "ssImport" => true,
            "ssItero" => true,
            "ssTRIOS" => true,
            "ssImport3ShapeSTL" => false,
            "ssImportDCM" => false,
            "ss3SD2000" => false,
            "ss3ShapeDesktopScanner" => false,
            "ss3SE4" => false,
            "ss3SE3" => false,
            "ss3SE2" => false,
            "ss3SD1000" => false,
            "ss3SD900" => false,
            "ss3SD810" => false,
            "ss3SD800" => false,
            "ss3SD700" => false,
            "ss3SD640" => false,
            "ss3SD500" => false,
            _ => false,
        };
    }

    public static string GetStatus(string processStatusID)
    {
        return processStatusID switch
        {
            "psCreated" => "Created",
            "psScanned" => "Scanned",
            "psScanning" => "Scanning in progress",
            "psModelled" => "Designed",
            "psModelling" => "Design in progress",
            "psClosed" => "Closed",
            "psAccepted" => "Accepted for production",
            "psRejected" => "Rejected by the manufacturer",
            "psSent" => "Sent to manufacturer",
            _ => processStatusID,
        };
    }

    public static string GetTraySystem(string traySystemType, string processStatusID)
    {
        switch (traySystemType)
        {
            case "stNone":
                {
                    if (processStatusID == "psCreated")
                        return "-";
                    else
                        return "Model";
                }
            case "stImpAntTripleTray": return "Anterior triple tray impression";
            case "stImpPostTripleTray": return "Posterior triple tray impression";
            case "stImpSingleTray": return "Single tray impression (no antagonist)";
            case "stImpFrontBite": return "Two single tray impression";


            default: return traySystemType;
        }
    }



    public static string GetLockStatus(string processLockID)
    {
        return processLockID switch
        {
            "plLocked" => "Locked",
            "plReady" => "Closed",
            "plCheckedOut" => "CheckedOut",
            "plSent" => "Sent",
            _ => processLockID,
        };
    }



    public static string IconSelect(string processStatusID, string scanSource, string processLockID)
    {

        if (processLockID == "plSent")
            return "psSent";



        if (processLockID == "plLocked")
        {
            switch (processStatusID)
            {
                case "psCreated":
                    return "psCreated_Locked";

                case "psScanned":
                    if (
                        (scanSource == "ssTRIOS") ||
                        (scanSource == "ssItero") ||
                        (scanSource == "ssImportThirdPartySTL")
                        )
                    {
                        return "trios_Locked";
                    }
                    else
                    {
                        if (
                            (scanSource == "ss3ShapeDesktopScanner") ||
                            (scanSource == "ss3SE4") ||
                            (scanSource == "ss3SE3") ||
                            (scanSource == "ss3SE2")
                            )
                        {
                            return "psScanningE";
                        }
                        else
                            return "psScanning";
                    }

                case "psModelled":
                    return "psModelling";
            }
        }


        if (processLockID == "plCheckedOut")
        {
            switch (processStatusID)
            {
                case "psScanned":
                    if (
                        (scanSource == "ssTRIOS") ||
                        (scanSource == "ssItero") ||
                        (scanSource == "ssImportThirdPartySTL")
                        )
                    {
                        return "trios_CheckedOut";
                    }
                    else
                    {
                        if (
                            (scanSource == "ss3ShapeDesktopScanner") ||
                            (scanSource == "ss3SE4") ||
                            (scanSource == "ss3SE3") ||
                            (scanSource == "ss3SE2")
                            )
                        {
                            return "psScannedE_CheckedOut";
                        }
                        else
                            return "psScanned_CheckedOut";
                    }

                case "psModelled":
                    return "psModelled_CheckedOut";
            }
        }



        switch (processStatusID)
        {
            case "psScanned":
                if (
                    (scanSource == "ssTRIOS") ||
                    (scanSource == "ssItero") ||
                    (scanSource == "ssImportThirdPartySTL")
                    )
                {
                    return "trios_scanned";
                }
                else
                {
                    if (
                        (scanSource == "ss3ShapeDesktopScanner") ||
                        (scanSource == "ss3SE4") ||
                        (scanSource == "ss3SE3") ||
                        (scanSource == "ss3SE2")
                        )
                    {
                        return "psScannedE";
                    }
                    else
                        return "psScanned";
                }
            case "psModelled": return "psModelled";
            case "psModelling": return "psModelling";
            case "psCreated": return "psCreated";
            case "psAccepted": return "psAccepted";
            case "psScanning": return "psScanning";
            case "psClosed": return "psClosed";
            case "psRejected": return "psRejected";
            case "psShipped": return "psSent";
            case "psUnknown": return "";
            default: return "";
        }
    }

    public static string CaseStatusSelect(string processStatusID, string scanSource, string processLockID)
    {

        if (processLockID == "plSent")
            return "6-Sent for production";



        if (processLockID == "plLocked")
        {
            switch (processStatusID)
            {
                case "psCreated":
                    return "1-Created";

                case "psScanned":
                    if (
                        (scanSource == "ssTRIOS") ||
                        (scanSource == "ssItero") ||
                        (scanSource == "ssImportThirdPartySTL")
                        )
                    {
                        return "2-Scanned";
                    }
                    else
                    {
                        if (
                            (scanSource == "ss3ShapeDesktopScanner") ||
                            (scanSource == "ss3SE4") ||
                            (scanSource == "ss3SE3") ||
                            (scanSource == "ss3SE2")
                            )
                        {
                            return "2-Scanned";
                        }
                        else
                            return "2-Scanned";
                    }

                case "psModelled":
                    return "0-Designing";
            }
        }


        if (processLockID == "plCheckedOut")
        {
            switch (processStatusID)
            {
                case "psScanned":
                    if (
                        (scanSource == "ssTRIOS") ||
                        (scanSource == "ssItero") ||
                        (scanSource == "ssImportThirdPartySTL")
                        )
                    {
                        return "8-Sent to designer";
                    }
                    else
                    {
                        if (
                            (scanSource == "ss3ShapeDesktopScanner") ||
                            (scanSource == "ss3SE4") ||
                            (scanSource == "ss3SE3") ||
                            (scanSource == "ss3SE2")
                            )
                        {
                            return "8-Sent to designer";
                        }
                        else
                            return "8-Sent to designer";
                    }

                case "psModelled":
                    return "9-Checked out";
            }
        }



        switch (processStatusID)
        {
            case "psScanned":
                if (
                    (scanSource == "ssTRIOS") ||
                    (scanSource == "ssItero") ||
                    (scanSource == "ssImportThirdPartySTL")
                    )
                {
                    return "2-Scanned";
                }
                else
                {
                    if (
                        (scanSource == "ss3ShapeDesktopScanner") ||
                        (scanSource == "ss3SE4") ||
                        (scanSource == "ss3SE3") ||
                        (scanSource == "ss3SE2")
                        )
                    {
                        return "2-Scanned";
                    }
                    else
                        return "2-Scanned";
                }
            case "psModelled": return "7-Designed";
            case "psModelling": return "0-Designing";
            case "psCreated": return "1-Created";
            case "psAccepted": return "5-Accepted for production";
            case "psScanning": return "0-Scanning";
            case "psClosed": return "9-Closed";
            case "psRejected": return "4-Failed at production";
            case "psShipped": return "6-Sent for production";
            case "psUnknown": return "";
            default: return "";
        }
    }

    public static bool IsItToday(string DateTimeString)
    {
        _ = DateTime.TryParse(DateTimeString, out DateTime value);

        if (value.ToString("yyyy-MM-dd") == DateTime.Now.ToString("yyyy-MM-dd"))
            return true;
        else
            return false;
    }

    public static bool IsItThisYear(string DateTimeString)
    {
        _ = DateTime.TryParse(DateTimeString, out DateTime value);

        if (value.ToString("yyyy") == DateTime.Now.ToString("yyyy"))
            return true;
        else
            return false;
    }

    public static string RemoveChineseCharacters(string Text)
    {
        string text = Text;
        text = text.Replace("未分割模型", "Unsectioned model");
        text = text.Replace("对合模型", "Antagonist model");
        text = text.Replace("分割模型", "Sectioned (die ditched) model");
        text = text.Replace("软组织", "Soft tissue");
        text = text.Replace("代型", "Die");

        text = text.Replace("已制备模型上的临时冠", "Temporary on prepared model");
        text = text.Replace("解剖牙桥 含牙龈", "Anatomy bridge with gingiva");
        text = text.Replace("牙冠 含牙龈", "Crown with gingiva");
        text = text.Replace("解剖型内冠", "Anatomical coping");


        text = text.Replace("解剖牙桥", "Anatomy bridge");
        text = text.Replace("框架桥", "Frame bridge");

        text = text.Replace("临时冠", "Temporary Crown");

        text = text.Replace("解剖型基台", "Anatomical Abutment");
        text = text.Replace("桩核", "Post and Core");
        text = text.Replace("嵌体", "Inlay");
        text = text.Replace("高嵌体", "Onlay");
        text = text.Replace("基台", "Abutment");
        text = text.Replace("螺丝固位冠", "Screw Retained Crown");
        text = text.Replace("螺丝固位解剖型牙桥", "Screw retained anatomy bridge");
        text = text.Replace("贴面", "Veneer");

        text = text.Replace("内冠", "Coping");
        text = text.Replace("牙冠", "Crown");

        return text;
    }


    public static bool IsServerBusy()
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT sValue FROM dbo.Settings WHERE sName = 'ServerIsWritingDatabase'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = bool.TryParse(reader["sValue"].ToString(), out bool result);
                return result;
            }
        }
        catch
        {
            return true;
        }
        return true;
    }


    public static List<LastTouchedByModel> GetLastTouchedByListData(string OrderID)
    {
        List<LastTouchedByModel> list = [];
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT * FROM dbo.OrderHistory WHERE OrderID = '" + OrderID + @"' ORDER BY ModificationDate DESC";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new LastTouchedByModel(
                    ReadComputerName(reader["UserID"].ToString()!),
                                     reader["ModificationDate"].ToString()!
                    ));
            }
        }
        catch
        {
            return list;
        }
        return list;
    }


    public static bool IsCaseUsingFDI(string OrderID)
    {
        string XMLFile = GetServerFileDirectory() + OrderID + "\\" + OrderID + ".xml";
        if (File.Exists(XMLFile))
        {
            XmlDocument doc = new();
            doc.Load(XMLFile);

            XmlElement root = doc.DocumentElement!;

            List<XmlNode> nodes = [];

            foreach (XmlNode node in root.ChildNodes[0]!.ChildNodes)
            {
                if (!nodes.Contains(node))
                    nodes.Add(node);
            }


            XmlNode TDM_List_ModelJob_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "ModelJobList");

            Dictionary<string, string> TDM_List_ModelJob = new Dictionary<string, string>();

            foreach (XmlNode node in TDM_List_ModelJob_XMLNode.ChildNodes[0]!.ChildNodes[0]!)
            {
                TDM_List_ModelJob.Add(node.Attributes!["name"]!.Value, node.Attributes!["value"]!.Value);
            }
        }

        return false;
    }

    public static string ConvertFDIinString(string Text)
    {
        string content = Text;
        string contentHelper = Text;
        string result = "";
        Regex regx = FDIRegex();
        Match match = regx.Match(Text);

        while (match.Success)
        {
            content = content.Substring(match.Index + match.Value.Length);
            result += string.Concat(contentHelper.AsSpan(0, match.Index), ConvertFDI(match.Value));
            contentHelper = contentHelper[(match.Index + match.Value.Length)..];
            match = regx.Match(content);
        }

        return result;
    }

    public static string ConvertFDI(string ToothNumber)
    {
        return ToothNumber switch
        {
            "18" => "1",
            "17" => "2",
            "16" => "3",
            "15" => "4",
            "14" => "5",
            "13" => "6",
            "12" => "7",
            "11" => "8",
            "21" => "9",
            "22" => "10",
            "23" => "11",
            "24" => "12",
            "25" => "13",
            "26" => "14",
            "27" => "15",
            "28" => "16",
            "38" => "17",
            "37" => "18",
            "36" => "19",
            "35" => "20",
            "34" => "21",
            "33" => "22",
            "32" => "23",
            "31" => "24",
            "41" => "25",
            "42" => "26",
            "43" => "27",
            "44" => "28",
            "45" => "29",
            "46" => "30",
            "47" => "31",
            "48" => "32",
            _ => "",
        };
    }


    public static Dictionary<string, int> GetEWCategoriesAndCounts()
    {
        Dictionary<string, int> dict = [];

        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT COUNT(RuleName), RuleName FROM dbo.EWCatchedEmails GROUP BY RuleName";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader[0].ToString(), out int catCount);
                dict.Add(reader["RuleName"].ToString()!, catCount);
            }

        }
        catch
        {

        }
        return dict;
    }

    public static Dictionary<string, int> GetMeditCasesWithCounts()
    {
        Dictionary<string, int> dict = [];

        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT COUNT(Seller), Seller FROM dbo.MeditDigitalCases WHERE Status = 'PENDING' GROUP BY Seller";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = int.TryParse(reader[0].ToString(), out int catCount);
                dict.Add(reader["Seller"].ToString()!, catCount);
            }

        }
        catch
        {

        }
        return dict;
    }

    public static string GetSingleEWIdentifier(string sentTo)
    {
        string result = "";

        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = $@"SELECT * FROM dbo.EWLabIdentifier WHERE SentTo = '{sentTo}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                result = reader["ReplaceWith"].ToString()!;
            }

        }
        catch (Exception ex)
        {
            Debug.WriteLine("(ConfigHelper)" + ex.Message);
        }

        if (string.IsNullOrEmpty(result))
            result = sentTo;

        return result;
    }

    public static List<ProcessedPanNumberModel> GetAllNotProcessedNumbers()
    {
        List<ProcessedPanNumberModel> list = [];
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT * FROM dbo.CPPanNumbers WHERE IsProcessed = '0'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string collected = "false";
                if (reader["IsCollected"].ToString() == "1")
                    collected = "true";

                string processed = "false";
                if (reader["IsProcessed"].ToString() == "1")
                    processed = "true";
                list.Add(new ProcessedPanNumberModel()
                {
                    PanNumber = reader["PanNumber"].ToString(),
                    PostedTime = reader["PostedTime"].ToString(),
                    IsCollected = collected,
                    IsProcessed = processed,
                });
            }
        }
        catch
        {
        }
        return list;
    }

    public static List<ProcessedPanNumberModel> GetAllPendingDigiNumbersInLast30Days()
    {
        List<ProcessedPanNumberModel> list = [];
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = $@"SELECT * FROM dbo.CPPanNumbers WHERE PostedTime > '{DateTime.Now.AddDays(-30):yyyy-MM-dd HH:mm:ss}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string collected = "false";
                if (reader["IsCollected"].ToString() == "1")
                    collected = "true";

                string processed = "false";
                if (reader["IsProcessed"].ToString() == "1")
                    processed = "true";
                list.Add(new ProcessedPanNumberModel()
                {
                    Id = reader["Id"].ToString(),
                    PanNumber = reader["PanNumber"].ToString(),
                    PostedTime = reader["PostedTime"].ToString(),
                    ProcessedTime = reader["ProcessedTime"].ToString(),
                    PostedBy = reader["PostedBy"].ToString(),
                    ProcessedBy = reader["ProcessedBy"].ToString(),
                    Comment = reader["Comment"].ToString(),
                    IsCollected = collected,
                    IsProcessed = processed,
                    PostedTimeForSorting = reader["PostedTime"].ToString(),
                });
            }
        }
        catch
        {
        }
        return list;
    }

    public static List<ProcessedPanNumberModel> GetAllNotCollectedNumbers()
    {
        List<ProcessedPanNumberModel> list = [];
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @"SELECT * FROM dbo.CPPanNumbers WHERE IsCollected <> '1' AND IsProcessed = '0'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string collected = "false";
                if (reader["IsCollected"].ToString() == "1")
                    collected = "true";
                list.Add(new ProcessedPanNumberModel()
                {
                    PanNumber = reader["PanNumber"].ToString(),
                    PostedTime = reader["PostedTime"].ToString(),
                    IsCollected = collected
                });
            }
        }
        catch
        {
        }
        return list;
    }

    public static List<FolderSubscriptionModel> GetFolderSubscriptions(string searchString)
    {
        List<FolderSubscriptionModel> list = [];
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @$"SELECT * FROM dbo.FolderSubscription WHERE Name LIKE '%{searchString}%'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = DateTime.TryParse(reader["Time"].ToString(), out DateTime SavedDate);
                double TotalDaysDBL = (DateTime.Now - SavedDate).TotalDays;
                int TotalDays = (int)Math.Floor(TotalDaysDBL);
                string ageForSorting = "";
                if (TotalDays > 99)
                    ageForSorting = TotalDays.ToString();

                if (TotalDays < 100)
                    ageForSorting = "0" + TotalDays.ToString();

                if (TotalDays < 10)
                    ageForSorting = "00" + TotalDays.ToString();

                list.Add(new FolderSubscriptionModel()
                {
                    FolderName = reader["Name"].ToString(),
                    LastModified = reader["Time"].ToString(),
                    Path = reader["Path"].ToString(),
                    Age = $"{TotalDays} days",
                    AgeForSorting = ageForSorting,
                    AgeForColoring = TotalDays,
                });
            }
        }
        catch
        {
        }
        return list;
    }

    public static string MarkPanNumberAsCollected(string number)
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();

            string query = @"merge dbo.CPPanNumbers with(HOLDLOCK) as target
                                 using (values ('" + number + @"', '1'))
                                     as source (PanNumber, IsCollected)
                                     on target.PanNumber = '" + number + @"'
                                 when matched then
                                     update
                                     set IsCollected = source.IsCollected;";

            RunSQLCommandAsynchronously(query, connectionString);
            return "good";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string RemovePanNumberFromAvailablePans(string number)
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();

            string query = $@"DELETE FROM dbo.PMPanNumbers WHERE PanNumber = '{number}'";

            RunSQLCommandAsynchronously(query, connectionString);
            return "good";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string SendSironaInfoToServer(string PanNumber, string PatientName, string SironaOrderNumber, string Type)
    {
        try
        {
            string Received = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string connectionString = ConnectionStrToStatsDatabase();

            string query = $@"INSERT INTO dbo.SironaDigitalCases (PanNumber, PatientName, OrderNumber, Type, Received)
                              VALUES ('{PanNumber}', '{PatientName}', '{SironaOrderNumber}', '{Type}', '{Received}')";

            RunSQLCommandAsynchronously(query, connectionString);
            return "good";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string AddNameToSentToList(string SendToName)
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();

            string query = $@"INSERT INTO dbo.PMSendToEntries (SendTo)
                              VALUES ('{SendToName}')";

            RunSQLCommandAsynchronously(query, connectionString);
            return "good";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string RemoveNameFromSentToList(string SendToName)
    {
        try
        {
            string connectionString = ConnectionStrToStatsDatabase();

            string query = $@"DELETE FROM dbo.PMSendToEntries WHERE SendTo = '{SendToName}'";

            RunSQLCommandAsynchronously(query, connectionString);
            return "good";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static List<string> GetAllSendToEnties()
    {
        List<string> list = [];
        list.Add("-");
        try
        {
            string computerName = Environment.MachineName;

            string connectionString = ConnectionStrToStatsDatabase();
            string query = @$"SELECT * FROM dbo.PMSendToEntries";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!list.Contains(reader["SendTo"].ToString()!))
                    list.Add(reader["SendTo"].ToString()!);
            }
        }
        catch
        {
        }

        return list;
    }

    public static string AddNewPanNumber(string number)
    {
        try
        {
            string computerName = Environment.MachineName;
            string connectionString = ConnectionStrToStatsDatabase();

            string query = $@"merge dbo.PMPanNumbers with(HOLDLOCK) as target
                                 using (values ('{number}', '0', '{computerName}'))
                                     as source (PanNumber, Used, Owner)
                                     on target.PanNumber = '" + number + @"'
                                 when matched then
                                     update
                                     set PanNumber = source.PanNumber,
                                              Used = source.Used,
                                             Owner = source.Owner
                                 when not matched then
                                     insert (PanNumber, Used, Owner)
                                     values (source.PanNumber, source.Used, source.Owner);
                                ;";

            RunSQLCommandAsynchronously(query, connectionString);
            return "good";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }


    public static List<string> GetPanNumbers()
    {
        List<string> list = [];
        try
        {
            string computerName = Environment.MachineName;

            string connectionString = ConnectionStrToStatsDatabase();
            string query = @$"SELECT PanNumber FROM dbo.PMPanNumbers WHERE Used = '0' AND Owner = '{computerName}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!list.Contains(reader["PanNumber"].ToString()!))
                    list.Add(reader["PanNumber"].ToString()!);
            }
        }
        catch
        {
        }

        return list;
    }


    public static bool IsItEncodeUnit(string Manufacturer, string CacheMaterialName)
    {
        if (((Manufacturer.Contains("BIOMET") ||
             Manufacturer.Contains("PBG") ||
             Manufacturer.Contains("TSV")) &&

            (CacheMaterialName.Contains("BIOMET") ||
             CacheMaterialName.Contains("PBG") ||
             CacheMaterialName.Contains("TSV") ||
             CacheMaterialName.Contains("BellaTek") ||
             CacheMaterialName.Contains("ZBE2-LDA")
             )) ||
            CacheMaterialName.Contains("BellaTek"))
        {
            return true;
        }

        return false;
    }

    public static async Task<List<SentOutCasesModel>> GetSentOutCasesFromStatsDatabase(string side)
    {
        List<SentOutCasesModel> list = [];

        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query;

            if (side.Equals("both", StringComparison.CurrentCultureIgnoreCase))
                query = @$"SELECT * FROM dbo.SentOutCases";
            else
                query = @$"SELECT * FROM dbo.SentOutCases WHERE Side = '{side}'";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                SentOutCasesModel model = new();
                model.OrderID = reader["OrderID"].ToString();
                model.Crowns = reader["Crowns"].ToString();
                model.Abutments = reader["Abutments"].ToString();
                model.Models = reader["Models"].ToString();

                _ = int.TryParse(model.Crowns, out int crowns);
                _ = int.TryParse(model.Abutments, out int abutments);

                model.TotalUnits = (crowns + abutments).ToString();
                model.Comment = reader["Comment"].ToString();
                model.SentOn = reader["SentOn"].ToString();
                model.Items = reader["Items"].ToString();
                model.Manufacturer = reader["Manufacturer"].ToString();
                model.Rush = reader["Rush"].ToString();
                model.Side = reader["Side"].ToString();
                model.Directory = reader["Directory"].ToString();
                model.MaxProcessStatusID = reader["MaxProcessStatusID"].ToString();
                model.ProcessLockID = reader["ProcessLockID"].ToString();
                model.ScanSource = reader["ScanSource"].ToString();
                model.CommentIcon = reader["CommentIcon"].ToString();
                model.CommentColor = reader["CommentColor"].ToString();
                model.CommentIn3Shape = reader["CommentIn3Shape"].ToString();

                if (model.Directory != null)
                    model.Directory = model.Directory.Replace(@"\", "|");

                list.Add(model);
            }
        }
        catch (Exception)
        {
        }

        return list;
    }


    public static StatsDBSettingsModel GetStatsDBSettingsModel()
    {
        StatsDBSettingsModel model = new();

        try
        {
            string connectionString = ConnectionStrToStatsDatabase();
            string query = @$"SELECT * FROM dbo.Settings
                              WHERE sName = 'LastDBUpdate' OR
                                    sName = 'StatsServerStatus' OR
                                    sName = 'LastServerPing' OR
                                    sName = 'AutoSendActive' OR
                                    sName = 'AutoSend-0' OR
                                    sName = 'AutoSend-15' OR
                                    sName = 'AutoSend-30' OR
                                    sName = 'AutoSend-45' OR
                                    sName = 'FriendlyNameExportAnteriors' OR
                                    sName = 'FriendlyNameExportPosteriors' OR
                                    sName = 'ExportFolderAnteriors' OR
                                    sName = 'ServerIsWritingDatabase' OR
                                    sName = 'Selected server' OR
                                    sName = 'ExportFolderPosteriors' 
                            ";

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["sName"].ToString()!.Equals("LastDBUpdate"))
                    model.LastDBUpdate = reader["sValue"].ToString();

                if (reader["sName"].ToString()!.Equals("StatsServerStatus"))
                    model.StatsServerStatus = reader["sValue"].ToString();

                if (reader["sName"].ToString()!.Equals("LastServerPing"))
                    model.LastServerPing = reader["sValue"].ToString();

                if (reader["sName"].ToString()!.Equals("ExportFolderAnteriors"))
                    model.ExportFolderAnteriors = reader["sValue"].ToString()!.Replace(@"\", "|");

                if (reader["sName"].ToString()!.Equals("ExportFolderPosteriors"))
                    model.ExportFolderPosteriors = reader["sValue"].ToString()!.Replace(@"\", "|");

                if (reader["sName"].ToString()!.Equals("FriendlyNameExportAnteriors"))
                    model.DesignerNameAnteriors = reader["sValue"].ToString();

                if (reader["sName"].ToString()!.Equals("FriendlyNameExportPosteriors"))
                    model.DesignerNamePosteriors = reader["sValue"].ToString();

                if (reader["sName"].ToString()!.Equals("Selected server"))
                    model.SelectedServer = reader["sValue"].ToString();

                if (reader["sName"].ToString()!.Equals("AutoSendActive"))
                {
                    if (reader["sValue"].ToString()!.Equals("true"))
                        model.AutoSendActive = true;
                    else
                        model.AutoSendActive = false;
                }

                if (reader["sName"].ToString()!.Equals("AutoSend-0"))
                {
                    if (reader["sValue"].ToString()!.Equals("true"))
                        model.AutoSend0 = true;
                    else
                        model.AutoSend0 = false;
                }

                if (reader["sName"].ToString()!.Equals("AutoSend-15"))
                {
                    if (reader["sValue"].ToString()!.Equals("true"))
                        model.AutoSend15 = true;
                    else
                        model.AutoSend15 = false;
                }

                if (reader["sName"].ToString()!.Equals("AutoSend-30"))
                {
                    if (reader["sValue"].ToString()!.Equals("true"))
                        model.AutoSend30 = true;
                    else
                        model.AutoSend30 = false;
                }

                if (reader["sName"].ToString()!.Equals("AutoSend-45"))
                {
                    if (reader["sValue"].ToString()!.Equals("true"))
                        model.AutoSend45 = true;
                    else
                        model.AutoSend45 = false;
                }

                if (reader["sName"].ToString()!.Equals("ServerIsWritingDatabase"))
                {
                    if (reader["sValue"].ToString()!.Equals("true"))
                        model.ServerIsWritingDatabase = true;
                    else
                        model.ServerIsWritingDatabase = false;
                }
            }

            model.SiteID = MainViewModel.Instance.ServerID.ToString();
        }
        catch (Exception)
        {
        }

        return model;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex FDIRegex();

    #region PAYMENT HISTORY - Order Lists

    public static async Task<(ObservableCollection<PaymentHistoryModel> items, int totalCount)> GetPaymentHistoryByDesignerPaged(
        string designerId, int daysBack, int pageNumber, int pageSize)
    {
        ObservableCollection<PaymentHistoryModel> list = [];
        int totalCount = 0;

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            // Get PaymentListCutOffDate from settings, default to October 1st, 2025
            string earliestDateStr = ReadStatsSetting("PaymentListCutOffDate");
            DateTime earliestDate;

            if (string.IsNullOrEmpty(earliestDateStr))
            {
                earliestDate = new DateTime(2025, 10, 1);
                WriteStatsSetting("PaymentListCutOffDate", earliestDate.ToString("yyyy-MM-dd"));
                Debug.WriteLine($"[DatabaseOperations] PaymentListCutOffDate not found, setting default: {earliestDate:yyyy-MM-dd}");
            }
            else
            {
                earliestDate = DateTime.Parse(earliestDateStr);
                Debug.WriteLine($"[DatabaseOperations] PaymentListCutOffDate loaded: {earliestDate:yyyy-MM-dd}");
            }

            // First get total count of unique ImportHistory records
            // Get most recent entry per OrderID, filter by date and designer
            string countQuery = @"
                WITH LatestImports AS (
                    SELECT *,
                           ROW_NUMBER() OVER (PARTITION BY OrderID ORDER BY ImportTime DESC) as rn
                    FROM dbo.ImportHistory
                    WHERE DesignerID = @DesignerID
                      AND TRY_CAST(DateTime AS DATE) >= DATEADD(day, -@DaysBack, GETDATE())
                      AND TRY_CAST(DateTime AS DATE) >= @EarliestDate
                )
                SELECT COUNT(*) 
                FROM LatestImports 
                WHERE rn = 1";

            using SqlConnection countConnection = new(connectionString);
            SqlCommand countCommand = new(countQuery, countConnection);
            countCommand.Parameters.AddWithValue("@DesignerID", designerId);
            countCommand.Parameters.AddWithValue("@DaysBack", daysBack);
            countCommand.Parameters.AddWithValue("@EarliestDate", earliestDate);
            await countConnection.OpenAsync();
            totalCount = (int)await countCommand.ExecuteScalarAsync();

            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesignerPaged - Total count: {totalCount}");

            // Then get paged data with paid status from PaymentHistory
            string query = @"
                WITH LatestImports AS (
                    SELECT *,
                           ROW_NUMBER() OVER (PARTITION BY OrderID ORDER BY ImportTime DESC) as rn
                    FROM dbo.ImportHistory
                    WHERE DesignerID = @DesignerID
                      AND TRY_CAST(DateTime AS DATE) >= DATEADD(day, -@DaysBack, GETDATE())
                      AND TRY_CAST(DateTime AS DATE) >= @EarliestDate
                )
                SELECT 
                    li.OrderID,
                    li.DesignerID,
                    li.FriendlyName,
                    li.ImportPath,
                    li.DateTime,
                    li.ImportTime,
                    li.IsitRedo,
                    li.Crowns,
                    li.Abutments,
                    li.TotalUnits,
                    li.Patient_Lastname,
                    li.Patient_Firstname,
                    li.PanNumber,
                    ph.LxLabnextID,
                    ph.LxInvoiceDate,
                    ph.LxPanNumber,
                    ph.LxPatient_FirstName,
                    ph.LxPatient_LastName,
                    ph.LxUnitCount,
                    ph.LxItems,
                    ph.LxPaid,
                    ph.Customer,
                    ph.LxInvoiceDateRange
                FROM LatestImports li
                LEFT JOIN dbo.PaymentHistory ph ON li.OrderID = ph.OrderID AND li.DesignerID = ph.DesignerID
                WHERE li.rn = 1
                ORDER BY li.ImportTime DESC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY";

            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesignerPaged - DesignerID: {designerId}, DaysBack: {daysBack}, Page: {pageNumber}, PageSize: {pageSize}");

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@DesignerID", designerId);
            command.Parameters.AddWithValue("@DaysBack", daysBack);
            command.Parameters.AddWithValue("@EarliestDate", earliestDate);
            command.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
            command.Parameters.AddWithValue("@PageSize", pageSize);
            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string dateTimeStr = reader["DateTime"]?.ToString() ?? "";
                string payPeriod = "";
                if (!string.IsNullOrEmpty(dateTimeStr) && System.DateTime.TryParse(dateTimeStr, out System.DateTime parsedDate))
                {
                    payPeriod = Functions.GetPayPeriodForDate(parsedDate);
                }

                list.Add(new PaymentHistoryModel
                {
                    OrderID = reader["OrderID"]?.ToString() ?? "",
                    DesignerID = reader["DesignerID"]?.ToString() ?? "",
                    FriendlyName = reader["FriendlyName"]?.ToString() ?? "",
                    Customer = reader["Customer"] != DBNull.Value ? reader["Customer"]?.ToString() ?? "" : "",
                    ImportPath = reader["ImportPath"]?.ToString() ?? "",
                    DateTime = dateTimeStr,
                    ImportTime = reader["ImportTime"]?.ToString() ?? "",
                    IsitRedo = reader["IsitRedo"]?.ToString() ?? "",
                    Crowns = reader["Crowns"]?.ToString() ?? "",
                    Abutments = reader["Abutments"]?.ToString() ?? "",
                    TotalUnits = reader["TotalUnits"]?.ToString() ?? "",

                    // Use PaymentHistory fields if available, otherwise ImportHistory
                    LxPanNumber = reader["LxPanNumber"] != DBNull.Value ? Convert.ToInt32(reader["LxPanNumber"]) : 
                                  reader["PanNumber"] != DBNull.Value ? Convert.ToInt32(reader["PanNumber"]) : null,
                    LxPatient_FirstName = reader["LxPatient_FirstName"] != DBNull.Value ? reader["LxPatient_FirstName"]?.ToString() ?? "" :
                                          reader["Patient_Firstname"]?.ToString() ?? "",
                    LxPatient_LastName = reader["LxPatient_LastName"] != DBNull.Value ? reader["LxPatient_LastName"]?.ToString() ?? "" :
                                         reader["Patient_Lastname"]?.ToString() ?? "",
                    LxUnitCount = reader["LxUnitCount"] != DBNull.Value ? Convert.ToInt32(reader["LxUnitCount"]) :
                                  reader["TotalUnits"] != DBNull.Value ? (int?)Convert.ToInt32(reader["TotalUnits"]) : null,

                    // Payment info (will be null if not paid)
                    LxLabnextID = reader["LxLabnextID"]?.ToString() ?? "",
                    LxInvoiceDate = reader["LxInvoiceDate"] != DBNull.Value ? reader["LxInvoiceDate"]?.ToString() ?? "" : reader["DateTime"]?.ToString() ?? "",
                    LxItems = reader["LxItems"]?.ToString() ?? "",
                    LxPaid = reader["LxPaid"] != DBNull.Value ? Convert.ToInt16(reader["LxPaid"]) : (short)0,
                    LxInvoiceDateRange = reader["LxInvoiceDateRange"]?.ToString() ?? "",
                    PayPeriod = payPeriod
                });
            }

            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesignerPaged - Found {list.Count} records for page {pageNumber}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesignerPaged - Error: {ex.Message}");
        }

        return (list, totalCount);
    }

    public static async Task<(ObservableCollection<PaymentHistoryModel> items, int totalCount)> GetUnpaidCasesByDesignerPaged(
        string designerId, DateTime? sinceDate, int pageNumber, int pageSize)
    {
        ObservableCollection<PaymentHistoryModel> list = [];
        int totalCount = 0;

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            // Get PaymentListCutOffDate from settings
            string cutOffDateStr = ReadStatsSetting("PaymentListCutOffDate");
            DateTime cutOffDate;

            if (string.IsNullOrEmpty(cutOffDateStr))
            {
                cutOffDate = new DateTime(2025, 10, 1);
                WriteStatsSetting("PaymentListCutOffDate", cutOffDate.ToString("yyyy-MM-dd"));
                Debug.WriteLine($"[DatabaseOperations] PaymentListCutOffDate not found for unpaid, setting default: {cutOffDate:yyyy-MM-dd}");
            }
            else
            {
                cutOffDate = DateTime.Parse(cutOffDateStr);
                Debug.WriteLine($"[DatabaseOperations] PaymentListCutOffDate loaded for unpaid: {cutOffDate:yyyy-MM-dd}");
            }

            // First get total count of unpaid cases
            // Query ImportHistory, get most recent entry per OrderID, exclude OrderIDs in PaymentHistory
            string countQuery = @"
                WITH LatestImports AS (
                    SELECT *,
                           ROW_NUMBER() OVER (PARTITION BY OrderID ORDER BY ImportTime DESC) as rn
                    FROM dbo.ImportHistory
                    WHERE DesignerID = @DesignerID
                      AND TRY_CAST(DateTime AS DATE) >= @CutOffDate
                )
                SELECT COUNT(*) 
                FROM LatestImports 
                WHERE rn = 1 
                AND OrderID NOT IN (SELECT OrderID FROM dbo.PaymentHistory WHERE DesignerID = @DesignerID)";

            if (sinceDate.HasValue)
            {
                countQuery += " AND TRY_CAST(DateTime AS DATE) <= @SinceDate";
            }

            using SqlConnection countConnection = new(connectionString);
            SqlCommand countCommand = new(countQuery, countConnection);
            countCommand.Parameters.AddWithValue("@DesignerID", designerId);
            countCommand.Parameters.AddWithValue("@CutOffDate", cutOffDate);
            if (sinceDate.HasValue)
            {
                countCommand.Parameters.AddWithValue("@SinceDate", sinceDate.Value);
            }
            await countConnection.OpenAsync();
            totalCount = (int)await countCommand.ExecuteScalarAsync();

            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesignerPaged - Total unpaid count: {totalCount}");

            // Then get paged data
            string query = @"
                WITH LatestImports AS (
                    SELECT *,
                           ROW_NUMBER() OVER (PARTITION BY OrderID ORDER BY ImportTime DESC) as rn
                    FROM dbo.ImportHistory
                    WHERE DesignerID = @DesignerID
                      AND TRY_CAST(DateTime AS DATE) >= @CutOffDate
                )
                SELECT 
                    li.OrderID,
                    li.DesignerID,
                    li.FriendlyName,
                    li.ImportPath,
                    li.DateTime,
                    li.ImportTime,
                    li.IsitRedo,
                    li.Crowns,
                    li.Abutments,
                    li.TotalUnits,
                    li.Patient_Lastname AS Patient_LastName,
                    li.Patient_Firstname AS Patient_FirstName,
                    li.PanNumber
                FROM LatestImports li
                WHERE li.rn = 1 
                AND li.OrderID NOT IN (SELECT OrderID FROM dbo.PaymentHistory WHERE DesignerID = @DesignerID)";

            if (sinceDate.HasValue)
            {
                query += " AND TRY_CAST(DateTime AS DATE) <= @SinceDate";
            }

            query += @" ORDER BY li.DateTime DESC, li.ImportTime DESC
                       OFFSET @Offset ROWS
                       FETCH NEXT @PageSize ROWS ONLY";

            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesignerPaged - DesignerID: {designerId}, SinceDate: {sinceDate}, Page: {pageNumber}, PageSize: {pageSize}");

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@DesignerID", designerId);
            command.Parameters.AddWithValue("@CutOffDate", cutOffDate);
            command.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
            command.Parameters.AddWithValue("@PageSize", pageSize);
            if (sinceDate.HasValue)
            {
                command.Parameters.AddWithValue("@SinceDate", sinceDate.Value);
            }
            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string dateTimeStr = reader["DateTime"]?.ToString() ?? "";
                string payPeriod = "";
                if (!string.IsNullOrEmpty(dateTimeStr) && System.DateTime.TryParse(dateTimeStr, out System.DateTime parsedDate))
                {
                    payPeriod = Functions.GetPayPeriodForDate(parsedDate);
                }

                list.Add(new PaymentHistoryModel
                {
                    OrderID = reader["OrderID"]?.ToString() ?? "",
                    DesignerID = reader["DesignerID"]?.ToString() ?? "",
                    FriendlyName = reader["FriendlyName"]?.ToString() ?? "",
                    Customer = "", // Customer not available in ImportHistory, will be empty for unpaid cases
                    ImportPath = reader["ImportPath"]?.ToString() ?? "",
                    DateTime = dateTimeStr,
                    ImportTime = reader["ImportTime"]?.ToString() ?? "",
                    IsitRedo = reader["IsitRedo"]?.ToString() ?? "",
                    Crowns = reader["Crowns"]?.ToString() ?? "",
                    Abutments = reader["Abutments"]?.ToString() ?? "",
                    TotalUnits = reader["TotalUnits"]?.ToString() ?? "",
                    LxPatient_LastName = reader["Patient_LastName"]?.ToString() ?? "",
                    LxPatient_FirstName = reader["Patient_FirstName"]?.ToString() ?? "",
                    LxPanNumber = reader["PanNumber"] != DBNull.Value ? Convert.ToInt32(reader["PanNumber"]) : null,
                    LxInvoiceDate = dateTimeStr,
                    LxPaid = 0,
                    LxItems = "",
                    LxUnitCount = reader["TotalUnits"] != DBNull.Value ? (int?)Convert.ToInt32(reader["TotalUnits"]) : null,
                    PayPeriod = payPeriod
                });
            }

            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesignerPaged - Found {list.Count} unpaid records for page {pageNumber}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesignerPaged - Error: {ex.Message}");
        }

        return (list, totalCount);
    }

    public static async Task<List<PayPeriodStatisticsModel>> GetPayPeriodStatisticsByDesigner(string designerId, int periodsToShow = 10)
    {
        List<PayPeriodStatisticsModel> statistics = [];

        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);

            // Get PaymentListCutOffDate from settings
            string cutOffDateStr = ReadStatsSetting("PaymentListCutOffDate");
            DateTime cutOffDate;

            if (string.IsNullOrEmpty(cutOffDateStr))
            {
                cutOffDate = new DateTime(2025, 10, 1);
            }
            else
            {
                cutOffDate = DateTime.Parse(cutOffDateStr);
            }

            // Calculate pay periods going back from current date
            DateTime payPeriodAnchor = new DateTime(2026, 3, 3);
            int periodLengthDays = 17;

            for (int i = 0; i < periodsToShow; i++)
            {
                DateTime periodEnd = payPeriodAnchor.AddDays(-i * periodLengthDays);
                DateTime periodStart = periodEnd.AddDays(-periodLengthDays + 1);

                // Skip if period is before cut-off date
                if (periodEnd < cutOffDate)
                    continue;

                string payPeriodStr = $"{periodStart:MMM d} - {periodEnd:MMM d}";

                // Get designed cases count (all cases in ImportHistory for this period)
                string designedQuery = @"
                    WITH LatestImports AS (
                        SELECT *,
                               ROW_NUMBER() OVER (PARTITION BY OrderID ORDER BY ImportTime DESC) as rn
                        FROM dbo.ImportHistory
                        WHERE DesignerID = @DesignerID
                          AND TRY_CAST(DateTime AS DATE) >= @PeriodStart
                          AND TRY_CAST(DateTime AS DATE) <= @PeriodEnd
                    )
                    SELECT 
                        COUNT(*) as DesignedCount,
                        ISNULL(SUM(CAST(TotalUnits AS INT)), 0) as TotalUnits
                    FROM LatestImports 
                    WHERE rn = 1";

                int designedCases = 0;
                int totalDesignedUnits = 0;

                using SqlConnection designedConn = new(connectionString);
                SqlCommand designedCmd = new(designedQuery, designedConn);
                designedCmd.Parameters.AddWithValue("@DesignerID", designerId);
                designedCmd.Parameters.AddWithValue("@PeriodStart", periodStart);
                designedCmd.Parameters.AddWithValue("@PeriodEnd", periodEnd);
                await designedConn.OpenAsync();

                using (SqlDataReader reader = await designedCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        designedCases = reader["DesignedCount"] != DBNull.Value ? Convert.ToInt32(reader["DesignedCount"]) : 0;
                        totalDesignedUnits = reader["TotalUnits"] != DBNull.Value ? Convert.ToInt32(reader["TotalUnits"]) : 0;
                    }
                }

                // Get unpaid cases count and units
                string unpaidQuery = @"
                    WITH LatestImports AS (
                        SELECT *,
                               ROW_NUMBER() OVER (PARTITION BY OrderID ORDER BY ImportTime DESC) as rn
                        FROM dbo.ImportHistory
                        WHERE DesignerID = @DesignerID
                          AND TRY_CAST(DateTime AS DATE) >= @PeriodStart
                          AND TRY_CAST(DateTime AS DATE) <= @PeriodEnd
                    )
                    SELECT 
                        COUNT(*) as UnpaidCount,
                        ISNULL(SUM(CAST(TotalUnits AS INT)), 0) as UnpaidUnits
                    FROM LatestImports li
                    WHERE li.rn = 1 
                      AND li.OrderID NOT IN (SELECT OrderID FROM dbo.PaymentHistory WHERE DesignerID = @DesignerID)";

                int unpaidCases = 0;
                int unpaidUnits = 0;

                using SqlConnection unpaidConn = new(connectionString);
                SqlCommand unpaidCmd = new(unpaidQuery, unpaidConn);
                unpaidCmd.Parameters.AddWithValue("@DesignerID", designerId);
                unpaidCmd.Parameters.AddWithValue("@PeriodStart", periodStart);
                unpaidCmd.Parameters.AddWithValue("@PeriodEnd", periodEnd);
                await unpaidConn.OpenAsync();

                using (SqlDataReader reader = await unpaidCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        unpaidCases = reader["UnpaidCount"] != DBNull.Value ? Convert.ToInt32(reader["UnpaidCount"]) : 0;
                        unpaidUnits = reader["UnpaidUnits"] != DBNull.Value ? Convert.ToInt32(reader["UnpaidUnits"]) : 0;
                    }
                }

                // Calculate paid units (total designed units - unpaid units)
                int paidUnits = totalDesignedUnits - unpaidUnits;

                statistics.Add(new PayPeriodStatisticsModel
                {
                    PayPeriod = payPeriodStr,
                    PeriodStartDate = periodStart,
                    PeriodEndDate = periodEnd,
                    UnpaidCases = unpaidCases,
                    UnpaidUnits = unpaidUnits,
                    DesignedCases = designedCases,
                    PaidUnits = paidUnits
                });
            }

            Debug.WriteLine($"[DatabaseOperations] GetPayPeriodStatisticsByDesigner - Generated {statistics.Count} period statistics");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseOperations] GetPayPeriodStatisticsByDesigner - Error: {ex.Message}");
        }

        return statistics;
    }

    public static async Task<ObservableCollection<PaymentHistoryModel>> GetPaymentHistoryByDesigner(string designerId, int daysBack)
    {
        ObservableCollection<PaymentHistoryModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT * FROM dbo.PaymentHistory 
                           WHERE DesignerID = @DesignerID 
                           AND TRY_CAST(LxInvoiceDate AS DATE) >= DATEADD(day, -@DaysBack, GETDATE())
                           ORDER BY LxInvoiceDate DESC";

            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesigner - DesignerID: {designerId}, DaysBack: {daysBack}");

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@DesignerID", designerId);
            command.Parameters.AddWithValue("@DaysBack", daysBack);
            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentHistoryModel
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                    PaymentID = reader["PaymentID"]?.ToString() ?? "",
                    OrderID = reader["OrderID"]?.ToString() ?? "",
                    DesignerID = reader["DesignerID"]?.ToString() ?? "",
                    FriendlyName = reader["FriendlyName"]?.ToString() ?? "",
                    ImportPath = reader["ImportPath"]?.ToString() ?? "",
                    DateTime = reader["DateTime"]?.ToString() ?? "",
                    ImportTime = reader["ImportTime"]?.ToString() ?? "",
                    IsitRedo = reader["IsitRedo"]?.ToString() ?? "",
                    Crowns = reader["Crowns"]?.ToString() ?? "",
                    Gingiva = reader["Gingiva"]?.ToString() ?? "",
                    Abutments = reader["Abutments"]?.ToString() ?? "",
                    TotalUnits = reader["TotalUnits"]?.ToString() ?? "",
                    LxLabnextID = reader["LxLabnextID"]?.ToString() ?? "",
                    LxCreationDate = reader["LxCreationDate"]?.ToString() ?? "",
                    LxInvoiceDate = reader["LxInvoiceDate"]?.ToString() ?? "",
                    LxPanNumber = reader["LxPanNumber"] != DBNull.Value ? Convert.ToInt32(reader["LxPanNumber"]) : null,
                    LxStatus = reader["LxStatus"]?.ToString() ?? "",
                    LxPatient_FirstName = reader["LxPatient_FirstName"]?.ToString() ?? "",
                    LxPatient_LastName = reader["LxPatient_LastName"]?.ToString() ?? "",
                    LxUnitCount = reader["LxUnitCount"] != DBNull.Value ? Convert.ToInt32(reader["LxUnitCount"]) : null,
                    LxItems = reader["LxItems"]?.ToString() ?? "",
                    LxTeethNumbers = reader["LxTeethNumbers"]?.ToString() ?? "",
                    LxPrice = reader["LxPrice"] != DBNull.Value ? Convert.ToInt32(reader["LxPrice"]) : null,
                    LxPaid = reader["LxPaid"] != DBNull.Value ? Convert.ToInt16(reader["LxPaid"]) : null,
                    LxIssue = reader["LxIssue"]?.ToString() ?? "",
                    LxInvoiceDateRange = reader["LxInvoiceDateRange"]?.ToString() ?? "",
                    ProcessedBy = reader["ProcessedBy"]?.ToString() ?? "",
                    IsAutoProcess = reader["IsAutoProcess"] != DBNull.Value ? Convert.ToInt16(reader["IsAutoProcess"]) : null,
                    Customer = reader["Customer"]?.ToString() ?? "",
                    OrderID2 = reader["OrderID2"]?.ToString() ?? ""
                });
            }

            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesigner - Found {list.Count} records");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseOperations] GetPaymentHistoryByDesigner - Error: {ex.Message}");
        }

        return list;
    }

    public static async Task<ObservableCollection<PaymentHistoryModel>> GetUnpaidCasesByDesigner(string designerId, DateTime? sinceDate = null)
    {
        ObservableCollection<PaymentHistoryModel> list = [];
        try
        {
            string connectionString = await Task.Run(ConnectionStrToStatsDatabase);
            string query = @"SELECT * FROM dbo.PaymentHistory 
                           WHERE DesignerID = @DesignerID 
                           AND (LxPaid = 0 OR LxPaid IS NULL)";

            if (sinceDate.HasValue)
            {
                query += " AND TRY_CAST(LxInvoiceDate AS DATE) >= @SinceDate";
            }

            query += " ORDER BY LxInvoiceDate DESC";

            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesigner - DesignerID: {designerId}, SinceDate: {sinceDate}");

            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@DesignerID", designerId);
            if (sinceDate.HasValue)
            {
                command.Parameters.AddWithValue("@SinceDate", sinceDate.Value);
            }
            await connection.OpenAsync();

            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PaymentHistoryModel
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                    PaymentID = reader["PaymentID"]?.ToString() ?? "",
                    OrderID = reader["OrderID"]?.ToString() ?? "",
                    DesignerID = reader["DesignerID"]?.ToString() ?? "",
                    FriendlyName = reader["FriendlyName"]?.ToString() ?? "",
                    ImportPath = reader["ImportPath"]?.ToString() ?? "",
                    DateTime = reader["DateTime"]?.ToString() ?? "",
                    ImportTime = reader["ImportTime"]?.ToString() ?? "",
                    IsitRedo = reader["IsitRedo"]?.ToString() ?? "",
                    Crowns = reader["Crowns"]?.ToString() ?? "",
                    Gingiva = reader["Gingiva"]?.ToString() ?? "",
                    Abutments = reader["Abutments"]?.ToString() ?? "",
                    TotalUnits = reader["TotalUnits"]?.ToString() ?? "",
                    LxLabnextID = reader["LxLabnextID"]?.ToString() ?? "",
                    LxCreationDate = reader["LxCreationDate"]?.ToString() ?? "",
                    LxInvoiceDate = reader["LxInvoiceDate"]?.ToString() ?? "",
                    LxPanNumber = reader["LxPanNumber"] != DBNull.Value ? Convert.ToInt32(reader["LxPanNumber"]) : null,
                    LxStatus = reader["LxStatus"]?.ToString() ?? "",
                    LxPatient_FirstName = reader["LxPatient_FirstName"]?.ToString() ?? "",
                    LxPatient_LastName = reader["LxPatient_LastName"]?.ToString() ?? "",
                    LxUnitCount = reader["LxUnitCount"] != DBNull.Value ? Convert.ToInt32(reader["LxUnitCount"]) : null,
                    LxItems = reader["LxItems"]?.ToString() ?? "",
                    LxTeethNumbers = reader["LxTeethNumbers"]?.ToString() ?? "",
                    LxPrice = reader["LxPrice"] != DBNull.Value ? Convert.ToInt32(reader["LxPrice"]) : null,
                    LxPaid = reader["LxPaid"] != DBNull.Value ? Convert.ToInt16(reader["LxPaid"]) : null,
                    LxIssue = reader["LxIssue"]?.ToString() ?? "",
                    LxInvoiceDateRange = reader["LxInvoiceDateRange"]?.ToString() ?? "",
                    ProcessedBy = reader["ProcessedBy"]?.ToString() ?? "",
                    IsAutoProcess = reader["IsAutoProcess"] != DBNull.Value ? Convert.ToInt16(reader["IsAutoProcess"]) : null,
                    Customer = reader["Customer"]?.ToString() ?? "",
                    OrderID2 = reader["OrderID2"]?.ToString() ?? ""
                });
            }

            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesigner - Found {list.Count} records");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseOperations] GetUnpaidCasesByDesigner - Error: {ex.Message}");
        }

        return list;
    }

    #endregion

}
