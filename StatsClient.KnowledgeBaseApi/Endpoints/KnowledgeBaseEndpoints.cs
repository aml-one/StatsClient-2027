using StatsClient.KnowledgeBase.Core;
using StatsClient.KnowledgeBaseApi.Models;
using StatsClient.KnowledgeBaseApi.Services;

namespace StatsClient.KnowledgeBaseApi.Endpoints;

public static class KnowledgeBaseEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeBaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kb");

        group.MapGet("/status", async (KnowledgeBaseRepository repo, CancellationToken ct) =>
        {
            bool available = await KnowledgeBaseSchemaProbe.IsAvailableAsync(ct).ConfigureAwait(false);
            return Results.Ok(new KnowledgeBaseStatusDto { SchemaAvailable = available });
        });

        group.MapGet("/tags", async (KnowledgeBaseRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetTagsAsync(ct).ConfigureAwait(false)));

        group.MapGet("/categories", async (KnowledgeBaseRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetCategoriesAsync(ct).ConfigureAwait(false)));

        group.MapGet("/cards", async (
            KnowledgeBaseRepository repo,
            string? search,
            int? categoryId,
            string? tags,
            int skip,
            int take,
            CancellationToken ct) =>
        {
            var filter = new KnowledgeBaseCardFilter
            {
                SearchText = search,
                CategoryId = categoryId,
                TagNames = string.IsNullOrWhiteSpace(tags)
                    ? []
                    : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Skip = skip,
                Take = take <= 0 ? 100 : take
            };

            var cards = await repo.ListCardsAsync(filter, ct).ConfigureAwait(false);
            return Results.Ok(cards.Select(KnowledgeBaseCardSummaryDto.FromModel));
        });

        group.MapGet("/cards/{cardId:int}", async (int cardId, KnowledgeBaseRepository repo, CancellationToken ct) =>
        {
            var detail = await repo.GetCardDetailAsync(cardId, includeFullImageData: false, ct).ConfigureAwait(false);
            return detail is null ? Results.NotFound() : Results.Ok(KnowledgeBaseCardDetailDto.FromModel(detail));
        });

        group.MapPost("/cards", async (
            KnowledgeBaseSaveRequest request,
            KnowledgeBaseCardSaveService saveService,
            CancellationToken ct) =>
        {
            request.CardId = 0;
            var result = await saveService.SaveAsync(request, ct).ConfigureAwait(false);
            return Results.Created($"/api/kb/cards/{result.CardId}", result);
        });

        group.MapPut("/cards/{cardId:int}", async (
            int cardId,
            KnowledgeBaseSaveRequest request,
            KnowledgeBaseCardSaveService saveService,
            CancellationToken ct) =>
        {
            request.CardId = cardId;
            var result = await saveService.SaveAsync(request, ct).ConfigureAwait(false);
            return Results.Ok(result);
        });

        group.MapDelete("/cards/{cardId:int}", async (
            int cardId,
            string? machineName,
            KnowledgeBaseRepository repo,
            CancellationToken ct) =>
        {
            var existing = await repo.GetCardDetailAsync(cardId, includeFullImageData: false, ct).ConfigureAwait(false);
            if (existing is null)
            {
                return Results.NotFound();
            }

            await repo.SoftDeleteCardAsync(
                cardId,
                string.IsNullOrWhiteSpace(machineName) ? "api" : machineName.Trim(),
                ct).ConfigureAwait(false);

            return Results.NoContent();
        });

        group.MapGet("/cards/{cardId:int}/images/{imageId:int}", async (
            int cardId,
            int imageId,
            KnowledgeBaseRepository repo,
            bool full,
            string? format,
            CancellationToken ct) =>
        {
            var image = await repo.GetImageAsync(imageId, full, ct).ConfigureAwait(false);
            if (image is null || image.CardId != cardId)
            {
                return Results.NotFound();
            }

            if (string.Equals(format, "base64", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new KnowledgeBaseImagePayloadDto
                {
                    ImageId = image.ImageId,
                    CardId = image.CardId,
                    FileName = image.FileName,
                    ContentType = image.ContentType,
                    DataBase64 = image.ImageData.Length > 0 ? Convert.ToBase64String(image.ImageData) : null
                });
            }

            return Results.File(image.ImageData, image.ContentType, image.FileName);
        });

        group.MapPost("/cards/{cardId:int}/images", async (
            int cardId,
            HttpRequest request,
            KnowledgeBaseRepository repo,
            KnowledgeBaseCardSaveService saveService,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Expected multipart form with image field.");
            }

            var form = await request.ReadFormAsync(ct).ConfigureAwait(false);
            var file = form.Files.GetFile("image");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("Missing image file.");
            }

            if (file.Length > KnowledgeBaseRepository.MaxImageBytes)
            {
                return Results.BadRequest($"Image exceeds {KnowledgeBaseRepository.MaxImageBytes} bytes.");
            }

            var detail = await repo.GetCardDetailAsync(cardId, includeFullImageData: true, ct).ConfigureAwait(false);
            if (detail is null)
            {
                return Results.NotFound();
            }

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            var bytes = ms.ToArray();

            detail.Images.Add(new KnowledgeBaseCardImage
            {
                CardId = cardId,
                FileName = string.IsNullOrWhiteSpace(file.FileName) ? "image.png" : file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                ImageData = bytes,
                ThumbnailData = bytes.Length <= 120_000 ? bytes : null,
                SortOrder = detail.Images.Count
            });

            var saveRequest = new KnowledgeBaseSaveRequest
            {
                CardId = cardId,
                Title = detail.Title,
                BodyText = detail.BodyText,
                CategoryId = detail.CategoryId,
                CategoryName = detail.CategoryName,
                MachineName = form.TryGetValue("machineName", out var machine) && !string.IsNullOrWhiteSpace(machine)
                    ? machine.ToString()
                    : "api",
                Links = detail.Links,
                Images = detail.Images,
                Tags = detail.Tags,
                IsAutoTagPass = false
            };

            await saveService.SaveAsync(saveRequest, ct).ConfigureAwait(false);
            var updated = await repo.GetCardDetailAsync(cardId, includeFullImageData: false, ct).ConfigureAwait(false);
            var uploaded = updated?.Images
                .OrderByDescending(i => i.SortOrder)
                .ThenByDescending(i => i.ImageId)
                .FirstOrDefault();
            return Results.Created(
                $"/api/kb/cards/{cardId}/images/{uploaded?.ImageId}",
                new { imageId = uploaded?.ImageId ?? 0 });
        });

        group.MapPost("/search-by-image", async (
            HttpRequest request,
            KnowledgeBaseRepository repo,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Expected multipart form with image field.");
            }

            var form = await request.ReadFormAsync(ct).ConfigureAwait(false);
            var file = form.Files.GetFile("image");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("Missing image file.");
            }

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            var bytes = ms.ToArray();

            var cardIds = form.TryGetValue("cardIds", out var raw) && !string.IsNullOrWhiteSpace(raw)
                ? raw.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList()
                : [];

            var service = new KnowledgeBaseVisionSearchService(repo);
            string? apiKey = form.TryGetValue("nvidiaApiKey", out var key) && !string.IsNullOrWhiteSpace(key)
                ? key.ToString()
                : await KnowledgeBaseSettingsReader.ReadAsync("Nvidia_API_KEY", ct).ConfigureAwait(false);

            var matches = await service.SearchAsync(bytes, cardIds, () => apiKey, ct).ConfigureAwait(false);
            return Results.Ok(matches);
        });

        return app;
    }
}
