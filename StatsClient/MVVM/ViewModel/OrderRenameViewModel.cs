using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.ViewModel.MainViewModel;

namespace StatsClient.MVVM.ViewModel;

public class OrderRenameViewModel : ObservableObject
{
    #region Properties
    private static OrderRenameViewModel? instance;
    public static OrderRenameViewModel Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChangedStatic(nameof(Instance));
        }
    }

    private ThreeShapeOrdersModel? threeShapeObject;
    public ThreeShapeOrdersModel? ThreeShapeObject
    {
        get => threeShapeObject;
        set
        {
            threeShapeObject = value;
            RaisePropertyChanged(nameof(ThreeShapeObject));
        }
    }

    
    private OrderRenameWindow? _renameWindow;
    public OrderRenameWindow _RenameWindow
    {
        get => _renameWindow!;
        set
        {
            _renameWindow = value;
            RaisePropertyChanged(nameof(_RenameWindow));
        }
    }

    private SMessageBoxResult sMessageBoxxResult;
    public SMessageBoxResult SMessageBoxxResult
    {
        get => sMessageBoxxResult;
        set
        {
            sMessageBoxxResult = value;
            RaisePropertyChanged(nameof(SMessageBoxxResult));
        }
    }

    private bool makeCommentAboutRename = true;
    public bool MakeCommentAboutRename
    {
        get => makeCommentAboutRename!;
        set
        {
            makeCommentAboutRename = value;
            RaisePropertyChanged(nameof(MakeCommentAboutRename));
        }
    }

    private string logMessage = "";
    public string LogMessage
    {
        get => logMessage!;
        set
        {
            logMessage = value;
            RaisePropertyChanged(nameof(LogMessage));
        }
    }
    
    private List<string> logMessages = [];
    public List<string> LogMessages
    {
        get => logMessages!;
        set
        {
            logMessages = value;
            RaisePropertyChanged(nameof(LogMessages));
        }
    }
    
    private string patientName = "";
    public string PatientName
    {
        get => patientName!;
        set
        {
            patientName = value;
            RaisePropertyChanged(nameof(PatientName));
        }
    }
    
    private bool orderIDIsValid = true;
    public bool OrderIDIsValid
    {
        get => orderIDIsValid!;
        set
        {
            orderIDIsValid = value;
            RaisePropertyChanged(nameof(OrderIDIsValid));
        }
    }
    
    private bool goWithGivenPanNumber = false;
    public bool GoWithGivenPanNumber
    {
        get => goWithGivenPanNumber!;
        set
        {
            goWithGivenPanNumber = value;
            RaisePropertyChanged(nameof(GoWithGivenPanNumber));
        }
    }
    
    private string orderID = "";
    public string OrderID
    {
        get => orderID!;
        set
        {
            orderID = value;
            RaisePropertyChanged(nameof(OrderID));
            ValidateOrderID();
            if (OrderIDBeforeChange != value)
                ShowResetButton = Visibility.Visible;
            else
                ShowResetButton = Visibility.Collapsed;
        }
    }
    
    private string originalOrderID = "";
    public string OriginalOrderID
    {
        get => originalOrderID!;
        set
        {
            originalOrderID = value;
            RaisePropertyChanged(nameof(OriginalOrderID));
        }
    }
    
    private string today = "";
    public string Today
    {
        get => today!;
        set
        {
            today = value;
            RaisePropertyChanged(nameof(Today));
        }
    }
    
    
    private string threeShapeDirectoryHelper = "";
    public string ThreeShapeDirectoryHelper
    {
        get => threeShapeDirectoryHelper!;
        set
        {
            threeShapeDirectoryHelper = value;
            RaisePropertyChanged(nameof(ThreeShapeDirectoryHelper));
        }
    }
    
    private Visibility showResetButton = Visibility.Collapsed;
    public Visibility ShowResetButton
    {
        get => showResetButton!;
        set
        {
            showResetButton = value;
            RaisePropertyChanged(nameof(ShowResetButton));
        }
    }
    
    private string orderIDBeforeChange = "";
    public string OrderIDBeforeChange
    {
        get => orderIDBeforeChange!;
        set
        {
            orderIDBeforeChange = value;
            RaisePropertyChanged(nameof(OrderIDBeforeChange));
        }
    }
    
    private string toothNumbersString = "";
    public string ToothNumbersString
    {
        get => toothNumbersString!;
        set
        {
            toothNumbersString = value;
            RaisePropertyChanged(nameof(ToothNumbersString));
        }
    }
    
    private bool controlsEnabled = true;
    public bool ControlsEnabled
    {
        get => controlsEnabled!;
        set
        {
            controlsEnabled = value;
            RaisePropertyChanged(nameof(ControlsEnabled));
        }
    }


    #endregion Properties

    public RelayCommand CloseWindowCommand { get; set; }
    public RelayCommand ResetChangesCommand { get; set; }
    public RelayCommand CopyOriginalOrderIdCommand { get; set; }
    public RelayCommand RenameCommand { get; set; }
    public RelayCommand GenerateNameCommand { get; set; }

    public OrderRenameViewModel()
    {
        Instance = this;
        CloseWindowCommand = new RelayCommand(o => CloseWindow());
        ResetChangesCommand = new RelayCommand(o => ResetChanges());
        CopyOriginalOrderIdCommand = new RelayCommand(o => CopyOriginalOrderId());
        RenameCommand = new RelayCommand(o => RenameOrder());
        GenerateNameCommand = new RelayCommand(o => GenerateName());

        ThreeShapeDirectoryHelper = GetServerFileDirectory();
        Today = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt");
        
    }

    private async void GenerateName()
    {
        int givenPanNr = -1;
        bool FoundNumber = false;

        string digitalSystem = await GetDigiSystemName(ThreeShapeObject!.IntOrderID!);
        if (OrderID.Contains('-')) 
        {
            string panNrPart = OrderID[..OrderID.IndexOf('-')];
            _ = int.TryParse(panNrPart, out givenPanNr);

            if (givenPanNr > 0)
                FoundNumber = true;
        }

        if (!FoundNumber)
            _ = int.TryParse(OrderID, out givenPanNr);

        if (givenPanNr > 0)
            FoundNumber = true;

        ContinueHere:
        if (FoundNumber)
        {
            if (await PanNumberIsValid(givenPanNr) || GoWithGivenPanNumber)
            {
                GoWithGivenPanNumber = false;
                string patientNm = ThreeShapeObject!.Patient_LastName!.Replace(" ", "_")
                                                                     .Replace(",", "")
                                                                     .Replace("'", "_")
                                                                     .Replace("\"", "_")
                                                                     .Replace("+", "_")
                                                                     .Replace("\\", "_")
                                                                     .Replace("/", "_")
                                                                     .Replace(":", "_")
                                                                     .Replace("*", "_")
                                                                     .Replace("?", "_")
                                                                     .Replace("<", "_")
                                                                     .Replace(">", "_")
                                                                     .Replace("&", "-")
                                                                     .Replace("|", "_")
                                                                     .Trim();

                string customer = ThreeShapeObject!.Customer!;

                List<string> customerSuggestions = await CustomerHasSuggestedName(customer);
                if (customerSuggestions.Count > 0)
                    customer = customerSuggestions[0];   

                customer = CleanUpCustomerName(customer);

                ToothNumbersString = await GetToothNumbersString(ThreeShapeObject!.IntOrderID!);

                if (!string.IsNullOrEmpty(digitalSystem))
                    digitalSystem = $"-{digitalSystem}";

                string builtOrderName = $"{givenPanNr}-{ToothNumbersString}-{patientNm}-{customer}{digitalSystem}";

                if (ThreeShapeObject.OrderComments!.Contains("screw retained", StringComparison.CurrentCultureIgnoreCase) ||
                    ThreeShapeObject.OrderComments!.Contains("screwretained", StringComparison.CurrentCultureIgnoreCase) ||
                    ThreeShapeObject.OrderComments!.Contains("access hole", StringComparison.CurrentCultureIgnoreCase))
                    builtOrderName += "-SCR";
            
                OrderID = builtOrderName.Trim().ToUpper();
            }
            else
            {
                SMessageBoxResult dlg = ShowMessageBox("Pan number not recognised", $"The given pan number not registered within the system!\nAre you sure you want to use this number:{givenPanNr}?", SMessageBoxButtons.YesNo, NotificationIcon.Question, 15, OrderRenameWindow.StaticInstance);
                
                if (dlg == SMessageBoxResult.Yes)
                {
                    GoWithGivenPanNumber = true;
                    goto ContinueHere;
                }
            }


        }
        else
        {
            ShowMessageBox("Pan number not recognised", $"Please enter a pan number into the Order ID field above.\n(Pan number only)", SMessageBoxButtons.Ok, NotificationIcon.Info, 15, OrderRenameWindow.StaticInstance);
        }
    }

    

    private async void RenameOrder()
    {
        OriginalOrderID = ThreeShapeObject!.IntOrderID!;
        if (!CheckIfOrderIDIsUnique(OrderID))
        {
            ShowMessageBox("OrderID issue", $"Not possible to rename the order.\nAn another order in 3Shape has the same name already.\n\nPlease ensure that the order number is unique.", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, OrderRenameWindow.StaticInstance);
            return;
        }


        OrderRenameWindow.StaticInstance.Cursor = Cursors.Wait;
        ControlsEnabled = false;
        OrderIDIsValid = false;
        await RenamingProcess();
    }

    private void ValidateOrderID()
    {
        if (OrderID.Contains('\'') ||
            OrderID.Contains('"') ||
            OrderID.Contains('+') ||
            OrderID.Contains('\\') ||
            OrderID.Contains('/') ||
            OrderID.Contains(':') ||
            OrderID.Contains('*') ||
            OrderID.Contains('?') ||
            OrderID.Contains('<') ||
            OrderID.Contains('>') ||
            OrderID.Contains('|'))
            OrderIDIsValid = false;
        else
            OrderIDIsValid = true;

    }

    private void CopyOriginalOrderId()
    {
        OrderID = ThreeShapeObject!.OriginalOrderID! + "-CHANGE";
    }

    private void ResetChanges()
    {
        OrderID = ThreeShapeObject!.IntOrderID!;
    }

    private async void CloseWindow()
    {
        await UnLockOrderIn3Shape(ThreeShapeObject!.IntOrderID);
        OrderRenameWindow.StaticInstance.Close();
    }

    
    public async Task RenamingProcess()
    {     
        ThreeShapeOrderInspectionModel inspectedOrder = InspectThreeShapeOrder(ThreeShapeObject!.IntOrderID!);
        bool error = false;
        bool NewlyCreatedOrder = false;
        string NewFileName = OrderID;
        string NewFolderName = NewFileName;


        if (inspectedOrder.CaseStatus == "psCreated")
            NewlyCreatedOrder = true;
        //
        // starting renaming process 
        //

        try
        {

            // renaming Order's folder to the new name
            try
            {
                Directory.Move($"{ThreeShapeDirectoryHelper}{OriginalOrderID}", $"{ThreeShapeDirectoryHelper}{NewFileName}");
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
                LogMessage = $"Couldn't rename the order's folder! (some app might still use it or 3Shape has a folder named as the order's new desired name)";
                ControlsEnabled = true;
                OrderIDIsValid = true;
                OrderRenameWindow.StaticInstance.Cursor = Cursors.Arrow;

                return;
            }

            // renaming the XML file to the new name
            File.Move(@$"{ThreeShapeDirectoryHelper}{NewFileName}\{OriginalOrderID}.xml", @$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}.xml");

            //
            // renaming the 3ML file if exists (designed orders only)
            //
            try
            {
                if (File.Exists(@$"{ThreeShapeDirectoryHelper}{NewFileName}\{OriginalOrderID}_3pl.3ml"))
                    File.Move(@$"{ThreeShapeDirectoryHelper}{NewFileName}\{OriginalOrderID}_3pl.3ml", @$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}_3pl.3ml");
            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
            }
            //
            // END
            //



            // 
            // dealing with the XML file
            //
            string XMLFileContent = "";

            try
            {
                // opening up the XML file
                XMLFileContent = File.ReadAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}.xml");

                // replacing all the entry in the text where the original filename presented to the new name
                XMLFileContent = XMLFileContent.Replace(OriginalOrderID, NewFileName);

                //
                // checking if the user want to make a note in the comments (if checkbox checked)


                if (MakeCommentAboutRename && !NewlyCreatedOrder)
                {
                    if (ThreeShapeObject!.OriginalOrderID!.Length > 0)
                    {
                        // if it's a copied order then adding a comment line like this
                        XMLFileContent = XMLFileContent.Replace("Property name=\"OrderComments\" value=\"", "Property name=\"OrderComments\" value=\"This case is a copy of: " + ThreeShapeObject!.OriginalOrderID! + " \n");
                    }
                    else
                    {
                        // if it's not a copied order, adding a comment line like this
                        XMLFileContent = XMLFileContent.Replace("Property name=\"OrderComments\" value=\"", "Property name=\"OrderComments\" value=\"Renamed file of: " + OriginalOrderID + " \n");
                    }
                }
                //
                // END
                //



                // saving the XML file
                File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\{NewFileName}.xml", XMLFileContent);




                //
                // creating and extra TXT file inside the order folder for info, about name changes
                //
                if (ThreeShapeObject!.OriginalOrderID!.Length > 0)
                {
                    File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\RENAMED_ORDER_INFO_{DateTime.Now:yyyyMMdd_HHmmss}.infoFile",
                        "# This 3Shape order was renamed with StatsClient 2025\n\n"
                        + ""
                        + "[Details]\n"
                        + $"New filename={NewFileName}\n"
                        + $"Original filename={OriginalOrderID}\n"
                        + $"Original Order ID={ThreeShapeObject!.OriginalOrderID!}\n"
                        + $"Renamed at={Environment.MachineName} - {Today}");
                }
                else
                {
                    File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\RENAMED_ORDER_INFO_{DateTime.Now:yyyyMMdd_HHmmss}.infoFile",
                        "#This 3Shape order was renamed with StatsClient 2025\n\n"
                        + ""
                        + "[Details]\n"
                        + $"New filename={NewFileName}\n"
                        + $"Original filename={OriginalOrderID}\n"
                        + ""
                        + $"Renamed at={Environment.MachineName} - {Today}");
                }
                //
                // END
                //


                //
                // Renaming in the database
                //

                try
                {


                    ///
                    /// renaming
                    /// 
                    /// [PrintJobItem] [OrderID]
                    /// [OrderHistory] [OrderID]
                    /// [OrderExchangeElement] [OrderID]
                    /// [ImageOverlay] [OrderID]
                    /// [CustomData] [OrderID]
                    ///

                    string connectionString = DatabaseConnection.ConnectionStrFor3Shape();

                    string queryCopyLine = @$"INSERT INTO Orders ( 
                                             [IntOrderID] 
                                            ,[ExtOrderID] 
                                            ,[ClientID] 
                                            ,[ClientOrderNo] 
                                            ,[OrderDate] 
                                            ,[OrderImportanceID] 
                                            ,[Patient_RefNo] 
                                            ,[Patient_FirstName]
                                            ,[Patient_LastName] 
                                            ,[DeliveryAddress1] 
                                            ,[DeliveryAddress2] 
                                            ,[DeliveryZip] 
                                            ,[DeliveryCity] 
                                            ,[DeliveryState] 
                                            ,[DeliveryCountryID] 
                                            ,[DeliveryType] 
                                            ,[ShipToDeliveryAddress] 
                                            ,[ClientContactPerson] 
                                            ,[LabID] 
                                            ,[LabOperator] 
                                            ,[OrderComments] 
                                            ,[CreatedFromApp] 
                                            ,[RelativePos] 
                                            ,[OperatorID] 
                                            ,[DisplayOrderID] 
                                            ,[NumOrderID] 
                                            ,[DesignModuleID] 
                                            ,[ScanModuleID] 
                                            ,[FaceScanModuleID] 
                                            ,[Items] 
                                            ,[OperatorName] 
                                            ,[Customer] 
                                            ,[ManufName] 
                                            ,[OrderRelativePositionClass] 
                                            ,[ShipToERPCustNo] 
                                            ,[ERPCustomerNo] 
                                            ,[ShipToID] 
                                            ,[ModelManufacturingID] 
                                            ,[CacheMaterialName] 
                                            ,[ScanSource] 
                                            ,[ImprovementProgramSendDate] 
                                            ,[GroupFolder] 
                                            ,[CacheColor] 
                                            ,[OriginalOrderID] 
                                            ,[ImportOrderID] 
                                            ,[CacheMaxScanDate] 
                                            ,[TraySystemType]
                                            ,[ExternalLabID] 
                                            ,[ShipToDifferentAddress] 
                                            ,[PatientGuid]) 

                                        SELECT '{NewFileName}'
                                            ,[ExtOrderID] 
                                            ,[ClientID] 
                                            ,[ClientOrderNo] 
                                            ,[OrderDate] 
                                            ,[OrderImportanceID] 
                                            ,[Patient_RefNo] 
                                            ,[Patient_FirstName] 
                                            ,[Patient_LastName] 
                                            ,[DeliveryAddress1] 
                                            ,[DeliveryAddress2] 
                                            ,[DeliveryZip] 
                                            ,[DeliveryCity] 
                                            ,[DeliveryState] 
                                            ,[DeliveryCountryID] 
                                            ,[DeliveryType] 
                                            ,[ShipToDeliveryAddress] 
                                            ,[ClientContactPerson] 
                                            ,[LabID] 
                                            ,[LabOperator] 
                                            ,[OrderComments] 
                                            ,[CreatedFromApp] 
                                            ,[RelativePos] 
                                            ,[OperatorID] 
                                            ,[DisplayOrderID] 
                                            ,[NumOrderID] 
                                            ,[DesignModuleID] 
                                            ,[ScanModuleID] 
                                            ,[FaceScanModuleID] 
                                            ,[Items] 
                                            ,[OperatorName] 
                                            ,[Customer] 
                                            ,[ManufName] 
                                            ,[OrderRelativePositionClass] 
                                            ,[ShipToERPCustNo] 
                                            ,[ERPCustomerNo] 
                                            ,[ShipToID] 
                                            ,[ModelManufacturingID] 
                                            ,[CacheMaterialName] 
                                            ,[ScanSource] 
                                            ,[ImprovementProgramSendDate] 
                                            ,[GroupFolder] 
                                            ,[CacheColor] 
                                            ,[OriginalOrderID] 
                                            ,[ImportOrderID] 
                                            ,[CacheMaxScanDate] 
                                            ,[TraySystemType] 
                                            ,[ExternalLabID] 
                                            ,[ShipToDifferentAddress] 
                                            ,[PatientGuid] FROM Orders WHERE IntOrderID = '{OriginalOrderID}'";

                    await RunCommandAsynchronouslyWithLogging(queryCopyLine, connectionString);




                    string query6 = $"UPDATE ModelJob SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query6, connectionString);

                    string query2 = $"UPDATE OrderHistory SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query2, connectionString);

                    string query5 = $"UPDATE CustomData SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query5, connectionString);

                    string query1 = $"UPDATE PrintJobItem SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query1, connectionString);

                    string query7 = $"UPDATE CommunicateOrders SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query7, connectionString);

                    string query3 = $"UPDATE OrderExchangeElement SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query3, connectionString);

                    string query4 = $"UPDATE ImageOverlay SET OrderID = '{NewFileName}' WHERE OrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(query4, connectionString);




                    UpdateLastModifyDateinDatabase(NewFileName);




                    string queryRemoveOriginalLine = $"DELETE FROM Orders WHERE IntOrderID = '{OriginalOrderID}'";
                    await RunCommandAsynchronouslyWithLogging(queryRemoveOriginalLine, connectionString);



                    //
                    // checking if the user want to make a note in the comments (if checkbox checked)
                    //
                    if (MakeCommentAboutRename && !NewlyCreatedOrder)
                    {
                        string commentsHelper = "";
                        if (ThreeShapeObject!.OriginalOrderID!.Length > 0)
                        {
                            // if it's a copied order then adding a comment line like this
                            commentsHelper = $"This case is a copy of: {ThreeShapeObject!.OriginalOrderID!} \n{commentsHelper}";

                        }
                        else
                        {
                            // if it's not a copied order, adding a comment line like this
                            commentsHelper = $"Renamed file of: {OriginalOrderID} \n{commentsHelper}";
                        }

                        string queryUpdateComment = $"UPDATE Orders SET OrderComments = '{commentsHelper}' WHERE IntOrderID = '{NewFileName}'";
                        await RunCommandAsynchronouslyWithLogging(queryUpdateComment, connectionString);
                    }
                    //
                    // END
                    //



                }
                catch (Exception ex)
                {
                    MainViewModel.Instance.AddDebugLine(ex);
                    LogMessage = $"Error ({ex.LineNumber()}): [{ex.Message}]";
                    LogMessages.Add(LogMessage);
                    error = true;
                }

                //
                // END
                //





            }
            catch (Exception ex)
            {
                MainViewModel.Instance.AddDebugLine(ex);
                error = true;
                LogMessage = $"Error ({ex.LineNumber()}): [{ex.Message}]";
                LogMessages.Add(LogMessage);
                ShowMessageBox("Error", $"{ex.LineNumber()} - {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, OrderRenameWindow.StaticInstance);
            }
            //
            // END
            //


        }
        catch (Exception e)
        {
            MainViewModel.Instance.AddDebugLine(e);
            error = true;
            LogMessage = $"Error ({e.LineNumber()}): [{e.Message}]";
            LogMessages.Add(LogMessage);
        }


        //
        // returning every form control to original stage
        //
        OrderRenameWindow.StaticInstance.Cursor = Cursors.Arrow;

        if (!error)
        {
            LogMessage = $"\nRenaming finised with no issues.";
            LogMessages.Add(LogMessage);

            if (LogMessages.Count > 0)
            {
                string message = "";
                foreach (string line in LogMessages)
                    message += line + "\n";
                try
                {
                    File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\OrderRename.log", message);
                }
                catch (Exception ex)
                {
                    MainViewModel.Instance.AddDebugLine(ex);
                }
            }

            //openOrderIdHelper = NewFileName;
            await UnLockOrderIn3Shape(NewFileName);
            OrderRenameWindow.StaticInstance.Close();
        }
        else
        {
            LogMessage = $"\nEncountered some issues during renaming..";
            LogMessages.Add(LogMessage);

            if (LogMessages.Count > 0)
            {
                string message = "";
                foreach (string line in LogMessages)
                    message += line + "\n";
                try
                {
                    File.WriteAllText(@$"{ThreeShapeDirectoryHelper}{NewFolderName}\OrderRename.log", message);
                }
                catch (Exception ex)
                {
                    MainViewModel.Instance.AddDebugLine(ex);
                }
            }
        }


        ControlsEnabled = true;
        OrderIDIsValid = true;
        //
        // END
        //
        OrderRenameWindow.StaticInstance.Cursor = Cursors.Arrow;
    }




    private async Task RunCommandAsynchronouslyWithLogging(string commandText, string connectionString)
    {
        using SqlConnection connection = new(connectionString);
        try
        {
            SqlCommand command = new(commandText, connection);
            connection.Open();

            IAsyncResult result = command.BeginExecuteNonQuery();
            while (!result.IsCompleted)
            {
                Thread.Sleep(100);
            }
            LogMessage = $"Command complete. Affected [{command.EndExecuteNonQuery(result)}] rows.";
            LogMessages.Add(LogMessage);
            await Task.Delay(20);
        }
        catch (SqlException ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            LogMessage = $"Error Exception ({ex.LineNumber()}): [{ex.Message}]";
            LogMessages.Add(LogMessage);
            await Task.Delay(300);
        }
        catch (InvalidOperationException ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            LogMessage = $"Error ({ex.LineNumber()}): [{ex.Message}]";
            LogMessages.Add(LogMessage);
            await Task.Delay(300);
        }
        catch (Exception ex)
        {
            MainViewModel.Instance.AddDebugLine(ex);
            LogMessage = $"Error General ({ex.LineNumber()}): [{ex.Message}]";
            LogMessages.Add(LogMessage);
            await Task.Delay(300);
        }
    }

    public SMessageBoxResult ShowMessageBox(string Title, string Message, SMessageBoxButtons Buttons,
                                              NotificationIcon MessageBoxIcon,
                                              double DismissAfterSeconds = 300,
                                              Window? Owner = null)
    {
        SMessageBox sMessageBox = new(Title, Message, Buttons, MessageBoxIcon, DismissAfterSeconds);
        if (Owner is null)
            sMessageBox.Owner = MainWindow.Instance;
        else
            sMessageBox.Owner = Owner;

        sMessageBox.ShowDialog();

        return MainViewModel.Instance.SMessageBoxxResult;
    }
}
