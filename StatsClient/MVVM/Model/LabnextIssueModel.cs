namespace StatsClient.MVVM.Model;

public class LabnextIssueModel
{
    public int LabnextID { get; set; } = 0;
    public string? CreationDate { get; set; }
    public string? InvoiceDate { get; set; }
    public int PanNumber { get; set; } = 0;
    public string? Status { get; set; }
    public string? Patient_FirstName { get; set; }
    public string? Patient_LastName { get; set; }
    public int? UnitCount { get; set; } = 0;
    public string? Items { get; set; }
    public string? TeethNumbers { get; set; }
    public double? Price { get; set; } = 0;
    public string? Issue { get; set; }
    public string? InvoiceDateRange { get; set; }
    public string? DesignerName { get; set; }
    public string? DesignerID { get; set; }
    public string? GotPaid { get; set; }
    public string? Customer { get; set; }
}
