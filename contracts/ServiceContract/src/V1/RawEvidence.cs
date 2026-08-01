namespace ServiceContract.V1;

public sealed record RawEvidenceRequest
{
    public string? DatasetVersion { get; init; }
    public required string EntityId { get; init; }
    public string? EvidenceType { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; init; }
}

public sealed record RawEvidenceResponse
{
    public required IReadOnlyList<EvidenceRecord> Records { get; init; }
    public required string DatasetVersion { get; init; }
    public required int TotalRecords { get; init; }
}

public sealed record EvidenceRecord
{
    public required string RecordId { get; init; }
    public required string EntityId { get; init; }
    public required string Type { get; init; }
    public required IReadOnlyDictionary<string, string> Fields { get; init; }
    public DateTimeOffset? SourceTimestamp { get; init; }
}
