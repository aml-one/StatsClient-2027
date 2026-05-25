using StatsClient.MVVM.Model;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using Bitmap = System.Drawing.Bitmap;


namespace StatsClient.MVVM.Core;

internal class Functions
{
    public static string GetUnixTimeStampFromDate(DateTime date)
    {
        int unixTimestamp = (int)date.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        return unixTimestamp.ToString();
    }

    public static string GetPayPeriodForDate(DateTime date)
    {
        DateTime payPeriodAnchor = new DateTime(2026, 3, 3);
        int periodLengthDays = 17;

        int daysDifference = (payPeriodAnchor.Date - date.Date).Days;
        int periodsBack = daysDifference >= 0 ? daysDifference / periodLengthDays : (daysDifference / periodLengthDays) - 1;

        DateTime periodEnd = payPeriodAnchor.AddDays(-periodsBack * periodLengthDays);
        DateTime periodStart = periodEnd.AddDays(-periodLengthDays + 1);

        return $"{periodStart:MMM d} - {periodEnd:MMM d}";
    }

    public static (DateTime start, DateTime end) GetPayPeriodBoundariesForDate(DateTime date)
    {
        DateTime payPeriodAnchor = new DateTime(2026, 3, 3);
        int periodLengthDays = 17;

        int daysDifference = (payPeriodAnchor.Date - date.Date).Days;
        int periodsBack = daysDifference >= 0 ? daysDifference / periodLengthDays : (daysDifference / periodLengthDays) - 1;

        DateTime periodEnd = payPeriodAnchor.AddDays(-periodsBack * periodLengthDays);
        DateTime periodStart = periodEnd.AddDays(-periodLengthDays + 1);

        return (periodStart, periodEnd);
    }

    /// <summary>
    /// Returns the substring starting after the first occurrence of a keyword.
    /// </summary>
    public static string CopyStringTill(string source, char stopChar)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        if (!source.Contains(stopChar, StringComparison.OrdinalIgnoreCase))
            return source; // Keyword not found

