using StatsClient.KnowledgeBase.Core;
using StatsClient.KnowledgeBaseApi.Models;

namespace StatsClient.KnowledgeBaseApi.Services;

public sealed class KnowledgeBaseCardSaveService
{
    private readonly KnowledgeBaseRepository _repository;
    private readonly KnowledgeBaseKeywordExtractor _keywordExtractor;

    public KnowledgeBaseCardSaveService(
        KnowledgeBaseRepository repository,
        KnowledgeBaseKeywordExtractor keywordExtractor)
    {
        _repository = repository;
        _keywordExtractor = keywordExtractor;
    }

    public async Task<KnowledgeBaseSaveResponseDto> SaveAsync(
        KnowledgeBaseSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        request.MachineName = string.IsNullOrWhiteSpace(request.MachineName)
            ? Environment.MachineName
            : request.MachineName.Trim();

        await ResolveCategoryAsync(request, cancellationToken).ConfigureAwait(false);

        if (KnowledgeBaseCardContentRules.IsEmpty(request))
        {
            throw new KnowledgeBaseApiException("Card has no content to save.", StatusCodes.Status400BadRequest);
        }

        bool created = false;
        if (request.CardId <= 0)
        {
            request.CardId = await _repository.CreateCardAsync(request.MachineName, cancellationToken).ConfigureAwait(false);
            created = true;
        }
        else
        {
            var existing = await _repository.GetCardDetailAsync(request.CardId, includeFullImageData: false, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                throw new KnowledgeBaseApiException($"Card {request.CardId} was not found.", StatusCodes.Status404NotFound);
            }
        }

        await _repository.SaveCardAsync(request, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> autoTags = [];
        if (request.IsAutoTagPass)
        {
            var nvidiaKey = await KnowledgeBaseSettingsReader.ReadAsync("Nvidia_API_KEY", cancellationToken).ConfigureAwait(false);
            autoTags = await _keywordExtractor.ExtractTagsAsync(
                request.Title,
                request.BodyText,
                request.Links.Select(l => l.Label),
                () => nvidiaKey,
                cancellationToken).ConfigureAwait(false);

            if (autoTags.Count > 0)
            {
                await _repository.MergeAutoTagsAsync(request.CardId, autoTags, cancellationToken).ConfigureAwait(false);
            }
        }

        return new KnowledgeBaseSaveResponseDto
        {
            CardId = request.CardId,
            Created = created,
            AutoTags = autoTags
        };
    }

    private async Task ResolveCategoryAsync(KnowledgeBaseSaveRequest request, CancellationToken cancellationToken)
    {
        if (request.CategoryId is > 0)
        {
            return;
        }

        var name = request.CategoryName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            request.CategoryId = null;
            return;
        }

        request.CategoryId = await _repository.EnsureCategoryAsync(name, cancellationToken).ConfigureAwait(false);
    }
}
