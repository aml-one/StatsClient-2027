using StatsClient.KnowledgeBase.Core;

namespace StatsClient.KnowledgeBaseApi.Services;

public sealed class KnowledgeBaseApiKeyProvider
{
    private readonly IConfiguration _configuration;

    public KnowledgeBaseApiKeyProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string?> GetExpectedApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var configured = _configuration["KnowledgeBaseApiKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return await KnowledgeBaseSettingsReader.ReadAsync("KnowledgeBase_ApiKey", cancellationToken).ConfigureAwait(false);
    }
}
