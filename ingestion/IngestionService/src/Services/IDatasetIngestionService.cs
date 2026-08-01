using ServiceContract.VersionRegistry;

namespace IngestionService.Services;

public interface IDatasetIngestionService
{
    Task<IngestResponse> IngestAsync(
        IngestRequest request,
        CancellationToken cancellationToken = default);

    Task<RollbackResponse> RollbackToVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    Task<ListVersionsResponse> ListVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<DatasetVersionRecord?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    Task InitializeAsync(CancellationToken cancellationToken = default);
}
