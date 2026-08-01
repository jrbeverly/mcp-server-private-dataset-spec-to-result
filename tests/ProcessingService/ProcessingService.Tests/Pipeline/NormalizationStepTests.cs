using ProcessingService.Pipeline;
using Xunit;

namespace ProcessingService.Tests.Pipeline;

public class NormalizationStepTests
{
    [Fact]
    public async Task Normalize_SampleCsv_ReturnsEntities()
    {
        var csvPath = Path.Combine("TestData", "sample.csv");

        var entities = await NormalizationStep.NormalizeAsync(csvPath);

        Assert.NotEmpty(entities);
        Assert.Equal(10, entities.Count);
    }

    [Fact]
    public async Task Normalize_SampleCsv_MapsEntityId()
    {
        var csvPath = Path.Combine("TestData", "sample.csv");

        var entities = await NormalizationStep.NormalizeAsync(csvPath);

        Assert.Contains(entities, e => e.EntityId == "entity_001");
        Assert.Contains(entities, e => e.Name == "Acme Corp");
    }

    [Fact]
    public async Task Normalize_SampleCsv_MapsCategory()
    {
        var csvPath = Path.Combine("TestData", "sample.csv");

        var entities = await NormalizationStep.NormalizeAsync(csvPath);

        var techEntities = entities.Where(e => e.Category == "Technology").ToList();
        Assert.Equal(5, techEntities.Count);
    }

    [Fact]
    public async Task Normalize_SampleCsv_ExtractsMetrics()
    {
        var csvPath = Path.Combine("TestData", "sample.csv");

        var entities = await NormalizationStep.NormalizeAsync(csvPath);

        var acme = entities.First(e => e.EntityId == "entity_001");
        Assert.True(acme.Metrics.ContainsKey("revenue"));
        Assert.Equal(5000000m, acme.Metrics["revenue"]);
        Assert.True(acme.Metrics.ContainsKey("employees"));
        Assert.Equal(250m, acme.Metrics["employees"]);
        Assert.True(acme.Metrics.ContainsKey("growth_rate"));
        Assert.Equal(0.15m, acme.Metrics["growth_rate"]);
    }

    [Fact]
    public async Task Normalize_SampleCsv_ExtractsDescription()
    {
        var csvPath = Path.Combine("TestData", "sample.csv");

        var entities = await NormalizationStep.NormalizeAsync(csvPath);

        var acme = entities.First(e => e.EntityId == "entity_001");
        Assert.Equal("Leading tech company", acme.Description);
    }

    [Fact]
    public async Task Normalize_SampleCsv_FromDirectory()
    {
        var dir = Path.Combine("TestData");

        var entities = await NormalizationStep.NormalizeAsync(dir);

        Assert.Equal(10, entities.Count);
    }

    [Fact]
    public async Task Normalize_EmptyDirectory_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NormalizationStep.NormalizeAsync(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
