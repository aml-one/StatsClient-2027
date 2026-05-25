namespace StatsClient.MVVM.Model;

public class ArchivesOrdersModel
{
    public int Id { get; set; } = 0;
    public string? OrderID { get; set; }
    public string? PanNumber { get; set; }
    public string? Patient_FirstName { get; set; }
    public string? Patient_LastName { get; set; }
    public string? Registered { get; set; }
    public string? Customer { get; set; }
    public string? XMLFile { get; set; }
    public string? BaseFolder { get; set; }
    public string? HostingComputer { get; set; }

    public string? LastUdated { get; set; }
    public string? Items { get; set; }
    public string? ItemsDetailed { get; set; }
    public string? OrderComments { get; set; }
    public string? Icon { get; set; }
    public string? ProcessStatusID { get; set; }
    public string? ProcessLockID { get; set; }
    public string? ScanSource { get; set; }

    public string? DesignModuleID { get; set; }
    public string? DentalVersion { get; set; }

    public string? OriginalOrderID { get; set; }
    public string? ManufName { get; set; }
    public string? CacheMaterialName { get; set; }


    public string? CreateDate { get; set; }
    public string? CacheMaxScanDate { get; set; }
    public string? IsStillAlive { get; set; }
    public string? ReasonIsDead { get; set; }
    public string? DesignerID { get; set; }
    public string? DesignerName { get; set; }

    public string? CreateYear { get; set; }
}
