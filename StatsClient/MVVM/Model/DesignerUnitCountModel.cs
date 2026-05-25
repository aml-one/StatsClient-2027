namespace StatsClient.MVVM.Model;

public class DesignerUnitCountModel
{
    public string? DesignerID { get; set; }
    public string? DesignerName { get; set; }
    
    public int TotalCasesCount { get; set; }
    public int TotalUnits { get; set; }
    public int Crowns { get; set; }
    public int Abutments { get; set; }
    public int Gingiva { get; set; }
    
    
    public int RedoCasesCount { get; set; }
    public int RedoUnits { get; set; }

    
    public int CrownPrice { get; set; }
    public int AbutmentPrice { get; set; }
    public int GingivaPrice { get; set; }
    public int TotalPrice { get; set; }
}
