using Moq;
using QueryService.Domain.Data;
using QueryService.Domain.Exceptions;
using QueryService.Domain.Services;
using ServiceConfiguration;
using ServiceContract.V1;
using Xunit;

namespace QueryService.Tests.Services;

public class DiscoveryServiceTests
{
    private readonly Mock<IServingStoreRepository> _repoMock;
    private readonly Mock<IDatasetVersionResolver> _versionResolverMock;
    private readonly DatasetVersionConfiguration _versionConfig;
    private readonly DiscoveryService _service;

    public DiscoveryServiceTests()
    {
        _repoMock = new Mock<IServingStoreRepository>();
        _versionResolverMock = new Mock<IDatasetVersionResolver>();
        _versionConfig = new DatasetVersionConfiguration { AllowVersionPinning = true };
        _service = new DiscoveryService(_repoMock.Object, _versionResolverMock.Object, _versionConfig);
    }

    [Fact]
    public async Task Discover_ResolvesActiveVersion_ByDefault()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.SearchEntitiesAsync("v1", null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEntityRow>());
        _repoMock.Setup(r => r.CountSearchEntitiesAsync("v1", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await _service.DiscoverAsync(new DiscoveryRequest());

        Assert.Equal("v1", response.DatasetVersion);
        Assert.Equal(0, response.TotalMatches);
        Assert.Empty(response.Entities);
    }

    [Fact]
    public async Task Discover_AppliesDefaultPagination()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.SearchEntitiesAsync("v1", null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEntityRow>());
        _repoMock.Setup(r => r.CountSearchEntitiesAsync("v1", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.DiscoverAsync(new DiscoveryRequest());

        _repoMock.Verify(
            r => r.SearchEntitiesAsync("v1", null, null, 0, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Discover_UsesPinnedVersion_WhenRequested()
    {
        _versionResolverMock.Setup(v => v.VersionExistsAsync("v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.SearchEntitiesAsync("v2", null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEntityRow>());
        _repoMock.Setup(r => r.CountSearchEntitiesAsync("v2", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await _service.DiscoverAsync(new DiscoveryRequest { DatasetVersion = "v2" });

        Assert.Equal("v2", response.DatasetVersion);
    }

    [Fact]
    public async Task Discover_Throws_WhenVersionPinningDisabled()
    {
        var config = new DatasetVersionConfiguration { AllowVersionPinning = false };
        var service = new DiscoveryService(_repoMock.Object, _versionResolverMock.Object, config);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => service.DiscoverAsync(new DiscoveryRequest { DatasetVersion = "v2" }));
        Assert.Contains("pinning", ex.Message);
    }

    [Fact]
    public async Task Discover_Throws_WhenVersionNotFound()
    {
        _versionResolverMock.Setup(v => v.VersionExistsAsync("v2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<VersionNotAvailableException>(
            () => _service.DiscoverAsync(new DiscoveryRequest { DatasetVersion = "v2" }));
    }

    [Fact]
    public async Task Discover_MapsEntitiesCorrectly()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var rows = new List<ServingEntityRow>
        {
            new()
            {
                EntityId = "e1", Name = "Entity 1", Category = "cat-a",
                Description = "A test entity",
                PropertiesJson = "{}", MetricsJson = "{}",
                RelevanceScore = 0.85
            }
        };
        _repoMock.Setup(r => r.SearchEntitiesAsync("v1", null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        _repoMock.Setup(r => r.CountSearchEntitiesAsync("v1", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _service.DiscoverAsync(new DiscoveryRequest());

        Assert.Single(response.Entities);
        Assert.Equal("e1", response.Entities[0].EntityId);
        Assert.Equal("Entity 1", response.Entities[0].Name);
        Assert.Equal("cat-a", response.Entities[0].Category);
        Assert.Equal("A test entity", response.Entities[0].Description);
        Assert.Equal(0.85m, response.Entities[0].RelevanceScore);
    }

    [Fact]
    public async Task Discover_RelevanceScoreNull_WhenNoQueryProvided()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var rows = new List<ServingEntityRow>
        {
            new()
            {
                EntityId = "e1", Name = "Entity 1", Category = "cat-a",
                PropertiesJson = "{}", MetricsJson = "{}",
                RelevanceScore = null
            }
        };
        _repoMock.Setup(r => r.SearchEntitiesAsync("v1", null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        _repoMock.Setup(r => r.CountSearchEntitiesAsync("v1", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _service.DiscoverAsync(new DiscoveryRequest());

        Assert.Single(response.Entities);
        Assert.Null(response.Entities[0].RelevanceScore);
    }
}
