namespace ServiceContract.V1;

public sealed record AggregateRequest
{
    public string? DatasetVersion { get; init; }
    public required string Metric { get; init; }
    public string? GroupBy { get; init; }
    public string? Filter { get; init; }
    public string? Sort { get; init; }
    public int? Limit { get; init; }
}

public sealed record AggregateResponse
{
    public required IReadOnlyList<AggregateBucket> Buckets { get; init; }
    public required string Metric { get; init; }
    public required string DatasetVersion { get; init; }
}

public sealed record AggregateBucket
{
    public required string Key { get; init; }
    public required decimal Value { get; init; }
    public string? Label { get; init; }
    public int? Count { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public decimal? Sum { get; init; }
}
