namespace StatsClient.MVVM.Model;

public class UsefulWebsiteModel
{
    public int WebsiteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string TagsDisplay => string.Join(", ", Tags);
    public int SortOrder { get; set; }
    public DateTime ModifiedUtc { get; set; }
}
