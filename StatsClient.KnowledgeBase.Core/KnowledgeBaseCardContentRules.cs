namespace StatsClient.KnowledgeBase.Core;

public static class KnowledgeBaseCardContentRules
{
    public static bool IsEmpty(
        string? title,
        string? bodyText,
        int? categoryId,
        IEnumerable<string>? tags,
        IEnumerable<KnowledgeBaseCardLink>? links,
        IEnumerable<KnowledgeBaseCardImage>? images)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            return false;
        }

        if (categoryId is > 0)
        {
            return false;
        }

        if (tags?.Any(t => !string.IsNullOrWhiteSpace(t)) == true)
        {
            return false;
        }

        if (images?.Any() == true)
        {
            return false;
        }

        if (links?.Any(l => !string.IsNullOrWhiteSpace(l.Label) || !string.IsNullOrWhiteSpace(l.Url)) == true)
        {
            return false;
        }

        return true;
    }

    public static bool IsEmpty(KnowledgeBaseSaveRequest request) =>
        IsEmpty(
            request.Title,
            request.BodyText,
            request.CategoryId,
            request.Tags,
            request.Links,
            request.Images);

    public static bool IsEmpty(KnowledgeBaseCardDetail detail) =>
        IsEmpty(
            detail.Title,
            detail.BodyText,
            detail.CategoryId,
            detail.Tags,
            detail.Links,
            detail.Images);
}
