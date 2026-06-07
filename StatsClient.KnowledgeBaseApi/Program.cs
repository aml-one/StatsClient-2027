using StatsClient.KnowledgeBase.Core;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("StatsDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:StatsDb must be configured in appsettings.json.");
}

KnowledgeBaseDatabase.ConnectionStringFactory = () => connectionString;

var apiKey = builder.Configuration["KnowledgeBaseApiKey"] ?? string.Empty;

builder.Services.AddSingleton(new KnowledgeBaseRepository());
builder.Services.AddSingleton(new KnowledgeBaseBackupRepository());

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid API key.");
            return;
        }
    }

    await next();
});

app.MapGet("/api/kb/tags", async (KnowledgeBaseRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.GetTagsAsync(ct)));

app.MapGet("/api/kb/cards", async (
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

    return Results.Ok(await repo.ListCardsAsync(filter, ct));
});

app.MapGet("/api/kb/cards/{cardId:int}", async (int cardId, KnowledgeBaseRepository repo, CancellationToken ct) =>
{
    var detail = await repo.GetCardDetailAsync(cardId, ct);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
});

app.MapGet("/api/kb/cards/{cardId:int}/images/{imageId:int}", async (
    int cardId,
    int imageId,
    KnowledgeBaseRepository repo,
    bool full,
    CancellationToken ct) =>
{
    var image = await repo.GetImageAsync(imageId, full, ct);
    if (image is null || image.CardId != cardId)
    {
        return Results.NotFound();
    }

    return Results.File(image.ImageData, image.ContentType, image.FileName);
});

app.MapPost("/api/kb/cards", async (KnowledgeBaseSaveRequest request, KnowledgeBaseRepository repo, CancellationToken ct) =>
{
    if (request.CardId <= 0)
    {
        request.CardId = await repo.CreateCardAsync(request.MachineName, ct);
    }

    await repo.SaveCardAsync(request, ct);
    return Results.Ok(new { request.CardId });
});

app.MapPost("/api/kb/search-by-image", async (
    HttpRequest request,
    KnowledgeBaseRepository repo,
    CancellationToken ct) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart form with image field.");
    }

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("image");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("Missing image file.");
    }

    await using var stream = file.OpenReadStream();
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms, ct);
    var bytes = ms.ToArray();

    var cardIds = form.TryGetValue("cardIds", out var raw) && !string.IsNullOrWhiteSpace(raw)
        ? raw.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList()
        : [];

    var service = new StatsClient.KnowledgeBaseApi.Services.KnowledgeBaseVisionSearchService(repo);
    var apiKey = form.TryGetValue("nvidiaApiKey", out var key) ? key.ToString() : null;
    var matches = await service.SearchAsync(bytes, cardIds, () => apiKey, ct);
    return Results.Ok(matches);
});

app.Run();
