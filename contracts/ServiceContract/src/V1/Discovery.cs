namespace ServiceContract.V1;

public sealed record DiscoveryRequest
{
    public string? DatasetVersion { get; init; }
    public string? Query { get; init; }
    public string? Category { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; init; }
}

public sealed record DiscoveryResponse
{
    public required IReadOnlyList<DiscoveredEntity> Entities { get; init; }
    public required string DatasetVersion { get; init; }
    public required int TotalMatches { get; init; }
}

public sealed record DiscoveredEntity
{
    public required string EntityId { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public decimal? RelevanceScore { get; init; }
    public IReadOnlyDictionary<string, string>? Highlights { get; init; }
}
