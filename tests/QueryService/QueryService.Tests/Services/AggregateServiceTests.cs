using System.Text.Json;
using Moq;
using QueryService.Domain.Data;
using QueryService.Domain.Exceptions;
using QueryService.Domain.Services;
using ServiceConfiguration;
using ServiceContract.V1;
using Xunit;

namespace QueryService.Tests.Services;

public class AggregateServiceTests
{
    private readonly Mock<IServingStoreRepository> _repoMock;
    private readonly Mock<IDatasetVersionResolver> _versionResolverMock;
    private readonly DatasetVersionConfiguration _versionConfig;
    private readonly AggregateService _service;

    public AggregateServiceTests()
    {
        _repoMock = new Mock<IServingStoreRepository>();
        _versionResolverMock = new Mock<IDatasetVersionResolver>();
        _versionConfig = new DatasetVersionConfiguration { AllowVersionPinning = true };
        _service = new AggregateService(_repoMock.Object, _versionResolverMock.Object, _versionConfig);
    }

    [Fact]
    public async Task Aggregate_ResolvesActiveVersion_ByDefault()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetCategoryAggregatesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CategoryAggregateRow>());

        var response = await _service.AggregateAsync(new AggregateRequest { Metric = "count" });

        Assert.Equal("v1", response.DatasetVersion);
        Assert.Equal("count", response.Metric);
        Assert.Empty(response.Buckets);
    }

    [Fact]
    public async Task Aggregate_MapsBucketsCorrectly()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var rows = new List<CategoryAggregateRow>
        {
            new() { Key = "cat-a", Label = "Category A", Count = 10, Percentage = 50.0 }
        };
        _repoMock.Setup(r => r.GetCategoryAggregatesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var response = await _service.AggregateAsync(new AggregateRequest { Metric = "count" });

        Assert.Single(response.Buckets);
        Assert.Equal("cat-a", response.Buckets[0].Key);
        Assert.Equal("Category A", response.Buckets[0].Label);
        Assert.Equal(10, response.Buckets[0].Count);
    }

    [Fact]
    public async Task Aggregate_AppliesLimit()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var rows = new List<CategoryAggregateRow>
        {
            new() { Key = "cat-a", Count = 10, Percentage = 60.0 },
            new() { Key = "cat-b", Count = 5, Percentage = 40.0 }
        };
        _repoMock.Setup(r => r.GetCategoryAggregatesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var response = await _service.AggregateAsync(new AggregateRequest { Metric = "count", Limit = 1 });

        Assert.Single(response.Buckets);
    }

    [Fact]
    public async Task Aggregate_Throws_OnUnsupportedGroupBy()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetCategoryAggregatesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CategoryAggregateRow>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => _service.AggregateAsync(new AggregateRequest
            {
                Metric = "count",
                GroupBy = "unsupported_dim"
            }));
        Assert.Contains("Unsupported group-by", ex.Message);
    }

    [Fact]
    public async Task Aggregate_SurfacesMinMaxSum_WhenMetricSummariesPresent()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var metricSummaries = new Dictionary<string, object>
        {
            ["revenue"] = new { min = 10.0m, max = 100.0m, avg = 55.0m, sum = 550.0m }
        };
        var rows = new List<CategoryAggregateRow>
        {
            new()
            {
                Key = "cat-a", Label = "Category A", Count = 10, Percentage = 50.0,
                MetricSummariesJson = JsonSerializer.Serialize(metricSummaries)
            }
        };
        _repoMock.Setup(r => r.GetCategoryAggregatesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var response = await _service.AggregateAsync(new AggregateRequest { Metric = "revenue" });

        Assert.Single(response.Buckets);
        Assert.Equal(55.0m, response.Buckets[0].Value);
        Assert.Equal(10.0m, response.Buckets[0].Min);
        Assert.Equal(100.0m, response.Buckets[0].Max);
        Assert.Equal(550.0m, response.Buckets[0].Sum);
    }

    [Fact]
    public async Task Aggregate_MinMaxSumNull_WhenNoMetricSummaries()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var rows = new List<CategoryAggregateRow>
        {
            new() { Key = "cat-a", Label = "Category A", Count = 10, Percentage = 50.0 }
        };
        _repoMock.Setup(r => r.GetCategoryAggregatesAsync("v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var response = await _service.AggregateAsync(new AggregateRequest { Metric = "count" });

        Assert.Single(response.Buckets);
        Assert.Equal(50.0m, response.Buckets[0].Value);
        Assert.Null(response.Buckets[0].Min);
        Assert.Null(response.Buckets[0].Max);
        Assert.Null(response.Buckets[0].Sum);
    }

    [Fact]
    public async Task Aggregate_Throws_WhenVersionPinningDisabled()
    {
        var config = new DatasetVersionConfiguration { AllowVersionPinning = false };
        var service = new AggregateService(_repoMock.Object, _versionResolverMock.Object, config);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.AggregateAsync(new AggregateRequest
            {
                Metric = "count",
                DatasetVersion = "v2"
            }));
    }
}
