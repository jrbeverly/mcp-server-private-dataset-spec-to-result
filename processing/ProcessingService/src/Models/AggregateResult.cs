namespace ProcessingService.Models;

public sealed record AggregateResult
{
    public required string Dimension { get; init; }
    public required IReadOnlyList<AggregateBucket> Buckets { get; init; }
    public required int TotalEntityCount { get; init; }
    public required int TotalCategoryCount { get; init; }
}

public sealed record AggregateBucket
{
    public required string Key { get; init; }
    public string? Label { get; init; }
    public required int Count { get; init; }
    public double Percentage { get; init; }
    public IReadOnlyDictionary<string, MetricSummary>? MetricSummaries { get; init; }
}

public sealed record MetricSummary
{
    public decimal Min { get; init; }
    public decimal Max { get; init; }
    public decimal Avg { get; init; }
    public decimal Sum { get; init; }
}
