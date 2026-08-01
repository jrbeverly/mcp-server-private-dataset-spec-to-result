namespace ServiceContract.V1;

public sealed record MetadataExplorationRequest
{
    public string? DatasetVersion { get; init; }
    public string? Category { get; init; }
}

public sealed record MetadataExplorationResponse
{
    public required IReadOnlyList<Dimension> Dimensions { get; init; }
    public required string DatasetVersion { get; init; }
    public required int TotalEntityCount { get; init; }
    public required int TotalCategoryCount { get; init; }
    public IReadOnlyList<string>? AvailableMetrics { get; init; }
    public IReadOnlyList<string>? AvailableProperties { get; init; }
}

public sealed record Dimension
{
    public required string Name { get; init; }
    public required IReadOnlyList<DimensionValue> Values { get; init; }
}

public sealed record DimensionValue
{
    public required string Value { get; init; }
    public string? Label { get; init; }
    public required int EntityCount { get; init; }
}
