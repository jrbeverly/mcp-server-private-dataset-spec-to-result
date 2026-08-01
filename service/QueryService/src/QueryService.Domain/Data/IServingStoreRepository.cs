namespace QueryService.Domain.Data;

public interface IServingStoreRepository
{
    // -- Schema -----------------------------------------------
    Task InitializeSchemaAsync(CancellationToken cancellationToken = default);

    // -- Version lifecycle ------------------------------------
    Task LoadVersionAsync(
        string versionId,
        IReadOnlyList<ServingEntityRow> entities,
        IReadOnlyList<ServingEvidenceRow> evidence,
        CancellationToken cancellationToken = default);

    Task ClearVersionAsync(string versionId, CancellationToken cancellationToken = default);
    Task ActivateVersionAsync(string versionId, CancellationToken cancellationToken = default);
    Task DeactivateVersionAsync(string versionId, CancellationToken cancellationToken = default);
    Task<string?> GetActiveVersionIdAsync(CancellationToken cancellationToken = default);
    Task<ServingVersionState?> GetVersionStateAsync(string versionId, CancellationToken cancellationToken = default);

    // -- Discovery --------------------------------------------
    Task<IReadOnlyList<ServingEntityRow>> SearchEntitiesAsync(
        string versionId,
        string? query,
        string? category,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountSearchEntitiesAsync(
        string versionId,
        string? query,
        string? category,
        CancellationToken cancellationToken = default);

    // -- Drill-down -------------------------------------------
    Task<ServingEntityRow?> GetEntityByIdAsync(
        string versionId, string entityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServingEntityRow>> GetEntitiesByCategoryAsync(
        string versionId, string category, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServingEntityRow>> GetRelatedEntitiesAsync(
        string versionId, string entityId, CancellationToken cancellationToken = default);

    // -- Aggregation ------------------------------------------
    Task<IReadOnlyList<CategoryAggregateRow>> GetCategoryAggregatesAsync(
        string versionId, CancellationToken cancellationToken = default);

    Task<int> GetTotalEntityCountAsync(string versionId, CancellationToken cancellationToken = default);
    Task<int> GetTotalCategoryCountAsync(string versionId, CancellationToken cancellationToken = default);

    // -- Metadata exploration ---------------------------------
    Task<IReadOnlyList<DimensionValueRow>> GetDimensionValuesAsync(
        string versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailableMetricsAsync(
        string versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAvailablePropertiesAsync(
        string versionId, CancellationToken cancellationToken = default);

    // -- Raw evidence -----------------------------------------
    Task<IReadOnlyList<ServingEvidenceRow>> GetEvidenceByEntityAsync(
        string versionId,
        string entityId,
        string? type,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountEvidenceByEntityAsync(
        string versionId,
        string entityId,
        string? type,
        CancellationToken cancellationToken = default);

    // -- Bulk read (for version loading) ----------------------
    Task<IReadOnlyList<ServingEntityRow>> GetAllEntitiesByVersionAsync(
        string versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServingEvidenceRow>> GetAllEvidenceByVersionAsync(
        string versionId, CancellationToken cancellationToken = default);
}

public sealed record ServingEntityRow
{
    public required string EntityId { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public required string PropertiesJson { get; init; }
    public required string MetricsJson { get; init; }
    public double? RelevanceScore { get; init; }
}

public sealed record ServingEvidenceRow
{
    public required string RecordId { get; init; }
    public required string EntityId { get; init; }
    public required string Type { get; init; }
    public required string FieldsJson { get; init; }
    public DateTimeOffset? SourceTimestamp { get; init; }
}

public sealed record ServingVersionState
{
    public required string VersionId { get; init; }
    public required DateTimeOffset LoadedAt { get; init; }
    public required int EntityCount { get; init; }
    public required int EvidenceCount { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record CategoryAggregateRow
{
    public required string Key { get; init; }
    public string? Label { get; init; }
    public required int Count { get; init; }
    public required double Percentage { get; init; }
    public string? MetricSummariesJson { get; init; }
}

public sealed record DimensionValueRow
{
    public required string Value { get; init; }
    public string? Label { get; init; }
    public required int EntityCount { get; init; }
}
