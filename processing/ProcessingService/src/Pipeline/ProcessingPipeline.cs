using IngestionService.Services;
using ProcessingService.Models;
using ProcessingService.Services;
using ServiceContract.VersionRegistry;

namespace ProcessingService.Pipeline;

public sealed class ProcessingPipeline
{
    private readonly IProcessedArtifactStore _artifactStore;
    private readonly IVersionRegistry _versionRegistry;

    public ProcessingPipeline(IProcessedArtifactStore artifactStore, IVersionRegistry versionRegistry)
    {
        _artifactStore = artifactStore;
        _versionRegistry = versionRegistry;
    }

    public async Task<ProcessedOutput> ProcessAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await _versionRegistry.GetVersionAsync(versionId, cancellationToken)
            ?? throw new InvalidOperationException($"Version '{versionId}' not found");

        if (version.Status != VersionStatus.Raw && version.Status != VersionStatus.Processing)
            throw new InvalidOperationException(
                $"Version '{versionId}' has status '{version.Status}' — only Raw or Processing versions can be processed");

        if (string.IsNullOrEmpty(version.RawArtifactPath))
            throw new InvalidOperationException(
                $"Version '{versionId}' has no raw artifact path — ingestion may have failed");

        await _versionRegistry.MarkVersionStatusAsync(versionId, VersionStatus.Processing, cancellationToken: cancellationToken);

        try
        {
            var rawPath = version.RawArtifactPath;

            var entities = await NormalizationStep.NormalizeAsync(rawPath, cancellationToken);

            var enriched = await EnrichmentStep.EnrichAsync(entities, cancellationToken);

            var aggregates = await AggregationStep.AggregateAsync(enriched, cancellationToken);

            var evidence = await EvidenceStep.GenerateAsync(enriched, cancellationToken);

            var validation = ValidationStep.Validate(enriched);
            if (!validation.IsValid)
            {
                await _versionRegistry.MarkVersionStatusAsync(
                    versionId, VersionStatus.Failed,
                    string.Join("; ", validation.Errors),
                    cancellationToken);

                throw new InvalidOperationException(
                    $"Validation failed: {string.Join("; ", validation.Errors)}");
            }

            var metadata = new ExplorationMetadata
            {
                Categories = aggregates.Buckets
                    .Where(b => !b.Key.StartsWith("__"))
                    .Select(b => new CategoryInfo
                    {
                        Name = b.Key,
                        EntityCount = b.Count,
                        TopEntities = enriched
                            .Where(e => e.Category == b.Key && e.Metrics.Count > 0)
                            .OrderByDescending(e => e.Metrics.Values.Sum())
                            .Take(5)
                            .Select(e => e.Name)
                            .ToList()
                    })
                    .ToList(),
                TotalEntityCount = entities.Count,
                TotalCategoryCount = aggregates.TotalCategoryCount,
                AvailableMetrics = enriched
                    .SelectMany(e => e.Metrics.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AvailableProperties = enriched
                    .SelectMany(e => e.Properties.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            var output = new ProcessedOutput
            {
                VersionId = versionId,
                Entities = enriched,
                Aggregates = aggregates,
                Metadata = metadata,
                Evidence = evidence,
                Manifest = new ProcessingManifest
                {
                    VersionId = versionId,
                    SourceRawArtifactPath = rawPath,
                    PipelineVersion = PublicationStep.PipelineVersion,
                    ProcessedAt = DateTimeOffset.UtcNow,
                    EntityCount = enriched.Count
                }
            };

            var processedDir = _artifactStore.GetProcessedArtifactPath(versionId);
            await PublicationStep.PublishAsync(
                output, rawPath, processedDir,
                _artifactStore, _versionRegistry, cancellationToken);

            await _versionRegistry.MarkVersionStatusAsync(versionId, VersionStatus.Processed, cancellationToken: cancellationToken);

            return output;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            await _versionRegistry.MarkVersionStatusAsync(
                versionId, VersionStatus.Failed, ex.Message, cancellationToken);

            throw new InvalidOperationException($"Processing failed for version '{versionId}': {ex.Message}", ex);
        }
    }

    public async Task PublishAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var version = await _versionRegistry.GetVersionAsync(versionId, cancellationToken)
            ?? throw new InvalidOperationException($"Version '{versionId}' not found");

        if (version.Status != VersionStatus.Processed)
            throw new InvalidOperationException(
                $"Version '{versionId}' has status '{version.Status}' — only Processed versions can be published");

        await _versionRegistry.MarkVersionStatusAsync(versionId, VersionStatus.Published, cancellationToken: cancellationToken);
    }
}
