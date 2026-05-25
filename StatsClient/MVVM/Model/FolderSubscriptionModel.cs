namespace StatsClient.MVVM.Model;

public class FolderSubscriptionModel
{
    public string? FolderName { get; set; }
    public string? LastModified { get; set; }
    public string? Path { get; set; }
    public string? Age { get; set; }
    public string? AgeForSorting { get; set; }
    public int AgeForColoring { get; set; }
}
