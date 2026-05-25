namespace StatsClient.MVVM.Model;

public class StatsDBSettingsModel
{
    public string? LastDBUpdate { get; set; }
    public string? StatsServerStatus { get; set; }
    public string? LastServerPing { get; set; }
    public bool AutoSendActive { get; set; }
    public bool AutoSend0 { get; set; }
    public bool AutoSend15 { get; set; }
    public bool AutoSend30 { get; set; }
    public bool AutoSend45 { get; set; }
    public bool ServerIsWritingDatabase { get; set; } = false;

    public string? ExportFolderAnteriors { get; set; }
    public string? ExportFolderPosteriors { get; set; }
    public string? DesignerNameAnteriors { get; set; }
    public string? DesignerNamePosteriors { get; set; }
    public string? SiteID { get; set; }
    public string? SelectedServer { get; set; }
}
