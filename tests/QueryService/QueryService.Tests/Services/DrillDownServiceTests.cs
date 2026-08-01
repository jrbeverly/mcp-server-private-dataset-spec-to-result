using Moq;
using QueryService.Domain.Data;
using QueryService.Domain.Exceptions;
using QueryService.Domain.Services;
using ServiceConfiguration;
using ServiceContract.V1;
using Xunit;

namespace QueryService.Tests.Services;

public class DrillDownServiceTests
{
    private readonly Mock<IServingStoreRepository> _repoMock;
    private readonly Mock<IDatasetVersionResolver> _versionResolverMock;
    private readonly DatasetVersionConfiguration _versionConfig;
    private readonly DrillDownService _service;

    public DrillDownServiceTests()
    {
        _repoMock = new Mock<IServingStoreRepository>();
        _versionResolverMock = new Mock<IDatasetVersionResolver>();
        _versionConfig = new DatasetVersionConfiguration { AllowVersionPinning = true };
        _service = new DrillDownService(_repoMock.Object, _versionResolverMock.Object, _versionConfig);
    }

    [Fact]
    public async Task DrillDown_ReturnsEntity_WhenFound()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEntityByIdAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServingEntityRow
            {
                EntityId = "e1", Name = "Entity 1", Category = "cat-a",
                PropertiesJson = """{"country":"US"}""",
                MetricsJson = """{"revenue":100}"""
            });
        _repoMock.Setup(r => r.GetRelatedEntitiesAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEntityRow>());

        var response = await _service.DrillDownAsync(new DrillDownRequest { EntityId = "e1" });

        Assert.Equal("v1", response.DatasetVersion);
        Assert.Equal("e1", response.Entity.EntityId);
        Assert.Equal("Entity 1", response.Entity.Name);
        Assert.NotNull(response.Entity.Properties);
        Assert.Equal("US", response.Entity.Properties!["country"]);
    }

    [Fact]
    public async Task DrillDown_ReturnsRelatedEntities()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEntityByIdAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServingEntityRow
            {
                EntityId = "e1", Name = "Entity 1", Category = "cat-a",
                PropertiesJson = "{}", MetricsJson = "{}"
            });
        _repoMock.Setup(r => r.GetRelatedEntitiesAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServingEntityRow>
            {
                new() { EntityId = "e2", Name = "Entity 2", Category = "cat-a", PropertiesJson = "{}", MetricsJson = "{}" }
            });

        var response = await _service.DrillDownAsync(new DrillDownRequest { EntityId = "e1" });

        Assert.Single(response.RelatedEntities!);
        Assert.Equal("e2", response.RelatedEntities![0].EntityId);
    }

    [Fact]
    public async Task DrillDown_Throws_WhenEntityNotFound()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEntityByIdAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServingEntityRow?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DrillDownAsync(new DrillDownRequest { EntityId = "e1" }));
    }

    [Fact]
    public async Task DrillDown_DeserializesProperties()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEntityByIdAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServingEntityRow
            {
                EntityId = "e1", Name = "E1",
                PropertiesJson = """{"industry":"tech","employees":"500"}""",
                MetricsJson = "{}"
            });
        _repoMock.Setup(r => r.GetRelatedEntitiesAsync("v1", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEntityRow>());

        var response = await _service.DrillDownAsync(new DrillDownRequest { EntityId = "e1" });

        Assert.NotNull(response.Entity.Properties);
        Assert.Equal(2, response.Entity.Properties!.Count);
        Assert.Equal("tech", response.Entity.Properties["industry"]);
    }

    [Fact]
    public async Task DrillDown_UsesPinnedVersion()
    {
        _versionResolverMock.Setup(v => v.VersionExistsAsync("v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.GetEntityByIdAsync("v2", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServingEntityRow
            {
                EntityId = "e1", Name = "E1",
                PropertiesJson = "{}", MetricsJson = "{}"
            });
        _repoMock.Setup(r => r.GetRelatedEntitiesAsync("v2", "e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEntityRow>());

        var response = await _service.DrillDownAsync(new DrillDownRequest
        {
            EntityId = "e1",
            DatasetVersion = "v2"
        });

        Assert.Equal("v2", response.DatasetVersion);
    }
}
