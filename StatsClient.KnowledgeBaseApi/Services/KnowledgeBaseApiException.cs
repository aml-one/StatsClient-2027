namespace StatsClient.KnowledgeBaseApi.Services;

public sealed class KnowledgeBaseApiException : Exception
{
    public KnowledgeBaseApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
