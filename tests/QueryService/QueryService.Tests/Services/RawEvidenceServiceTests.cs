using Moq;
using QueryService.Domain.Data;
using QueryService.Domain.Services;
using ServiceConfiguration;
using ServiceContract.V1;
using Xunit;

namespace QueryService.Tests.Services;

public class RawEvidenceServiceTests
{
    private readonly Mock<IServingStoreRepository> _repoMock;
    private readonly Mock<IDatasetVersionResolver> _versionResolverMock;
    private readonly DatasetVersionConfiguration _versionConfig;
    private readonly RawEvidenceService _service;

    public RawEvidenceServiceTests()
    {
        _repoMock = new Mock<IServingStoreRepository>();
        _versionResolverMock = new Mock<IDatasetVersionResolver>();
        _versionConfig = new DatasetVersionConfiguration { AllowVersionPinning = true };
        _service = new RawEvidenceService(_repoMock.Object, _versionResolverMock.Object, _versionConfig);
    }

    [Fact]
    public async Task GetRawEvidence_ReturnsRecords()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        var records = new List<ServingEvidenceRow>
        {
            new() { RecordId = "r1", EntityId = "e1", Type = "financial", FieldsJson = """{"revenue":"100"}""" }
        };
        _repoMock.Setup(r => r.GetEvidenceByEntityAsync("v1", "e1", null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);
        _repoMock.Setup(r => r.CountEvidenceByEntityAsync("v1", "e1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _service.GetRawEvidenceAsync(new RawEvidenceRequest { EntityId = "e1" });

        Assert.Equal("v1", response.DatasetVersion);
        Assert.Single(response.Records);
        Assert.Equal("r1", response.Records[0].RecordId);
        Assert.Equal("financial", response.Records[0].Type);
    }

    [Fact]
    public async Task GetRawEvidence_ReturnsEmpty_WhenEntityNotFound()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEvidenceByEntityAsync("v1", "missing", null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEvidenceRow>());
        _repoMock.Setup(r => r.CountEvidenceByEntityAsync("v1", "missing", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await _service.GetRawEvidenceAsync(new RawEvidenceRequest { EntityId = "missing" });

        Assert.Equal(0, response.TotalRecords);
        Assert.Empty(response.Records);
    }

    [Fact]
    public async Task GetRawEvidence_DeserializesFields()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEvidenceByEntityAsync("v1", "e1", null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServingEvidenceRow>
            {
                new() { RecordId = "r1", EntityId = "e1", Type = "t", FieldsJson = """{"a":"1","b":"2"}""" }
            });
        _repoMock.Setup(r => r.CountEvidenceByEntityAsync("v1", "e1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _service.GetRawEvidenceAsync(new RawEvidenceRequest { EntityId = "e1" });

        Assert.Equal(2, response.Records[0].Fields.Count);
        Assert.Equal("1", response.Records[0].Fields["a"]);
    }

    [Fact]
    public async Task GetRawEvidence_AppliesDefaultPagination()
    {
        _versionResolverMock.Setup(v => v.ResolveActiveVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("v1");
        _repoMock.Setup(r => r.GetEvidenceByEntityAsync("v1", "e1", null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServingEvidenceRow>());
        _repoMock.Setup(r => r.CountEvidenceByEntityAsync("v1", "e1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.GetRawEvidenceAsync(new RawEvidenceRequest { EntityId = "e1" });

        _repoMock.Verify(
            r => r.GetEvidenceByEntityAsync("v1", "e1", null, 0, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
