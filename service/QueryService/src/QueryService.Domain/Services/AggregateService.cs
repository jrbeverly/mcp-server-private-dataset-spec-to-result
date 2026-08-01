using System.Text.Json;
using QueryService.Domain.Data;
using QueryService.Domain.Exceptions;
using ServiceConfiguration;
using ServiceContract.V1;

namespace QueryService.Domain.Services;

public sealed class AggregateService : QueryServiceBase, IAggregateService
{
    private static readonly JsonSerializerOptions MetricJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public AggregateService(
        IServingStoreRepository repository,
        IDatasetVersionResolver versionResolver,
        DatasetVersionConfiguration versionConfig)
        : base(repository, versionResolver, versionConfig) { }

    public async Task<AggregateResponse> AggregateAsync(
        AggregateRequest request, CancellationToken cancellationToken = default)
    {
        var version = await ResolveVersionAsync(request.DatasetVersion, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.GroupBy)
            && !string.Equals(request.GroupBy, "category", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                $"Unsupported group-by dimension: '{request.GroupBy}'. Supported: category.");
        }

        var rows = await Repository.GetCategoryAggregatesAsync(version, cancellationToken);
        var buckets = rows.Select(r => MapToBucket(r, request.Metric)).ToList();

        if (!string.IsNullOrWhiteSpace(request.Filter))
            buckets = ApplyFilter(buckets, request.Filter);

        buckets = ApplySort(buckets, request.Sort);

        if (request.Limit > 0)
            buckets = buckets.Take(request.Limit.Value).ToList();

        return new AggregateResponse
        {
            DatasetVersion = version,
            Metric = request.Metric,
            Buckets = buckets
        };
    }

    private static AggregateBucket MapToBucket(CategoryAggregateRow row, string requestedMetric)
    {
        var value = (decimal)row.Percentage;
        decimal? min = null;
        decimal? max = null;
        decimal? sum = null;

        if (!string.IsNullOrWhiteSpace(row.MetricSummariesJson))
        {
            try
            {
                var summaries = JsonSerializer.Deserialize<
                    Dictionary<string, MetricSummaryJson>>(row.MetricSummariesJson, MetricJsonOptions);
                if (summaries is not null
                    && summaries.TryGetValue(requestedMetric, out var summary))
                {
                    value = summary.Avg;
                    min = summary.Min;
                    max = summary.Max;
                    sum = summary.Sum;
                }
            }
            catch (JsonException) { }
        }

        return new AggregateBucket
        {
            Key = row.Key,
            Label = row.Label ?? row.Key,
            Count = row.Count,
            Value = value,
            Min = min,
            Max = max,
            Sum = sum
        };
    }

    private static List<AggregateBucket> ApplyFilter(List<AggregateBucket> buckets, string filter)
    {
        return buckets
            .Where(b => b.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || (b.Label is not null
                            && b.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<AggregateBucket> ApplySort(List<AggregateBucket> buckets, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "value" or "value_asc" => buckets.OrderBy(b => b.Value).ToList(),
            "value_desc" => buckets.OrderByDescending(b => b.Value).ToList(),
            "count" or "count_asc" => buckets.OrderBy(b => b.Count ?? 0).ToList(),
            "count_desc" => buckets.OrderByDescending(b => b.Count ?? 0).ToList(),
            "key" or "key_asc" => buckets.OrderBy(b => b.Key).ToList(),
            "key_desc" => buckets.OrderByDescending(b => b.Key).ToList(),
            _ => buckets
        };
    }

    private sealed record MetricSummaryJson
    {
        public decimal Min { get; init; }
        public decimal Max { get; init; }
        public decimal Avg { get; init; }
        public decimal Sum { get; init; }
    }
}
