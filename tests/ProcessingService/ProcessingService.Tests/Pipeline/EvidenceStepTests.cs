using ProcessingService.Models;
using ProcessingService.Pipeline;
using Xunit;

namespace ProcessingService.Tests.Pipeline;

public class EvidenceStepTests
{
    [Fact]
    public async Task Generate_CreatesPropertiesEvidence()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "ent-001",
                Name = "Test Entity",
                Properties = new Dictionary<string, string> { ["industry"] = "SaaS", ["region"] = "US" },
                Metrics = new Dictionary<string, decimal>()
            }
        };

        var evidence = await EvidenceStep.GenerateAsync(entities);

        Assert.Single(evidence);
        Assert.Equal("ent-001-properties", evidence[0].RecordId);
        Assert.Equal("ent-001", evidence[0].EntityId);
        Assert.Equal("properties", evidence[0].Type);
        Assert.Equal("SaaS", evidence[0].Fields["industry"]);
        Assert.Equal("US", evidence[0].Fields["region"]);
    }

    [Fact]
    public async Task Generate_CreatesMetricsEvidence()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "ent-001",
                Name = "Test Entity",
                Properties = new Dictionary<string, string>(),
                Metrics = new Dictionary<string, decimal> { ["revenue"] = 100.5m, ["employees"] = 50 }
            }
        };

        var evidence = await EvidenceStep.GenerateAsync(entities);

        Assert.Single(evidence);
        Assert.Equal("ent-001-metrics", evidence[0].RecordId);
        Assert.Equal("ent-001", evidence[0].EntityId);
        Assert.Equal("metrics", evidence[0].Type);
        Assert.Equal("100.5", evidence[0].Fields["revenue"]);
        Assert.Equal("50", evidence[0].Fields["employees"]);
    }

    [Fact]
    public async Task Generate_CreatesBothPropertyAndMetricEvidence()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "ent-001",
                Name = "Test Entity",
                Properties = new Dictionary<string, string> { ["industry"] = "SaaS" },
                Metrics = new Dictionary<string, decimal> { ["revenue"] = 100m }
            }
        };

        var evidence = await EvidenceStep.GenerateAsync(entities);

        Assert.Equal(2, evidence.Count);
        Assert.Contains(evidence, e => e.Type == "properties");
        Assert.Contains(evidence, e => e.Type == "metrics");
    }

    [Fact]
    public async Task Generate_HandlesMultipleEntities()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "ent-001", Name = "A",
                Properties = new Dictionary<string, string> { ["x"] = "1" },
                Metrics = new Dictionary<string, decimal> { ["y"] = 1m }
            },
            new()
            {
                EntityId = "ent-002", Name = "B",
                Properties = new Dictionary<string, string> { ["x"] = "2" },
                Metrics = new Dictionary<string, decimal> { ["y"] = 2m }
            }
        };

        var evidence = await EvidenceStep.GenerateAsync(entities);

        Assert.Equal(4, evidence.Count);
    }

    [Fact]
    public async Task Generate_HandlesEmptyEntities()
    {
        var evidence = await EvidenceStep.GenerateAsync(Array.Empty<CanonicalEntity>());

        Assert.Empty(evidence);
    }

    [Fact]
    public async Task Generate_SkipsEntityWithNoPropertiesOrMetrics()
    {
        var entities = new List<CanonicalEntity>
        {
            new()
            {
                EntityId = "ent-001",
                Name = "Entity",
                Properties = new Dictionary<string, string>(),
                Metrics = new Dictionary<string, decimal>()
            }
        };

        var evidence = await EvidenceStep.GenerateAsync(entities);

        Assert.Empty(evidence);
    }
}
