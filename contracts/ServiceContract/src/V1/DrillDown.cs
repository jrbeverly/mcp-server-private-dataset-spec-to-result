namespace ServiceContract.V1;

public sealed record DrillDownRequest
{
    public string? DatasetVersion { get; init; }
    public required string EntityId { get; init; }
}

public sealed record DrillDownResponse
{
    public required EntityDetail Entity { get; init; }
    public required string DatasetVersion { get; init; }
    public IReadOnlyList<RelatedEntity>? RelatedEntities { get; init; }
}

public sealed record EntityDetail
{
    public required string EntityId { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
    public IReadOnlyDictionary<string, decimal>? Metrics { get; init; }
}

public sealed record RelatedEntity
{
    public required string EntityId { get; init; }
    public required string Name { get; init; }
    public required string RelationshipType { get; init; }
    public string? Category { get; init; }
}
