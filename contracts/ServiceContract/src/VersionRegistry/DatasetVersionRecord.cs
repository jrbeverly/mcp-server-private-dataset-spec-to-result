namespace ServiceContract.VersionRegistry;

public sealed record DatasetVersionRecord
{
    public required string VersionId { get; init; }
    public required VersionStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? SourcePath { get; init; }
    public string? RawArtifactPath { get; init; }
    public string? ProcessedArtifactPath { get; init; }
    public string? Metadata { get; init; }
    public string? PreviousActiveVersionId { get; init; }
    public string? ReplacedByVersionId { get; init; }
    public string? ErrorMessage { get; init; }
}
