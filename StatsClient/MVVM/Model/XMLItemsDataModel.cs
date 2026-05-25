using System.Windows.Media;

namespace StatsClient.MVVM.Model
{
    public class XMLItemsDataModel
    {
        public string Item { get; set; }
        public string CacheMaterialName { get; set; }
        public string ModelJobID { get; set; }
        public string ManufacturingProcessID { get; set; }
        public string ManufacturerID { get; set; }
        public string ManufName { get; set; }
        public string? ManufacturingProcessID_stCopy { get; set; }
        public string? ManufacturerID_stCopy { get; set; }
        public string? ManufName_stCopy { get; set; }
        public SolidColorBrush StCopyExists { get; set; }
        public string? ValidateItemIntegrity { get; set; }

        public XMLItemsDataModel(string item, string cacheMaterialName, string modelJobID, string manufacturingProcessID,
                            string manufacturerID, string manufName, string manufacturingProcessID_stCopy,
                            string manufacturerID_stCopy, string manufName_stCopy, SolidColorBrush stCopyExists,
                            string validateItemIntegrity)
        {
            Item = item;
            CacheMaterialName = cacheMaterialName;
            ModelJobID = modelJobID;
            ManufacturingProcessID = manufacturingProcessID;
            ManufacturerID = manufacturerID;
            ManufName = manufName;
            ManufacturingProcessID_stCopy = manufacturingProcessID_stCopy;
            ManufacturerID_stCopy = manufacturerID_stCopy;
            ManufName_stCopy = manufName_stCopy;
            StCopyExists = stCopyExists;
            ValidateItemIntegrity = validateItemIntegrity;
        }
    }
}