using IngestionService.Services;
using ProcessingService.Models;
using ProcessingService.Services;
using ServiceContract.VersionRegistry;

namespace ProcessingService.Pipeline;

public static class PublicationStep
{
    public const string PipelineVersion = "1.0.0";

    public static async Task<ProcessingManifest> PublishAsync(
        ProcessedOutput output,
        string rawArtifactPath,
        string processedArtifactDir,
        IProcessedArtifactStore artifactStore,
        IVersionRegistry versionRegistry,
        CancellationToken cancellationToken = default)
    {
        await output.WriteToDirectoryAsync(processedArtifactDir, cancellationToken);

        var manifest = new ProcessingManifest
        {
            VersionId = output.VersionId,
            SourceRawArtifactPath = rawArtifactPath,
            PipelineVersion = PipelineVersion,
            ProcessedAt = DateTimeOffset.UtcNow,
            EntityCount = output.Entities.Count,
            RawInputSha256 = ProcessingManifest.ComputeInputHash(rawArtifactPath)
        };

        var manifestPath = Path.Combine(processedArtifactDir, "manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }),
            cancellationToken);

        await versionRegistry.UpdateProcessedArtifactAsync(
            output.VersionId, processedArtifactDir, cancellationToken);

        return manifest;
    }
}
