namespace StatsClient.KnowledgeBase.Core;

public sealed class KnowledgeBaseCategory
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class KnowledgeBaseTag
{
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public sealed class KnowledgeBaseCardLink
{
    public int LinkId { get; set; }
    public int CardId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class KnowledgeBaseCardImage
{
    public int ImageId { get; set; }
    public int CardId { get; set; }
    public string FileName { get; set; } = "image.png";
    public string ContentType { get; set; } = "image/png";
    public byte[] ImageData { get; set; } = [];
    public byte[]? ThumbnailData { get; set; }
    public int SortOrder { get; set; }
}

public sealed class KnowledgeBaseCardSummary
{
    public int CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyPreview { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public byte[]? ThumbnailData { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];
}

public sealed class KnowledgeBaseCardDetail
{
    public int CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public string CreatedByMachine { get; set; } = string.Empty;
    public string ModifiedByMachine { get; set; } = string.Empty;
    public List<KnowledgeBaseCardLink> Links { get; set; } = [];
    public List<KnowledgeBaseCardImage> Images { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public sealed class KnowledgeBaseCardBackupInfo
{
    public int BackupId { get; set; }
    public int CardId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime BackedUpUtc { get; set; }
}

public sealed class KnowledgeBaseCardSnapshot
{
    public int CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<KnowledgeBaseCardLink> Links { get; set; } = [];
    public List<KnowledgeBaseCardImageSnapshot> Images { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public DateTime SnapshotUtc { get; set; }
}

public sealed class KnowledgeBaseCardImageSnapshot
{
    public int ImageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
    public string? ThumbnailBase64 { get; set; }
    public int SortOrder { get; set; }
}

public sealed class KnowledgeBaseSaveRequest
{
    public int CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public List<KnowledgeBaseCardLink> Links { get; set; } = [];
    public List<KnowledgeBaseCardImage> Images { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public bool IsAutoTagPass { get; set; } = true;
}

public sealed class KnowledgeBaseCardFilter
{
    public string? SearchText { get; set; }
    public int? CategoryId { get; set; }
    public IReadOnlyList<string> TagNames { get; set; } = [];
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

public sealed class KnowledgeBaseVisionMatch
{
    public int CardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}
