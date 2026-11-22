namespace StatsClient.MVVM.Core;

public static class WatchListLabels
{
    public static string DescribeProcessStatus(string? statusId) => statusId switch
    {
        "psCreated" => "Created",
        "psScanned" => "Scanned",
        "psModelled" => "Designed",
        "psSent" => "Sent",
        _ => statusId ?? "Unknown"
    };

    public static string DescribeProcessLock(string? lockId) => lockId switch
    {
        "plReady" => "Ready",
        "plCheckedOut" => "Checked out",
        _ => lockId ?? "Unknown"
    };

    public static string AccentColorForStatus(string? statusId) => statusId switch
    {
        "psCreated" => "#7DD3FC",
        "psScanned" => "#38BDF8",
        "psModelled" => "#5EEAD4",
        "psSent" => "#A78BFA",
        _ => "#38BDF8"
    };

    public static string AccentColorForLock(string? lockId) => lockId switch
    {
        "plReady" => "#67E8F9",
        "plCheckedOut" => "#FBBF24",
        _ => "#38BDF8"
    };

    public static string AccentColorForRenamed() => "#FB923C";

    public static string AccentColorForRushDesigned() => "#F87171";

    public static string TitleForNewStatus(string? statusId) => statusId switch
    {
        "psCreated" => "Got Created",
        "psScanned" => "Got Scanned",
        "psModelled" => "Got Designed",
        "psSent" => "Got Sent",
        _ => "Status updated"
    };

    public static string TitleForNewLock(string? lockId) => lockId switch
    {
        "plCheckedOut" => "Got Checked out",
        "plReady" => "Got Ready",
        _ => "Lock status changed"
    };

    public static string AccentColorForStatusChange(string? newStatusId, string? orderComments)
    {
        if (string.Equals(newStatusId, "psModelled", StringComparison.OrdinalIgnoreCase)
            && OrderCommentKeywords.ContainsRushKeyword(orderComments))
        {
            return AccentColorForRushDesigned();
        }

        return AccentColorForStatus(newStatusId);
    }
}
