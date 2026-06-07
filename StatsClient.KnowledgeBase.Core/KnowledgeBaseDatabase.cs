namespace StatsClient.KnowledgeBase.Core;

public static class KnowledgeBaseDatabase
{
    public static Func<string> ConnectionStringFactory { get; set; } =
        () => throw new InvalidOperationException("KnowledgeBaseDatabase.ConnectionStringFactory is not configured.");
}
