namespace ProcessingService.Services;

public interface IProcessedArtifactStore
{
    string GetProcessedArtifactPath(string versionId);
}
