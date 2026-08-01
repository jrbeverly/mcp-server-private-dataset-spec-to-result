using ProcessingService.Models;
using ProcessingService.Pipeline;
using Xunit;

namespace ProcessingService.Tests.Pipeline;

public class EnrichmentStepTests
{
    [Fact]
    public async Task Enrich_AddsTotalMetric()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "e1",
                Name = "Test",
                Metrics = new Dictionary<string, decimal>
                {
                    ["revenue"] = 100,
                    ["costs"] = 40
                }
            }
        };

        var enriched = await EnrichmentStep.EnrichAsync(entities);

        var entity = enriched[0];
        Assert.True(entity.Metrics.ContainsKey("total"));
        Assert.Equal(140m, entity.Metrics["total"]);
    }

    [Fact]
    public async Task Enrich_AddsPropertyCount()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "e1",
                Name = "Test",
                Properties = new Dictionary<string, string>
                {
                    ["color"] = "red",
                    ["size"] = "large",
                    ["weight"] = "heavy"
                }
            }
        };

        var enriched = await EnrichmentStep.EnrichAsync(entities);

        Assert.Equal(3m, enriched[0].Metrics["property_count"]);
    }

    [Fact]
    public async Task Enrich_AddsEntityIdProperty()
    {
        var entities = new List<CanonicalEntity>
        {
            new() { EntityId = "abc123", Name = "Test" }
        };

        var enriched = await EnrichmentStep.EnrichAsync(entities);

        Assert.Equal("abc123", enriched[0].Properties["_entity_id"]);
    }

    [Fact]
    public async Task Enrich_PreservesInputCount()
    {
        var entities = Enumerable.Range(1, 5).Select(i => new CanonicalEntity
        {
            EntityId = $"e{i}",
            Name = $"Entity {i}"
        }).ToList();

        var enriched = await EnrichmentStep.EnrichAsync(entities);

        Assert.Equal(5, enriched.Count);
    }
}
