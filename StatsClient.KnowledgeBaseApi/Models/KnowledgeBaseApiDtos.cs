using StatsClient.KnowledgeBase.Core;

namespace StatsClient.KnowledgeBaseApi.Models;

public sealed class KnowledgeBaseCardSummaryDto
{
    public int CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string BodyPreview { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public string? ThumbnailBase64 { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    public static KnowledgeBaseCardSummaryDto FromModel(KnowledgeBaseCardSummary summary) =>
        new()
        {
            CardId = summary.CardId,
            Title = summary.Title,
            BodyPreview = summary.BodyPreview,
            CategoryId = summary.CategoryId,
            CategoryName = summary.CategoryName,
            ModifiedUtc = summary.ModifiedUtc,
            ThumbnailBase64 = summary.ThumbnailData is { Length: > 0 } bytes ? Convert.ToBase64String(bytes) : null,
            Tags = summary.Tags
        };
}

public sealed class KnowledgeBaseCardImageMetaDto
{
    public int ImageId { get; init; }
    public int CardId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "image/png";
    public int SortOrder { get; init; }
    public string? ThumbnailBase64 { get; init; }

    public static KnowledgeBaseCardImageMetaDto FromModel(KnowledgeBaseCardImage image) =>
        new()
        {
            ImageId = image.ImageId,
            CardId = image.CardId,
            FileName = image.FileName,
            ContentType = image.ContentType,
            SortOrder = image.SortOrder,
            ThumbnailBase64 = image.ThumbnailData is { Length: > 0 } bytes ? Convert.ToBase64String(bytes) : null
        };
}

public sealed class KnowledgeBaseCardDetailDto
{
    public int CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public string CreatedByMachine { get; init; } = string.Empty;
    public string ModifiedByMachine { get; init; } = string.Empty;
    public IReadOnlyList<KnowledgeBaseCardLink> Links { get; init; } = [];
    public IReadOnlyList<KnowledgeBaseCardImageMetaDto> Images { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];

    public static KnowledgeBaseCardDetailDto FromModel(KnowledgeBaseCardDetail detail) =>
        new()
        {
            CardId = detail.CardId,
            Title = detail.Title,
            BodyText = detail.BodyText,
            CategoryId = detail.CategoryId,
            CategoryName = detail.CategoryName,
            CreatedUtc = detail.CreatedUtc,
            ModifiedUtc = detail.ModifiedUtc,
            CreatedByMachine = detail.CreatedByMachine,
            ModifiedByMachine = detail.ModifiedByMachine,
            Links = detail.Links,
            Images = detail.Images.Select(KnowledgeBaseCardImageMetaDto.FromModel).ToList(),
            Tags = detail.Tags
        };
}

public sealed class KnowledgeBaseSaveResponseDto
{
    public int CardId { get; init; }
    public bool Created { get; init; }
    public IReadOnlyList<string> AutoTags { get; init; } = [];
}

public sealed class KnowledgeBaseImagePayloadDto
{
    public int ImageId { get; init; }
    public int CardId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "image/png";
    public string? DataBase64 { get; init; }
}

public sealed class KnowledgeBaseStatusDto
{
    public bool SchemaAvailable { get; init; }
    public string Version { get; init; } = "1.0";
}
