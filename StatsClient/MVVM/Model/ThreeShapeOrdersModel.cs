using System.Windows;

namespace StatsClient.MVVM.Model;

public class ThreeShapeOrdersModel{
    public string? IntOrderID { get; set; }
    public string? Patient_FirstName { get; set; }
    public string? Patient_LastName { get; set; }
    public string? Patient_RefNo { get; set; } 
    public string? ExtOrderID { get; set; } 
    public string? OrderComments { get; set; }
    public string? Items { get; set; } 
    public string? OperatorName { get; set; }
    public string? Customer { get; set; }
    public string? ManufName { get; set; }
    public string? CacheMaterialName { get; set; }
    public string? ScanSource { get; set; }
    public string? CacheMaxScanDate { get; set; }
    public string? TraySystemType { get; set; } 
    public string? MaxCreateDate { get; set; } 
    public string? MaxProcessStatusID { get; set; }
    public string? ProcessStatusID { get; set; } 
    public string? AltProcessStatusID { get; set; } 
    public string? ProcessLockID { get; set; } 
    public string? WasSent { get; set; } 
    public string? ModificationDate { get; set; } 
    public string? ImageSource { get; set; } 
    public string? ListViewGroup { get; set; } 
    public string? PanColor { get; set; } 
    public string? PanColorName { get; set; } 
    public string? CaseStatus { get; set; } 
    public string? PanNumber { get; set; } 
    public string? Shade { get; set; } 
    public string? LastModificationForSorting { get; set; } 
    public string? LastModifiedComputerName { get; set; } 
    public string? CreateDateForSorting { get; set; } 
    public string? ScanSourceFriendlyName { get; set; } 
    public string? CacheMaxScanDateFriendly { get; set; } 
    public string? MaxCreateDateFriendly { get; set; } 
    public string? CaseStatusByManufacturer { get; set; } 
    public string? AlternateColoring { get; set; } 
    public string? OriginalOrderID { get; set; }
    public string? DesignerID { get; set; }
    public string? DesignerName { get; set; }
    public string? OrderFolderPath { get; set; }
    public string? XmlFilePath { get; set; }
    public string? ArchiveBaseFolderPath { get; set; }

    public bool IsCaseWereDesigned { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public bool IsCheckedOut { get; set; } = false;
    public bool CanBeRenamed { get; set; } = false;
    public bool CanGenerateStCopy { get; set; } = false;
    public bool HasDesignerHistory { get; set; } = false;
    public bool PreviouslyDesigned { get; set; } = false;
    public List<DesignerHistoryModel>? DesignerHistory { get; set; } = [];
    public bool HasAnyImage { get; set; } = false;
    public bool IsItRedo { get; set; } = false;
}


public class DesignerHistoryModel()
{
    public string? Year { get; set; }
    public string? Day { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public string? DesignerName { get; set; }
}