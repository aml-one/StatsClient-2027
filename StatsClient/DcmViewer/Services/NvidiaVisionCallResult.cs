namespace DCMViewer.Services;

/// <summary>HTTP + payload summary for Encode Identifier vision debugging.</summary>
public sealed class NvidiaVisionCallResult
{
    public string Content { get; init; } = string.Empty;
    public int HttpStatusCode { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public int ImageBytes { get; init; }
    public int PromptChars { get; init; }
    public string ResponseBodyPreview { get; init; } = string.Empty;
    public string ErrorSummary { get; init; } = string.Empty;

    public bool IsSuccess =>
        HttpStatusCode is >= 200 and < 300 && !string.IsNullOrWhiteSpace(Content);

    public string ToDebugLog()
    {
        var lines = new List<string>
        {
            $"Endpoint: {Endpoint}",
            $"HTTP status: {HttpStatusCode}",
            $"Image bytes: {ImageBytes:N0}",
            $"Prompt chars: {PromptChars:N0}",
        };

        if (!string.IsNullOrWhiteSpace(ErrorSummary))
        {
            lines.Add($"Error: {ErrorSummary}");
        }

        if (!string.IsNullOrWhiteSpace(ResponseBodyPreview))
        {
            lines.Add("Response body (preview):");
            lines.Add(ResponseBodyPreview);
        }

        if (!string.IsNullOrWhiteSpace(Content))
        {
            lines.Add("Parsed content:");
            lines.Add(Content);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
