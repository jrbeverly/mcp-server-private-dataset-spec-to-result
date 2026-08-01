using ProcessingService.Models;

namespace ProcessingService.Pipeline;

public static class AggregationStep
{
    public static async Task<AggregateResult> AggregateAsync(
        IReadOnlyList<CanonicalEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var categories = entities
            .Where(e => e.Category is not null)
            .GroupBy(e => e.Category!)
            .OrderByDescending(g => g.Count())
            .ToList();

        var buckets = new List<AggregateBucket>(categories.Count);

        foreach (var cat in categories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var catEntities = cat.ToList();
            var metricNames = catEntities
                .SelectMany(e => e.Metrics.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var metricSummaries = new Dictionary<string, MetricSummary>(StringComparer.OrdinalIgnoreCase);
            foreach (var metricName in metricNames)
            {
                var values = catEntities
                    .Select(e => e.Metrics.TryGetValue(metricName, out var v) ? v : (decimal?)null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (values.Count > 0)
                {
                    metricSummaries[metricName] = new MetricSummary
                    {
                        Min = values.Min(),
                        Max = values.Max(),
                        Avg = Math.Round(values.Average(), 2),
                        Sum = values.Sum()
                    };
                }
            }

            var totalCategories = categories.Count;
            var bucket = new AggregateBucket
            {
                Key = cat.Key,
                Count = catEntities.Count,
                Percentage = totalCategories > 0
                    ? Math.Round((double)catEntities.Count / entities.Count * 100, 2)
                    : 0,
                MetricSummaries = metricSummaries.Count > 0 ? metricSummaries : null
            };

            buckets.Add(bucket);
        }

        // Top entity aggregation
        if (entities.Any(e => e.Metrics.Count > 0))
        {
            var topByMetrics = entities
                .Where(e => e.Metrics.Count > 0)
                .OrderByDescending(e => e.Metrics.Values.Sum())
                .Take(10)
                .Select(e => new AggregateBucket
                {
                    Key = e.EntityId,
                    Label = e.Name,
                    Count = e.Metrics.Count,
                    Percentage = 0
                })
                .ToList();

            if (topByMetrics.Count > 0)
            {
                buckets.Add(new AggregateBucket
                {
                    Key = "__top_by_metric_total__",
                    Label = "Top Entities by Total Metric Value",
                    Count = topByMetrics.Count,
                    Percentage = 0
                });
                buckets.AddRange(topByMetrics);
            }
        }

        await Task.CompletedTask;

        return new AggregateResult
        {
            Dimension = "category",
            Buckets = buckets,
            TotalEntityCount = entities.Count,
            TotalCategoryCount = categories.Count
        };
    }
}
