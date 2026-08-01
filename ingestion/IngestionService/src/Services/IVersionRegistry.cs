namespace IngestionService.Services;

using ServiceContract.VersionRegistry;

public interface IVersionRegistry
{
    Task<DatasetVersionRecord> RegisterRawVersionAsync(
        string versionId,
        string sourcePath,
        string? rawArtifactPath,
        string? metadata,
        CancellationToken cancellationToken = default);

    Task MarkVersionStatusAsync(
        string versionId,
        VersionStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<RollbackResponse> RollbackToVersionAsync(
        string targetVersionId,
        CancellationToken cancellationToken = default);

    Task<DatasetVersionRecord?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    Task<DatasetVersionRecord?> GetActiveVersionAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatasetVersionRecord>> ListVersionsAsync(
        CancellationToken cancellationToken = default);

    Task UpdateProcessedArtifactAsync(
        string versionId,
        string processedArtifactPath,
        CancellationToken cancellationToken = default);

    Task InitializeSchemaAsync(CancellationToken cancellationToken = default);
}
