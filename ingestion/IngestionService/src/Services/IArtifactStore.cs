namespace IngestionService.Services;

public interface IArtifactStore
{
    Task<string> StoreRawArtifactAsync(
        string versionId, string sourcePath,
        CancellationToken cancellationToken = default);
}
