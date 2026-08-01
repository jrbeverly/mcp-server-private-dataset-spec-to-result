using System.Text.Json;
using QueryService.Domain.Data;

namespace QueryService.Infrastructure;

public sealed class ServingVersionLoader
{
    private readonly IServingStoreRepository _repository;
    private readonly string _processedArtifactRoot;

    public ServingVersionLoader(IServingStoreRepository repository, string processedArtifactRoot)
    {
        _repository = repository;
        _processedArtifactRoot = processedArtifactRoot;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _repository.InitializeSchemaAsync(cancellationToken);
    }

    public async Task LoadVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var processedDir = Path.Combine(_processedArtifactRoot, "processed", versionId);

        if (!Directory.Exists(processedDir))
            throw new InvalidOperationException(
                $"Processed artifact directory not found: '{processedDir}'. " +
                "Run the processing pipeline first.");

        var entitiesPath = Path.Combine(processedDir, "entities.json");
        if (!File.Exists(entitiesPath))
            throw new InvalidOperationException(
                $"entities.json not found in '{processedDir}'.");

        var entityRows = ReadEntitiesFromJson(entitiesPath);
        var evidenceRows = ReadEvidenceFromDir(processedDir);

        await _repository.LoadVersionAsync(versionId, entityRows, evidenceRows, cancellationToken);
    }

    public async Task ActivateVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetVersionStateAsync(versionId, cancellationToken);
        if (state is null)
            throw new InvalidOperationException(
                $"Version '{versionId}' has not been loaded into the serving store. Load it first.");

        await _repository.ActivateVersionAsync(versionId, cancellationToken);
    }

    public async Task<string?> GetActiveVersionAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveVersionIdAsync(cancellationToken);
    }

    public async Task RefreshVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        await _repository.ClearVersionAsync(versionId, cancellationToken);
        await LoadVersionAsync(versionId, cancellationToken);
        await _repository.ActivateVersionAsync(versionId, cancellationToken);
    }

    public async Task RollbackToVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetVersionStateAsync(versionId, cancellationToken);
        if (state is null)
            throw new InvalidOperationException(
                $"Version '{versionId}' has not been loaded into the serving store.");

        await _repository.ActivateVersionAsync(versionId, cancellationToken);
    }

    // -- Internal helpers -------------------------------------------------

    private static IReadOnlyList<ServingEntityRow> ReadEntitiesFromJson(string path)
    {
        var json = File.ReadAllText(path);
        var source = JsonSerializer.Deserialize<List<ProcessedEntityJson>>(json)
            ?? new List<ProcessedEntityJson>();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return source.Select(e =>
        {
            var properties = e.Properties is not null
                ? JsonSerializer.Serialize(e.Properties, options)
                : "{}";
            var metrics = e.Metrics is not null
                ? SerializeMetrics(e.Metrics, options)
                : "{}";

            return new ServingEntityRow
            {
                EntityId = e.EntityId ?? Guid.NewGuid().ToString(),
                Name = e.Name ?? "Unnamed",
                Category = e.Category,
                Description = e.Description,
                PropertiesJson = properties,
                MetricsJson = metrics
            };
        }).ToList();
    }

    private static string SerializeMetrics(
        Dictionary<string, decimal> metrics,
        JsonSerializerOptions options)
    {
        var dict = metrics.ToDictionary(
            kv => kv.Key,
            kv => (object)kv.Value);
        return JsonSerializer.Serialize(dict, options);
    }

    private static IReadOnlyList<ServingEvidenceRow> ReadEvidenceFromDir(string processedDir)
    {
        var evidencePath = Path.Combine(processedDir, "evidence.json");
        if (!File.Exists(evidencePath))
            return Array.Empty<ServingEvidenceRow>();

        var json = File.ReadAllText(evidencePath);
        var source = JsonSerializer.Deserialize<List<EvidenceJson>>(json)
            ?? new List<EvidenceJson>();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return source.Select(e => new ServingEvidenceRow
        {
            RecordId = e.RecordId ?? Guid.NewGuid().ToString(),
            EntityId = e.EntityId ?? "",
            Type = e.Type ?? "unknown",
            FieldsJson = e.Fields is not null
                ? JsonSerializer.Serialize(e.Fields, options)
                : "{}",
            SourceTimestamp = e.SourceTimestamp
        }).ToList();
    }

    private sealed record ProcessedEntityJson
    {
        public string? EntityId { get; init; }
        public string? Name { get; init; }
        public string? Category { get; init; }
        public string? Description { get; init; }
        public Dictionary<string, string>? Properties { get; init; }
        public Dictionary<string, decimal>? Metrics { get; init; }
    }

    private sealed record EvidenceJson
    {
        public string? RecordId { get; init; }
        public string? EntityId { get; init; }
        public string? Type { get; init; }
        public Dictionary<string, string>? Fields { get; init; }
        public DateTimeOffset? SourceTimestamp { get; init; }
    }
}
