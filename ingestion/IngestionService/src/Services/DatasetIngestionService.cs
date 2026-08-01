using ServiceContract.VersionRegistry;
using IngestionService.Validation;

namespace IngestionService.Services;

public sealed class DatasetIngestionService : IDatasetIngestionService
{
    private readonly IVersionRegistry _registry;
    private readonly IArtifactStore _artifactStore;

    public DatasetIngestionService(IVersionRegistry registry, IArtifactStore artifactStore)
    {
        _registry = registry;
        _artifactStore = artifactStore;
    }

    public async Task<IngestResponse> IngestAsync(
        IngestRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the dataset — failures do NOT mutate the active version
        var validation = DatasetValidator.Validate(request.SourcePath);
        if (!validation.IsValid)
        {
            return new IngestResponse
            {
                VersionId = "(not created)",
                Status = VersionStatus.Failed,
                CreatedAt = DateTimeOffset.UtcNow,
                ErrorMessage = string.Join("; ", validation.Errors)
            };
        }

        var versionId = GenerateVersionId();

        try
        {
            var artifactPath = await _artifactStore.StoreRawArtifactAsync(
                versionId, request.SourcePath, cancellationToken);

            var record = await _registry.RegisterRawVersionAsync(
                versionId,
                request.SourcePath,
                artifactPath,
                request.Metadata,
                cancellationToken);

            return new IngestResponse
            {
                VersionId = record.VersionId,
                Status = record.Status,
                CreatedAt = record.CreatedAt
            };
        }
        catch (Exception ex)
        {
            // Record the failure in the registry for diagnostics
            try
            {
                await _registry.RegisterRawVersionAsync(
                    versionId, request.SourcePath, null, request.Metadata, cancellationToken);
                await _registry.MarkVersionStatusAsync(
                    versionId, VersionStatus.Failed, ex.Message, cancellationToken);
            }
            catch
            {
                // Registry itself may be unavailable; surface the original error
            }

            return new IngestResponse
            {
                VersionId = versionId,
                Status = VersionStatus.Failed,
                CreatedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<RollbackResponse> RollbackToVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        return await _registry.RollbackToVersionAsync(versionId, cancellationToken);
    }

    public async Task<ListVersionsResponse> ListVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = await _registry.ListVersionsAsync(cancellationToken);
        return new ListVersionsResponse { Versions = versions };
    }

    public async Task<DatasetVersionRecord?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        return await _registry.GetVersionAsync(versionId, cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _registry.InitializeSchemaAsync(cancellationToken);
    }

    private static string GenerateVersionId()
    {
        return $"v{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }
}
