namespace ProcessingService.Services;

public sealed class ProcessedArtifactStore : IProcessedArtifactStore
{
    private readonly string _rootPath;

    public ProcessedArtifactStore(string rootPath)
    {
        _rootPath = rootPath;
    }

    public string GetProcessedArtifactPath(string versionId)
    {
        return Path.Combine(_rootPath, "processed", versionId);
    }
}
