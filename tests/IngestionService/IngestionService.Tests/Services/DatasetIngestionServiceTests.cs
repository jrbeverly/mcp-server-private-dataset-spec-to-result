using Moq;
using ServiceContract.VersionRegistry;
using IngestionService.Services;
using Xunit;

namespace IngestionService.Tests.Services;

public class DatasetIngestionServiceTests
{
    private readonly Mock<IVersionRegistry> _registryMock;
    private readonly Mock<IArtifactStore> _artifactStoreMock;
    private readonly DatasetIngestionService _service;

    public DatasetIngestionServiceTests()
    {
        _registryMock = new Mock<IVersionRegistry>();
        _artifactStoreMock = new Mock<IArtifactStore>();
        _service = new DatasetIngestionService(_registryMock.Object, _artifactStoreMock.Object);
    }

    [Fact]
    public async Task Ingest_ValidFile_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test content");
            _artifactStoreMock
                .Setup(s => s.StoreRawArtifactAsync(
                    It.IsAny<string>(), tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync("/artifacts/raw/v20260101000000/data.txt");
            _registryMock
                .Setup(r => r.RegisterRawVersionAsync(
                    It.IsAny<string>(), tempFile, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DatasetVersionRecord
                {
                    VersionId = "v20260101000000",
                    Status = VersionStatus.Raw,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    SourcePath = tempFile
                });

            var response = await _service.IngestAsync(new IngestRequest
            {
                SourcePath = tempFile
            });

            Assert.NotNull(response);
            Assert.NotEqual("(not created)", response.VersionId);
            Assert.Equal(VersionStatus.Raw, response.Status);
            Assert.Null(response.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Ingest_InvalidPath_ReturnsFailureAndDoesNotMutateRegistry()
    {
        var response = await _service.IngestAsync(new IngestRequest
        {
            SourcePath = "/nonexistent/path"
        });

        Assert.Equal(VersionStatus.Failed, response.Status);
        Assert.NotNull(response.ErrorMessage);
        Assert.Equal("(not created)", response.VersionId);
        // Registry should not be called for a non-existent path
        _registryMock.Verify(
            r => r.RegisterRawVersionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _artifactStoreMock.Verify(
            s => s.StoreRawArtifactAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ingest_StorageFailure_RecordsFailedVersion()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test content");
            _artifactStoreMock
                .Setup(s => s.StoreRawArtifactAsync(
                    It.IsAny<string>(), tempFile, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("disk full"));

            _registryMock
                .Setup(r => r.RegisterRawVersionAsync(
                    It.IsAny<string>(), tempFile, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DatasetVersionRecord
                {
                    VersionId = "v20260101000000",
                    Status = VersionStatus.Raw,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

            var response = await _service.IngestAsync(new IngestRequest
            {
                SourcePath = tempFile
            });

            Assert.Equal(VersionStatus.Failed, response.Status);
            Assert.NotNull(response.ErrorMessage);
            Assert.Contains("disk full", response.ErrorMessage);
            // The version was registered and then marked as Failed
            _registryMock.Verify(
                r => r.MarkVersionStatusAsync(
                    It.IsAny<string>(), VersionStatus.Failed, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Rollback_DelegatesToRegistry()
    {
        _registryMock
            .Setup(r => r.RollbackToVersionAsync("v20260101000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RollbackResponse
            {
                PreviousActiveVersionId = "v20250101000000",
                NewActiveVersionId = "v20260101000000"
            });

        var response = await _service.RollbackToVersionAsync("v20260101000000");

        Assert.Equal("v20250101000000", response.PreviousActiveVersionId);
        Assert.Equal("v20260101000000", response.NewActiveVersionId);
    }

    [Fact]
    public async Task ListVersions_ReturnsAllVersions()
    {
        var records = new List<DatasetVersionRecord>
        {
            new() { VersionId = "v2", Status = VersionStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { VersionId = "v1", Status = VersionStatus.Superseded, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        _registryMock
            .Setup(r => r.ListVersionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var response = await _service.ListVersionsAsync();

        Assert.Equal(2, response.Versions.Count);
        Assert.Equal("v2", response.Versions[0].VersionId);
        Assert.Equal("v1", response.Versions[1].VersionId);
    }
}
