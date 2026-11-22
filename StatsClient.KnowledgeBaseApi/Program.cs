using StatsClient.KnowledgeBase.Core;
using StatsClient.KnowledgeBaseApi.Endpoints;
using StatsClient.KnowledgeBaseApi.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("StatsDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:StatsDb must be configured in appsettings.json.");
}

KnowledgeBaseDatabase.ConnectionStringFactory = () => connectionString;

builder.Services.AddSingleton<KnowledgeBaseRepository>();
builder.Services.AddSingleton<KnowledgeBaseBackupRepository>();
builder.Services.AddSingleton<KnowledgeBaseKeywordExtractor>();
builder.Services.AddSingleton<KnowledgeBaseApiKeyProvider>();
builder.Services.AddSingleton<KnowledgeBaseCardSaveService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var keyProvider = context.RequestServices.GetRequiredService<KnowledgeBaseApiKeyProvider>();
    var expectedKey = await keyProvider.GetExpectedApiKeyAsync(context.RequestAborted).ConfigureAwait(false);
    if (!string.IsNullOrWhiteSpace(expectedKey))
    {
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != expectedKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or missing X-Api-Key header.").ConfigureAwait(false);
            return;
        }
    }

    try
    {
        await next(context).ConfigureAwait(false);
    }
    catch (KnowledgeBaseApiException ex)
    {
        context.Response.StatusCode = ex.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message }).ConfigureAwait(false);
    }
});

app.MapGet("/", () => Results.Redirect("/api/kb/status"));
app.MapKnowledgeBaseEndpoints();

app.Run();
