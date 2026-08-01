using System.Text.Json;

namespace ProcessingService.Models;

public sealed record ProcessedOutput
{
    public required string VersionId { get; init; }
    public required IReadOnlyList<CanonicalEntity> Entities { get; init; }
    public required AggregateResult Aggregates { get; init; }
    public required ExplorationMetadata Metadata { get; init; }
    public required ProcessingManifest Manifest { get; init; }
    public IReadOnlyList<EvidenceRecord>? Evidence { get; init; }

    public async Task WriteToDirectoryAsync(string outputDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "entities.json"),
            JsonSerializer.Serialize(Entities, options),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "aggregates.json"),
            JsonSerializer.Serialize(Aggregates, options),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "metadata.json"),
            JsonSerializer.Serialize(Metadata, options),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "manifest.json"),
            JsonSerializer.Serialize(Manifest, options),
            cancellationToken);

        if (Evidence is not null)
        {
            await File.WriteAllTextAsync(
                Path.Combine(outputDir, "evidence.json"),
                JsonSerializer.Serialize(Evidence, options),
                cancellationToken);
        }
    }
}

public sealed record EvidenceRecord
{
    public required string RecordId { get; init; }
    public required string EntityId { get; init; }
    public required string Type { get; init; }
    public Dictionary<string, string> Fields { get; init; } = new();
    public DateTimeOffset? SourceTimestamp { get; init; }
}

public sealed record ExplorationMetadata
{
    public required IReadOnlyList<CategoryInfo> Categories { get; init; }
    public required int TotalEntityCount { get; init; }
    public required int TotalCategoryCount { get; init; }
    public required IReadOnlyList<string> AvailableMetrics { get; init; }
    public required IReadOnlyList<string> AvailableProperties { get; init; }
}

public sealed record CategoryInfo
{
    public required string Name { get; init; }
    public required int EntityCount { get; init; }
    public IReadOnlyList<string>? TopEntities { get; init; }
}
