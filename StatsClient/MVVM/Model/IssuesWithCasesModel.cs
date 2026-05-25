namespace StatsClient.MVVM.Model;

public class IssuesWithCasesModel(string level, string orderID, string skipReason, string foreColor, string createDate, string? iconSource)
{
    public string? Level { get; set; } = level;
    public string? OrderID { get; set; } = orderID;
    public string? SkipReason { get; set; } = skipReason;
    public string? ForeColor { get; set; } = foreColor;
    public string? CreateDate { get; set; } = createDate;
    public string? IconSource { get; set; } = iconSource;
}