        return source[..source.IndexOf(stopChar)].Trim();
    }
    
    public static string CopyStringTill(string source, string stopChar)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        if (!source.Contains(stopChar, StringComparison.OrdinalIgnoreCase))
            return source; // Keyword not found

        return source[..source.IndexOf(stopChar)].Trim();
    }

    public static string CopyStringFromAfter(string source, string after, int thisMuchCharacters = 0)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(after))
            return string.Empty;

        int index = source.IndexOf(after, StringComparison.OrdinalIgnoreCase);
        if (index == -1)
            return string.Empty; // Keyword not found

        // Start after the keyword
        int startIndex = index + after.Length;
        if (startIndex >= source.Length)
            return string.Empty; // Nothing after keyword

        if (thisMuchCharacters == 0)
            return source[startIndex..].Trim();
        else if (startIndex + thisMuchCharacters <= source.Length)
            return source.Substring(startIndex, thisMuchCharacters).Trim();
        else
            return source[startIndex..].Trim();
    }
    public static string UnixTimeStampToDateTime(string TimeStamp, bool withMonthNames = false, bool overSimplified = false)
    {
        _ = double.TryParse(TimeStamp, out var unixTimeStamp);
        DateTime dateTime = new (1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();

        if (overSimplified)
            return dateTime.ToString("MMM d, yyyy");

        if (withMonthNames)
            return dateTime.ToString("MMM d. yyyy - h:mm:ss tt");
        else
            return dateTime.ToString("M/d/yyyy h:mm:ss tt");
    }

    public static async Task<bool> AddLastDesignedByToOrder(string orderID, string designerID)
    {
        bool allGood = true;
        List<DesignerModel> designersList = await GetDesignersListAtStartAsync();

        string designerName = designersList.FirstOrDefault(x => x.DesignerID == designerID)?.FriendlyName!;
        string ThreeShapeDirectoryHelper = DatabaseOperations.GetServerFileDirectory();

        // DesignedBy file
        string designedByFile = @$"{ThreeShapeDirectoryHelper}{orderID}\History\DesignedBy";

        List<string> fileContent = [];

        if (File.Exists(designedByFile))
        {
            try
            {
                File.ReadAllLines(designedByFile).ToList().ForEach(fileContent.Add);
            }
            catch (Exception)
            {                
            }
        }
        else if (File.Exists(designedByFile.Replace(@"\History\", @"\")))
        {
            designedByFile = designedByFile.Replace(@"\History\", @"\");

            try
            {
                File.ReadAllLines(designedByFile).ToList().ForEach(fileContent.Add);
            }
            catch (Exception)
            {
            }
        }

        fileContent.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {designerName}");

        try
        {
            if (!Directory.Exists(@$"{ThreeShapeDirectoryHelper}{orderID}\History"))
                Directory.CreateDirectory(@$"{ThreeShapeDirectoryHelper}{orderID}\History");

            File.WriteAllLines(designedByFile, fileContent);
        }
        catch (Exception)
        {
            allGood = false;
        }

        // lastDesigner file
        string lastDesignerFile = @$"{ThreeShapeDirectoryHelper}{orderID}\lastDesigner";
        try
        {
            File.WriteAllText(lastDesignerFile, designerID);
        }
        catch (Exception)
        {
            allGood = false;
        }

        // XML Injection
        string xMLFile = @$"{ThreeShapeDirectoryHelper}{orderID}\{orderID}.xml";
        try
        {
            XmlDocument doc = new();
            doc.Load(xMLFile);

            XmlElement? root = doc.DocumentElement;

            List<XmlNode> nodes = [];

            foreach (XmlNode node in root!.ChildNodes[0]!.ChildNodes)
            {
                if (!nodes.Contains(node))
                    nodes.Add(node);
            }

            XmlNode TDM_Item_Order_XMLNode = nodes.Single(x => x.Attributes!["name"]!.Value == "OrderList");

            bool NeedToSaveXML = false;
            foreach (XmlNode node in TDM_Item_Order_XMLNode.ChildNodes[0]!.ChildNodes[0]!)
            {
                if (node.Attributes!["name"]!.Value == "Patient_RefNo")
                {
                    NeedToSaveXML = true;
                    node.Attributes["value"]!.Value = $"Designed by: {designerName.Replace("(", "").Replace(")", "").Trim()} - {DateTime.Now:MM/dd h:mm tt}";
                }
                if (node.Attributes!["name"]!.Value == "ExtOrderID")
                {
                    NeedToSaveXML = true;
                    node.Attributes["value"]!.Value = $"{designerName.Replace("(", "").Replace(")", "").Trim()}";
                }
            }

            if (NeedToSaveXML)
            {
                Task.Run(() => doc.Save(xMLFile)).Wait();
                Task.Run(() => RemoveTrailingSpaceInXML(xMLFile)).Wait();
            }
        }
        catch (Exception)
        {
            allGood = false;
        }

        // database injection
        try
        {
            string connectionString = await Task.Run(ConnectionStrFor3Shape);
            string query = @$"UPDATE Orders SET ExtOrderID = '{designerName.Replace("(", "").Replace(")", "").Trim()}', Patient_RefNo = 'Designed by: {designerName.Replace("(", "").Replace(")", "").Trim()} - {DateTime.Now:MM/dd h:mm tt}' WHERE IntOrderID = '{orderID}'";
            Debug.WriteLine(query);
            RunSQLCommandAsynchronously(query, connectionString);
        }
        catch (Exception)
        {
            allGood = false;
        }

        return allGood;
    }

    public static async Task RemoveTrailingSpaceInXML(string xMLFile)
    {
        string text = await Task.Run(() => File.ReadAllText(xMLFile));
        text = text.Replace(" />", "/>");
        await Task.Run(() => File.WriteAllText(xMLFile, text));
    }

    public static List<DesignedByModel> GetLastDesignedByListData(string orderID)
    {
        List<DesignedByModel> designerHistory = [];
        
        string ThreeShapeDirectoryHelper = DatabaseOperations.GetServerFileDirectory();
        string designedByFile = @$"{ThreeShapeDirectoryHelper}{orderID}\History\DesignedBy";

        if (File.Exists(designedByFile))
        {
            try
            {
                File.ReadAllLines(designedByFile).ToList().ForEach(x =>
                {
                    string[] parts = x.Split(']');

                    _ = DateTime.TryParse(parts[0].Replace("[", ""), out DateTime dtTime);
                    string designr = parts[1].Trim();
                    
                    designerHistory.Add(new DesignedByModel()
                    {
                        DateTimeStr = dtTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Designer = designr
                    });
                });

                if (designerHistory.Count > 0)
                    designerHistory.Reverse();

            }
            catch (Exception)
            {                
            }
        }
        else if (File.Exists(designedByFile.Replace(@"\History\", @"\")))
        {
            designedByFile = designedByFile.Replace(@"\History\", @"\");

            try
            {
                File.ReadAllLines(designedByFile).ToList().ForEach(x =>
                {
                    string[] parts = x.Split(']');

                    _ = DateTime.TryParse(parts[0].Replace("[", ""), out DateTime dtTime);
                    string designr = parts[1].Trim();

                    designerHistory.Add(new DesignedByModel()
                    {
                        DateTimeStr = dtTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Designer = designr
                    });
                });

                if (designerHistory.Count > 0)
                    designerHistory.Reverse();

            }
            catch (Exception)
            {
            }
        }

        return designerHistory;
    }

    public static async Task<string> GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("version.txt"));
        string versionResult = "";
        using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
        using (StreamReader reader = new(stream))
        {
            versionResult = reader.ReadToEnd();
        }

        await Task.Delay(10);

        return versionResult;
    }

    public static bool IsWindowIsShown<T>(string name = "") where T : Window
    {
        return string.IsNullOrEmpty(name)
           ? Application.Current.Windows.OfType<T>().FirstOrDefault()!.Visibility == Visibility.Visible
           : Application.Current.Windows.OfType<T>().FirstOrDefault(w => w.Name.Equals(name))!.Visibility == Visibility.Visible;
    }

    /// <summary>
    /// Finds a Child of a given item in the visual tree. 
    /// </summary>
    /// <param name="parent">A direct parent of the queried item.</param>
    /// <typeparam name="T">The type of the queried item.</typeparam>
    /// <param name="childName">x:Name or Name of child. </param>
    /// <returns>The first parent item that matches the submitted type parameter or null if not found</returns> 
    public static T FindChild<T>(DependencyObject parent, string childName)
       where T : DependencyObject
    {
        // Confirm parent and childName are valid. 
        if (parent == null) return null;

        T foundChild = null;

        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            // If the child is not of the request child type child
            T childType = child as T;
            if (childType == null)
            {
                // recursively drill down the tree
                foundChild = FindChild<T>(child, childName);

                // If the child is found, break so we do not overwrite the found child. 
                if (foundChild != null) break;
            }
            else if (!string.IsNullOrEmpty(childName))
            {
                var frameworkElement = child as FrameworkElement;
                // If the child's name is set for search
                if (frameworkElement != null && frameworkElement.Name == childName)
                {
                    // if the child's name is of the request name
                    foundChild = (T)child;
                    break;
                }
            }
            else
            {
                // child element found.
                foundChild = (T)child;
                break;
            }
        }

        return foundChild;
    }

    public static bool CheckIfItsDarkColor(string rgbColor)
    {
        string[] panColorParts = rgbColor.Split('-');

        _ = int.TryParse(panColorParts[0], out int colorR);
        _ = int.TryParse(panColorParts[1], out int colorG);
        _ = int.TryParse(panColorParts[2], out int colorB);

        if (colorR * 0.2126 + colorG * 0.7152 + colorB * 0.0722 < 255 / 2)
        {
            // dark color
            return true;
        }
        else
        {
            // light color
            return false;
        }
    }

    public static Brush RgbToBrushConverter(string rgbColor)
    {
        try
        {
            string[] panColorParts = rgbColor.Split('-');

            _ = int.TryParse(panColorParts[0], out int colorR);
            _ = int.TryParse(panColorParts[1], out int colorG);
            _ = int.TryParse(panColorParts[2], out int colorB);

            Brush panColor = new SolidColorBrush(Color.FromArgb(255, (byte)colorR, (byte)colorG, (byte)colorB));

            return panColor;
        }
        catch (Exception)
        {
            return new SolidColorBrush(Color.FromArgb(255,255,255,255));
        }
    }


    public static string CleanUpCustomerName(string customer)
    {
        customer = customer.Trim().ToLower();

        if (customer.StartsWith("dr ", StringComparison.CurrentCultureIgnoreCase))
            customer = customer[2..].Trim();

        if (customer.StartsWith("dr.", StringComparison.CurrentCultureIgnoreCase))
            customer = customer.Replace("dr.","").Trim();
        
        if (customer.StartsWith("dr_", StringComparison.CurrentCultureIgnoreCase))
            customer = customer.Replace("dr_","").Trim();

        if (customer.Contains('('))
            customer = customer[..customer.IndexOf('(')].Trim();
        


        return customer.Replace(" ", "_")
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
                       .Trim()
                       .ToUpper();
    }

    public static bool CopyDirectory(string source, string target, bool moveInsteadOfCopy = false)
    {
        try
        {
            var stack = new Stack<Folders>();
            stack.Push(new Folders(source, target));

            while (stack.Count > 0)
            {
                var folders = stack.Pop();
                Directory.CreateDirectory(folders.Target);
                foreach (var file in Directory.GetFiles(folders.Source, "*.*"))
                {
                    try
                    {
                        string targetFile = Path.Combine(folders.Target, Path.GetFileName(file));
                        if (File.Exists(targetFile))
                            File.Delete(targetFile);

                        if (moveInsteadOfCopy)
                            File.Move(file, targetFile, true);
                        else
                            File.Copy(file, targetFile, true);
                    }
                    catch
                    {

                    }
                }

                foreach (var folder in Directory.GetDirectories(folders.Source))
                {
                    stack.Push(new Folders(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
                }
            }

            if (moveInsteadOfCopy)
                Directory.Delete(source, true);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public class Folders(string source, string target)
    {
        public string Source { get; private set; } = source;
        public string Target { get; private set; } = target;
    }

    public static Color GetPixelColor(BitmapSource bitmap, int x, int y)
    {
        Color color = Colors.White;
        try
        {
            var bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
            var bytes = new byte[bytesPerPixel];
            var rect = new Int32Rect(x, y, 1, 1);

            bitmap.CopyPixels(rect, bytes, bytesPerPixel, 0);

            if (bitmap.Format == PixelFormats.Bgra32)
            {
                color = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            }
            else if (bitmap.Format == PixelFormats.Bgr32)
            {
                color = Color.FromRgb(bytes[2], bytes[1], bytes[0]);
            }
            else
            {
                color = Colors.White;
            }
        }
        catch
        {
        }
        return color;
    }

    public static void CleanTempFolder()
    {
        // creating Temp folder
        if (Directory.Exists(DataBaseFolder + "\\Temp"))
        {
            try
            {
                Directory.Delete(DataBaseFolder + "\\Temp", true);
                Directory.CreateDirectory(DataBaseFolder + "\\Temp");
            }
            catch (Exception ex)
            {
                MessageBox.Show("#13\n\n" + ex.Message);
            }
        }
        else
            Directory.CreateDirectory(DataBaseFolder + "\\Temp");
    }

    public static Bitmap Crop(Bitmap bitmap, string PageHeaderIsHigh)
    {
        bool isSironaPaper = false;
        if (PageHeaderIsHigh == "4")
            isSironaPaper = true;

        int w = bitmap.Width;
        int h = bitmap.Height;

        if (isSironaPaper)
        {
            h = h - 103;
        }

        Func<int, bool> IsAllWhiteRow = row =>
        {
            for (int i = 0; i < w; i++)
            {
                if (bitmap.GetPixel(i, row).R != 255)
                {
                    return false;
                }
            }
            return true;
        };

        Func<int, bool> IsAllWhiteColumn = col =>
        {
            for (int i = 0; i < h; i++)
            {
                if (bitmap.GetPixel(col, i).R != 255)
                {
                    return false;
                }
            }
            return true;
        };

        int leftMost = 0;
        for (int col = 0; col < w; col++)
        {
            if (IsAllWhiteColumn(col)) leftMost = col + 1;
            else break;
        }

        int rightMost = w - 1;
        for (int col = rightMost; col > 0; col--)
        {
            if (IsAllWhiteColumn(col)) rightMost = col - 1;
            else break;
        }

        int topMost = 0;
        for (int row = 0; row < h; row++)
        {
            if (IsAllWhiteRow(row)) topMost = row + 1;
            else break;
        }

        int bottomMost = h - 1;
        for (int row = bottomMost; row > 0; row--)
        {
            if (IsAllWhiteRow(row)) bottomMost = row - 1;
            else break;
        }

        if (rightMost == 0 && bottomMost == 0 && leftMost == w && topMost == h)
        {
            return bitmap;
        }

        int croppedWidth = rightMost - leftMost + 1;
        int croppedHeight = bottomMost - topMost + 1;

        try
        {
            Bitmap target = new Bitmap(w, croppedHeight);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(target))
            {
                g.DrawImage(bitmap,
                    new System.Drawing.RectangleF(0, 15, w, croppedHeight),
                    new System.Drawing.RectangleF(0, topMost, w, croppedHeight + 15),
                    System.Drawing.GraphicsUnit.Pixel);
            }
            return target;
        }
        catch (Exception ex)
        {
            throw new Exception(string.Format("Values are top={0} bottom={1} left={2} right={3}", topMost, bottomMost, leftMost, rightMost), ex);
        }
    }

    public static Bitmap CombineImages(List<String> files)
    {
        //read all images into memory
        List<Bitmap> images = [];
        Bitmap finalImage = null;

        try
        {
            int width = 0;
            int height = 0;

            foreach (string image in files)
            {
                //create a Bitmap from the file and add it to the list
                Bitmap bitmap = new (image);

                //update the size of the final bitmap
                width = bitmap.Width > width ? bitmap.Width : width;
                height += bitmap.Height;

                images.Add(bitmap);
            }

            //create a bitmap to hold the combined image
            finalImage = new Bitmap(width, height);

            //get a graphics object from the image so we can draw on it
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(finalImage))
            {
                //set background color
                g.Clear(System.Drawing.Color.White);

                //go through each image and draw it on the final image
                int offset = 0;
                foreach (Bitmap image in images)
                {
                    g.DrawImage(image,
                      new System.Drawing.Rectangle(0, offset, image.Width, image.Height));
                    offset += image.Height;
                }
            }

            return finalImage;
        }
        catch (Exception ex)
        {
            finalImage?.Dispose();
            throw;
        }
        finally
        {
            //clean up memory
            foreach (Bitmap image in images)
            {
                image.Dispose();
            }
        }
    }

    public static BitmapSource BitmapToBitmapSourceConvert(Bitmap bitmap)
    {
        var bitmapData = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

        var bitmapSource = BitmapSource.Create(
            bitmapData.Width, bitmapData.Height,
            bitmap.HorizontalResolution, bitmap.VerticalResolution,
            PixelFormats.Bgr24, null,
            bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

        bitmap.UnlockBits(bitmapData);

        return bitmapSource;
    }

    public static Bitmap GetBitmap(BitmapSource source)
    {
        Bitmap bmp = new Bitmap(
          source.PixelWidth,
          source.PixelHeight,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        BitmapData data = bmp.LockBits(
          new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size),
          ImageLockMode.WriteOnly,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        source.CopyPixels(
          Int32Rect.Empty,
          data.Scan0,
          data.Height * data.Stride,
          data.Stride);
        bmp.UnlockBits(data);
        return bmp;
    }

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject([In] IntPtr hObject);

    public static ImageSource ImageSourceFromBitmap(Bitmap bmp)
    {
        var handle = bmp.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        finally { DeleteObject(handle); }
    }

    public static async Task<bool> WriteResourceToFile(string resourceName, string fileName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(assembly.GetManifestResourceNames().Single(str => str.EndsWith(resourceName)));
            using var file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            await Task.Run(() => resource!.CopyTo(file));
            return true;
        }
        catch
        {
        }
        return false;
    }


    public static string DetermininingShade(string OrderID)
    {
        string ThreeShapeDirectory = DatabaseConnection.GetServerFileDirectory();

        // check in filename
        string[] parts = OrderID.Split('-');

        foreach (string part in parts)
        {
            if (Shades.Any(x => x.Equals(part, StringComparison.CurrentCultureIgnoreCase)))
                return part.ToUpper().Replace(",", "").Replace(".", "");
        }

        //check in shade file
        if (File.Exists($@"{ThreeShapeDirectory}\{OrderID}\shade"))
        {
            string shade = File.ReadAllText($@"{ThreeShapeDirectory}\{OrderID}\shade");
            if (Shades.Any(x => x.Equals(shade, StringComparison.CurrentCultureIgnoreCase)))
                return shade.ToUpper().Replace(",", "").Replace(".", "");
        }

        //check in XML file
        string XMLFile = $@"{ThreeShapeDirectory}\{OrderID}\{OrderID}.xml";
        if (File.Exists(XMLFile))
        {
            string shade = "";
            bool checkInLines = false;
            var lines = File.ReadAllLines(XMLFile);
            for (var i = 0; i < lines.Length; i += 1)
            {
                var line = lines[i];
                if (line.Contains("value=\"DS_ShadeField\""))
                    checkInLines = true;

                if (checkInLines)
                {
                    if (line.Contains("Property name=\"Value\""))
                    {
                        shade = line.Replace("<Property name=\"Value\" value=\"", "").Replace("\"/>", "").Replace("\" />", "").Trim();
                        return shade.ToUpper().Replace(",", "").Replace(".", "").Replace("-", "");
                    }
                }

                if (line.Contains("</List>"))
                    checkInLines = false;
            }
        }

        return "";
    }

    public static List<string> Shades =
        [
            "A1",
            "A2",
            "A3",
            "A35",
            "A3.5",
            "A3,5",
            "A4",
            "B1",
            "B2",
            "B3",
            "B4",
            "C1",
            "C2",
            "C3",
            "C4",
            "D2",
            "D3",
            "D4",
            "010",
            "020",
            "030",
            "040",
            "BL1",
            "BL2",
            "BL3",
            "BL4",
            "0M1",
            "0M2",
            "0M3",
            "0M4",
            "OM1",
            "OM2",
            "OM3",
            "OM4",
            "1M1",
            "1M2",
            "2M1",
            "2M2",
            "2M3",
            "3M1",
            "3M2",
            "3M3",
            "4M1",
            "4M2",
            "4M3",
            "5M1",
            "5M2",
            "5M3",
            "2L15",
            "2L1.5",
            "2L1,5",
            "2L25",
            "2L2.5",
            "2L2,5",
            "2R15",
            "2R1.5",
            "2R1,5",
            "2R25",
            "2R2.5",
            "2R2,5",
            "3L15",
            "3L1.5",
            "3L1,5",
            "3L25",
            "3L2.5",
            "3L2,5",
            "3R15",
            "3R1.5",
            "3R1,5",
            "3R25",
            "3R2.5",
            "3R2,5",
            "4L15",
            "4L1.5",
            "4L1,5",
            "4L25",
            "4L2.5",
            "4L2,5",
            "4R15",
            "4R1.5",
            "4R1,5",
            "4R25",
            "4R2.5",
            "4R2,5",
        ];
}
