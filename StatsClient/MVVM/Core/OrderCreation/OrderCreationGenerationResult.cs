namespace StatsClient.MVVM.Core.OrderCreation;

public sealed class OrderCreationGenerationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? OrderFolderPath { get; init; }
    public string? OrderXmlPath { get; init; }
    public string? MaterialsXmlPath { get; init; }
    public string? Manufacturers3mlPath { get; init; }
}
