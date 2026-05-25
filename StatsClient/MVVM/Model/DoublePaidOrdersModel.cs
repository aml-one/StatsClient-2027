namespace StatsClient.MVVM.Model;

public class DoublePaidOrdersModel
{
    public string? OrderID { get; set; }
    
    public string? DesignerName { get; set; }
    public string? DesignDate { get; set; }
    public string? DesignTime { get; set; }
    
    public string? GotPaid { get; set; }
    
    public string? SecondDesignerName { get; set; }
    public string? SecondDesignDate { get; set; }
    public string? SecondDesignTime { get; set; }

    public string? Patient_Lastname { get; set; }
    public string? Patient_Firstname { get; set; }
    public int? PanNumber { get; set; }
    public int? LxLabnextID { get; set; }
    public string? LxInvoiceDate { get; set; }
    public string? LxInvoiceDateRange { get; set; }

    public bool IsitRedo { get; set; }
}
