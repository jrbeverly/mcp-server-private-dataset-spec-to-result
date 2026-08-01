using ProcessingService.Models;
using ProcessingService.Pipeline;
using Xunit;

namespace ProcessingService.Tests.Pipeline;

public class AggregationStepTests
{
    [Fact]
    public async Task Aggregate_ComputesCategoryDistribution()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "A", Category = "Tech", Metrics = new Dictionary<string, decimal> { ["revenue"] = 100 } },
            new() { EntityId = "e2", Name = "B", Category = "Tech", Metrics = new Dictionary<string, decimal> { ["revenue"] = 200 } },
            new() { EntityId = "e3", Name = "C", Category = "Finance", Metrics = new Dictionary<string, decimal> { ["revenue"] = 300 } }
        };

        var result = await AggregationStep.AggregateAsync(entities);

        Assert.Equal(3, result.TotalEntityCount);
        Assert.Equal(2, result.TotalCategoryCount);
        Assert.Contains(result.Buckets, b => b.Key == "Tech" && b.Count == 2);
        Assert.Contains(result.Buckets, b => b.Key == "Finance" && b.Count == 1);
    }

    [Fact]
    public async Task Aggregate_ComputesMetricSummaries()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "A", Category = "Tech", Metrics = new Dictionary<string, decimal> { ["revenue"] = 100 } },
            new() { EntityId = "e2", Name = "B", Category = "Tech", Metrics = new Dictionary<string, decimal> { ["revenue"] = 200 } }
        };

        var result = await AggregationStep.AggregateAsync(entities);

        var techBucket = result.Buckets.First(b => b.Key == "Tech");
        Assert.NotNull(techBucket.MetricSummaries);
        Assert.True(techBucket.MetricSummaries!.ContainsKey("revenue"));
        Assert.Equal(100m, techBucket.MetricSummaries["revenue"].Min);
        Assert.Equal(200m, techBucket.MetricSummaries["revenue"].Max);
        Assert.Equal(150m, techBucket.MetricSummaries["revenue"].Avg);
        Assert.Equal(300m, techBucket.MetricSummaries["revenue"].Sum);
    }

    [Fact]
    public async Task Aggregate_EntitiesWithoutCategory_Excluded()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "A", Category = "Tech" },
            new() { EntityId = "e2", Name = "B", Category = null }
        };

        var result = await AggregationStep.AggregateAsync(entities);

        Assert.Equal(2, result.TotalEntityCount);
        Assert.Equal(1, result.TotalCategoryCount);
    }

    [Fact]
    public async Task Aggregate_PercentageSumIs100()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "e1", Name = "A", Category = "Tech" },
            new() { EntityId = "e2", Name = "B", Category = "Tech" },
            new() { EntityId = "e3", Name = "C", Category = "Finance" },
            new() { EntityId = "e4", Name = "D", Category = "Finance" }
        };

        var result = await AggregationStep.AggregateAsync(entities);

        var pctSum = result.Buckets
            .Where(b => !b.Key.StartsWith("__"))
            .Sum(b => b.Percentage);
        Assert.Equal(100.0, pctSum);
    }
}
