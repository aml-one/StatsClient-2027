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
}
