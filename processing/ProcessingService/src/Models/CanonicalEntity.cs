namespace ProcessingService.Models;

public sealed record CanonicalEntity
{
    public required string EntityId { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
    public Dictionary<string, decimal> Metrics { get; init; } = new();
}
