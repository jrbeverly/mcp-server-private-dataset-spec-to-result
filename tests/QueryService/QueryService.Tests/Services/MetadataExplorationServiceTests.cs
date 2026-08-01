using Moq;
using QueryService.Domain.Data;
using QueryService.Domain.Services;
using ServiceConfiguration;
using ServiceContract.V1;
using Xunit;

namespace QueryService.Tests.Services;

public class MetadataExplorationServiceTests
{
    private readonly Mock<IServingStoreRepository> _repoMock;
    private readonly Mock<IDatasetVersionResolver> _versionResolverMock;
    private readonly DatasetVersionConfiguration _versionConfig;
    private readonly MetadataExplorationService _service;

    public MetadataExplorationServiceTests()
    {
        _repoMock = new Mock<IServingStoreRepository>();
        _versionResolverMock = new Mock<IDatasetVersionResolver>();
        _versionConfig = new DatasetVersionConfiguration { AllowVersionPinning = true };
        _service = new MetadataExplorationService(_repoMock.Object, _versionResolverMock.Object, _versionConfig);
    }

    [Fact]
    public async Task ExploreMetadata_ReturnsCounts()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetDimensionValuesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DimensionValueRow>());
        _repoMock.Setup(r => r.GetTotalEntityCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);
        _repoMock.Setup(r => r.GetTotalCategoryCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _repoMock.Setup(r => r.GetAvailableMetricsAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _repoMock.Setup(r => r.GetAvailablePropertiesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var response = await _service.ExploreMetadataAsync(new MetadataExplorationRequest());

        Assert.Equal("v1", response.DatasetVersion);
        Assert.Equal(100, response.TotalEntityCount);
        Assert.Equal(5, response.TotalCategoryCount);
    }

    [Fact]
    public async Task ExploreMetadata_ReturnsAvailableMetrics()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetDimensionValuesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DimensionValueRow>());
        _repoMock.Setup(r => r.GetTotalEntityCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);
        _repoMock.Setup(r => r.GetTotalCategoryCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _repoMock.Setup(r => r.GetAvailableMetricsAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "revenue", "employees" });
        _repoMock.Setup(r => r.GetAvailablePropertiesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "industry", "region" });

        var response = await _service.ExploreMetadataAsync(new MetadataExplorationRequest());

        Assert.NotNull(response.AvailableMetrics);
        Assert.Equal(2, response.AvailableMetrics!.Count);
        Assert.Contains("revenue", response.AvailableMetrics);
        Assert.Contains("employees", response.AvailableMetrics);
        Assert.NotNull(response.AvailableProperties);
        Assert.Equal(2, response.AvailableProperties!.Count);
        Assert.Contains("industry", response.AvailableProperties);
    }

    [Fact]
    public async Task ExploreMetadata_EmptyMetricsAndProperties_WhenNoneExist()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetDimensionValuesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DimensionValueRow>());
        _repoMock.Setup(r => r.GetTotalEntityCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.GetTotalCategoryCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.GetAvailableMetricsAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _repoMock.Setup(r => r.GetAvailablePropertiesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var response = await _service.ExploreMetadataAsync(new MetadataExplorationRequest());

        Assert.NotNull(response.AvailableMetrics);
        Assert.Empty(response.AvailableMetrics!);
        Assert.NotNull(response.AvailableProperties);
        Assert.Empty(response.AvailableProperties!);
    }

    [Fact]
    public async Task ExploreMetadata_BuildsCategoryDimension()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var values = new List<DimensionValueRow>
        {
            new() { Value = "cat-a", Label = "Category A", EntityCount = 50 },
            new() { Value = "cat-b", Label = "Category B", EntityCount = 30 }
        };
        _repoMock.Setup(r => r.GetDimensionValuesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(values);
        _repoMock.Setup(r => r.GetTotalEntityCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);
        _repoMock.Setup(r => r.GetTotalCategoryCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _repoMock.Setup(r => r.GetAvailableMetricsAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _repoMock.Setup(r => r.GetAvailablePropertiesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var response = await _service.ExploreMetadataAsync(new MetadataExplorationRequest());

        Assert.Single(response.Dimensions);
        Assert.Equal("category", response.Dimensions[0].Name);
        Assert.Equal(2, response.Dimensions[0].Values.Count);
        Assert.Equal("cat-a", response.Dimensions[0].Values[0].Value);
    }

    [Fact]
    public async Task ExploreMetadata_EmptyDimensions_WhenNoValues()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetDimensionValuesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DimensionValueRow>());
        _repoMock.Setup(r => r.GetTotalEntityCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.GetTotalCategoryCountAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.GetAvailableMetricsAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _repoMock.Setup(r => r.GetAvailablePropertiesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var response = await _service.ExploreMetadataAsync(new MetadataExplorationRequest());

        Assert.Empty(response.Dimensions);
    }
}
